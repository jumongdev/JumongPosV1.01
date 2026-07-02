/* ══════════════════════════════════════════════════════════════════════════════════ */
// Loaded BEFORE Alpine CDN. Registers in alpine:init so
// Alpine.store/Alpine.data exist when initTree processes DOM.

/* Constants & utilities needed by Alpine components at init time */
const PAGE_SIZE = 20;
window.fmt = n => Number(n || 0).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
window.fmtInt = n => Number(n || 0).toLocaleString('en-PH');
window.esc = s => (s + '').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
window.shortStore = (sid, name) => (name && name.trim()) ? name.trim() : (sid ? sid.replace('STORE-', '').slice(0, 12) : 'Unknown');

document.addEventListener('alpine:init', () => {

Alpine.store('app', {
    section: 'dashboard',
    whSubpage: 'product',
    storeId: '',
    range: 'today',
    customFrom: '',
    customTo: '',
    singleDate: new Date().toISOString().slice(0, 10),
    rangeFrom: '',
    rangeTo: '',
    darkMode: localStorage.getItem('theme') === 'dark',
    isOnline: true,
    stores: [],
    storeMap: {},
    lastRefresh: '',
    cache: {},
    editorOpen: false, editingId: null, editingProductData: null,
    saleModalOpen: false, saleInvoiceNo: '', saleItems: [], saleLoading: false,
    salePaymentMethod: '', saleReferenceNo: '', saleEwPaid: 0, saleGrandTotal: 0,
    _sidebarOpen: window.innerWidth < 768 ? false : localStorage.getItem('sidebar') !== 'collapsed',
    _whBadge: 0,

    toggleDark() {
      this.darkMode = !this.darkMode;
      localStorage.setItem('theme', this.darkMode ? 'dark' : 'light');
      document.documentElement.classList.toggle('dark', this.darkMode);
    },
    async _showSaleItems(invoiceNo, storeId) {
      this.saleInvoiceNo = invoiceNo;
      this.saleModalOpen = true;
      this.saleLoading = true;
      this.salePaymentMethod = '';
      this.saleReferenceNo = '';
      this.saleEwPaid = 0;
      this.saleGrandTotal = 0;
      try {
        let url = API + '/sale-items?invoiceNo=' + encodeURIComponent(invoiceNo);
        if (storeId) url += '&storeId=' + encodeURIComponent(storeId);
        const data = await fetchJSON(url);
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
      document.getElementById('sidebar')?.classList.remove('open');
      if (section.startsWith('wh-')) {
        this.section = 'warehouse';
        this.whSubpage = section.replace('wh-', '');
        dispatchEvent(new CustomEvent('load-warehouse'));
        return;
      }
      this.section = section;
      if (section === 'customers') dispatchEvent(new CustomEvent('load-customers'));
      if (section === 'users') dispatchEvent(new CustomEvent('load-users'));
      if (section === 'warehouse') { this.whSubpage = 'product'; dispatchEvent(new CustomEvent('load-warehouse')); }
      if (section === 'products') dispatchEvent(new CustomEvent('load-products'));
      if (section === 'available') { dispatchEvent(new CustomEvent('load-stock')); dispatchEvent(new CustomEvent('load-receiving')) }
      if (section === 'analytics') dispatchEvent(new CustomEvent('load-analytics'));
    },
    switchWhSubpage(subpage) {
      this.whSubpage = subpage;
    },
    isActive(id) {
      if (id.startsWith('wh-')) return this.section === 'warehouse' && this.whSubpage === id.replace('wh-', '');
      return this.section === id;
    },
    setStore(val) { this.storeId = val; this.refreshAll() },
    setRange(range) {
      this.range = range;
      if (range !== 'custom') { this.customFrom = ''; this.customTo = ''; this.singleDate = ''; this.rangeFrom = ''; this.rangeTo = '' }
      this.refreshAll();
    },
    applyCustom() {
      if (!this.customFrom || !this.customTo) { toast('Select both dates', 'error'); return }
      this.range = 'custom';
      this.singleDate = '';
      this.rangeFrom = '';
      this.rangeTo = '';
      this.refreshAll();
    },
    setSingleDate(val) {
      if (!val) return;
      this.singleDate = val;
      this.customFrom = '';
      this.customTo = '';
      this.rangeFrom = '';
      this.rangeTo = '';
      this.range = 'custom';
      this.refreshAll();
    },
    setDateRange() {
      if (!this.rangeFrom || !this.rangeTo) { toast('Select both dates', 'error'); return }
      this.singleDate = '';
      this.customFrom = '';
      this.customTo = '';
      this.range = 'custom';
      this.refreshAll();
    },
    refreshAll() {
      dispatchEvent(new CustomEvent('refresh-data'));
      this.lastRefresh = new Date().toLocaleTimeString('en-PH', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true });
    },
    get storeParam() { return this.storeId ? '&storeId=' + encodeURIComponent(this.storeId) : '' },
    get rangeParam() {
      if (this.singleDate) return '&range=custom&date=' + this.singleDate;
      if (this.rangeFrom && this.rangeTo) return '&range=custom&date=' + this.rangeFrom + '&date_to=' + this.rangeTo;
      if (this.range === 'custom' && this.customFrom) return '&range=custom&date=' + this.customFrom;
      return '&range=' + this.range;
    },
    get filterParams() { return this.storeParam + this.rangeParam }
  });

  /* ΓöÇΓöÇ Summary Cards ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('summaryCards', () => ({
    d: null, loading: true,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/summary?' + Alpine.store('app').filterParams.replace('&', '')) } catch (e) { this.d = null }
      this.loading = false;
    }
  }));

  /* ΓöÇΓöÇ Profit Cards ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Daily Trends ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Peak Hours ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Sale Profits ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Recent Sales ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Void Logs ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Cashier Performance ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Expenses ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Shift History ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Stock Status ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('stockStatus', () => ({
    d: [], loading: false, search: '', catFilter: '', page: 0,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/stock-status?' + Alpine.store('app').filterParams.replace('&', ''));
        Alpine.store('app').cache.stock = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
      this.page = 0;
    },
    setFilter(c) { this.catFilter = c; this.page = 0 },
    get categories() { const c = []; this.d.forEach(x => { if (x.category && !c.includes(x.category)) c.push(x.category) }); return c.sort() },
    get filtered() {
      let items = this.d;
      if (this.search) { const q = this.search.toLowerCase(); items = items.filter(x => (x.name || '').toLowerCase().includes(q) || (x.barcode || '').toLowerCase().includes(q)) }
      if (this.catFilter) items = items.filter(x => x.category === this.catFilter);
      return items;
    },
    get total() { return this.filtered.length },
    get pages() { return Math.ceil(this.total / PAGE_SIZE) },
    get paged() { return this.filtered.slice(this.page * PAGE_SIZE, (this.page + 1) * PAGE_SIZE) },
    prev() { if (this.page > 0) this.page-- },
    next() { if (this.page < this.pages - 1) this.page++ },
    stockClass(qty) { return qty === 0 ? 'text-red-400' : qty < 10 ? 'text-amber-400' : '' }
  }));

  /* ΓöÇΓöÇ Stock Receiving ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
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

  /* ΓöÇΓöÇ Master Products ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('masterProducts', () => ({
    d: [], loading: true, search: '', catFilter: '',
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
      let items = this.d;
      if (this.search) { const q = this.search.toLowerCase(); items = items.filter(x => (x.name || '').toLowerCase().includes(q) || (x.barcode || '').toLowerCase().includes(q) || (x.category || '').toLowerCase().includes(q)) }
      if (this.catFilter) items = items.filter(x => x.category === this.catFilter);
      return items;
    },
    margin(p) { return p.price > 0 ? ((p.price - p.cost) / p.price * 100).toFixed(1) : '0.0' },
    marginClass(m) { const v = parseFloat(m); return v > 20 ? 'text-emerald-400' : v > 0 ? 'text-amber-400' : 'text-red-400' },
    openEditor(id) {
      Alpine.store('app').editingId = id || null;
      Alpine.store('app').editingProductData = id ? this.d.find(x => x.id === id) || null : null;
      Alpine.store('app').editorOpen = true;
    },
    closeEditor() { Alpine.store('app').editorOpen = false; Alpine.store('app').editingId = null; Alpine.store('app').editingProductData = null },
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
    get editingProduct() { const id = Alpine.store('app').editingId; return id ? this.d.find(x => x.id === id) : null }
  }));

  /* ΓöÇΓöÇ Product Editor ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('productEditor', () => ({
    name: '', barcode: '', category: '', price: 0, cost: 0, imageData: '',
    units: [], productId: null, categories: [],
    async init() {
      this.$watch('$store.app.section', () => { if (this.$store.app.section !== 'products') this.reset() });
      this.$watch('$store.app.editorOpen', (v) => { if (v) this.open(Alpine.store('app').editingId) });
      try { this.categories = await fetchJSON(API + '/products/categories') } catch (e) {}
    },
    open(id) {
      this.productId = id || null;
      const p = Alpine.store('app').editingProductData;
      if (id && p) { this.name = p.name; this.barcode = p.barcode || ''; this.category = p.category || ''; this.price = p.price; this.cost = p.cost; this.imageData = p.imageData || ''; this.units = (p.units || []).map(u => ({ ...u })) }
      else { this.name = ''; this.barcode = ''; this.category = ''; this.price = 0; this.cost = 0; this.imageData = ''; this.units = [] }
    },
    reset() { this.productId = null; this.name = ''; this.barcode = ''; this.category = ''; this.price = 0; this.cost = 0; this.imageData = ''; this.units = []; Alpine.store('app').editorOpen = false; Alpine.store('app').editingId = null; Alpine.store('app').editingProductData = null },
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
        this.reset();
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

  /* ΓöÇΓöÇ Warehouse ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('warehousePanel', () => ({
    products: [], clientsData: [], orders: [], transfers: [], loading: true, catFilter: '', search: '',
    async init() { window.addEventListener('load-warehouse', () => this.load()); await this.load(); this.startPoll() },
    async load() {
      this.loading = true;
      try {
        const sp = Alpine.store('app').whSubpage;
        if (sp === 'product' || sp === 'inventory') this.products = await fetchJSON(API + '/warehouse/products');
        else if (sp === 'onlineorder') { this.clientsData = await fetchJSON(API + '/warehouse/clients'); this.orders = await fetchJSON(API + '/warehouse/orders') }
        else if (sp === 'transfer') this.transfers = await fetchJSON(API + '/warehouse/transfers/pending');
      } catch (e) { this.products = []; this.clientsData = []; this.orders = []; this.transfers = [] }
      this.loading = false;
    },
    get sp() { return Alpine.store('app').whSubpage },
    get categories() {
      if (this.sp !== 'product' && this.sp !== 'inventory') return [];
      const c = []; this.products.forEach(x => { if (x.category && !c.includes(x.category)) c.push(x.category) }); return c.sort()
    },
    get filtered() {
      let items = this.sp === 'product' || this.sp === 'inventory' ? this.products : this.sp === 'onlineorder' ? this.orders : this.transfers;
      if (this.catFilter && (this.sp === 'product' || this.sp === 'inventory')) items = items.filter(x => x.category === this.catFilter);
      if (this.search) { const q = this.search.toLowerCase(); items = items.filter(x => JSON.stringify(x).toLowerCase().includes(q)) }
      return items;
    },
    setFilter(cat) { this.catFilter = cat },

    modalOpen: false, modalTitle: '', modalMode: 'add', modalId: null, form: {},

    openAdd() {
      this.modalMode = 'add'; this.modalId = null; this.modalOpen = true;
      if (this.sp === 'product') this.form = { name: '', barcode: '', category: '', boxPrice: 0, boxCost: 0, boxQty: 1, piecePrice: 0, stockQty: 0 };
      else this.form = { name: '', contact: '', address: '', storeType: 'pos', storeId: '' };
      this.modalTitle = this.sp === 'product' ? 'Add Product' : 'Add Client';
    },
    openEdit(id) {
      const arr = this.sp === 'product' || this.sp === 'inventory' ? this.products : this.clientsData;
      const p = arr.find(x => x.id === id); if (!p) return;
      this.modalMode = 'edit'; this.modalId = id; this.modalOpen = true;
      if (this.sp === 'product' || this.sp === 'inventory') this.form = { name: p.name, barcode: p.barcode || '', category: p.category || '', boxPrice: p.boxPrice, boxCost: p.boxCost, boxQty: p.boxQty, piecePrice: p.boxQty > 0 ? (p.boxPrice / p.boxQty).toFixed(2) : p.piecePrice, stockQty: p.stockQty };
      else this.form = { name: p.name, contact: p.contact || '', address: p.address || '', storeType: p.storeType || 'pos', storeId: p.storeId || '' };
      this.modalTitle = (this.sp === 'product' || this.sp === 'inventory') ? 'Edit: ' + p.name : 'Edit: ' + p.name;
      this._computePiecePrice();
    },
    closeModal() { this.modalOpen = false },
    _computePiecePrice() {
      const bp = parseFloat(this.form.boxPrice) || 0, bq = parseInt(this.form.boxQty) || 1;
      if (bp && bq) this.form.piecePrice = (bp / bq).toFixed(2);
    },
    async save() {
      try {
        const isProduct = this.sp === 'product' || this.sp === 'inventory';
        const baseUrl = API + (isProduct ? '/warehouse/products' : '/warehouse/clients');
        const method = this.modalId ? 'PUT' : 'POST';
        const url = this.modalId ? baseUrl + '/' + this.modalId : baseUrl;
        const body = isProduct
          ? { name: this.form.name, barcode: this.form.barcode, category: this.form.category, boxPrice: parseFloat(this.form.boxPrice) || 0, boxCost: parseFloat(this.form.boxCost) || 0, boxQty: parseInt(this.form.boxQty) || 1, piecePrice: parseFloat(this.form.piecePrice) || 0 }
          : { name: this.form.name, contact: this.form.contact, address: this.form.address, storeType: this.form.storeType, storeId: this.form.storeId };
        const r = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (!r.ok) throw new Error('Failed');
        if (isProduct && !this.modalId) {
          const j = await r.json();
          if (this.form.stockQty) await fetch(API + '/warehouse/products/' + j.id + '/stock', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ stockQty: parseInt(this.form.stockQty) || 0 }) });
        }
        toast((this.modalId ? 'Updated' : 'Created') + ' successfully', 'success');
        this.modalOpen = false;
        this.load();
      } catch (e) { toast('Save failed: ' + e.message, 'error') }
    },
    async deleteItem(id) {
      const arr = this.sp === 'transfer' ? this.transfers : (this.sp === 'onlineorder' ? this.clientsData : this.products);
      const p = arr.find(x => x.id === id); if (!p) return;
      if (!confirm('Delete "' + p.name + '"?')) return;
      try {
        const isProduct = this.sp === 'product' || this.sp === 'inventory';
        await fetch(API + (isProduct ? '/warehouse/products' : '/warehouse/clients') + '/' + id, { method: 'DELETE' });
        toast('Deleted', 'success');
        this.load();
      } catch (e) { toast('Delete failed: ' + e.message, 'error') }
    },
    async adjustStock(id) {
      const p = this.products.find(x => x.id === id); if (!p) return;
      const qty = prompt('Set stock quantity for "' + p.name + '":', p.stockQty);
      if (qty === null) return;
      const n = parseInt(qty); if (isNaN(n) || n < 0) { toast('Invalid quantity', 'error'); return }
      try {
        await fetch(API + '/warehouse/products/' + id + '/stock', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ stockQty: n }) });
        toast('Stock updated', 'success');
        this.load();
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },

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
        const r = await fetch(API + '/warehouse/orders/' + id + '/receive', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: '{}' });
        const j = await r.json();
        if (!r.ok) throw new Error(j.error || 'Failed');
        if (j.shortages && j.shortages.length > 0)
          toast('Order #' + id + ' received with ' + j.shortages.length + ' shortage(s)', 'warning');
        else
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
        if (!items || !items.length) { toast('No items', 'info'); return }
        this.orderViewItems = items;
        this.orderViewId = id;
        this.orderViewOpen = true;
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    orderViewOpen: false, orderViewId: null, orderViewItems: [],
    closeOrderView() { this.orderViewOpen = false; this.orderViewItems = [] },
    async importFromMaster() {
      try {
        const mp = await fetchJSON(API + '/products/master');
        this.masterImportList = mp;
        this.masterImportOpen = true;
      } catch (e) { toast('Error loading master products', 'error') }
    },
    masterImportOpen: false, masterImportList: [], masterSearch: '', importBoxQty: 1,
    closeImport() { this.masterImportOpen = false; this.masterSearch = '' },
    async doImport(mid) {
      try {
        const r = await fetch(API + '/warehouse/products/from-master/' + mid + '?boxQty=' + this.importBoxQty, { method: 'POST' });
        const j = await r.json();
        if (j.id) { toast('Imported (ID: ' + j.id + ')', 'success'); this.masterImportOpen = false; this.load() }
        else toast('Failed to import', 'error');
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    get filteredMaster() {
      if (!this.masterSearch) return this.masterImportList || [];
      const q = this.masterSearch.toLowerCase();
      return this.masterImportList.filter(x => (x.name || '').toLowerCase().includes(q) || (x.barcode || '').toLowerCase().includes(q) || (x.category || '').toLowerCase().includes(q));
    },
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
      setInterval(() => { if (Alpine.store('app').whSubpage === 'onlineorder' || Alpine.store('app').whSubpage === 'transfer') this.load(); this.updateBadge() }, 30000);
    }
  }));




  /* ΓöÇΓöÇ Customers ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('customersList', () => ({
    d: [], loading: true,
    async init() { window.addEventListener('load-customers', () => this.load()); await this.load() },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/customers?' + Alpine.store('app').storeParam.replace('&', '')) } catch (e) { this.d = [] }
      this.loading = false;
    }
  }));

  /* ΓöÇΓöÇ Users ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('usersList', () => ({
    d: [], loading: true,
    async init() { window.addEventListener('load-users', () => this.load()); await this.load() },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/users?' + Alpine.store('app').storeParam.replace('&', '')) } catch (e) { this.d = [] }
      this.loading = false;
    }
  }));

  /* ΓöÇΓöÇ Product Analytics ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('productAnalytics', () => ({
    d: [], loading: true, collapsed: false, sortBy: 'qty', limit: 20, page: 0, catFilter: '',
    async init() {
      window.addEventListener('load-analytics', () => this.load());
      window.addEventListener('refresh-data', () => this.load());
      await this.load();
    },
    async load() {
      this.loading = true;
      try {
        const params = Alpine.store('app').filterParams.replace('&', '');
        this.d = await fetchJSON(API + '/top-products?limit=5000&sort=' + this.sortBy + (params ? '&' + params : ''));
        Alpine.store('app').cache.analytics = this.d;
      } catch (e) { this.d = [] }
      this.loading = false;
      this.page = 0;
    },
    setSort(s) { this.sortBy = s; this.load() },
    setLimit(v) { this.limit = parseInt(v); this.page = 0 },
    setFilter(c) { this.catFilter = c; this.page = 0 },
    get categories() { const c = []; this.d.forEach(x => { if (x.category && !c.includes(x.category)) c.push(x.category) }); return c.sort() },
    get filtered() {
      if (!this.catFilter) return this.d;
      return this.d.filter(x => x.category === this.catFilter);
    },
    get total() { return this.filtered.length },
    get pages() { return Math.ceil(this.total / this.limit) },
    get paged() { return this.filtered.slice(this.page * this.limit, (this.page + 1) * this.limit) },
    prev() { if (this.page > 0) this.page-- },
    next() { if (this.page < this.pages - 1) this.page++ },
    marginClass(m) { const v = parseFloat(m); return v > 20 ? 'text-emerald-400' : v > 0 ? 'text-amber-400' : 'text-red-400' }
  }));

});