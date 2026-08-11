package com.jumong.warehouse

import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothSocket
import android.content.Context
import android.util.Log
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.util.UUID

object BluetoothPrinter {
    private const val TAG = "WhBluetooth"
    private val SPP_UUID: UUID = UUID.fromString("00001101-0000-1000-8000-00805F9B34FB")
    private val ANDROID_SPP_UUID: UUID = UUID.fromString("FA87C0D0-AFAC-11DE-8A39-0800200C9A66")
    private const val CONNECT_TIMEOUT_MS = 4000
    private const val CHANNEL_TIMEOUT_MS = 1500
    private const val CHANNEL_MAX = 30
    // Hard cap for the whole connect() call — a dead/busy printer must never
    // freeze the app; after this the in-flight socket is closed to abort.
    private const val CONNECT_BUDGET_MS = 8000

    // ESC/POS "initialize printer" — clears leftover state at the start of every job
    private val ESC_INIT: ByteArray = byteArrayOf(0x1B, 0x40)

    // Guards the RFCOMM link: keep-alive must never write mid-print
    private val printLock = Object()
    @Volatile
    private var printing = false

    @Volatile
    private var socket: BluetoothSocket? = null

    @Volatile
    private var lastAddress: String? = null

    @Volatile
    private var keepAlive: Thread? = null

    // Socket currently inside an openSocket() connect attempt — closed on deadline
    // to abort a blocked connect() (the only reliable way to unfreeze it).
    @Volatile
    private var pinSocket: BluetoothSocket? = null

    val isConnected: Boolean
        get() = socket?.isConnected == true

    fun getPairedDevices(): List<BluetoothDevice> {
        val adapter = BluetoothAdapter.getDefaultAdapter() ?: return emptyList()
        return adapter.bondedDevices?.toList() ?: emptyList()
    }

    /**
     * Returns only printer devices (filters out speakers, earphones, phones, etc.).
     * Matches by Bluetooth class (Imaging+PRINTER minor, or Peripheral) OR common thermal printer name keywords.
     */
    fun getPrinterDevices(): List<BluetoothDevice> {
        val all = getPairedDevices()
        val printers = all.filter { d ->
            isPrinterDevice(d)
        }
        return printers
    }

    fun isPrinterDevice(device: BluetoothDevice): Boolean {
        return try {
            isPrinterClass(device.bluetoothClass) || isPrinterName(device.name ?: "")
        } catch (_: Exception) {
            false
        }
    }

    private fun isPrinterClass(bc: android.bluetooth.BluetoothClass?): Boolean {
        if (bc == null) return false
        val major = bc.majorDeviceClass
        // Imaging major (0x0600) with PRINTER minor (0x80); Peripheral major (0x0500) covers some BT printers
        return major == 0x0600 || major == 0x0500
    }

    private fun isPrinterName(name: String): Boolean {
        val n = name.lowercase()
        return n.contains("printer") ||
            n.contains("thermal") ||
            n.contains("xprinter") ||
            n.contains("xp-") ||
            n.contains("q80") ||
            n.contains("mp-") ||
            n.contains("80mm") ||
            n.contains("58mm") ||
            n.contains("spp")
    }

    /**
     * Connect like Loyverse/Zobaze do: try standard SPP UUID (secure+insecure),
     * Android SPP alias, SDP-discovered UUIDs, then enumerate raw RFCOMM channels.
     * Cheap multi-device printers expose several service slots and commonly only
     * accept connection on a non-default channel - enumeration finds it.
     */
    fun connect(address: String): String? {
        try {
            disconnect()
            lastAddress = address
            val adapter = BluetoothAdapter.getDefaultAdapter() ?: return "Bluetooth not available"
            val device = adapter.getRemoteDevice(address)
            adapter.cancelDiscovery()

            // Run the whole connection matrix on a worker thread with a hard
            // budget. On deadline the in-flight socket is closed, which aborts
            // any blocked connect() — so the app never freezes, it just says
            // "timed out" and you can tap the printer again.
            val deadline = System.currentTimeMillis() + CONNECT_BUDGET_MS
            var result: BluetoothSocket? = null
            val worker = Thread {
                try {
                    result = tryMatrix(device, deadline)
                } catch (e: Exception) {
                    Log.e(TAG, "connect worker failed", e)
                }
            }
            worker.start()
            worker.join(CONNECT_BUDGET_MS.toLong())
            if (result != null) {
                socket = result
                startKeepAlive()
                return null
            }
            pinSocket?.let { s -> try { s.close() } catch (_: IOException) { } }
            worker.join(300)
            if (result != null) {
                socket = result
                startKeepAlive()
                return null
            }
            return if (worker.isAlive) "Connection timed out (printer busy?)" else "Connection failed (all channels tried)"
        } catch (e: Exception) {
            Log.e(TAG, "connect failed", e)
            socket = null
            stopKeepAlive()
            return e.message ?: "Connection failed"
        }
    }

    private fun tryMatrix(device: BluetoothDevice, deadline: Long): BluetoothSocket? {
        val remaining = { (deadline - System.currentTimeMillis()).toInt().coerceIn(30, CONNECT_TIMEOUT_MS) }
        // 1) Standard SPP UUID (secure then insecure)
        tryUuid(device, SPP_UUID, remaining())?.let { return it }
        // 2) Android SPP alias UUID
        tryUuid(device, ANDROID_SPP_UUID, remaining())?.let { return it }
        // 3) SDP-discovered UUIDs (what the printer actually advertises)
        for (u in fetchSdpUuids(device)) {
            if (u == SPP_UUID || u == ANDROID_SPP_UUID) continue
            tryUuid(device, u, remaining())?.let { return it }
        }
        // 4) Raw RFCOMM channel enumeration - finds the free slot even when other
        //    apps (Zobaze/Loyverse) already hold the default channel. Stops as
        //    soon as the connect budget runs out.
        for (ch in 1..CHANNEL_MAX) {
            val r = remaining()
            if (r < 100) return null
            tryChannel(device, ch, insecure = false, timeoutMs = minOf(CHANNEL_TIMEOUT_MS, r))?.let { return it }
        }
        for (ch in 1..CHANNEL_MAX) {
            val r = remaining()
            if (r < 100) return null
            tryChannel(device, ch, insecure = true, timeoutMs = minOf(CHANNEL_TIMEOUT_MS, r))?.let { return it }
        }
        return null
    }

    private fun tryUuid(device: BluetoothDevice, uuid: UUID, timeoutMs: Int = CONNECT_TIMEOUT_MS): BluetoothSocket? {
        val s = try { device.createRfcommSocketToServiceRecord(uuid) } catch (e: Exception) { return null }
        if (openSocket(s, timeoutMs)) return s
        try { s.close() } catch (_: IOException) { }
        val si = createInsecureRfcomm(device, uuid) ?: return null
        if (openSocket(si, timeoutMs)) return si
        try { si.close() } catch (_: IOException) { }
        return null
    }

    private fun createInsecureRfcomm(device: BluetoothDevice, uuid: UUID): BluetoothSocket? {
        return try {
            val m = device.javaClass.getMethod(
                "createInsecureRfcommSocketToServiceRecord",
                java.util.UUID::class.java
            )
            m.invoke(device, uuid) as BluetoothSocket
        } catch (_: Exception) {
            null
        }
    }

    /**
     * Asks the printer for its SDP records (up to ~4s) so we know which
     * RFCOMM channels/UUIDs it actually exposes.
     */
    private fun fetchSdpUuids(device: BluetoothDevice): List<UUID> {
        val known = device.uuids
        if (known != null && known.isNotEmpty()) return known.map { it.uuid }
        return try {
            device.fetchUuidsWithSdp()
            val deadline = System.currentTimeMillis() + 4000
            while (System.currentTimeMillis() < deadline) {
                val u = device.uuids
                if (u != null && u.isNotEmpty()) return u.map { it.uuid }
                try { Thread.sleep(200) } catch (_: InterruptedException) { break }
            }
            device.uuids?.map { it.uuid } ?: emptyList()
        } catch (_: Exception) { emptyList() }
    }

    /**
     * Connects to a raw RFCOMM channel via hidden API reflection.
     */
    private fun tryChannel(device: BluetoothDevice, channel: Int, insecure: Boolean, timeoutMs: Int = CHANNEL_TIMEOUT_MS): BluetoothSocket? {
        val s = try {
            val method = device.javaClass.getMethod(
                if (insecure) "createInsecureRfcommSocket" else "createRfcommSocket",
                Int::class.javaPrimitiveType
            )
            method.invoke(device, channel) as BluetoothSocket
        } catch (_: Exception) { return null }
        if (openSocket(s, timeoutMs)) return s
        try { s.close() } catch (_: IOException) { }
        return null
    }

    /**
     * Bounded connect. Prefers the hidden connect(int) overload (self-timeouts);
     * otherwise runs the plain connect() on a helper thread and aborts it by
     * closing the socket when the timeout expires. Never calls s.connect()
     * directly — that can block for minutes on a busy printer slot.
     */
    private fun openSocket(s: BluetoothSocket, timeoutMs: Int): Boolean {
        pinSocket = s
        return try {
            try {
                s.javaClass.getMethod("connect", Int::class.java).apply {
                    isAccessible = true
                    invoke(s, timeoutMs)
                }
                true
            } catch (_: NoSuchMethodException) {
                val done = java.util.concurrent.atomic.AtomicBoolean(false)
                val t = Thread {
                    try {
                        s.connect()
                        done.set(true)
                    } catch (_: Exception) { }
                }
                t.start()
                val deadline = System.currentTimeMillis() + timeoutMs
                while (!done.get() && System.currentTimeMillis() < deadline) {
                    try { Thread.sleep(20) } catch (_: InterruptedException) { break }
                }
                if (!done.get()) {
                    // Abort: closing the socket unblocks the stuck connect()
                    try { s.close() } catch (_: IOException) { }
                    false
                } else {
                    true
                }
            }
        } catch (_: Exception) {
            try { s.close() } catch (_: IOException) { }
            false
        } finally {
            pinSocket = null
        }
    }

    fun disconnect() {
        stopKeepAlive()
        try { socket?.close() } catch (_: IOException) { }
        socket = null
    }

    /**
     * Keeps the printer's RFCOMM link alive while idle. Many cheap thermal printers
     * sleep/shut down their BT radio after ~1-2 minutes with no inbound data, which
     * makes the POS think it's "connected" but the first print fails with
     * "read failed, socket might closed, read ret: -1".
     *
     * Pings with ESC @ (initialize) instead of DLE EOT — cheap clones print
     * garbage on DLE EOT and it can corrupt the stream. Never writes while a
     * print job is in progress (printLock).
     */
    private fun startKeepAlive() {
        stopKeepAlive()
        keepAlive = Thread {
            while (true) {
                try {
                    Thread.sleep(8000)
                    if (keepAlive !== Thread.currentThread()) break
                    val s = socket ?: break
                    if (!s.isConnected) break
                    if (printing) continue
                    var busy = false
                    synchronized(printLock) {
                        if (printing) { busy = true; return@synchronized }
                        try {
                            // ESC @ — no-op reset, keeps printer awake, prints nothing
                            val out = s.outputStream
                            out.write(ESC_INIT)
                            out.flush()
                        } catch (e: Exception) {
                            // Link died while idle — silently reconnect so the next print works
                            Log.e(TAG, "keepalive died, reconnecting", e)
                            val addr = lastAddress
                            if (addr != null) connect(addr)
                            return@synchronized
                        }
                    }
                    if (busy) continue
                } catch (_: InterruptedException) {
                    break
                }
            }
        }.apply { isDaemon = true; name = "PrinterKeepAlive"; start() }
    }

    private fun stopKeepAlive() {
        keepAlive?.interrupt()
        keepAlive = null
    }

    /**
     * Prints raw ESC/POS bytes. Throws IOException on failure.
     *
     * Data is written in small chunks with a short delay between each so the cheap
     * thermal printer buffer never overflows. Sending the whole receipt in a single
     * write() on these SPP printers causes the socket to close with
     * "read failed, socket might closed or timeout, read ret: -1".
     *
     * Every job starts with ESC @ (initialize) to clear any leftover printer state
     * that would otherwise print as garbage before the receipt header, and the
     * whole job runs under printLock so the keep-alive ping can't interleave.
     */
    @Throws(IOException::class)
    fun printBytes(bytes: ByteArray) {
        synchronized(printLock) {
            printing = true
            try {
                val job = ESC_INIT + bytes
                try {
                    writeChunked(job)
                } catch (e: Exception) {
                    // Socket is stale — reconnect and retry the whole job once (like POS SDKs do).
                    Log.e(TAG, "print failed, reconnecting + retry", e)
                    val addr = lastAddress
                    if (addr != null) {
                        val err = connect(addr)
                        if (err == null) {
                            Thread.sleep(200)
                            writeChunked(job)
                            return@synchronized
                        }
                    }
                    throw IOException("Print failed: " + (e.message ?: "connection lost"))
                }
            } finally {
                printing = false
            }
        }
    }

    private fun writeChunked(bytes: ByteArray) {
        val s = socket ?: throw IOException("Not connected")
        val out = s.outputStream
        val chunkSize = 96
        var offset = 0
        while (offset < bytes.size) {
            val len = minOf(chunkSize, bytes.size - offset)
            out.write(bytes, offset, len)
            out.flush()
            offset += len
            // Give the printer time to drain its RX buffer between chunks.
            try { Thread.sleep(8) } catch (_: InterruptedException) { }
        }
    }

    /**
     * Prints plain text receipt using ESC/POS. Handles alignment and line feeds.
     * 80mm printer = 48 chars per line at font size 1.
     *
     * Non-ASCII characters (like ₱, U+20B1) are replaced with ASCII-safe text so
     * the encoding never drops the socket mid-stream.
     */
    fun printText(text: String, width: Int = 48) {
        val out = ByteArrayOutputStream()
        val lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
        for (line in lines) {
            if (line.trim().isEmpty()) {
                out.write(ESC_NL)
                out.write('\n'.code)
                continue
            }
            // Alignment detection
            val trimmed = line.trim()
            val align = when {
                trimmed.startsWith("<<CENTER>>") -> "center"
                trimmed.startsWith("<<RIGHT>>") -> "right"
                else -> "left"
            }
            val content = trimmed
                .removePrefix("<<CENTER>>")
                .removePrefix("<<RIGHT>>")
                // Peso sign (U+20B1) is outside 7-bit ASCII - print "P" instead of "?"
                .replace('\u20B1', 'P')

            when (align) {
                "center" -> out.write(ESC_ALIGN_CENTER)
                "right" -> out.write(ESC_ALIGN_RIGHT)
                else -> out.write(ESC_ALIGN_LEFT)
            }
            out.write(ESC_BOLD_ON)
            out.write(content.toByteArray(Charsets.US_ASCII))
            out.write('\n'.code)
        }
        // Cut paper + feed
        out.write(ESC_FEED_3)
        out.write(ESC_CUT)

        try {
            printBytes(out.toByteArray())
        } catch (e: Exception) {
            throw e
        }
    }

    fun printTest() {
        val out = ByteArrayOutputStream()
        out.write(ESC_ALIGN_CENTER)
        out.write(ESC_BOLD_ON)
        out.write("JUMONG WAREHOUSE\n".toByteArray(Charsets.US_ASCII))
        out.write(ESC_BOLD_ON)
        out.write("Bluetooth Printer OK\n".toByteArray(Charsets.US_ASCII))
        out.write(ESC_BOLD_ON)
        out.write("Testing...\n".toByteArray(Charsets.US_ASCII))
        out.write('\n'.code)
        out.write('\n'.code)
        out.write(ESC_FEED_3)
        out.write(ESC_CUT)
        try {
            printBytes(out.toByteArray())
        } catch (e: Exception) {
            throw e
        }
    }

    // ESC/POS commands
    private const val ESC: Byte = 0x1B
    private const val GS: Byte = 0x1D
    private val ESC_ALIGN_LEFT = byteArrayOf(ESC, 0x61, 0x00)
    private val ESC_ALIGN_CENTER = byteArrayOf(ESC, 0x61, 0x01)
    private val ESC_ALIGN_RIGHT = byteArrayOf(ESC, 0x61, 0x02)
    private val ESC_NL = byteArrayOf(ESC, 0x64, 0x01)
    private val ESC_BOLD_ON = byteArrayOf(ESC, 0x45, 0x01)
    private val ESC_FEED_3 = byteArrayOf(ESC, 0x64, 0x03)
    private val ESC_CUT = byteArrayOf(GS, 0x56, 0x42, 0x00)
}
