package com.jumong.warehouse

import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothSocket
import android.content.Context
import android.util.Log
import java.io.IOException
import java.util.UUID

object BluetoothPrinter {
    private const val TAG = "WhBluetooth"
    private val SPP_UUID: UUID = UUID.fromString("00001101-0000-1000-8000-00805F9B34FB")

    @Volatile
    private var socket: BluetoothSocket? = null

    @Volatile
    private var lastAddress: String? = null

    @Volatile
    private var keepAlive: Thread? = null

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
            isPrinterClass(d.bluetoothClass) || isPrinterName(d.name ?: "")
        }
        return printers
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
     * Connect with a timeout so the UI never hangs, then fall back to an insecure
     * RFCOMM socket if the standard SPP connection is refused (common on cheap
     * thermal printers — same trick Loyverse/Zobaze-style apps use).
     */
    fun connect(address: String): String? {
        try {
            disconnect()
            lastAddress = address
            val adapter = BluetoothAdapter.getDefaultAdapter() ?: return "Bluetooth not available"
            val device = adapter.getRemoteDevice(address)
            adapter.cancelDiscovery()

            // 1st try: standard SPP socket
            var s = device.createRfcommSocketToServiceRecord(SPP_UUID)
            if (!openSocket(s)) {
                // 2nd try: insecure RFCOMM (many printers reject the secure channel)
                s = createInsecureRfcomm(device)
                if (s == null || !openSocket(s)) {
                    return "Connection refused"
                }
            }
            socket = s
            startKeepAlive()
            return null
        } catch (e: Exception) {
            Log.e(TAG, "connect failed", e)
            socket = null
            stopKeepAlive()
            return e.message ?: "Connection failed"
        }
    }

    private fun openSocket(s: BluetoothSocket): Boolean {
        return try {
            // Enforce a 8s connect timeout via reflection (s.connect() can block forever)
            s.javaClass.getMethod("connect", Int::class.java).apply {
                isAccessible = true
                invoke(s, 8000)
            }
            true
        } catch (_: NoSuchMethodException) {
            try {
                s.connect()
                true
            } catch (_: Exception) {
                try { s.close() } catch (_: IOException) { }
                false
            }
        } catch (_: Exception) {
            try { s.close() } catch (_: IOException) { }
            false
        }
    }

    private fun createInsecureRfcomm(device: BluetoothDevice): BluetoothSocket? {
        return try {
            val m = device.javaClass.getMethod("createInsecureRfcommSocketToServiceRecord", UUID::class.java)
            m.invoke(device, SPP_UUID) as BluetoothSocket
        } catch (_: Exception) {
            null
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
     * This mirrors what Loyverse/Zobaze-style apps do: a background status poll
     * every ~8s sends a tiny ESC/POS status request so the printer never sleeps.
     * If the write fails the link is considered dead and we auto-reconnect.
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
                    try {
                        // ESC/POS DLE EOT printer status request — 3 tiny bytes, no paper feed
                        val out = s.outputStream
                        out.write(byteArrayOf(0x10, 0x04, 0x01))
                        out.flush()
                    } catch (e: Exception) {
                        // Link died while idle — silently reconnect so the next print works
                        Log.e(TAG, "keepalive died, reconnecting", e)
                        val addr = lastAddress
                        if (addr != null) connect(addr)
                        break
                    }
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
     */
    @Throws(IOException::class)
    fun printBytes(bytes: ByteArray) {
        try {
            writeChunked(bytes)
        } catch (e: Exception) {
            // Socket is stale — reconnect and retry the whole job once (like POS SDKs do).
            Log.e(TAG, "print failed, reconnecting + retry", e)
            val addr = lastAddress
            if (addr != null) {
                val err = connect(addr)
                if (err == null) {
                    Thread.sleep(200)
                    writeChunked(bytes)
                    return
                }
            }
            throw IOException("Print failed: " + (e.message ?: "connection lost"))
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
        val sb = StringBuilder()
        val lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
        for (line in lines) {
            if (line.trim().isEmpty()) {
                sb.append(ESC_NL).append('\n')
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
                // ₱ (U+20B1) is outside 7-bit ASCII — print "P" instead of "?"
                .replace('\u20B1', 'P')

            when (align) {
                "center" -> {
                    sb.append(ESC_ALIGN_CENTER)
                    sb.append(content).append('\n')
                }
                "right" -> {
                    sb.append(ESC_ALIGN_RIGHT)
                    sb.append(content).append('\n')
                }
                else -> {
                    sb.append(ESC_ALIGN_LEFT)
                    sb.append(content).append('\n')
                }
            }
        }
        // Cut paper + feed
        sb.append(ESC_FEED_3)
        sb.append(ESC_CUT)

        try {
            printBytes(sb.toString().toByteArray(Charsets.US_ASCII))
        } catch (e: Exception) {
            throw e
        }
    }

    fun printTest() {
        val sb = StringBuilder()
        sb.append(ESC_ALIGN_CENTER)
        sb.append("JUMONG WAREHOUSE\n")
        sb.append("Bluetooth Printer OK\n")
        sb.append("Testing...\n")
        sb.append('\n').append('\n')
        sb.append(ESC_FEED_3)
        sb.append(ESC_CUT)
        try {
            printBytes(sb.toString().toByteArray(Charsets.US_ASCII))
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
    private val ESC_FEED_3 = byteArrayOf(ESC, 0x64, 0x03)
    private val ESC_CUT = byteArrayOf(GS, 0x56, 0x42, 0x00)
}
