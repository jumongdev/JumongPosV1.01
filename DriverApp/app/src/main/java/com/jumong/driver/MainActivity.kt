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
import android.widget.TextView
import android.widget.Toast
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.FileProvider
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import org.json.JSONArray
import org.json.JSONObject
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

        bindViews()
        wireEvents()

        // Back button: mag-navigate sa loob ng app (pay -> detail -> main -> exit)
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                when {
                    cancelOverlay.visibility == View.VISIBLE -> cancelOverlay.visibility = View.GONE
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
        findViewById<View>(R.id.btnReturnHq).setOnClickListener { returnToHq() }
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
                backToList()
            } else { tvCancelErr.text = errMsg(status, body, "Cancel failed"); tvCancelErr.visibility = View.VISIBLE }
        }
    }

    private fun backToList() {
        curOrder = null
        showScreen(mainScreen)
        loadOrders()
    }

    private fun returnToHq() {
        runApi("POST", "/driver/return-to-hq", token, null) { status, body ->
            if (status == 200) {
                try {
                    val j = JSONObject(body)
                    toast("🏠 Returned to HQ — nakolekta: ₱" + fmt(j.optDouble("cashTotal")) + " cash · ₱" + fmt(j.optDouble("gcashTotal")) + " gcash (" + j.optInt("delivered") + " orders)")
                } catch (e: Exception) { toast("🏠 Returned to HQ ✓") }
            } else toast(errMsg(status, body, "Failed"))
        }
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
