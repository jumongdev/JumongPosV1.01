package com.jumong.driver

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.provider.MediaStore
import android.provider.Settings
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.view.WindowManager
import android.widget.BaseAdapter
import android.widget.EditText
import android.widget.ImageView
import android.widget.ListView
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.FileProvider
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import org.json.JSONArray
import org.json.JSONObject
import android.util.Log
import java.io.BufferedReader
import java.io.File
import java.io.InputStreamReader
import java.io.OutputStream
import java.net.HttpURLConnection
import java.net.URL
import java.text.NumberFormat
import java.util.Locale

class MainActivity : AppCompatActivity() {

    companion object {
        const val API = "https://admin.jumongdev.com/api/dashboard"
        const val ASSETS = "https://admin.jumongdev.com/assets/"
        const val UPDATES = "https://driver.jumongdev.com/updates"
        const val REQ_CAMERA_PIC = 1001
        const val REQ_CAMERA_PIC2 = 1002
    }

    private lateinit var prefs: android.content.SharedPreferences
    private var token: String = ""
    private var drvName: String = ""
    private var orders: JSONArray = JSONArray()
    private var visOrders: JSONArray = JSONArray()
    private var onlyCollect = true
    private var curOrder: JSONObject? = null
    private var curItems: JSONArray = JSONArray()
    private var payMethod = "cash"
    private var pendingPicFor: Int = -1
    private var pic0: File? = null
    private var pic1: File? = null

    // views
    private lateinit var loginScreen: View
    private lateinit var mainScreen: View
    private lateinit var detailScreen: View
    private lateinit var payScreen: View
    private lateinit var cancelOverlay: View
    private lateinit var updateOverlay: View
    private lateinit var endShiftOverlay: View
    private lateinit var historyOverlay: View
    private var endInfo: JSONObject? = null
    private var endShiftOpen = false
    private lateinit var updVersion: TextView
    private lateinit var updChangelog: android.widget.LinearLayout
    private lateinit var orderList: ListView
    private lateinit var lgErr: TextView
    private lateinit var tvDrvName: TextView
    private lateinit var listEmpty: TextView
    private lateinit var tvDetailOrder: TextView
    private lateinit var tvDetailDate: TextView
    private lateinit var tvPaidBadge: TextView
    private lateinit var tvDetailCustomer: TextView
    private lateinit var tvDetailPhone: TextView
    private lateinit var tvDetailAddr: TextView
    private lateinit var tvDetailPay: TextView
    private lateinit var tvDetailNote: TextView
    private lateinit var detailItems: android.widget.LinearLayout
    private lateinit var tvDetailTotal: TextView
    private lateinit var tvPayOrder: TextView
    private lateinit var tvPayTotal: TextView
    private lateinit var tvChange: TextView
    private lateinit var cashRow: View
    private lateinit var gcashRow: View
    private lateinit var splitRow: View
    private lateinit var ivPayQr1: ImageView
    private lateinit var ivPayQr2: ImageView
    private lateinit var tvQrHeader1: TextView
    private lateinit var tvQrHeader2: TextView
    private lateinit var tvPayErr: TextView
    private lateinit var tvPicName: TextView
    private lateinit var tvPicName2: TextView
    private lateinit var tvCancelErr: TextView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE)

        prefs = getSharedPreferences("drv_prefs", Context.MODE_PRIVATE)
        token = prefs.getString("token", "") ?: ""
        drvName = prefs.getString("name", "") ?: ""

        // Report crashes remotely (hindi na kami maghuhula kung may mag-close na app)
        Thread.setDefaultUncaughtExceptionHandler { t, e ->
            try { reportCrash("uncaught", (t?.name ?: "?") + "\n" + Log.getStackTraceString(e)) } catch (_: Exception) {}
        }

        bindViews()
        wireEvents()

        // Back button: mag-navigate sa loob ng app (pay -> detail -> main -> exit)
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                when {
                    cancelOverlay.visibility == View.VISIBLE -> cancelOverlay.visibility = View.GONE
                    historyOverlay.visibility == View.VISIBLE -> closeHistory()
                    endShiftOverlay.visibility == View.VISIBLE -> closeEndShift()
                    payScreen.visibility == View.VISIBLE -> showScreen(detailScreen)
                    detailScreen.visibility == View.VISIBLE -> backToList()
                    else -> { isEnabled = false; onBackPressedDispatcher.onBackPressed() }
                }
            }
        })

        // Swipe down = i-refresh ang deliveries + payment QRs
        val refresh = findViewById<SwipeRefreshLayout>(R.id.refreshLayout)
        refresh.setColorSchemeResources(android.R.color.holo_purple, android.R.color.holo_blue_light)
        refresh.setOnRefreshListener {
            loadOrders(true)
            loadPaymentQrs()
            refresh.postDelayed({ refresh.isRefreshing = false }, 3000)
        }

        token = token.trim()
        if (token.isNotEmpty()) { showScreen(mainScreen); loadOrders(); } else showScreen(loginScreen)
        loadPaymentQrs()
        checkUpdate()
    }

    private fun bindViews() {
        loginScreen = findViewById(R.id.loginScreen)
        mainScreen = findViewById(R.id.mainScreen)
        detailScreen = findViewById(R.id.detailScreen)
        payScreen = findViewById(R.id.payScreen)
        cancelOverlay = findViewById(R.id.cancelOverlay)
        updateOverlay = findViewById(R.id.updateOverlay)
        endShiftOverlay = findViewById(R.id.endShiftOverlay)
        historyOverlay = findViewById(R.id.historyOverlay)
        updVersion = findViewById(R.id.updVersion)
        updChangelog = findViewById(R.id.updChangelog)
        orderList = findViewById(R.id.orderList)
        lgErr = findViewById(R.id.lgErr)
        tvDrvName = findViewById(R.id.drvName)
        listEmpty = findViewById(R.id.listEmpty)
        tvDetailOrder = findViewById(R.id.tvDetailOrder)
        tvDetailDate = findViewById(R.id.tvDetailDate)
        tvPaidBadge = findViewById(R.id.tvPaidBadge)
        tvDetailCustomer = findViewById(R.id.tvDetailCustomer)
        tvDetailPhone = findViewById(R.id.tvDetailPhone)
        tvDetailAddr = findViewById(R.id.tvDetailAddr)
        tvDetailPay = findViewById(R.id.tvDetailPay)
        tvDetailNote = findViewById(R.id.tvDetailNote)
        detailItems = findViewById(R.id.detailItems)
        tvDetailTotal = findViewById(R.id.tvDetailTotal)
        tvPayOrder = findViewById(R.id.tvPayOrder)
        tvPayTotal = findViewById(R.id.tvPayTotal)
        tvChange = findViewById(R.id.tvChange)
        cashRow = findViewById(R.id.cashRow)
        gcashRow = findViewById(R.id.gcashRow)
        splitRow = findViewById(R.id.splitRow)
        ivPayQr1 = findViewById(R.id.ivPayQr1)
        ivPayQr2 = findViewById(R.id.ivPayQr2)
        tvQrHeader1 = findViewById(R.id.tvQrHeader1)
        tvQrHeader2 = findViewById(R.id.tvQrHeader2)
        tvPayErr = findViewById(R.id.tvPayErr)
        tvPicName = findViewById(R.id.tvPicName)
        tvPicName2 = findViewById(R.id.tvPicName2)
        tvCancelErr = findViewById(R.id.tvCancelErr)
    }

    private fun wireEvents() {
        findViewById<View>(R.id.btnLogin).setOnClickListener { doLogin() }
        findViewById<View>(R.id.lgPass).setOnKeyListener { _, keyCode, _ ->
            if (keyCode == android.view.KeyEvent.KEYCODE_ENTER) { doLogin(); true } else false
        }
        findViewById<View>(R.id.btnDownload).setOnClickListener {
            openUrl(UPDATES + "/JumongDriver.apk")
        }
        findViewById<View>(R.id.btnUpdateLater).setOnClickListener { updateOverlay.visibility = View.GONE }
        findViewById<View>(R.id.btnUpdateGo).setOnClickListener { downloadAndInstall() }
        findViewById<View>(R.id.btnRefresh).setOnClickListener { loadOrders(); loadPaymentQrs(); toast("↻ Refreshing...") }
        findViewById<View>(R.id.btnCollectOnly).setOnClickListener {
            onlyCollect = !onlyCollect
            findViewById<TextView>(R.id.btnCollectOnly).setText(if (onlyCollect) "TO COLLECT" else "ALL")
            findViewById<View>(R.id.btnCollectOnly).setBackgroundResource(if (onlyCollect) R.drawable.badge_amber else R.drawable.bg_outline)
            findViewById<TextView>(R.id.btnCollectOnly).setTextColor(if (onlyCollect) 0xFFfbbf24.toInt() else 0xFFa78bfa.toInt())
            rebindOrders()
        }
        findViewById<View>(R.id.btnEndShift).setOnClickListener { openEndShift() }
        findViewById<View>(R.id.esConfirm).setOnClickListener { confirmEndShift() }
        findViewById<View>(R.id.esClose).setOnClickListener { closeEndShift() }
        findViewById<View>(R.id.btnHistory).setOnClickListener { openHistory() }
        findViewById<View>(R.id.hsClose).setOnClickListener { closeHistory() }
        findViewById<View>(R.id.btnLogout).setOnClickListener { logout() }
        findViewById<View>(R.id.btnBackDetail).setOnClickListener { backToList() }
        findViewById<View>(R.id.btnArrived).setOnClickListener { markArrived() }
        findViewById<View>(R.id.btnCancel).setOnClickListener { cancelOverlay.visibility = View.VISIBLE }
        findViewById<View>(R.id.btnCancelClose).setOnClickListener { cancelOverlay.visibility = View.GONE }
        findViewById<View>(R.id.btnCancelGo).setOnClickListener { confirmCancel() }
        findViewById<View>(R.id.btnCollect).setOnClickListener { openPay() }
        findViewById<View>(R.id.btnPayBack).setOnClickListener { showScreen(detailScreen) }
        findViewById<View>(R.id.btnMCash).setOnClickListener { setMethod("cash") }
        findViewById<View>(R.id.btnMGcash).setOnClickListener { setMethod("gcash") }
        findViewById<View>(R.id.btnMSplit).setOnClickListener { setMethod("split") }
        findViewById<View>(R.id.btnTakePic).setOnClickListener { takePic(REQ_CAMERA_PIC, 0) }
        findViewById<View>(R.id.btnTakePic2).setOnClickListener { takePic(REQ_CAMERA_PIC2, 1) }
        findViewById<View>(R.id.btnAccept).setOnClickListener { submitPayment() }
        val etCash = findViewById<EditText>(R.id.etPayCash)
        etCash.addTextChangedListener(object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, st: Int, c: Int, a: Int) {}
            override fun onTextChanged(s: CharSequence?, st: Int, b: Int, c: Int) { calcChange() }
            override fun afterTextChanged(s: android.text.Editable?) {}
        })
        val etSC = findViewById<EditText>(R.id.etSCash)
        val etSG = findViewById<EditText>(R.id.etSG)
        val w = object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, st: Int, c: Int, a: Int) {}
            override fun onTextChanged(s: CharSequence?, st: Int, b: Int, c: Int) { calcChange() }
            override fun afterTextChanged(s: android.text.Editable?) {}
        }
        etSC.addTextChangedListener(w); etSG.addTextChangedListener(w)
    }

    // ─── SCREENS ─────────────────────────────────────────────
    private fun showScreen(s: View) {
        loginScreen.visibility = View.GONE
        mainScreen.visibility = View.GONE
        detailScreen.visibility = View.GONE
        payScreen.visibility = View.GONE
        s.visibility = View.VISIBLE
    }

    private fun toast(msg: String) {
        Toast.makeText(this, msg, Toast.LENGTH_LONG).show()
    }

    private fun reportCrash(type: String, log: String) {
        Thread {
            try {
                val conn = URL(API + "/crash-report").openConnection() as HttpURLConnection
                conn.requestMethod = "POST"
                conn.doOutput = true
                conn.connectTimeout = 10000; conn.readTimeout = 10000
                conn.setRequestProperty("Content-Type", "application/json")
                val body = "{\"app\":\"driver-native\",\"version\":\"" + jsonEsc(currentVersion()) + "\",\"device\":\"" + jsonEsc(Build.MODEL) + "\",\"type\":\"" + jsonEsc(type) + "\",\"log\":" + JSONObject.quote(log.take(2500)) + "}"
                conn.outputStream.use { it.write(body.toByteArray(Charsets.UTF_8)) }
                conn.responseCode
                conn.disconnect()
            } catch (_: Exception) {}
        }.start()
    }

    private fun endErr(tag: String, t: Throwable) {
        try { reportCrash("endshift", tag + "\n" + Log.getStackTraceString(t)) } catch (_: Exception) {}
        try { toast("End shift error: " + (t.message ?: t.javaClass.simpleName)) } catch (_: Exception) {}
    }

    private fun fmt(v: Double): String =
        NumberFormat.getNumberInstance(Locale.US).apply { minimumFractionDigits = 2; maximumFractionDigits = 2 }.format(v)

    private fun openUrl(url: String) {
        try { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) } catch (e: Exception) { toast("Cannot open: " + url) }
    }

    // ─── LOGIN ───────────────────────────────────────────────
    private fun doLogin() {
        val u = findViewById<EditText>(R.id.lgUser).text.toString().trim()
        val p = findViewById<EditText>(R.id.lgPass).text.toString()
        lgErr.visibility = View.GONE
        if (u.isEmpty() || p.isEmpty()) { lgErr.text = "Enter username and password"; lgErr.visibility = View.VISIBLE; return }
        findViewById<View>(R.id.btnLogin).isEnabled = false
        runApi("POST", "/driver/login", null, "{\"username\":\"" + jsonEsc(u) + "\",\"password\":\"" + jsonEsc(p) + "\"}") { status, body ->
            findViewById<View>(R.id.btnLogin).isEnabled = true
            if (status == 200) {
                try {
                    val j = JSONObject(body)
                    token = j.getString("token")
                    drvName = j.optString("name", u)
                    prefs.edit().putString("token", token).putString("name", drvName).apply()
                    tvDrvName.text = drvName
                    showScreen(mainScreen)
                    loadOrders()
                } catch (e: Exception) { lgErr.text = "Invalid server response"; lgErr.visibility = View.VISIBLE }
            } else {
                lgErr.text = try { JSONObject(body).optString("error", "Login failed ($status)") } catch (e: Exception) { "Login failed ($status)" }
                lgErr.visibility = View.VISIBLE
            }
        }
    }

    private fun jsonEsc(s: String): String =
        s.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n")

    private fun logout() {
        token = ""
        prefs.edit().remove("token").remove("name").apply()
        findViewById<EditText>(R.id.lgPass).setText("")
        showScreen(loginScreen)
    }

    // ─── ORDERS LIST ─────────────────────────────────────────
    private fun loadOrders() { loadOrders(false) }
    private fun loadOrders(pull: Boolean) {
        runApi("GET", "/driver/orders", token, null) { status, body ->
            if (pull) findViewById<SwipeRefreshLayout>(R.id.refreshLayout).isRefreshing = false
            if (status == 401) { lgErr.text = "⚠ Hindi na-validate ang session — mag-login ulit"; lgErr.visibility = View.VISIBLE; logout(); return@runApi }
            if (status != 200) { toast("Failed to load orders: HTTP $status"); return@runApi }
            try {
                orders = JSONArray(body)
                rebindOrders()
                refreshEsBadge()
            } catch (e: Exception) { toast("Failed to load orders: " + e.message) }
        }
    }

    private fun rebindOrders() {
        val vis = JSONArray()
        for (i in 0 until orders.length()) {
            val o = orders.optJSONObject(i) ?: continue
            if (onlyCollect && o.optString("paidStatus") == "paid") continue
            vis.put(o)
        }
        visOrders = vis
        listEmpty.visibility = if (visOrders.length() == 0) View.VISIBLE else View.GONE
        orderList.adapter = object : BaseAdapter() {
            override fun getCount() = visOrders.length()
            override fun getItem(i: Int) = visOrders.optJSONObject(i)
            override fun getItemId(i: Int) = i.toLong()
            override fun getView(i: Int, cv: View?, parent: ViewGroup): View {
                val v = cv ?: LayoutInflater.from(this@MainActivity).inflate(R.layout.item_order, parent, false)
                val o = visOrders.optJSONObject(i) ?: return v
                v.findViewById<TextView>(R.id.rowOrderNo).text = o.optString("orderNo")
                val paid = o.optString("paidStatus") == "paid"
                v.findViewById<TextView>(R.id.rowPaid).visibility = if (paid) View.VISIBLE else View.GONE
                v.findViewById<TextView>(R.id.rowToCollect).visibility = if (paid) View.GONE else View.VISIBLE
                v.findViewById<TextView>(R.id.rowDate).text = fmtDate(o.optString("createdAt"))
                v.findViewById<TextView>(R.id.rowCustomer).text = o.optString("customerName")
                v.findViewById<TextView>(R.id.rowAddr).text = "📍 Blk " + o.optString("block", "-") + " Lot " + o.optString("lot", "-") + (if (o.optString("subdivision").isNotEmpty()) ", " + o.optString("subdivision") else "")
                v.findViewById<TextView>(R.id.rowMethod).text = o.optString("paymentMethod")
                v.findViewById<TextView>(R.id.rowTotal).text = "₱" + fmt(o.optDouble("total"))
                return v
            }
        }
        orderList.setOnItemClickListener { _, _, pos, _ ->
            openDetail(visOrders.optJSONObject(pos).optInt("id"))
        }
    }

    private fun fmtDate(iso: String): String {
        if (iso.isEmpty()) return ""
        return try {
            val sdf = java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm", java.util.Locale.US)
            sdf.timeZone = java.util.TimeZone.getTimeZone("UTC")
            val d = sdf.parse(iso.take(16))
            val out = java.text.SimpleDateFormat("MMM dd, yyyy · hh:mm a", java.util.Locale.US)
            out.timeZone = java.util.TimeZone.getTimeZone("Asia/Manila")
            out.format(d)
        } catch (e: Exception) { iso.take(16).replace("T", " ") }
    }

    // ─── ORDER DETAIL ────────────────────────────────────────
    private fun openDetail(id: Int) {
        runApi("GET", "/driver/orders/$id", token, null) { status, body ->
            if (status == 401) { lgErr.text = "⚠ Hindi na-validate ang session — mag-login ulit"; lgErr.visibility = View.VISIBLE; logout(); return@runApi }
            if (status != 200) { toast("Order not found"); return@runApi }
            try {
                val j = JSONObject(body)
                val o = j.getJSONObject("order")
                curOrder = o
                curItems = j.optJSONArray("items") ?: JSONArray()
                val paid = o.optString("paidStatus") == "paid"
                tvDetailOrder.text = o.optString("orderNo")
                tvDetailDate.text = "🕐 " + fmtDate(o.optString("createdAt"))
                tvPaidBadge.text = "PAID"
                tvPaidBadge.visibility = if (paid) View.VISIBLE else View.GONE
                tvDetailCustomer.text = o.optString("customerName")
                tvDetailPhone.text = "📞 " + o.optString("phone")
                tvDetailAddr.text = "📍 Blk " + o.optString("block", "-") + " Lot " + o.optString("lot", "-") + (if (o.optString("subdivision").isNotEmpty()) ", " + o.optString("subdivision") else "")
                tvDetailPay.text = "💳 " + o.optString("paymentMethod")
                val note = o.optString("deliveryNote")
                tvDetailNote.visibility = if (note.isNotEmpty()) View.VISIBLE else View.GONE
                if (note.isNotEmpty()) tvDetailNote.text = "📝 " + note
                detailItems.removeAllViews()
                val st = o.optString("status")
                for (i in 0 until curItems.length()) {
                    val it = curItems.optJSONObject(i)
                    val row = android.widget.LinearLayout(this)
                    row.orientation = android.widget.LinearLayout.HORIZONTAL
                    row.setPadding(0, 4, 0, 4)
                    val left = TextView(this)
                    left.text = it.optString("productName") + " × " + it.optInt("qty") + " " + it.optString("unitName")
                    left.setTextColor(0xFFE5E7EB.toInt()); left.textSize = 13f
                    val right = TextView(this)
                    right.text = "₱" + fmt(it.optDouble("total"))
                    right.setTextColor(0xFFE5E7EB.toInt()); right.textSize = 13f
                    right.setTypeface(null, android.graphics.Typeface.BOLD)
                    row.addView(left, android.widget.LinearLayout.LayoutParams(0, android.widget.LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
                    row.addView(right, android.widget.LinearLayout.LayoutParams(android.widget.LinearLayout.LayoutParams.WRAP_CONTENT, android.widget.LinearLayout.LayoutParams.WRAP_CONTENT))
                    detailItems.addView(row)
                }
                tvDetailTotal.text = "₱" + fmt(o.optDouble("total"))
                findViewById<View>(R.id.btnArrived).visibility = if (st == "shipped") View.VISIBLE else View.GONE
                findViewById<View>(R.id.btnCancel).visibility = if (st == "shipped" || st == "arrived" || st == "confirmed") View.VISIBLE else View.GONE
                findViewById<View>(R.id.btnCollect).visibility = if (paid) View.GONE else View.VISIBLE
                showScreen(detailScreen)
            } catch (e: Exception) { toast("Failed: " + e.message) }
        }
    }

    private fun markArrived() {
        runApi("POST", "/driver/orders/" + (curOrder?.optInt("id") ?: 0) + "/arrived", token, null) { status, body ->
            if (status == 200) { toast("📍 Arrived at customer"); openDetail(curOrder?.optInt("id") ?: 0) }
            else toast(errMsg(status, body, "Failed"))
        }
    }

    private fun confirmCancel() {
        val reason = findViewById<EditText>(R.id.etReason).text.toString().trim()
        tvCancelErr.visibility = View.GONE
        if (reason.length < 3) { tvCancelErr.text = "Ilagay ang dahilan ng cancellation"; tvCancelErr.visibility = View.VISIBLE; return }
        val id = curOrder?.optInt("id") ?: 0
        runApi("POST", "/driver/orders/$id/cancel", token, "{\"reason\":\"" + jsonEsc(reason) + "\"}") { status, body ->
            if (status == 200) {
                cancelOverlay.visibility = View.GONE
                findViewById<EditText>(R.id.etReason).setText("")
                toast("Order cancelled — ibinalik ang stock ✓")
                if (endShiftOpen) {
                    loadEndInfo { renderEndShift(); updateEsMain() }
                } else backToList()
            } else { tvCancelErr.text = errMsg(status, body, "Cancel failed"); tvCancelErr.visibility = View.VISIBLE }
        }
    }

    private fun backToList() {
        curOrder = null
        showScreen(mainScreen)
        loadOrders()
    }

    // ─── END SHIFT (1x/day, kasama ang remittance + carry-over) ──
    private fun dp(v: Int): Int = (v * resources.displayMetrics.density).toInt()

    private fun refreshEsBadge() {
        try { loadEndInfo { updateEsMain() } } catch (t: Throwable) { endErr("refreshEsBadge", t) }
    }

    private fun loadEndInfo(cb: (() -> Unit)? = null) {
        runApi("GET", "/driver/endshift-info", token, null) { status, body ->
            if (status == 200) {
                try { endInfo = JSONObject(body) } catch (e: Exception) { endInfo = null }
            } else endInfo = null
            cb?.invoke()
        }
    }

    private fun updateEsMain() {
        try {
            val btn = findViewById<TextView>(R.id.btnEndShift)
            val done = endInfo != null && endInfo!!.optBoolean("closedToday")
            if (done) {
                btn.text = "✅ END SHIFT DONE (bukas na ulit)"
                btn.setBackgroundResource(R.drawable.bg_outline)
                btn.setTextColor(0xFFa8a29e.toInt())
                btn.setAlpha(0.6f)
                btn.setOnClickListener(null)
            } else {
                btn.text = "🔚 END SHIFT & REMIT"
                btn.setBackgroundResource(R.drawable.bg_outline_green)
                btn.setTextColor(0xFF10b981.toInt())
                btn.setAlpha(1f)
                btn.setOnClickListener { openEndShift() }
            }
        } catch (t: Throwable) { endErr("updateEsMain", t) }
    }

    private fun openEndShift() {
        try {
            endShiftOpen = true
            endShiftOverlay.visibility = View.VISIBLE
            findViewById<TextView>(R.id.esStatus).text = "Kumukuha ng impormasyon..."
            loadEndInfo { renderEndShift() }
        } catch (t: Throwable) { endErr("openEndShift", t) }
    }

    private fun closeEndShift() {
        endShiftOpen = false
        endShiftOverlay.visibility = View.GONE
        findViewById<TextView>(R.id.esErr).visibility = View.GONE
    }

    // ─── END-SHIFT HISTORY (per-day remittance) ──────────
    private fun openHistory() {
        try {
            historyOverlay.visibility = View.VISIBLE
            findViewById<TextView>(R.id.hsTotal).text = "Kinukuha ang history..."
            loadEndInfo { renderHistory() }
        } catch (t: Throwable) { endErr("openHistory", t) }
    }

    private fun closeHistory() { historyOverlay.visibility = View.GONE }

    private fun renderHistory() {
        try {
            val wrap = findViewById<android.widget.LinearLayout>(R.id.hsList)
            val empty = findViewById<TextView>(R.id.hsEmpty)
            val totalTv = findViewById<TextView>(R.id.hsTotal)
            val arr = endInfo?.optJSONArray("history") ?: JSONArray()
            wrap.removeAllViews()
            if (arr.length() == 0) {
                empty.visibility = View.VISIBLE
                totalTv.text = ""
                wrap.addView(empty)
                return
            }
            empty.visibility = View.GONE
            var sumOrders = 0; var sumCash = 0.0; var sumGcash = 0.0
            for (i in 0 until arr.length()) {
                val h = arr.optJSONObject(i) ?: continue
                val orders = h.optInt("deliveredOrders")
                val cash = h.optDouble("cashTotal")
                val gcash = h.optDouble("gcashTotal")
                sumOrders += orders; sumCash += cash; sumGcash += gcash

                val row = android.widget.LinearLayout(this)
                row.orientation = android.widget.LinearLayout.VERTICAL
                row.setBackgroundColor(0xFF1a1a44.toInt())
                row.setPadding(dp(12), dp(10), dp(12), dp(10))
                val lp = android.widget.LinearLayout.LayoutParams(android.widget.LinearLayout.LayoutParams.MATCH_PARENT, android.widget.LinearLayout.LayoutParams.WRAP_CONTENT)
                lp.bottomMargin = dp(6)
                row.layoutParams = lp

                val tvDate = TextView(this)
                tvDate.text = h.optString("shiftDate") + "  ·  " + orders + " order(s)"
                tvDate.setTextColor(0xFFFFFFFF.toInt())
                tvDate.textSize = 13f
                tvDate.setTypeface(tvDate.typeface, android.graphics.Typeface.BOLD)
                row.addView(tvDate)

                val tvAmt = TextView(this)
                tvAmt.text = "Cash ₱" + fmt(cash) + "  ·  GCash ₱" + fmt(gcash) + "  ·  TOTAL ₱" + fmt(cash + gcash)
                tvAmt.setTextColor(0xFF34d399.toInt())
                tvAmt.textSize = 12f
                tvAmt.setPadding(0, dp(3), 0, 0)
                row.addView(tvAmt)

                val ended = h.optString("endedAt")
                if (ended.isNotEmpty()) {
                    val tvEnd = TextView(this)
                    tvEnd.text = "Ended: " + ended
                    tvEnd.setTextColor(0xFF8b8bb5.toInt())
                    tvEnd.textSize = 10f
                    tvEnd.setPadding(0, dp(2), 0, 0)
                    row.addView(tvEnd)
                }
                wrap.addView(row)
            }
            totalTv.text = arr.length().toString() + " shift(s) · TOTAL NA-REMIT: ₱" + fmt(sumCash + sumGcash) +
                "  (Cash ₱" + fmt(sumCash) + " · GCash ₱" + fmt(sumGcash) + ")"
        } catch (t: Throwable) { endErr("renderHistory", t) }
    }

    private fun renderEndShift() {
        try {
            val e = endInfo ?: run { toast("Hindi ma-load ang end-shift info"); return }
            val closed = e.optBoolean("closedToday")
            val confirm = findViewById<View>(R.id.esConfirm)
            val total = e.optJSONObject("sweep")?.let { it.optDouble("cash") + it.optDouble("gcash") } ?: 0.0
            findViewById<TextView>(R.id.esStatus).text =
                if (closed) "✅ Tapos na ang end shift ngayon (1x per day lang)." else drvName + " — isusuko mo na ang pera sa tindahan?"
            if (closed) {
                confirm.visibility = View.GONE
                val lc = e.optJSONObject("lastClosed")
                findViewById<TextView>(R.id.esToday).text =
                    "Na-remit kanina: " + (lc?.optInt("deliveredOrders") ?: 0) + " order(s) · Cash ₱" + fmt(lc?.optDouble("cashTotal") ?: 0.0) + " · GCash ₱" + fmt(lc?.optDouble("gcashTotal") ?: 0.0)
            } else {
                confirm.visibility = View.VISIBLE
                val sw = e.optJSONObject("sweep")
                findViewById<TextView>(R.id.esOrders).text = (sw?.optInt("count") ?: 0).toString()
                findViewById<TextView>(R.id.esCash).text = "₱" + fmt(sw?.optDouble("cash") ?: 0.0)
                findViewById<TextView>(R.id.esGcash).text = "₱" + fmt(sw?.optDouble("gcash") ?: 0.0)
                val td = e.optJSONObject("today")
                findViewById<TextView>(R.id.esToday).text =
                    "Delivered ngayon: " + (td?.optInt("count") ?: 0) + " order(s) · Cash ₱" + fmt(td?.optDouble("cash") ?: 0.0) + " · GCash ₱" + fmt(td?.optDouble("gcash") ?: 0.0)
                findViewById<TextView>(R.id.esConfirm).text = "🔚 END SHIFT & REMIT ₱" + fmt(total)
            }
            renderCarryOver(e.optJSONArray("carryOver") ?: JSONArray())
        } catch (t: Throwable) { endErr("renderEndShift", t) }
    }

    private fun renderCarryOver(list: JSONArray) {
        try {
            val box = findViewById<android.widget.LinearLayout>(R.id.esCarryList)
            val empty = findViewById<TextView>(R.id.esCarryEmpty)
            // esCarryEmpty ay child #0 — tanggalin lang ang mga lumang ROW (huwag i-remove ang empty para hindi ma-null ang reference)
            while (box.childCount > 1) box.removeViewAt(box.childCount - 1)
            empty.visibility = if (list.length() == 0) View.VISIBLE else View.GONE
            if (list.length() == 0) return
        for (i in 0 until list.length()) {
            val o = list.optJSONObject(i) ?: continue
            val id = o.optInt("id")
            val card = LinearLayout(this)
            card.orientation = LinearLayout.VERTICAL
            card.setBackgroundResource(R.drawable.bg_input)
            card.setPadding(dp(12), dp(10), dp(12), dp(10))
            val lp = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT)
            lp.topMargin = dp(6)
            box.addView(card, lp)

            val top = LinearLayout(this)
            top.orientation = LinearLayout.HORIZONTAL
            val tvNo = TextView(this)
            tvNo.text = o.optString("orderNo") + if (o.optInt("days") >= 1) "  ⚠ ${o.optInt("days")}D" else ""
            tvNo.setTextColor(0xFFE5E7EB.toInt()); tvNo.textSize = 13f
            tvNo.setTypeface(null, android.graphics.Typeface.BOLD)
            top.addView(tvNo, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
            val st = o.optString("status")
            val tvSt = TextView(this)
            tvSt.text = st.uppercase()
            tvSt.setTextColor(if (st == "arrived") 0xFF60a5fa.toInt() else 0xFFa78bfa.toInt())
            tvSt.textSize = 10f
            tvSt.setTypeface(null, android.graphics.Typeface.BOLD)
            top.addView(tvSt)
            card.addView(top)

            val tvC = TextView(this)
            tvC.text = o.optString("customerName") + "\n📍 Blk " + o.optString("block", "-") + " Lot " + o.optString("lot", "-") + (if (o.optString("subdivision").isNotEmpty()) ", " + o.optString("subdivision") else "")
            tvC.setTextColor(0xFFa1a1cc.toInt()); tvC.textSize = 12f
            tvC.setPadding(0, dp(6), 0, dp(6))
            card.addView(tvC)

            val row = LinearLayout(this)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = android.view.Gravity.CENTER_VERTICAL
            val tvTotal = TextView(this)
            tvTotal.text = "₱" + fmt(o.optDouble("total"))
            tvTotal.setTextColor(0xFFFFFFFF.toInt()); tvTotal.textSize = 14f
            tvTotal.setTypeface(null, android.graphics.Typeface.BOLD)
            row.addView(tvTotal, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
            val btnDeliver = TextView(this)
            btnDeliver.text = "DELIVER"
            btnDeliver.setTextColor(0xFF3b82f6.toInt()); btnDeliver.textSize = 12f
            btnDeliver.setTypeface(null, android.graphics.Typeface.BOLD)
            btnDeliver.setBackgroundResource(R.drawable.bg_outline)
            btnDeliver.setPadding(dp(14), dp(6), dp(14), dp(6))
            btnDeliver.setOnClickListener {
                closeEndShift()
                openDetail(id)
            }
            row.addView(btnDeliver, LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT))
            val btnCancel = TextView(this)
            btnCancel.text = "CANCEL"
            btnCancel.setTextColor(0xFFf87171.toInt()); btnCancel.textSize = 12f
            btnCancel.setTypeface(null, android.graphics.Typeface.BOLD)
            btnCancel.setBackgroundResource(R.drawable.bg_outline_red)
            btnCancel.setPadding(dp(14), dp(6), dp(14), dp(6))
            btnCancel.setOnClickListener {
                curOrder = JSONObject().put("id", id)
                cancelOverlay.visibility = View.VISIBLE
            }
            val bl = LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT)
            bl.leftMargin = dp(8)
            row.addView(btnCancel, bl)
            card.addView(row)
        }
        } catch (t: Throwable) { endErr("renderCarryOver", t) }
    }

    private fun confirmEndShift() {
        try {
            val e = endInfo ?: return
            val sw = e.optJSONObject("sweep")
            val count = sw?.optInt("count") ?: 0
            val cash = sw?.optDouble("cash") ?: 0.0
            val gcash = sw?.optDouble("gcash") ?: 0.0
            val msg = if (count > 0)
                "I-end shift mo na ba?\n\nRemit: $count order(s)\nCash: ₱" + fmt(cash) + "\nGCash: ₱" + fmt(gcash) +
                "\n\nIsinuko mo na ba ang perang ito sa tindahan? 1x per day lang — hindi mo na ito maaaring baguhin."
            else "Wala kang nakolektang payment. I-end shift mo na ba? (1x per day — hindi na maaaring baguhin.)"
            android.app.AlertDialog.Builder(this)
                .setTitle("🔚 End Shift")
                .setMessage(msg)
                .setPositiveButton("END SHIFT") { _, _ -> doEndShift() }
                .setNegativeButton("BALIK", null)
                .show()
        } catch (t: Throwable) { endErr("confirmEndShift", t) }
    }

    private fun doEndShift() {
        try {
            findViewById<View>(R.id.esConfirm).isEnabled = false
            runApi("POST", "/driver/end-shift", token, null) { status, body ->
                try {
                    findViewById<View>(R.id.esConfirm).isEnabled = true
                    val err = findViewById<TextView>(R.id.esErr)
                    if (status == 200) {
                        try {
                            val j = JSONObject(body)
                            err.visibility = View.GONE
                            loadEndInfo {
                                renderEndShift()
                                updateEsMain()
                                toast("✅ End shift OK — na-remit ang ₱" + fmt(j.optDouble("cashTotal")) + " cash + ₱" + fmt(j.optDouble("gcashTotal")) + " gcash")
                            }
                        } catch (e: Exception) { toast("End shift OK ✓") }
                    } else {
                        err.text = errMsg(status, body, "End shift failed")
                        err.visibility = View.VISIBLE
                        loadEndInfo { renderEndShift() }
                    }
                } catch (t: Throwable) { endErr("doEndShift-cb", t) }
            }
        } catch (t: Throwable) { endErr("doEndShift", t) }
    }

    // ─── PAYMENT ─────────────────────────────────────────────
    private fun openPay() {
        val o = curOrder ?: return
        payMethod = "cash"
        setMethod("cash")
        tvPayOrder.text = o.optString("orderNo") + " · " + o.optString("customerName")
        tvPayTotal.text = "₱" + fmt(o.optDouble("total"))
        tvPayErr.visibility = View.GONE
        findViewById<EditText>(R.id.etPayCash).setText("")
        findViewById<EditText>(R.id.etGAmt).setText("")
        findViewById<EditText>(R.id.etGRef).setText("")
        findViewById<EditText>(R.id.etSCash).setText("")
        findViewById<EditText>(R.id.etSG).setText("")
        findViewById<EditText>(R.id.etSRef).setText("")
        pic0 = null; pic1 = null
        tvPicName.text = ""; tvPicName2.text = ""
        showScreen(payScreen)
    }

    private fun setMethod(m: String) {
        payMethod = m
        val selBg = R.drawable.bg_method_sel; val baseBg = R.drawable.bg_method
        findViewById<View>(R.id.btnMCash).setBackgroundResource(if (m == "cash") selBg else baseBg)
        findViewById<View>(R.id.btnMGcash).setBackgroundResource(if (m == "gcash") selBg else baseBg)
        findViewById<View>(R.id.btnMSplit).setBackgroundResource(if (m == "split") selBg else baseBg)
        findViewById<TextView>(R.id.btnMCash).setTextColor(if (m == "cash") 0xFF7c3aed.toInt() else 0xFFE5E7EB.toInt())
        findViewById<TextView>(R.id.btnMGcash).setTextColor(if (m == "gcash") 0xFF7c3aed.toInt() else 0xFFE5E7EB.toInt())
        findViewById<TextView>(R.id.btnMSplit).setTextColor(if (m == "split") 0xFF7c3aed.toInt() else 0xFFE5E7EB.toInt())
        cashRow.visibility = if (m == "cash") View.VISIBLE else View.GONE
        gcashRow.visibility = if (m == "gcash") View.VISIBLE else View.GONE
        splitRow.visibility = if (m == "split") View.VISIBLE else View.GONE
        calcChange()
        // GCASH/SPLIT: awtomatikong buksan ang camera para sa proof picture ng GCash reference
        if (m != "cash" && pic0 == null) {
            payScreen.postDelayed({
                if (payMethod != "cash" && pic0 == null) takePic(REQ_CAMERA_PIC, 0)
            }, 600)
        }
    }

    private fun calcChange() {
        val o = curOrder ?: return
        val total = o.optDouble("total")
        var change = 0.0
        when (payMethod) {
            "cash" -> change = num(findViewById<EditText>(R.id.etPayCash)) - total
            "split" -> change = num(findViewById<EditText>(R.id.etSCash)) - (total - num(findViewById<EditText>(R.id.etSG)))
        }
        if (change < 0) change = 0.0
        tvChange.text = "Change: ₱" + fmt(change)
    }

    private fun num(et: EditText): Double = et.text.toString().toDoubleOrNull() ?: 0.0

    private fun takePic(reqCode: Int, slot: Int) {
        pendingPicFor = slot
        try {
            val dir = File(cacheDir, "pics"); dir.mkdirs()
            val f = File(dir, "pic$slot.jpg"); if (f.exists()) f.delete()
            val uri: Uri = FileProvider.getUriForFile(this, "$packageName.fileprovider", f)
            val intent = Intent(MediaStore.ACTION_IMAGE_CAPTURE)
            intent.putExtra(MediaStore.EXTRA_OUTPUT, uri)
            intent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
            startActivityForResult(intent, reqCode)
        } catch (e: Exception) { toast("Camera unavailable: " + e.message) }
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (resultCode != RESULT_OK) return
        if (requestCode == REQ_CAMERA_PIC || requestCode == REQ_CAMERA_PIC2) {
            val slot = pendingPicFor
            val f = File(cacheDir, "pics/pic$slot.jpg")
            if (f.exists() && f.length() > 0) {
                if (slot == 0) { pic0 = f; tvPicName.text = "✅ Proof picture ready" }
                else { pic1 = f; tvPicName2.text = "✅ Proof picture ready" }
            } else toast("Hindi nakuha ang picture — subukan muli")
        }
    }

    private fun submitPayment() {
        val o = curOrder ?: return
        val total = o.optDouble("total")
        tvPayErr.visibility = View.GONE
        val payments = JSONArray()
        var pics = listOf<File?>()
        when (payMethod) {
            "cash" -> {
                val cash = num(findViewById<EditText>(R.id.etPayCash))
                if (cash < total) { showPayErr("Cash received is less than the total (₱" + fmt(total) + ")"); return }
                payments.put(JSONObject().put("method", "cash").put("amount", total))
            }
            "gcash" -> {
                val amt = num(findViewById<EditText>(R.id.etGAmt))
                val ref = findViewById<EditText>(R.id.etGRef).text.toString().trim()
                if (Math.abs(amt - total) > 0.01) { showPayErr("GCash amount must equal the total (₱" + fmt(total) + ")"); return }
                if (ref.isEmpty()) { showPayErr("Enter GCash reference / account"); return }
                if (pic0 == null) { showPayErr("Kuhaan muna ng proof picture"); return }
                payments.put(JSONObject().put("method", "gcash").put("amount", amt).put("gcashRef", ref))
                pics = listOf(pic0)
            }
            "split" -> {
                val cash = num(findViewById<EditText>(R.id.etSCash))
                val g = num(findViewById<EditText>(R.id.etSG))
                val ref = findViewById<EditText>(R.id.etSRef).text.toString().trim()
                if (Math.abs(cash + g - total) > 0.01) { showPayErr("Cash + GCash must equal the total (₱" + fmt(total) + ")"); return }
                if (ref.isEmpty()) { showPayErr("Enter GCash reference for the split part"); return }
                if (pic0 == null) { showPayErr("Kuhaan muna ng proof picture"); return }
                if (g > 0) payments.put(JSONObject().put("method", "gcash").put("amount", g).put("gcashRef", ref))
                if (cash > 0) payments.put(JSONObject().put("method", "cash").put("amount", cash))
                pics = listOf(pic0, null)
            }
        }
        findViewById<View>(R.id.btnAccept).isEnabled = false
        runMultipart("/driver/orders/" + o.optInt("id") + "/pay", payments.toString(), pics) { status, body ->
            findViewById<View>(R.id.btnAccept).isEnabled = true
            if (status == 200) {
                toast("✔ Bayad na! Order marked PAID — nag-award ng points ✓")
                backToList()
            } else showPayErr(errMsg(status, body, "Payment failed"))
        }
    }

    private fun showPayErr(msg: String) { tvPayErr.text = msg; tvPayErr.visibility = View.VISIBLE }

    // ─── PAYMENT QRs (GCash) ────────────────────────────────
    private fun loadPaymentQrs() {
        Thread {
            try {
                val conn = URL(API + "/payment-qrs").openConnection() as HttpURLConnection
                conn.connectTimeout = 15000; conn.readTimeout = 15000
                val body = readStream(conn.inputStream)
                val arr = JSONArray(body)
                val qrs = (0 until arr.length()).map { val q = arr.getJSONObject(it); q.optString("header") to q.optString("file") }.filter { it.second.isNotEmpty() }
                runOnUiThread {
                    if (qrs.isNotEmpty()) {
                        tvQrHeader1.text = qrs[0].first
                        loadQrImage(qrs[0].second, ivPayQr1)
                    }
                    if (qrs.size > 1) {
                        tvQrHeader2.text = qrs[1].first
                        loadQrImage(qrs[1].second, ivPayQr2)
                    }
                }
            } catch (e: Exception) {}
        }.start()
    }

    private fun loadQrImage(file: String, iv: ImageView) {
        Thread {
            try {
                val conn = URL(ASSETS + file).openConnection() as HttpURLConnection
                conn.connectTimeout = 15000; conn.readTimeout = 15000
                val bmp = android.graphics.BitmapFactory.decodeStream(conn.inputStream)
                if (bmp != null) runOnUiThread { iv.setImageBitmap(bmp) }
            } catch (e: Exception) {}
        }.start()
    }

    // ─── UPDATE (self-update) ───────────────────────────────
    private fun currentVersion(): String {
        return try { packageManager.getPackageInfo(packageName, 0).versionName ?: "?" } catch (e: Exception) { "?" }
    }

    private fun checkUpdate() {
        Thread {
            try {
                val conn = URL(UPDATES + "/driver-version.json").openConnection() as HttpURLConnection
                conn.connectTimeout = 10000; conn.readTimeout = 10000
                val j = JSONObject(readStream(conn.inputStream))
                val latest = j.optString("version")
                val installed = currentVersion()
                if (latest.isNotEmpty() && latest != installed) {
                    val changelog = j.optString("changelog")
                    runOnUiThread { showUpdateDialog(latest, installed, changelog) }
                }
            } catch (e: Exception) {}
        }.start()
    }

    private fun showUpdateDialog(latest: String, installed: String, changelog: String) {
        updVersion.text = "Latest: v$latest · kasalukuyan: v$installed"
        updChangelog.removeAllViews()
        val items = changelog.split('\n').map { it.trim() }.filter { it.isNotEmpty() }
        items.forEach { line ->
            val tv = TextView(this)
            tv.text = "• " + line
            tv.setTextColor(0xFFc4c4e8.toInt()); tv.textSize = 12f
            tv.setPadding(0, 3, 0, 3)
            updChangelog.addView(tv)
        }
        updateOverlay.visibility = View.VISIBLE
    }

    private fun downloadAndInstall() {
        toast("Downloading update...")
        Thread {
            try {
                val file = File(getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "JumongDriver.apk")
                val conn = URL(UPDATES + "/JumongDriver.apk").openConnection() as HttpURLConnection
                conn.connectTimeout = 30000; conn.readTimeout = 30000
                conn.inputStream.use { input -> file.outputStream().use { output -> input.copyTo(output) } }
                runOnUiThread { installApk(file) }
            } catch (e: Exception) {
                runOnUiThread { toast("Update failed: " + e.message) }
            }
        }.start()
    }

    private fun installApk(file: File) {
        if (Build.VERSION.SDK_INT >= 26) {
            if (!packageManager.canRequestPackageInstalls()) {
                toast("Allow 'Install unknown apps' para sa Driver app")
                startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:$packageName")))
                return
            }
        }
        try {
            val uri = FileProvider.getUriForFile(this, "$packageName.fileprovider", file)
            val intent = Intent(Intent.ACTION_VIEW).apply {
                setDataAndType(uri, "application/vnd.android.package-archive")
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_GRANT_READ_URI_PERMISSION
            }
            startActivity(intent)
        } catch (e: Exception) { toast("Install failed: " + e.message) }
    }

    // ─── HTTP ───────────────────────────────────────────────
    private fun errMsg(status: Int, body: String, fallback: String): String {
        return try { JSONObject(body).optString("error", "$fallback (HTTP $status)") } catch (e: Exception) { "$fallback (HTTP $status)" }
    }

    private fun readStream(stream: java.io.InputStream): String {
        val r = BufferedReader(InputStreamReader(stream, Charsets.UTF_8))
        val sb = StringBuilder()
        r.forEachLine { sb.append(it).append('\n') }
        return sb.toString().trim()
    }

    private fun runApi(method: String, path: String, token: String?, body: String?, cb: (Int, String) -> Unit) {
        Thread {
            try {
                val conn = URL(API + path).openConnection() as HttpURLConnection
                conn.requestMethod = method
                conn.connectTimeout = 20000; conn.readTimeout = 30000
                if (!token.isNullOrEmpty()) conn.setRequestProperty("Authorization", "Bearer $token")
                if (body != null) {
                    conn.doOutput = true
                    conn.setRequestProperty("Content-Type", "application/json")
                    conn.outputStream.use { it.write(body.toByteArray(Charsets.UTF_8)) }
                }
                val status = conn.responseCode
                val resp = try { readStream(if (status in 200..299) conn.inputStream else conn.errorStream) } catch (e: Exception) { "" }
                conn.disconnect()
                runOnUiThread { cb(status, resp) }
            } catch (e: Exception) {
                runOnUiThread { cb(0, e.message ?: "Network error") }
            }
        }.start()
    }

    private fun runMultipart(path: String, paymentsJson: String, pics: List<File?>, cb: (Int, String) -> Unit) {
        Thread {
            try {
                val boundary = "----JumongBoundary" + System.currentTimeMillis()
                val conn = URL(API + path).openConnection() as HttpURLConnection
                conn.requestMethod = "POST"
                conn.doOutput = true
                conn.connectTimeout = 20000; conn.readTimeout = 60000
                conn.setRequestProperty("Authorization", "Bearer $token")
                conn.setRequestProperty("Content-Type", "multipart/form-data; boundary=$boundary")
                val os: OutputStream = conn.outputStream
                os.write(("--$boundary\r\n").toByteArray(Charsets.UTF_8))
                os.write(("Content-Disposition: form-data; name=\"payments\"\r\n\r\n").toByteArray(Charsets.UTF_8))
                os.write(paymentsJson.toByteArray(Charsets.UTF_8))
                os.write("\r\n".toByteArray(Charsets.UTF_8))
                pics.forEachIndexed { i, f ->
                    if (f != null && f.exists() && f.length() > 0) {
                        os.write(("--$boundary\r\n").toByteArray(Charsets.UTF_8))
                        os.write(("Content-Disposition: form-data; name=\"pic$i\"; filename=\"pic$i.jpg\"\r\n").toByteArray(Charsets.UTF_8))
                        os.write(("Content-Type: image/jpeg\r\n\r\n").toByteArray(Charsets.UTF_8))
                        f.inputStream().use { it.copyTo(os) }
                        os.write("\r\n".toByteArray(Charsets.UTF_8))
                    }
                }
                os.write(("--$boundary--\r\n").toByteArray(Charsets.UTF_8))
                os.flush(); os.close()
                val status = conn.responseCode
                val resp = try { readStream(if (status in 200..299) conn.inputStream else conn.errorStream) } catch (e: Exception) { "" }
                conn.disconnect()
                runOnUiThread { cb(status, resp) }
            } catch (e: Exception) {
                runOnUiThread { cb(0, e.message ?: "Network error") }
            }
        }.start()
    }
}
