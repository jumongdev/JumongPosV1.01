package com.jumong.warehouse

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.content.BroadcastReceiver
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.provider.Settings
import android.util.Log
import android.webkit.JavascriptInterface
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import android.webkit.PermissionRequest
import android.webkit.WebChromeClient
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.net.HttpURLConnection
import java.net.URL

@SuppressLint("SetJavaScriptEnabled")
class MainActivity : AppCompatActivity() {

    private lateinit var webView: WebView
    private var pendingPrinterDevice: String? = null
    private var pendingApkFile: File? = null

    private val requestBtPermission =
        registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { grants ->
            if (grants.values.any { !it } && Build.VERSION.SDK_INT >= 31) {
                if (!granted(Manifest.permission.BLUETOOTH_CONNECT)) {
                    // Try enabling BT anyway
                }
            }
        }

    private fun granted(p: String): Boolean =
        ContextCompat.checkSelfPermission(this, p) == PackageManager.PERMISSION_GRANTED

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Capture uncaught crashes into a file so they can be auto-reported to the
        // API on the next successful launch. Everything here must be safe — the
        // handler itself must never crash.
        Thread.setDefaultUncaughtExceptionHandler { thread, throwable ->
            try {
                var versionName = "?"
                try {
                    versionName = packageManager.getPackageInfo(packageName, 0).versionName ?: "?"
                } catch (_: Exception) { }
                val dir = getExternalFilesDir(null) ?: filesDir
                dir.mkdirs()
                val sb = StringBuilder()
                sb.append("=== CRASH ").append(System.currentTimeMillis()).append(" ===\n")
                sb.append("version: ").append(versionName).append('\n')
                sb.append("device: ").append(Build.MANUFACTURER).append(" ").append(Build.MODEL)
                    .append(" | sdk ").append(Build.VERSION.SDK_INT).append(" | android ").append(Build.VERSION.RELEASE).append('\n')
                sb.append("thread: ").append(thread.name).append('\n')
                sb.append("failed: ").append(throwable.javaClass.name).append(": ").append(throwable.message).append('\n')
                for (el in throwable.stackTrace) sb.append("    at ").append(el).append('\n')
                try {
                    File(dir, "crash.log").writeText(sb.toString())
                } catch (_: Exception) { }
            } catch (_: Exception) { }
            // Keep original behavior so the OS still shows its default error screen.
            Log.e("JumongCrash", "uncaught on ${thread.name}", throwable)
        }

        webView = WebView(this)
        // Dark WebView background so the pre-first-paint gap matches the native
        // splash — no white flash while the page downloads (was white before).
        webView.setBackgroundColor(android.graphics.Color.rgb(16, 16, 42))
        val pullToRefresh = androidx.swiperefreshlayout.widget.SwipeRefreshLayout(this)
        pullToRefresh.addView(
            webView,
            android.widget.FrameLayout.LayoutParams(
                android.widget.FrameLayout.LayoutParams.MATCH_PARENT,
                android.widget.FrameLayout.LayoutParams.MATCH_PARENT
            )
        )
        pullToRefresh.setColorSchemeResources(android.R.color.holo_blue_light, android.R.color.holo_green_light, android.R.color.holo_orange_light)
        pullToRefresh.setOnRefreshListener {
            // Modern refresh: re-run current tab's data loader in the web page.
            // Keeps user's position, cart, and search results — no full page reload.
            webView.evaluateJavascript("refreshCurrentTab()", null)
            webView.postDelayed({ pullToRefresh.isRefreshing = false }, 800)
        }
        // Only allow pull-to-refresh when scrolled to top (avoid accidental mid-scroll triggers)
        pullToRefresh.setOnChildScrollUpCallback { _, _ ->
            webView.scrollY > 0
        }
        // Push content below status bar / nav bar (fixes header hidden behind time/battery)
        ViewCompat.setOnApplyWindowInsetsListener(pullToRefresh) { v, insets ->
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            v.setPadding(0, bars.top, 0, bars.bottom)
            WindowInsetsCompat.CONSUMED
        }
        setContentView(pullToRefresh)

        val settings = webView.settings
        settings.javaScriptEnabled = true
        settings.domStorageEnabled = true
        settings.allowFileAccess = true
        settings.cacheMode = WebSettings.LOAD_NO_CACHE
        settings.mixedContentMode = WebSettings.MIXED_CONTENT_ALWAYS_ALLOW

        webView.webViewClient = object : WebViewClient() {
            override fun shouldOverrideUrlLoading(view: WebView?, request: WebResourceRequest?): Boolean {
                val url = request?.url?.toString() ?: return false
                // Only allow jumongdev.com (and localhost for dev)
                if (url.contains("jumongdev.com") || url.startsWith("http://localhost") || url.startsWith("http://192.168.")) {
                    view?.loadUrl(url)
                    return true
                }
                return false
            }

            override fun onPageFinished(view: WebView?, url: String?) {
                super.onPageFinished(view, url)
                pullToRefresh.isRefreshing = false
            }
        }

        webView.webChromeClient = object : WebChromeClient() {
            override fun onPermissionRequest(request: PermissionRequest) {
                // Allow camera for barcode scanning
                request.grant(request.resources)
            }
        }

        webView.addJavascriptInterface(PrinterBridge(), "AndroidPrinter")
        webView.addJavascriptInterface(AppBridge(), "AndroidApp")

        // No clearCache() here — it runs synchronously on the main thread and blocks
        // startup on slow phones (ANR "app not responding"). The URL below is
        // version-busted (?v=timestamp) and cacheMode is LOAD_NO_CACHE, so web
        // updates still reach the app fresh without clearing anything.
        val url = "https://admin.jumongdev.com/whmobile.html?v=" + System.currentTimeMillis()
        webView.loadUrl(url)

        // Auto-connect to last saved printer (background attempt, no UI block)
        val savedPrinter = getSharedPreferences("wh_prefs", MODE_PRIVATE)
            .getString("last_printer", "")
        if (!savedPrinter.isNullOrEmpty()) {
            Thread {
                BluetoothPrinter.connect(savedPrinter)
            }.start()
        }

        requestBluetoothPermissions()
    }

    override fun onPause() {
        super.onPause()
        // Release the printer while backgrounded so other devices can use it
        BluetoothPrinter.disconnect()
    }

    private fun requestBluetoothPermissions() {
        val perms = mutableListOf<String>()
        if (Build.VERSION.SDK_INT >= 31) {
            perms.add(Manifest.permission.BLUETOOTH_CONNECT)
            perms.add(Manifest.permission.BLUETOOTH_SCAN)
        } else {
            perms.add(Manifest.permission.ACCESS_FINE_LOCATION)
            perms.add(Manifest.permission.BLUETOOTH)
            perms.add(Manifest.permission.BLUETOOTH_ADMIN)
        }
        val needed = perms.filter { !granted(it) }
        if (needed.isNotEmpty()) {
            requestBtPermission.launch(needed.toTypedArray())
        }
    }

    override fun onDestroy() {
        BluetoothPrinter.disconnect()
        super.onDestroy()
    }

    /**
     * Switches the launcher icon among the pre-shipped aliases (default/xmas/gold/blue)
     * via PackageManager.setComponentEnabledSetting. One alias is enabled at a time.
     */
    private fun setAppIconFor(key: String) {
        try {
            val target = when (key) {
                "xmas" -> "$packageName.AliasXmas"
                "gold" -> "$packageName.AliasGold"
                "blue" -> "$packageName.AliasBlue"
                else -> "$packageName.AliasDefault"
            }
            for (alias in listOf("AliasDefault", "AliasXmas", "AliasGold", "AliasBlue")) {
                val cn = ComponentName(this, "$packageName.$alias")
                val state = if ("$packageName.$alias" == target)
                    PackageManager.COMPONENT_ENABLED_STATE_ENABLED
                else
                    PackageManager.COMPONENT_ENABLED_STATE_DISABLED
                packageManager.setComponentEnabledSetting(cn, state, PackageManager.DONT_KILL_APP)
            }
        } catch (e: Exception) {
            Log.w("JumongAppIcon", "setAppIcon failed: ${e.message}")
        }
    }

    override fun onBackPressed() {
        if (webView.canGoBack()) webView.goBack() else super.onBackPressed()
    }

    // ─── JS Bridge: Printer ───────────────────────────────────────────────
    inner class PrinterBridge {
        @JavascriptInterface
        fun getPrinters(): String {
            val arr = JSONArray()
            if (Build.VERSION.SDK_INT >= 31 && !granted(Manifest.permission.BLUETOOTH_CONNECT)) {
                return "[]"
            }
            BluetoothPrinter.getPrinterDevices().forEach { d ->
                arr.put(JSONObject().apply {
                    put("name", d.name ?: "Unknown")
                    put("address", d.address)
                    put("paired", true)
                })
            }
            return arr.toString()
        }

        @JavascriptInterface
        fun isConnected(): Boolean = BluetoothPrinter.isConnected

        @JavascriptInterface
        fun connect(address: String): String {
            val err = BluetoothPrinter.connect(address)
            if (err == null) {
                // Save this printer as the auto-connect target for next launches
                getSharedPreferences("wh_prefs", MODE_PRIVATE)
                    .edit().putString("last_printer", address).apply()
            }
            return if (err == null) "OK" else "ERR:$err"
        }

        @JavascriptInterface
        fun disconnect() {
            BluetoothPrinter.disconnect()
        }

        @JavascriptInterface
        fun print(text: String): String {
            return try {
                BluetoothPrinter.printText(text)
                "OK"
            } catch (e: Exception) {
                "ERR:${e.message}"
            }
        }

        @JavascriptInterface
        fun printTest(): String {
            return try {
                BluetoothPrinter.printTest()
                "OK"
            } catch (e: Exception) {
                "ERR:${e.message}"
            }
        }

        @JavascriptInterface
        fun enableBluetooth() {
            val adapter = BluetoothAdapter.getDefaultAdapter()
            if (adapter != null && !adapter.isEnabled) {
                if (Build.VERSION.SDK_INT >= 31) {
                    if (granted(Manifest.permission.BLUETOOTH_CONNECT)) {
                        adapter.enable()
                    }
                } else {
                    adapter.enable()
                }
            }
        }

        /**
         * Discovers nearby Bluetooth devices (like Loyverse's Search button).
         * Runs for ~8s, returns JSON [{name, address, paired}].
         */
        @JavascriptInterface
        fun searchDevices(): String {
            val adapter = BluetoothAdapter.getDefaultAdapter() ?: return "[]"
            if (Build.VERSION.SDK_INT >= 31) {
                if (!granted(Manifest.permission.BLUETOOTH_CONNECT) || !granted(Manifest.permission.BLUETOOTH_SCAN)) {
                    return "ERR:PERMISSION"
                }
            } else {
                if (!granted(Manifest.permission.ACCESS_FINE_LOCATION)) {
                    return "ERR:PERMISSION"
                }
            }
            if (!adapter.isEnabled) {
                adapter.enable()
                try { Thread.sleep(1500) } catch (_: InterruptedException) { }
            }
            val found = linkedMapOf<String, JSONObject>()
            try {
                BluetoothPrinter.getPairedDevices().forEach { d ->
                    if (!BluetoothPrinter.isPrinterDevice(d)) return@forEach
                    found[d.address] = JSONObject().apply {
                        put("name", d.name ?: "Unknown")
                        put("address", d.address)
                        put("paired", true)
                    }
                }
            } catch (_: Exception) { }
            val receiver = object : BroadcastReceiver() {
                override fun onReceive(context: Context?, intent: Intent?) {
                    if (intent?.action != BluetoothDevice.ACTION_FOUND) return
                    val d = intent.getParcelableExtra<BluetoothDevice>(BluetoothDevice.EXTRA_DEVICE) ?: return
                    if (!BluetoothPrinter.isPrinterDevice(d)) return
                    val name = d.name ?: return
                    found[d.address] = JSONObject().apply {
                        put("name", name)
                        put("address", d.address)
                        put("paired", d.bondState == BluetoothDevice.BOND_BONDED)
                    }
                }
            }
            registerReceiver(receiver, IntentFilter(BluetoothDevice.ACTION_FOUND))
            try { adapter.startDiscovery() } catch (_: Exception) { }
            try { Thread.sleep(12000) } catch (_: InterruptedException) { }
            try { adapter.cancelDiscovery() } catch (_: Exception) { }
            try { unregisterReceiver(receiver) } catch (_: Exception) { }
            return JSONArray(found.values.toList()).toString()
        }

        @JavascriptInterface
        fun pairDevice(address: String): String {
            return try {
                val adapter = BluetoothAdapter.getDefaultAdapter() ?: return "ERR:no-bt"
                val d = adapter.getRemoteDevice(address)
                d.createBond()
                "OK"
            } catch (e: Exception) {
                "ERR:${e.message}"
            }
        }
    }

    // ─── JS Bridge: App utilities ─────────────────────────────────────────
    inner class AppBridge {
        @JavascriptInterface
        fun toast(msg: String) {
            runOnUiThread { Toast.makeText(this@MainActivity, msg, Toast.LENGTH_LONG).show() }
        }

        @JavascriptInterface
        fun getVersion(): String {
            return try {
                packageManager.getPackageInfo(packageName, 0).versionName ?: "1.0.0"
            } catch (e: Exception) {
                "1.0.0"
            }
        }

        @JavascriptInterface
        fun getPaperWidth(): Int {
            return getSharedPreferences("wh_prefs", MODE_PRIVATE).getInt("paper_width", 80)
        }

        @JavascriptInterface
        fun setPaperWidth(w: Int) {
            getSharedPreferences("wh_prefs", MODE_PRIVATE)
                .edit().putInt("paper_width", if (w <= 50) 50 else 80).apply()
        }

        @JavascriptInterface
        fun setAppIcon(key: String) {
            setAppIconFor(key)
        }

        @JavascriptInterface
        fun getCrashLog(): String {
            return try {
                val f = File(getExternalFilesDir(null) ?: filesDir, "crash.log")
                if (f.exists()) f.readText() else ""
            } catch (_: Exception) { "" }
        }

        @JavascriptInterface
        fun clearCrashLog() {
            try {
                val f = File(getExternalFilesDir(null) ?: filesDir, "crash.log")
                if (f.exists()) f.delete()
            } catch (_: Exception) { }
        }

        @JavascriptInterface
        fun openSettings() {
            startActivity(Intent(Settings.ACTION_SETTINGS))
        }

        @JavascriptInterface
        fun checkForUpdate(versionUrl: String): String {
            return try {
                val conn = URL(versionUrl).openConnection() as HttpURLConnection
                conn.connectTimeout = 10000
                conn.readTimeout = 10000
                // Read as UTF-8 and strip any BOM so JSON.parse never sees an invalid token.
                val text = conn.inputStream.bufferedReader(Charsets.UTF_8).readText().removePrefix("\uFEFF").trim()
                conn.disconnect()
                text
            } catch (e: Exception) {
                "ERR:${e.message}"
            }
        }

        @JavascriptInterface
        fun downloadAndInstall(apkUrl: String) {
            Thread {
                try {
                    val fileName = "JumongWarehouse.apk"
                    val file = File(getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), fileName)
                    val conn = URL(apkUrl).openConnection() as HttpURLConnection
                    conn.connectTimeout = 30000
                    conn.readTimeout = 30000
                    conn.inputStream.use { input ->
                        file.outputStream().use { output -> input.copyTo(output) }
                    }
                    conn.disconnect()
                    runOnUiThread { installApk(file) }
                } catch (e: Exception) {
                    runOnUiThread {
                        Toast.makeText(this@MainActivity, "Update failed: ${e.message}", Toast.LENGTH_LONG).show()
                    }
                }
            }.start()
        }
    }

    private fun installApk(file: File) {
        if (Build.VERSION.SDK_INT >= 26) {
            if (!packageManager.canRequestPackageInstalls()) {
                pendingApkFile = file
                Toast.makeText(this, "Allow 'Install unknown apps' for this app", Toast.LENGTH_LONG).show()
                startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:$packageName")))
                return
            }
        }
        launchApkInstall(file)
    }

    override fun onResume() {
        super.onResume()
        // If the user just granted "Install unknown apps" from Settings, resume the pending install.
        val file = pendingApkFile
        if (file != null && Build.VERSION.SDK_INT >= 26 && packageManager.canRequestPackageInstalls()) {
            pendingApkFile = null
            launchApkInstall(file)
        }
        // Reconnect to last saved printer when app returns to foreground
        val savedPrinter = getSharedPreferences("wh_prefs", MODE_PRIVATE)
            .getString("last_printer", "")
        if (!savedPrinter.isNullOrEmpty() && !BluetoothPrinter.isConnected) {
            Thread { BluetoothPrinter.connect(savedPrinter) }.start()
        }
    }

    private fun launchApkInstall(file: File) {
        val uri = androidx.core.content.FileProvider.getUriForFile(this, "$packageName.fileprovider", file)
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_GRANT_READ_URI_PERMISSION
        }
        startActivity(intent)
    }
}
