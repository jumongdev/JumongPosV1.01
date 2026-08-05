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

    fun connect(address: String): String? {
        try {
            disconnect()
            val adapter = BluetoothAdapter.getDefaultAdapter() ?: return "Bluetooth not available"
            val device = adapter.getRemoteDevice(address)
            val s = device.createRfcommSocketToServiceRecord(SPP_UUID)
            adapter.cancelDiscovery()
            s.connect()
            socket = s
            return null
        } catch (e: Exception) {
            Log.e(TAG, "connect failed", e)
            socket = null
            return e.message ?: "Connection failed"
        }
    }

    fun disconnect() {
        try { socket?.close() } catch (_: IOException) { }
        socket = null
    }

    /**
     * Prints raw ESC/POS bytes. Throws IOException on failure.
     */
    @Throws(IOException::class)
    fun printBytes(bytes: ByteArray) {
        val s = socket ?: throw IOException("Not connected")
        val out = s.outputStream
        out.write(bytes)
        out.flush()
    }

    /**
     * Prints plain text receipt using ESC/POS. Handles alignment and line feeds.
     * 80mm printer = 48 chars per line at font size 1.
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
