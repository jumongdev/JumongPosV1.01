/* ═══════════════════════════════════════════════════════
   Jumong POS Dashboard — App.js
   Alpine.js 3.x Components + Utilities
   ═══════════════════════════════════════════════════════ */

const API = '/api/dashboard';
const PAGE_SIZE = 20;

/* ── Utilities ──────────────────────────────────────── */
window.fmt = n => Number(n || 0).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
window.fmtInt = n => Number(n || 0).toLocaleString('en-PH');
window.esc = s => (s + '').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
window.shortStore = (sid, name) => (name && name.trim()) ? name.trim() : (sid ? sid.replace('STORE-', '').slice(0, 12) : 'Unknown');

async function fetchJSON(url) {
  try {
    const r = await fetch(url + (url.includes('?') ? '&' : '?') + '_t=' + Date.now());
    if (!r.ok) throw new Error('HTTP ' + r.status);
    Alpine.store('app').isOnline = true;
    return await r.json();
  } catch (e) {
    Alpine.store('app').isOnline = false;
    toast('Failed to load data: ' + e.message, 'error');
    throw e;
  }
}

function toast(msg, type = 'info') {
  const container = document.getElementById('toastContainer');
  if (!container) return;
  const t = document.createElement('div');
  const icons = { error: '&#9888;', success: '&#10003;', info: '&#8505;' };
  t.className = 'toast flex items-center gap-2 px-4 py-3 rounded-lg text-sm shadow-lg backdrop-blur-md border ' +
    (type === 'error' ? 'bg-red-900/20 border-red-500/30 text-red-400' :
      type === 'success' ? 'bg-emerald-900/20 border-emerald-500/30 text-emerald-400' :
        'bg-blue-900/20 border-blue-500/30 text-blue-400');
  t.innerHTML = (icons[type] || '') + ' ' + msg;
  container.appendChild(t);
  setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity .3s'; setTimeout(() => t.remove(), 300) }, 4000);
}

/* ── Alpine Store ───────────────────────────────────── */
document.addEventListener('alpine:init', () => {
  Alpine.store('app', {
    section: 'dashboard',
    storeId: '',
    range: 'today',
    customFrom: '',
    customTo: '',
    darkMode: localStorage.getItem('theme') === 'dark',
    isOnline: true,
    stores: [],
    storeMap: {},
    lastRefresh: '',
    cache: {},
    editorOpen: false,
    saleModalOpen: false, saleInvoiceNo: '', saleItems: [], saleLoading: false,
    salePaymentMethod: '', saleReferenceNo: '', saleEwPaid: 0, saleGrandTotal: 0,
    _sidebarOpen: localStorage.getItem('sidebar') !== 'collapsed',
    _whBadge: 0,

    toggleDark() {
      this.darkMode = !this.darkMode;
      localStorage.setItem('theme', this.darkMode ? 'dark' : 'light');
      document.documentElement.classList.toggle('dark', this.darkMode);
    },
    async _showSaleItems(invoiceNo) {
      this.saleInvoiceNo = invoiceNo;
      this.saleModalOpen = true;
      this.saleLoading = true;
      this.salePaymentMethod = '';
      this.saleReferenceNo = '';
      this.saleEwPaid = 0;
      this.saleGrandTotal = 0;
      try {
        const data = await fetchJSON(API + '/sale-items?invoiceNo=' + encodeURIComponent(invoiceNo));
        this.saleItems = data.items || [];
        this.salePaymentMethod = data.paymentMethod || '';
        this.saleReferenceNo = data.referenceNo || '';
        this.saleEwPaid = data.ewPaid || 0;
        this.saleGrandTotal = data.grandTotal || 0;
      } catch (e) { this.saleItems = []; this.salePaymentMethod = ''; this.saleReferenceNo = ''; this.saleEwPaid = 0; this.saleGrandTotal = 0 }
      this.saleLoading = false;
    },
    saleTotalRevenue() { return this.saleItems.reduce((s, x) => s + x.totalPrice, 0) },
    saleTotalCost() { return this.saleItems.reduce((s, x) => s + x.totalCost, 0) },
    saleProfit() { return this.saleTotalRevenue() - this.saleTotalCost() },
    saleMargin() { const r = this.saleTotalRevenue(); return r > 0 ? (this.saleProfit() / r * 100).toFixed(1) : '0.0' },
    saleProfitClass(v) { return v > 0 ? 'text-emerald-400' : 'text-red-400' },
    saleMarginClass(v) { const m = parseFloat(v); return m > 20 ? 'text-emerald-400' : m > 0 ? 'text-amber-400' : 'text-red-400' },
    switchSection(section) {
      this.section = section;
      document.getElementById('sidebar')?.classList.remove('open');
      // Load section-specific data
      if (section === 'customers') dispatchEvent(new CustomEvent('load-customers'));
      if (section === 'users') dispatchEvent(new CustomEvent('load-users'));
      if (section === 'warehouse') dispatchEvent(new CustomEvent('load-warehouse'));
      if (section === 'products') dispatchEvent(new CustomEvent('load-products'));
      if (section === 'available') { dispatchEvent(new CustomEvent('load-stock')); dispatchEvent(new CustomEvent('load-receiving')) }
      if (section === 'analytics') dispatchEvent(new CustomEvent('load-analytics'));
    },
    setStore(val) {
      this.storeId = val;
      this.refreshAll();
    },
    setRange(range) {
      this.range = range;
      if (range !== 'custom') { this.customFrom = ''; this.customTo = '' }
      this.refreshAll();
    },
    applyCustom() {
      if (!this.customFrom || !this.customTo) { toast('Select both dates', 'error'); return }
      this.range = 'custom';
      this.refreshAll();
    },
    refreshAll() {
      dispatchEvent(new CustomEvent('refresh-data'));
      this.lastRefresh = new Date().toLocaleTimeString('en-PH', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true });
    },
    get storeParam() { return this.storeId ? '&storeId=' + encodeURIComponent(this.storeId) : '' },
    get rangeParam() {
      if (this.range === 'custom' && this.customFrom) return '&range=custom&date=' + this.customFrom;
      return '&range=' + this.range;
    },
    get filterParams() { return this.storeParam + this.rangeParam }
  });

  /* ── Summary Cards ──────────────────────────────────── */
  Alpine.data('summaryCards', () => ({
    d: null, loading: true,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/summary?' + Alpine.store('app').filterParams.replace('&', '')) } catch (e) { this.d = null }
      this.loading = false;
    }
  }));

  /* ── Profit Cards ───────────────────────────────────── */
  Alpine.data('profitCards', () => ({
    d: null, loading: true,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/profit-summary?' + Alpine.store('app').filterParams.replace('&', '')) } catch (e) { this.d = null }
      this.loading = false;
    },
    profitClass(v) { return v >= 0 ? 'text-emerald-400' : 'text-red-400' },
    voidClass(v) { return v > 5 ? 'text-red-400' : v > 2 ? 'text-amber-400' : '' }
  }));

  /* ── Daily Trends ───────────────────────────────────── */
  Alpine.data('trendsChart', () => ({
    d: [], loading: true, collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/trends?days=30' + Alpine.store('app').filterParams);
        Alpine.store('app').cache.trends = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
    },
    maxRevenue() { return Math.max(...this.d.map(x => x.revenue), 1) },
    barHeight(r) { return Math.max((r / this.maxRevenue() * 100), 2) }
  }));

  /* ── Peak Hours ─────────────────────────────────────── */
  Alpine.data('peakHours', () => ({
    d: [], loading: true, collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        const raw = await fetchJSON(API + '/peak-hours?' + Alpine.store('app').filterParams.replace('&', ''));
        Alpine.store('app').cache.peakhours = raw;
        this.d = Array.from({ length: 24 }, (_, i) => { const h = raw.find(x => x.hour === i); return { hour: i, salesCount: h ? h.salesCount : 0, revenue: h ? h.revenue : 0 } });
      } catch (e) { this.d = Array.from({ length: 24 }, (_, i) => ({ hour: i, salesCount: 0, revenue: 0 })) }
      this.loading = false;
    },
    maxRevenue() { return Math.max(...this.d.map(x => x.revenue), 1) },
    barHeight(r) { return Math.max((r / this.maxRevenue() * 100), 1) }
  }));

  /* ── Sale Profits ───────────────────────────────────── */
  Alpine.data('saleProfits', () => ({
    d: [], loading: true, page: 0, search: '', collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/sale-profits?limit=5000' + Alpine.store('app').filterParams);
        Alpine.store('app').cache.saleprofits = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
      this.page = 0;
    },
    get filtered() { return this.search ? this.d.filter(x => JSON.stringify(x).toLowerCase().includes(this.search.toLowerCase())) : this.d },
    get total() { return this.filtered.length },
    get pages() { return Math.ceil(this.total / PAGE_SIZE) },
    get paged() { return this.filtered.slice(this.page * PAGE_SIZE, (this.page + 1) * PAGE_SIZE) },
    prev() { if (this.page > 0) this.page-- },
    next() { if (this.page < this.pages - 1) this.page++ },
    profitClass(v) { return v > 0 ? 'text-emerald-400' : v < 0 ? 'text-red-400' : 'text-amber-400' },
    marginClass(v) { return v > 20 ? 'text-emerald-400' : v > 0 ? 'text-amber-400' : 'text-red-400' }
  }));

  /* ── Recent Sales ───────────────────────────────────── */
  Alpine.data('recentSales', () => ({
    d: [], loading: true, page: 0, search: '', collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/recent-sales?limit=5000' + Alpine.store('app').filterParams);
        Alpine.store('app').cache.sales = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
      this.page = 0;
    },
    get filtered() { return this.search ? this.d.filter(x => JSON.stringify(x).toLowerCase().includes(this.search.toLowerCase())) : this.d },
    get total() { return this.filtered.length },
    get pages() { return Math.ceil(this.total / PAGE_SIZE) },
    get paged() { return this.filtered.slice(this.page * PAGE_SIZE, (this.page + 1) * PAGE_SIZE) },
    prev() { if (this.page > 0) this.page-- },
    next() { if (this.page < this.pages - 1) this.page++ },
    badgeClass(pm, isV) { return isV ? 'bg-red-500/20 text-red-400' : pm === 'Cash' ? 'bg-emerald-500/20 text-emerald-400' : pm === 'E-Wallet' ? 'bg-blue-500/20 text-blue-400' : pm === 'Credit' ? 'bg-amber-500/20 text-amber-400' : 'bg-purple-500/20 text-purple-400' }
  }));

  /* ── Void Logs ──────────────────────────────────────── */
  Alpine.data('voidLogs', () => ({
    d: [], loading: true, page: 0, search: '', collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/void-logs?limit=5000' + Alpine.store('app').filterParams);
        Alpine.store('app').cache.voids = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
      this.page = 0;
    },
    get filtered() { return this.search ? this.d.filter(x => JSON.stringify(x).toLowerCase().includes(this.search.toLowerCase())) : this.d },
    get total() { return this.filtered.length },
    get pages() { return Math.ceil(this.total / PAGE_SIZE) },
    get paged() { return this.filtered.slice(this.page * PAGE_SIZE, (this.page + 1) * PAGE_SIZE) },
    prev() { if (this.page > 0) this.page-- },
    next() { if (this.page < this.pages - 1) this.page++ }
  }));

  /* ── Cashier Performance ────────────────────────────── */
  Alpine.data('cashierPerf', () => ({
    d: [], loading: true, collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/cashier-performance?' + Alpine.store('app').filterParams.replace('&', ''));
        Alpine.store('app').cache.cashiers = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
    }
  }));

  /* ── Expenses ───────────────────────────────────────── */
  Alpine.data('expensesPanel', () => ({
    cat: [], detail: [], loading: true, collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.cat = await fetchJSON(API + '/expenses-summary?days=30' + Alpine.store('app').filterParams);
        this.detail = await fetchJSON(API + '/expenses-list?limit=5000' + Alpine.store('app').filterParams);
        Alpine.store('app').cache.expenses = this.detail;
      } catch (e) { this.cat = []; this.detail = [] }
      this.loading = false;
    },
    get total() { return this.cat.reduce((s, x) => s + x.total, 0) },
    pct(v) { return this.total > 0 ? (v / this.total * 100).toFixed(1) : 0 }
  }));

  /* ── Shift History ──────────────────────────────────── */
  Alpine.data('shiftHistory', () => ({
    d: [], loading: true, page: 0, search: '', collapsed: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/shift-history?days=60' + Alpine.store('app').filterParams);
        Alpine.store('app').cache.shifts = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
      this.page = 0;
    },
    get filtered() { return this.search ? this.d.filter(x => JSON.stringify(x).toLowerCase().includes(this.search.toLowerCase())) : this.d },
    get total() { return this.filtered.length },
    get pages() { return Math.ceil(this.total / PAGE_SIZE) },
    get paged() { return this.filtered.slice(this.page * PAGE_SIZE, (this.page + 1) * PAGE_SIZE) },
    prev() { if (this.page > 0) this.page-- },
    next() { if (this.page < this.pages - 1) this.page++ }
  }));

  /* ── Stock Status ───────────────────────────────────── */
  Alpine.data('stockStatus', () => ({
    d: null, loading: false,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/stock-status?' + Alpine.store('app').filterParams.replace('&', ''));
        Alpine.store('app').cache.stock = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
    },
    stockClass(qty) { return qty === 0 ? 'text-red-400' : qty < 10 ? 'text-amber-400' : '' }
  }));

  /* ── Stock Receiving ────────────────────────────────── */
  Alpine.data('receivingPanel', () => ({
    d: [], loading: false, limit: 100,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/recent-receiving?limit=' + this.limit + Alpine.store('app').filterParams);
        Alpine.store('app').cache.receiving = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
    },
    setLimit(v) { this.limit = v; this.load() }
  }));

  /* ── Master Products ────────────────────────────────── */
  Alpine.data('masterProducts', () => ({
    d: [], loading: true, catFilter: '', editingId: null,
    async init() { window.addEventListener('load-products', () => this.load()); await this.load() },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/products/master/download');
      } catch (e) { this.d = [] }
      this.loading = false;
    },
    get categories() { const c = []; this.d.forEach(x => { if (x.category && !c.includes(x.category)) c.push(x.category) }); return c.sort() },
    get filtered() {
      if (!this.catFilter) return this.d;
      return this.d.filter(x => x.category === this.catFilter)
    },
    margin(p) { return p.price > 0 ? ((p.price - p.cost) / p.price * 100).toFixed(1) : '0.0' },
    marginClass(m) { const v = parseFloat(m); return v > 20 ? 'text-emerald-400' : v > 0 ? 'text-amber-400' : 'text-red-400' },
    openEditor(id) {
      this.editingId = id || null;
      Alpine.store('app').editorOpen = true;
    },
    closeEditor() { Alpine.store('app').editorOpen = false; this.editingId = null },
    async deleteProduct(id) {
      const p = this.d.find(x => x.id === id); if (!p) return;
      if (!confirm('Delete "' + p.name + '"?')) return;
      try {
        const r = await fetch(API + '/products/master/' + id, { method: 'DELETE' });
        if (!r.ok) throw new Error('Failed');
        toast('Product deleted', 'success');
        this.load();
      } catch (e) { toast('Delete failed: ' + e.message, 'error') }
    },
    get editingProduct() { return this.editingId ? this.d.find(x => x.id === this.editingId) : null }
  }));

  /* ── Product Editor (nested within masterProducts) ──── */
  Alpine.data('productEditor', () => ({
    name: '', barcode: '', category: '', price: 0, cost: 0, imageData: '',
    units: [], productId: null, categories: [],
    async init() {
      this.$watch('$store.app.section', () => { if (this.$store.app.section !== 'products') Alpine.store('app').editorOpen = false });
      try { this.categories = await fetchJSON(API + '/products/categories') } catch (e) {}
    },
    open(id) {
      this.productId = id || null;
      if (id) {
        const p = document.querySelector('[x-data="masterProducts"]')?.__x?.$data?.d?.find(x => x.id === id);
        if (p) { this.name = p.name; this.barcode = p.barcode || ''; this.category = p.category || ''; this.price = p.price; this.cost = p.cost; this.imageData = p.imageData || ''; this.units = (p.units || []).map(u => ({ ...u })) }
      } else { this.name = ''; this.barcode = ''; this.category = ''; this.price = 0; this.cost = 0; this.imageData = ''; this.units = [] }
    },
    addUnit() { this.units.push({ unitName: '', price: 0, qtyPerUnit: 1, isDefault: false }) },
    removeUnit(i) { this.units.splice(i, 1) },
    async save() {
      if (!this.name) { toast('Name required', 'error'); return }
      const data = {
        name: this.name, barcode: this.barcode, category: this.category,
        price: parseFloat(this.price) || 0, cost: parseFloat(this.cost) || 0,
        imageData: this.imageData,
        units: this.units.filter(u => u.unitName).map(u => ({ ...u, cost: (u.qtyPerUnit || 1) * (parseFloat(this.cost) || 0) }))
      };
      try {
        const api = API + '/products/master';
        if (this.productId) {
          const r = await fetch(api + '/' + this.productId, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
          if (!r.ok) throw new Error('Failed'); toast('Product updated', 'success');
        } else {
          const r = await fetch(api, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
          if (!r.ok) throw new Error('Failed'); toast('Product created', 'success');
        }
        this.productId = null;
        Alpine.store('app').editorOpen = false;
        dispatchEvent(new CustomEvent('load-products'));
      } catch (e) { toast('Save failed: ' + e.message, 'error') }
    },
    previewImage(e) {
      const file = e.target.files[0]; if (!file) return;
      const reader = new FileReader();
      reader.onload = (ev) => { this.imageData = ev.target.result };
      reader.readAsDataURL(file);
    }
  }));

  /* ── Warehouse ──────────────────────────────────────── */
  Alpine.data('warehousePanel', () => ({
    tab: 'products', data: [], loading: true, catFilter: '', search: '',
    async init() { window.addEventListener('load-warehouse', () => this.load()); await this.load(); this.startPoll() },
    async load() {
      this.loading = true;
      try {
        const endpoints = { products: '/warehouse/products', clients: '/warehouse/clients', orders: '/warehouse/orders', transfers: '/warehouse/transfers/pending' };
        this.data = await fetchJSON(API + (endpoints[this.tab] || '/warehouse/products'));
      } catch (e) { this.data = [] }
      this.loading = false;
    },
    switchTab(t) { this.tab = t; this.load(); this.catFilter = '' },
    get categories() { if (this.tab !== 'products') return []; const c = []; this.data.forEach(x => { if (x.category && !c.includes(x.category)) c.push(x.category) }); return c.sort() },
    get filtered() {
      let items = this.data;
      if (this.catFilter && this.tab === 'products') items = items.filter(x => x.category === this.catFilter);
      if (this.search) { const q = this.search.toLowerCase(); items = items.filter(x => JSON.stringify(x).toLowerCase().includes(q)) }
      return items;
    },
    setFilter(cat) { this.catFilter = cat },

    /* Modals */
    modalOpen: false, modalTitle: '', modalMode: 'add', modalId: null, form: {},

    openAdd() {
      this.modalMode = 'add'; this.modalId = null; this.modalOpen = true;
      if (this.tab === 'products') this.form = { name: '', barcode: '', category: '', boxPrice: 0, boxCost: 0, boxQty: 1, piecePrice: 0, stockQty: 0 };
      else if (this.tab === 'clients') this.form = { name: '', contact: '', address: '', storeType: 'pos', storeId: '' };
      this.modalTitle = this.tab === 'products' ? 'Add Product' : 'Add Client';
    },
    openEdit(id) {
      const p = this.data.find(x => x.id === id); if (!p) return;
      this.modalMode = 'edit'; this.modalId = id; this.modalOpen = true;
      if (this.tab === 'products') this.form = { name: p.name, barcode: p.barcode || '', category: p.category || '', boxPrice: p.boxPrice, boxCost: p.boxCost, boxQty: p.boxQty, piecePrice: p.boxQty > 0 ? (p.boxPrice / p.boxQty).toFixed(2) : p.piecePrice, stockQty: p.stockQty };
      else if (this.tab === 'clients') this.form = { name: p.name, contact: p.contact || '', address: p.address || '', storeType: p.storeType || 'pos', storeId: p.storeId || '' };
      this.modalTitle = this.tab === 'products' ? 'Edit: ' + p.name : 'Edit: ' + p.name;
      this._computePiecePrice();
    },
    closeModal() { this.modalOpen = false },
    _computePiecePrice() {
      const bp = parseFloat(this.form.boxPrice) || 0, bq = parseInt(this.form.boxQty) || 1;
      if (bp && bq) this.form.piecePrice = (bp / bq).toFixed(2);
    },
    async save() {
      try {
        const baseUrl = API + (this.tab === 'products' ? '/warehouse/products' : '/warehouse/clients');
        const method = this.modalId ? 'PUT' : 'POST';
        const url = this.modalId ? baseUrl + '/' + this.modalId : baseUrl;
        const body = this.tab === 'products'
          ? { name: this.form.name, barcode: this.form.barcode, category: this.form.category, boxPrice: parseFloat(this.form.boxPrice) || 0, boxCost: parseFloat(this.form.boxCost) || 0, boxQty: parseInt(this.form.boxQty) || 1, piecePrice: parseFloat(this.form.piecePrice) || 0 }
          : { name: this.form.name, contact: this.form.contact, address: this.form.address, storeType: this.form.storeType, storeId: this.form.storeId };
        const r = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (!r.ok) throw new Error('Failed');
        if (this.tab === 'products' && !this.modalId) {
          const j = await r.json();
          if (this.form.stockQty) await fetch(API + '/warehouse/products/' + j.id + '/stock', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ stockQty: parseInt(this.form.stockQty) || 0 }) });
        }
        toast((this.modalId ? 'Updated' : 'Created') + ' successfully', 'success');
        this.modalOpen = false;
        this.load();
      } catch (e) { toast('Save failed: ' + e.message, 'error') }
    },
    async deleteItem(id) {
      const p = this.data.find(x => x.id === id); if (!p) return;
      if (!confirm('Delete "' + p.name + '"?')) return;
      try {
        const baseUrl = API + (this.tab === 'products' ? '/warehouse/products' : '/warehouse/clients');
        await fetch(baseUrl + '/' + id, { method: 'DELETE' });
        toast('Deleted', 'success');
        this.load();
      } catch (e) { toast('Delete failed: ' + e.message, 'error') }
    },
    async adjustStock(id) {
      const p = this.data.find(x => x.id === id); if (!p) return;
      const qty = prompt('Set stock quantity for "' + p.name + '":', p.stockQty);
      if (qty === null) return;
      const n = parseInt(qty); if (isNaN(n) || n < 0) { toast('Invalid quantity', 'error'); return }
      try {
        await fetch(API + '/warehouse/products/' + id + '/stock', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ stockQty: n }) });
        toast('Stock updated', 'success');
        this.load();
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },

    /* Orders */
    orderModal: false, orderForm: { clientId: '', clientName: '', notes: '', items: [] },
    openNewOrder() { this.orderModal = true; this.orderForm = { clientId: '', clientName: '', notes: '', items: [] } },
    closeOrder() { this.orderModal = false },
    async fetchClients() { try { return await fetchJSON(API + '/warehouse/clients') } catch (e) { return [] } },
    async fetchProducts() { try { return await fetchJSON(API + '/warehouse/products') } catch (e) { return [] } },
    addOrderItem(pid, pname, unit, qty, boxqty, boxprice, pieceprice) {
      const price = unit === 'piece' ? (parseFloat(pieceprice) || 0) : (parseFloat(boxprice) || 0);
      const total = price * qty;
      const baseQty = unit === 'box' ? qty * (parseInt(boxqty) || 1) : qty;
      this.orderForm.items.push({ productId: pid, productName: pname, unitType: unit, qty, price, totalPrice: total, baseQty, baseUnitName: 'Piece', boxQtyPerUnit: parseInt(boxqty) || 1 });
    },
    removeOrderItem(i) { this.orderForm.items.splice(i, 1) },
    get orderTotal() { return this.orderForm.items.reduce((s, x) => s + x.totalPrice, 0) },
    async saveOrder() {
      if (!this.orderForm.clientId) { toast('Select a client', 'error'); return }
      if (!this.orderForm.items.length) { toast('Add at least one item', 'error'); return }
      try {
        const r = await fetch(API + '/warehouse/orders', {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ clientId: parseInt(this.orderForm.clientId), clientName: this.orderForm.clientName, notes: this.orderForm.notes, items: this.orderForm.items })
        });
        const j = await r.json();
        if (j.id) { toast('Order #' + j.id + ' created', 'success'); this.orderModal = false; this.load() }
        else throw new Error('Failed');
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },

    /* Order actions */
    async changeOrderStatus(id, status) {
      try {
        await fetch(API + '/warehouse/orders/' + id + '/status', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }) });
        toast('Order #' + id + ' -> ' + status.toUpperCase(), 'success');
        this.load();
        this.updateBadge();
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    async receiveOrder(id) {
      if (!confirm('Mark order #' + id + ' as received?')) return;
      try {
        const r = await fetch(API + '/warehouse/orders/' + id + '/receive', { method: 'PUT' });
        const j = await r.json();
        if (!r.ok) throw new Error(j.error || 'Failed');
        toast('Order #' + id + ' received', 'success');
        this.load();
        this.updateBadge();
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    async cancelOrder(id) {
      if (!confirm('Cancel order #' + id + '?')) return;
      await this.changeOrderStatus(id, 'cancelled');
    },
    async viewOrder(id) {
      try {
        const items = await fetchJSON(API + '/warehouse/orders/' + id);
        // Show in a simple alert-like format or a dedicated modal
        if (!items || !items.length) { toast('No items', 'info'); return }
        // We'll use a simple approach: open the orderViewModal
        this.orderViewItems = items;
        this.orderViewId = id;
        this.orderViewOpen = true;
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    orderViewOpen: false, orderViewId: null, orderViewItems: [],
    closeOrderView() { this.orderViewOpen = false; this.orderViewItems = [] },
    async importFromMaster() {
      try {
        const mp = await fetchJSON(API + '/warehouse/products');
        this.masterImportList = mp;
        this.masterImportOpen = true;
      } catch (e) { toast('Error loading master products', 'error') }
    },
    masterImportOpen: false, masterImportList: [], masterSearch: '',
    closeImport() { this.masterImportOpen = false; this.masterSearch = '' },
    async doImport(mid) {
      try {
        const r = await fetch(API + '/warehouse/products/from-master/' + mid, { method: 'POST' });
        const j = await r.json();
        if (j.id) { toast('Imported (ID: ' + j.id + ')', 'success'); this.masterImportOpen = false; this.load() }
        else toast('Failed to import', 'error');
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    get filteredMaster() {
      if (!this.masterSearch) return this.masterImportList || [];
      const q = this.masterSearch.toLowerCase();
      return this.masterImportList.filter(x => (x.name || '').toLowerCase().includes(q) || (x.barcode || '').toLowerCase().includes(q));
    },

    /* Badge */
    badgeCount: 0,
    async updateBadge() {
      try {
        const d = await fetchJSON(API + '/warehouse/transfers/pending');
        this.badgeCount = d ? d.length : 0;
      } catch (e) { this.badgeCount = 0 }
      Alpine.store('app')._whBadge = this.badgeCount;
    },
    startPoll() {
      this.updateBadge();
      setInterval(() => { if (this.tab === 'orders' || this.tab === 'transfers') this.load(); this.updateBadge() }, 30000);
    }
  }));

  /* ── Customers ──────────────────────────────────────── */
  Alpine.data('customersList', () => ({
    d: [], loading: true,
    async init() { window.addEventListener('load-customers', () => this.load()); await this.load() },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/customers?' + Alpine.store('app').storeParam.replace('&', '')) } catch (e) { this.d = [] }
      this.loading = false;
    }
  }));

  /* ── Users ──────────────────────────────────────────── */
  Alpine.data('usersList', () => ({
    d: [], loading: true,
    async init() { window.addEventListener('load-users', () => this.load()); await this.load() },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/users?' + Alpine.store('app').storeParam.replace('&', '')) } catch (e) { this.d = [] }
      this.loading = false;
    }
  }));
});

/* ── Product Analytics ──────────────────────────────── */
Alpine.data('productAnalytics', () => ({
  d: [], loading: true, collapsed: false, sortBy: 'qty', limit: 10,
  async init() {
    window.addEventListener('load-analytics', () => this.load());
    window.addEventListener('refresh-data', () => this.load());
    await this.load();
  },
  async load() {
    this.loading = true;
    try {
      const params = Alpine.store('app').filterParams.replace('&', '');
      this.d = await fetchJSON(API + '/top-products?limit=50&sort=' + this.sortBy + (params ? '&' + params : ''));
      Alpine.store('app').cache.analytics = this.d;
    } catch (e) { this.d = [] }
    this.loading = false;
  },
  setSort(s) { this.sortBy = s; this.load() },
  get filtered() { return this.d.slice(0, this.limit) },
  marginClass(m) { const v = parseFloat(m); return v > 20 ? 'text-emerald-400' : v > 0 ? 'text-amber-400' : 'text-red-400' }
}));

/* ── CSV Export ──────────────────────────────────────── */
window.exportCSV = (name) => {
  const map = {
    sales: ['Invoice,Date,Amount,Method,Order Type,Voided,Cashier,Store', d => d.map(x => [x.invoiceNo, x.saleDate, x.grandTotal, x.paymentMethod, x.orderType, x.isVoided ? 'Yes' : 'No', x.cashier, x.storeId])],
    trends: ['Day,Sales,Revenue,Cashiers', d => d.map(x => [x.day, x.salesCount, x.revenue, x.cashiers])],
    peakhours: ['Hour,Sales,Revenue', d => d.map(x => [x.hour + ':00', x.salesCount, x.revenue])],
    saleprofits: ['Invoice,Date,Revenue,Cost,Profit,Margin%,Store,Cashier', d => d.map(x => [x.invoiceNo, x.saleDate, x.revenue, x.cost, x.profit, x.marginPct + '%', x.storeId, x.cashier])],
    cashiers: ['Cashier,Sales,Revenue,Avg Tx,Cash,E-Wallet,Credit', d => d.map(x => [x.cashier, x.totalSales, x.totalRevenue, x.avgTransaction, x.cashCount, x.ewalletCount, x.creditCount])],
    expenses: ['Amount,Category,Description,Reference,Cashier,Date', d => d.map(x => [x.amount, x.category, x.description, x.referenceNo, x.cashier, x.timestamp])],
    shifts: ['Close Date,Store,Sales,Cash,E-Wallet,Credit,Voided,Expenses,Cash on Hand,Variance,User', d => d.map(x => [x.closeDate, x.storeId, x.totalSales, x.totalCash, x.totalEwallet, x.totalCredit, x.totalVoided, x.totalExpenses, x.cashOnHand, x.difference, x.userName])],
    voids: ['Invoice,Action,Item,Reason,Qty,Amount,Cashier,Date', d => d.map(x => [x.invoiceNo, x.action, x.productName, x.reason, x.quantity, x.amount, x.userName, x.createdAt])],
    receiving: ['Product,Barcode,Qty Added,Before,After,Reference,Cashier,Store,Date', d => d.map(x => [x.productName, x.barcode, x.quantityAdded, x.stockBefore, x.stockAfter, x.reference, x.userName, x.storeId, x.createdAt])],
    stock: ['Product,Barcode,Category,Stock,Price,Cost', d => d.map(x => [x.name, x.barcode, x.category, x.stockQty, x.price, x.cost])],
    analytics: ['Product,Barcode,Category,Qty Sold,Revenue,Cost,Profit,Margin%', d => d.map(x => [x.productName, x.barcode, x.category, x.totalQty, x.totalRevenue, x.totalCost, x.totalProfit, x.marginPct + '%'])]
  };
  const cache = Alpine.store('app').cache[name] || Alpine.store('app').cache[name.replace('wh-', '')];
  if (!cache || !cache.length) { toast('No data to export', 'error'); return }
  if (name.startsWith('wh-')) {
    const wh = document.querySelector('[x-data="warehousePanel"]')?.__x?.$data;
    if (!wh) return;
    const data = wh.data;
    let headers, rows;
    if (name === 'wh-products') { headers = 'ID,Name,Barcode,Category,Box Price,Box Qty,Piece Price,Stock'; rows = data.map(x => [x.id, x.name, x.barcode, x.category, x.boxPrice, x.boxQty, x.piecePrice, x.stockQty]) }
    else if (name === 'wh-clients') { headers = 'ID,Name,Contact,Address,Type'; rows = data.map(x => [x.id, x.name, x.contact, x.address, x.storeType]) }
    else if (name === 'wh-orders') { headers = 'ID,Client,Status,Total,Notes,Date'; rows = data.map(x => [x.id, x.clientName, x.status, x.totalAmount, x.notes, x.createdAt]) }
    else if (name === 'wh-transfers') { headers = 'Order ID,Client,Total,Notes,Date'; rows = data.map(x => [x.orderId, x.clientName, x.totalAmount, x.notes, x.createdAt]) }
    if (!headers) return;
    let csv = headers + '\n' + rows.map(r => r.map(c => '"' + (c + '').replace(/"/g, '""') + '"').join(',')).join('\n');
    downloadCSV(csv, name);
    return;
  }
  const entry = map[name];
  if (!entry) { toast('Unknown export type', 'error'); return }
  let csv = entry[0] + '\n' + entry[1](cache).map(r => r.map(c => '"' + (c + '').replace(/"/g, '""') + '"').join(',')).join('\n');
  downloadCSV(csv, name);
};

window.exportAllCSV = () => { ['sales', 'trends', 'peakhours', 'saleprofits', 'cashiers', 'expenses', 'shifts', 'voids', 'receiving', 'stock', 'analytics'].forEach(n => { if (Alpine.store('app').cache[n]) exportCSV(n) }) };

function downloadCSV(csv, name) {
  const blob = new Blob([csv], { type: 'text/csv' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = name + '_' + new Date().toISOString().slice(0, 10) + '.csv';
  a.click();
  toast(name + ' exported', 'success');
}

/* ── Load Stores on Page Load ────────────────────────── */
(async () => {
  try {
    const stores = await fetchJSON(API + '/stores');
    Alpine.store('app').stores = stores;
    stores.forEach(s => Alpine.store('app').storeMap[s.storeId] = s.storeName || '');
  } catch (e) {}
  // Initial dark mode
  document.documentElement.classList.toggle('dark', Alpine.store('app').darkMode);
  // Auto refresh every 60s
  setInterval(() => { if (!document.hidden) Alpine.store('app').refreshAll() }, 60000);
  document.addEventListener('visibilitychange', () => { if (!document.hidden) Alpine.store('app').refreshAll() });
  // Initial load
  Alpine.store('app').refreshAll();
})();
