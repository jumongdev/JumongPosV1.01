/* ══════════════════════════════════════════════════════════════════════════════════ */
// Loaded BEFORE Alpine CDN. Registers in alpine:init so
// Alpine.store/Alpine.data exist when initTree processes DOM.

/* Constants & utilities needed by Alpine components at init time */
const PAGE_SIZE = 20;
window.fmt = n => Number(n || 0).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
window.fmtInt = n => Number(n || 0).toLocaleString('en-PH');
window.esc = s => (s + '').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
window.shortStore = (sid, name) => (name && name.trim()) ? name.trim() : (sid ? sid.replace('STORE-', '').slice(0, 12) : 'Unknown');

// Product-image thumbnails: IO-gated direct URL loading (no base64, browser-cached 1h)
let _pimgObs = null;
function _pimgSet(img) {
  const pid = img.dataset.pid;
  if (!pid) return;
  const url = '/api/dashboard/product-image/' + pid;
  if (img.dataset.src === url) return;
  img.dataset.src = url;
  img.style.display = '';
  img.src = url;
}
function watchProductImages() {
  const imgs = document.querySelectorAll('#masterTable img[data-pimg]');
  const vh = window.innerHeight;
  imgs.forEach(img => {
    const pid = img.dataset.pid;
    if (!pid) return;
    const url = '/api/dashboard/product-image/' + pid;
    if (img.dataset.src !== url) {
      const r = img.getBoundingClientRect();
      if (r.top < vh + 400 && r.bottom > -400) _pimgSet(img);
    }
  });
  if (typeof IntersectionObserver === 'undefined') return;
  if (!_pimgObs) {
    _pimgObs = new IntersectionObserver(entries => {
      entries.forEach(en => { if (en.isIntersecting) _pimgSet(en.target); });
    }, { rootMargin: '400px' });
  }
  imgs.forEach(img => {
    const url = '/api/dashboard/product-image/' + (img.dataset.pid || '');
    if (img.dataset.src !== url) _pimgObs.observe(img);
  });
}
window.addEventListener('scroll', () => setTimeout(watchProductImages, 120), { passive: true });
document.addEventListener('alpine:init', () => {
  setTimeout(watchProductImages, 1500);
  if (typeof MutationObserver !== 'undefined') {
    new MutationObserver(() => {
      if (document.querySelector('#masterTable')) setTimeout(watchProductImages, 80);
    }).observe(document.body, { childList: true, subtree: true });
  }
});

document.addEventListener('alpine:init', () => {

Alpine.store('app', {
    section: 'dashboard',
    stockSubpage: 'receiving',
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
    _stBadge: 0,
    _shopBadge: 0,
    _kbBadge: 0,
    _restockBadge: 0,
    _suggBadge: 0,
    _promoQBadge: 0,
    groupOpen: {},
    groupParents: {
      'ai-chat': 'grp-ai', 'ai-kb': 'grp-ai',
      'health': 'grp-system', 'suspect1pc': 'grp-system',
      'rpt-sales': 'grp-reports', 'rpt-invcost': 'grp-reports', 'rpt-shifts': 'grp-reports', 'analytics': 'grp-reports', 'rpt-invval': 'grp-reports',
      'grp-reports': 'grp-pos', 'products': 'grp-pos', 'grp-inv': 'grp-pos',
      'online-orders': 'grp-ecom', 'shop-content': 'grp-ecom', 'msgr-bot': 'grp-ecom', 'promo-banners': 'grp-ecom', 'restock-requests': 'grp-ecom', 'product-suggestions': 'grp-ecom', 'promo-groups': 'grp-ecom', 'promo-free-queue': 'grp-ecom',
      'st-receiving': 'grp-inv', 'st-trail': 'grp-inv', 'st-transfer': 'grp-inv',
      'settings': 'grp-settings', 'pospromo': 'grp-settings', 'posqr': 'grp-settings', 'branding': 'grp-settings', 'google-auth': 'grp-settings'
    },
    toggleGroup(id) {
      this.groupOpen[id] = !this.groupOpen[id];
    },
    isGroupOpen(id) {
      return !!this.groupOpen[id];
    },
    isGroupActive(id) {
      if (id === 'grp-pos' || id === 'grp-reports' || id === 'grp-inv') {
        if (id === 'grp-reports') return ['rpt-sales', 'rpt-invcost', 'rpt-shifts', 'analytics', 'rpt-invval'].includes(this.section);
        if (id === 'grp-inv') return this.section === 'stock' || ['st-receiving', 'st-trail', 'st-transfer'].includes(this.section);
        return this.section === 'products' || this.section === 'stock' || this.section === 'rpt-sales' || this.section === 'rpt-invcost' || this.section === 'rpt-shifts' || this.section === 'analytics' || this.section === 'rpt-invval';
      }
      if (id === 'grp-inv' && this.section === 'stock') return true;
      const parent = this.groupParents[this.section];
      return parent ? (parent === id || this.groupParents[parent] === id) : false;
    },
    openAncestors(id) {
      let p = this.groupParents[id];
      while (p) {
        this.groupOpen[p] = true;
        p = this.groupParents[p];
      }
    },

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
      this.openAncestors(section);
      document.getElementById('sidebar')?.classList.remove('open');
      if (section === 'health') { window.open('health.html', '_blank'); return; }
      if (section === 'shop') { window.open('shop.html', '_blank'); return; }
      if (section.startsWith('st-')) {
        this.section = 'stock';
        this.stockSubpage = section.replace('st-', '');
        dispatchEvent(new CustomEvent('load-stock'));
        return;
      }
      this.section = section;
      if (section === 'customers') dispatchEvent(new CustomEvent('load-customers'));
      if (section === 'users') dispatchEvent(new CustomEvent('load-users'));
      if (section === 'products') dispatchEvent(new CustomEvent('load-products'));
      if (section === 'stock') { this.stockSubpage = 'receiving'; dispatchEvent(new CustomEvent('load-stock')); }
      if (section === 'analytics') dispatchEvent(new CustomEvent('load-analytics'));
      if (section === 'suspect1pc') dispatchEvent(new CustomEvent('load-suspect1pc'));
    },
    switchStockSubpage(subpage) {
      this.stockSubpage = subpage;
    },
    isActive(id) {
      if (id.startsWith('st-')) return this.section === 'stock' && this.stockSubpage === id.replace('st-', '');
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

  /* ΓöÇΓöÇ Stock Receiving ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('receivingPanel', () => ({
    d: [], loading: false, limit: 100,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()); window.addEventListener('load-stock', () => this.load()) },
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

  /* ── Store Stock Trail (POS client LOCAL DB via agent) ─────────────────────── */
  Alpine.data('storeTrailPanel', () => ({
    stores: [], store: '', cats: [], cat: '', products: [], prodSearch: '',
    trail: null, trailProduct: '', trailRows: [], loading: false, status: '',
    async init() {
      await this.refreshStores();
      window.addEventListener('refresh-data', () => this.refreshStores());
      window.addEventListener('load-stock', () => this.refreshStores());
    },
    async refreshStores() {
      try { this.stores = await fetchJSON(API + '/agent/status') } catch (e) { this.stores = [] }
    },
    esc(v) { return String(v == null ? '' : v).replace(/'/g, "''") },
    parseTSV(out) {
      const lines = (out || '').replace(/\r/g, '').split('\n').filter(l => l.trim() !== '');
      if (lines.length === 0) return [];
      const cols = lines[0].split('\t');
      return lines.slice(1).map(l => {
        const v = l.split('\t'); const o = {};
        cols.forEach((c, i) => o[c] = v[i] !== undefined ? v[i] : '');
        return o;
      });
    },
    async query(sql) {
      if (!this.store) return [];
      this.loading = true; this.status = 'Querying ' + this.store + '...';
      try {
        const r = await fetchJSON(API + '/agent/send/' + encodeURIComponent(this.store),
          { method: 'POST', body: JSON.stringify({ type: 'sql', payload: sql }), headers: { 'Content-Type': 'application/json' } });
        const cmdId = r.commandId;
        var tries = 0;
        while (tries < 20) {
          await new Promise(res => setTimeout(res, 2000));
          try {
            const list = await fetchJSON(API + '/agent/results/' + encodeURIComponent(this.store));
            const found = list.find(x => x.commandId === cmdId);
            if (found) {
              if (found.error) { this.status = 'ERROR: ' + found.error; return [] }
              const rows = this.parseTSV(found.output || '');
              this.status = 'OK — ' + rows.length + ' row(s) from local DB';
              return rows;
            }
          } catch (e) { }
          tries++;
          this.status = 'Waiting for store... (' + tries + '/20)';
        }
        this.status = 'Timeout — agent may be offline.';
      } catch (e) { this.status = 'Error: ' + e.message }
      finally { this.loading = false }
      return [];
    },
    async onStore() {
      this.cat = ''; this.products = []; this.trail = null; this.trailRows = []; this.cats = [];
      if (!this.store) return;
      const rows = await this.query("SELECT DISTINCT Category FROM Products WHERE IsActive=1 AND TRIM(Category)<>'' ORDER BY Category");
      this.cats = rows.map(r => r.Category).filter(Boolean);
    },
    async onCat() {
      this.products = []; this.trail = null; this.trailRows = [];
      if (!this.store) return;
      const sql = this.cat === '__ALL__' || !this.cat
        ? "SELECT Id, Name, Barcode, StockQty FROM Products WHERE IsActive=1 ORDER BY Name LIMIT 500"
        : "SELECT Id, Name, Barcode, StockQty FROM Products WHERE IsActive=1 AND Category='" + this.esc(this.cat) + "' ORDER BY Name LIMIT 500";
      this.products = await this.query(sql);
    },
    get filteredProducts() {
      if (!this.prodSearch) return this.products;
      const q = this.prodSearch.toLowerCase();
      return this.products.filter(p => (p.Name || '').toLowerCase().includes(q) || (p.Barcode || '').toLowerCase().includes(q));
    },
    async showTrail(p) {
      this.trail = p;
      this.trailProduct = p.Name;
      this.trailRows = await this.query("SELECT CreatedAt, QuantityAdded, StockBefore, StockAfter, Reference, UserName, InvoiceNo, Synced FROM StockTrail WHERE ProductId=" + this.esc(p.Id) + " ORDER BY Id DESC LIMIT 200");
    },
    backToList() { this.trail = null; this.trailRows = []; this.trailProduct = '' },
    backToCats() { this.products = []; this.prodSearch = ''; this.trail = null; this.trailRows = [] },
    trailType(r) {
      if (r.InvoiceNo) return 'Sale';
      if ((r.Reference || '').includes('void')) return 'Void/Return';
      const ref = (r.Reference || '').toUpperCase();
      if (ref.startsWith('RECV') || ref.startsWith('RR-') || ref.startsWith('WH-TRANSFER') || ref.startsWith('TRANSFER')) return 'Receiving';
      return '—';
    },
    typeCls(t) {
      if (t === 'Sale') return 'bg-cyan-100 dark:bg-cyan-900/20 text-cyan-700 dark:text-cyan-300';
      if (t === 'Void/Return') return 'bg-amber-100 dark:bg-amber-900/20 text-amber-700 dark:text-amber-300';
      if (t === 'Receiving') return 'bg-emerald-100 dark:bg-emerald-900/20 text-emerald-700 dark:text-emerald-300';
      return 'bg-gray-100 dark:bg-[#222255] text-gray-500 dark:text-[#7878aa]';
    },
    fmtTrailDate(v) {
      const d = new Date(String(v).replace(' ', 'T'));
      return isNaN(d) ? String(v) : d.toLocaleString('en-PH', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Manila' });
    },
    qtyCls(v) { const n = Number(v); return n > 0 ? 'text-emerald-500 font-semibold' : n < 0 ? 'text-red-500 font-semibold' : 'text-gray-400' },
    qtyTxt(v) { const n = Number(v); return (n > 0 ? '+' : '') + n },
    exportTrail() {
      if (!this.trailRows.length) return;
      const head = 'Date,Qty,Before,After,Type,Reference,Cashier,Invoice,Synced';
      const rows = this.trailRows.map(r => [r.CreatedAt, r.QuantityAdded, r.StockBefore, r.StockAfter, this.trailType(r), r.Reference, r.UserName, r.InvoiceNo, r.Synced]
        .map(v => '"' + String(v == null ? '' : v).replace(/"/g, '""') + '"').join(','));
      const blob = new Blob([head + '\n' + rows.join('\n')], { type: 'text/csv' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = 'stock-trail_' + this.store + '_' + (this.trailProduct || 'product').replace(/[^\w\- ]+/g, '').trim().replace(/\s+/g, '_') + '.csv';
      a.click();
      URL.revokeObjectURL(a.href);
    }
  }));

  /* ΓöÇΓöÇ Master Products ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('masterProducts', () => ({
    d: [], loading: true, search: '', catFilter: '', status: 'active', stockTotals: {}, stockTotalsByName: {},
    hqStockByBc: {}, hqStockByName: {},
    batchBusy: false, batchDone: 0, batchTotal: 0,
    async init() {
      window.addEventListener('load-products', () => this.load());
      if (Alpine.store('app').section === 'products') await this.load();
    },
    async load(force) {
      if (!force) {
        const c = Alpine.store('app').cache.master;
        if (c && Date.now() - c.t < 15000) { this.d = c.data; this.loading = false; this.loadTotals(); setTimeout(watchProductImages, 120); return }
      }
      this.loading = true;
      try {
        this.d = await fetchJSON(API + '/products/master?noImages=true');
        Alpine.store('app').cache.master = { data: this.d, t: Date.now() };
      } catch (e) { this.d = [] }
      this.loading = false;
      this.loadTotals();
      setTimeout(watchProductImages, 150);
    },
    async loadTotals() {
      const c = Alpine.store('app').cache.stockTotals;
      if (c && Date.now() - c.t < 30000) { this.stockTotals = c.data; this.stockTotalsByName = c.byName; this.hqStockByBc = c.hq || {}; this.hqStockByName = c.hqByName || {}; return }
      try {
        const all = await fetchJSON(API + '/stock-status');
        const byBc = {}, byName = {}, seen = {}, hqByBc = {}, hqByName = {};
        all.forEach(x => {
          const key = (x.barcode || '') + '|' + x.storeId;
          if (seen[key]) return;
          seen[key] = true;
          const q = Number(x.stockQty) || 0;
          if (x.barcode) byBc[x.barcode] = (byBc[x.barcode] || 0) + q;
          else byName[x.name] = (byName[x.name] || 0) + q;
          if (x.storeId === 'STORE-20260602-7159') {
            if (x.barcode) hqByBc[x.barcode] = q;
            else hqByName[x.name] = q;
          }
        });
        this.stockTotals = byBc;
        this.stockTotalsByName = byName;
        this.hqStockByBc = hqByBc;
        this.hqStockByName = hqByName;
        Alpine.store('app').cache.stockTotals = { data: byBc, byName: byName, hq: hqByBc, hqByName: hqByName, t: Date.now() };
      } catch (e) { /* keep previous totals */ }
    },
    totalStock(p) { if (!p) return 0; if (p.stockParentId) { const ps = p.stockParentBarcode ? (this.stockTotals[p.stockParentBarcode] || 0) : 0; return Math.floor(ps / (p.linkRatio || 1)); } if (p.barcode) return this.stockTotals[p.barcode] || 0; return this.stockTotalsByName[p.name] || 0 },
    hqStock(p) { if (!p) return 0; if (p.stockParentId) { const ps = p.stockParentBarcode ? (this.hqStockByBc[p.stockParentBarcode] || 0) : 0; return Math.floor(ps / (p.linkRatio || 1)); } if (p.barcode) return this.hqStockByBc[p.barcode] || 0; return this.hqStockByName[p.name] || 0 },
    stockCls(q) { return q === 0 ? 'text-red-500' : q < 10 ? 'text-amber-500' : 'text-emerald-500' },
    get categories() { const c = []; this.d.forEach(x => { if (x.category && !c.includes(x.category)) c.push(x.category) }); return c.sort() },
    get inactiveCount() { return this.d.filter(x => x.isActive === false).length },
    get filtered() {
      let items = this.d;
      if (this.status === 'active') items = items.filter(x => x.isActive !== false);
      else if (this.status === 'inactive') items = items.filter(x => x.isActive === false);
      if (this.search) { const q = this.search.toLowerCase(); items = items.filter(x => (x.name || '').toLowerCase().includes(q) || (x.barcode || '').toLowerCase().includes(q) || (x.category || '').toLowerCase().includes(q)) }
      if (this.catFilter) items = items.filter(x => x.category === this.catFilter);
      return items;
    },
    margin(p) { return p.price > 0 ? ((p.price - p.cost) / p.price * 100).toFixed(1) : '0.0' },
    marginClass(m) { const v = parseFloat(m); return v > 20 ? 'text-emerald-400' : v > 0 ? 'text-amber-400' : 'text-red-400' },
    async openEditor(id) {
      Alpine.store('app').editingId = id || null;
      if (id) {
        const local = this.d.find(x => x.id === id);
        try {
          // Hintayin ang fetch bago buksan ang editor. Dati, ang local data (units: []) ay
          // inilalagay MUNA -> kung nagbukas ang form bago matapos ang fetch, walang units ang
          // form -> ang SAVE ay magse-save ng walang units -> UNIT PROTECTION ay nag-iingat ng
          // lumang units -> mukhang "auto-balik" ang mga deleted units (Mismo incident 2026-09-02).
          const r = await fetchJSON(API + '/products/master/' + id);
          Alpine.store('app').editingProductData = { ...r.product, units: r.units || [] };
        } catch (e) { Alpine.store('app').editingProductData = local ? { ...local, units: [] } : null; }
      } else {
        Alpine.store('app').editingProductData = null;
      }
      Alpine.store('app').editorOpen = true;
    },
    closeEditor() { Alpine.store('app').editorOpen = false; Alpine.store('app').editingId = null; Alpine.store('app').editingProductData = null },
    openStock(x) {
      Alpine.store('app').stockProduct = x;
      Alpine.store('app').stockOpen = true;
      dispatchEvent(new CustomEvent('stock-dialog-open'));
    },
    // BATCH IMAGE UPLOAD - file names = barcode (e.g. 4800092112782.jpg), auto-matched to products
    async batchUpload(e) {
      const files = Array.from(e.target.files || []);
      if (!files.length) return;
      this.batchBusy = true; this.batchDone = 0; this.batchTotal = files.length;
      const items = [];
      for (const f of files) {
        const bc = (f.name || '').replace(/\.[^.]+$/, '').trim();
        const dataUrl = await new Promise(res => { const r = new FileReader(); r.onload = () => res(r.result); r.onerror = () => res(''); r.readAsDataURL(f); });
        if (bc.length >= 3 && dataUrl.length > 100) items.push({ barcode: bc, imageData: dataUrl });
      }
      let updated = 0;
      const notFound = [];
      const CHUNK = 20000000;
      for (let i = 0; i < items.length;) {
        let size = 0, j = i;
        for (; j < items.length && size + items[j].imageData.length < CHUNK; j++) size += items[j].imageData.length;
        const chunk = items.slice(i, j);
        try {
          const r = await fetch(API + '/products/master/images/batch', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ items: chunk }) });
          const jr = await r.json();
          if (!r.ok) throw new Error(jr.error || 'HTTP ' + r.status);
          updated += (jr.updated || 0);
          (jr.notFound || []).forEach(b => notFound.push(b));
        } catch (err) { toast('Batch failed: ' + err.message, 'error'); break; }
        this.batchDone = Math.min(items.length, j);
        i = j;
      }
      this.batchBusy = false;
      Alpine.store('app').cache.master = null;
      this.load(true);
      toast('Batch: ' + updated + ' na-update' + (notFound.length ? ' · ' + notFound.length + ' hindi nahanap (i-check ang pangalan ng file = barcode)' : ' · lahat OK!'), notFound.length ? 'error' : 'success');
    },

    async deleteProduct(id) {      const p = this.d.find(x => x.id === id); if (!p) return;
      if (!confirm('Delete "' + p.name + '"?')) return;
      try {
        const r = await fetch(API + '/products/master/' + id, { method: 'DELETE' });
        if (!r.ok) throw new Error('Failed');
        toast('Product deleted', 'success');
        Alpine.store('app').cache.master = null;
        this.load(true);
      } catch (e) { toast('Delete failed: ' + e.message, 'error') }
    },
    get editingProduct() { const id = Alpine.store('app').editingId; return id ? this.d.find(x => x.id === id) : null },
    async toggleFlag(x, field, val) {      const prev = x[field];
      x[field] = val;
      try {
        const body = {};
        body[field] = val;
        const r = await fetch(API + '/products/master/' + x.id + '/flags', { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (!r.ok) throw new Error('Failed');
        toast((val ? 'ON' : 'OFF') + ' — ' + (x.name || '') + (field === 'sellOnline' ? ' (online shop)' : field === 'isActive' ? ' (active)' : ' (points exempt)'), 'success');
        Alpine.store('app').cache.master = null;
        dispatchEvent(new CustomEvent('load-products'));
      } catch (e) { x[field] = prev; toast('Update failed: ' + e.message, 'error') }
    }
  }));

  /* ΓöÇΓöÇ Product Stock Dialog (server-only: PG stock pushed by each POS) ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('productStockDialog', () => ({
    open: false, product: null, rows: [], status: '', loading: false, _autoTimer: null,
    async init() {
      window.addEventListener('stock-dialog-open', () => this.load());
      window.addEventListener('refresh-data', () => { if (this.open) this.load() });
    },
    async load() {
      const p = Alpine.store('app').stockProduct;
      if (!p) return;
      this.product = p;
      this.open = true;
      this.loading = true;
      this.rows = [];
      await this.refresh();
      this.loading = false;
      if (this._autoTimer) clearInterval(this._autoTimer);
      this._autoTimer = setInterval(() => this.autoRefresh(), 20000);
    },
    async autoRefresh() {
      if (!this.open || this.loading || !this.product) return;
      await this.refresh();
    },
    async refresh() {
      try {
        const all = await fetchJSON(API + '/stock-status');
        const bc = this.product.barcode || '';
        const seen = {};
        this.rows = all.filter(x => x.barcode === bc && !seen[x.storeId] && (seen[x.storeId] = true));
        const order = ['STORE-20260602-7159', 'STORE-20260602-AA36', 'STORE-20260626-A80C', 'STORE-20260622-E174', 'STORE-DEV-0001'];
        this.rows.sort((a, b) => {
          const ia = order.indexOf(a.storeId), ib = order.indexOf(b.storeId);
          return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
        });
        this.status = 'Refreshed ' + new Date().toLocaleTimeString('en-PH') + ' · auto-refresh 20s';
      } catch (e) { this.status = 'Error: ' + e.message }
    },
    storeLabel(sid) {
      const m = Alpine.store('app').storeMap || {};
      if (sid === 'STORE-20260602-7159') return 'Main — HQ';
      return m[sid] || sid;
    },
    isMain(sid) { return sid === 'STORE-20260602-7159' },
    qtyCls(q) { return q === 0 ? 'text-red-500' : q < 10 ? 'text-amber-500' : 'text-emerald-500' },
    close() {
      this.open = false;
      if (this._autoTimer) { clearInterval(this._autoTimer); this._autoTimer = null }
      Alpine.store('app').stockOpen = false;
      Alpine.store('app').stockProduct = null
    },
    fmtN(v) { const n = Number(v); return isNaN(n) ? '0.00' : n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }
  }));

  /* ΓöÇΓöÇ Product Editor ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('productEditor', () => ({
    name: '', barcode: '', category: '', price: 0, cost: 0, imageData: '',
    removeImage: false,
    pointsExempt: false, pointsPerUnit: 0, isActive: true, sellOnline: true,
    units: [], productId: null, categories: [],
    linkParentId: 0, linkRatio: 1, linkParentName: '', linkQ: '', linkOpen: false, linkProducts: [],
    async init() {
      this.$watch('$store.app.section', () => { if (this.$store.app.section !== 'products') this.reset() });
      this.$watch('$store.app.editorOpen', (v) => { if (v) this.open(Alpine.store('app').editingId) });
      try { this.categories = await fetchJSON(API + '/products/categories') } catch (e) {}
    },
    open(id) {
      this.productId = id || null;
      this.removeImage = false;
      const p = Alpine.store('app').editingProductData;
      if (id && p) {
        this.name = p.name; this.barcode = p.barcode || ''; this.category = p.category || ''; this.price = p.price; this.cost = p.cost; this.imageData = p.imageData || ''; this.pointsExempt = p.pointsExempt || false; this.pointsPerUnit = p.pointsPerUnit || 0; this.isActive = p.isActive !== false; this.sellOnline = p.sellOnline !== false; this.units = (p.units || []).map(u => ({ ...u }));
        this.linkParentId = p.stockParentId || 0; this.linkRatio = p.linkRatio || 1; this.linkParentName = p.stockParentName || ''; this.linkQ = p.stockParentName || '';
      }
      else { this.name = ''; this.barcode = ''; this.category = ''; this.price = 0; this.cost = 0; this.imageData = ''; this.pointsExempt = false; this.pointsPerUnit = 0; this.isActive = true; this.sellOnline = true; this.units = []; this.linkParentId = 0; this.linkRatio = 1; this.linkParentName = ''; this.linkQ = ''; }
    },
    reset() { this.productId = null; this.name = ''; this.barcode = ''; this.category = ''; this.price = 0; this.cost = 0; this.imageData = ''; this.removeImage = false; this.pointsExempt = false; this.pointsPerUnit = 0; this.isActive = true; this.sellOnline = true; this.units = []; this.linkParentId = 0; this.linkRatio = 1; this.linkParentName = ''; this.linkQ = ''; this.linkOpen = false; Alpine.store('app').editorOpen = false; Alpine.store('app').editingId = null; Alpine.store('app').editingProductData = null },
    async loadLinkProducts() {
      if (this.linkProducts.length) return;
      try {
        const cached = Alpine.store('app').cache.master;
        if (cached && cached.data && cached.data.length) { this.linkProducts = cached.data; return; }
        this.linkProducts = await fetchJSON(API + '/products/master?noImages=true');
      } catch (e) { this.linkProducts = []; }
    },
    get linkResults() {
      const q = (this.linkQ || '').toLowerCase().trim();
      let list = this.linkProducts.filter(p => p.isActive !== false && p.id !== this.productId);
      if (q) list = list.filter(p => (p.name || '').toLowerCase().includes(q) || (p.barcode || '').includes(q));
      return list.slice(0, 20);
    },
    linkPickFirst() { const r = this.linkResults; if (r.length) this.linkSelect(r[0]); else this.linkOpen = false; },
    linkSelect(p) { this.linkParentId = p.id; this.linkParentName = p.name; this.linkQ = p.name; this.linkOpen = false; },
    linkApply() {
      if (!this.linkParentId) { toast('Pumili muna ng parent product sa search', 'error'); return; }
      const r = Math.floor(Number(this.linkRatio) || 1);
      if (r < 1) { toast('Link ratio ay dapat 1 o mas mataas', 'error'); return; }
      this.linkRatio = r;
      toast('🔗 Link: 1 = ' + r + ' ' + this.linkParentName + ' — i-SAVE para i-save', 'success');
    },
    linkUnlink() {
      this.linkParentId = 0; this.linkParentName = ''; this.linkQ = '';
      toast('Link inalis — i-SAVE para i-save', 'success');
    },
    addCategory() {
      var name = prompt('Enter new category name:');
      if (!name || !name.trim()) return;
      name = name.trim();
      var lower = name.toLowerCase();
      if (this.categories.some(function(c) { return c.toLowerCase() === lower }))
        { alert('Category "' + name + '" already exists'); return }
      this.categories.push(name);
      this.category = name;
    },
    addUnit() { this.units.push({ unitName: '', price: 0, qtyPerUnit: 1, isDefault: false, pointsPerUnit: 0 }) },
    removeUnit(i) { this.units.splice(i, 1) },
    get profit() { return (parseFloat(this.price) || 0) - (parseFloat(this.cost) || 0) },
    get profitPct() {
      const p = parseFloat(this.price) || 0;
      if (p <= 0) return 0;
      return ((p - (parseFloat(this.cost) || 0)) / p) * 100;
    },
    async save() {
      if (!this.name) { toast('Name required', 'error'); return }
      const data = {
        name: this.name, barcode: this.barcode, category: this.category,
        price: parseFloat(this.price) || 0, cost: parseFloat(this.cost) || 0,
        imageData: this.imageData, removeImage: this.removeImage, isActive: this.isActive,
        sellOnline: this.sellOnline,
        pointsExempt: this.pointsExempt, pointsPerUnit: parseInt(this.pointsPerUnit) || 0,
        stockParentId: this.linkParentId || 0, linkRatio: Math.floor(Number(this.linkRatio) || 1),
        units: this.units.filter(u => u.unitName).map(u => ({ ...u, cost: (u.qtyPerUnit || 1) * (parseFloat(this.cost) || 0), pointsPerUnit: parseInt(u.pointsPerUnit) || 0 })),
        clearUnits: this.productId && !this.units.filter(u => u.unitName).length ? true : undefined
      };
      try {
        const api = API + '/products/master';
        const r = this.productId
          ? await fetch(api + '/' + this.productId, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) })
          : await fetch(api, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        if (!r.ok) { const j = await r.json(); throw new Error(j.error || 'Failed') }
        toast(this.productId ? 'Product updated' : 'Product created', 'success');
        this.reset();
        Alpine.store('app').cache.master = null;
        dispatchEvent(new CustomEvent('load-products'));
      } catch (e) { toast(e.message, 'error') }
    },
    previewImage(e) {
      const file = e.target.files[0]; if (!file) return;
      this.removeImage = false;
      const reader = new FileReader();
      reader.onload = (ev) => { this.imageData = ev.target.result };
      reader.readAsDataURL(file);
    },
    clearImage() {
      this.imageData = '';
      this.removeImage = true;
    }
  }));


  // ════════════════════════════════════════════════════
  // STORE TRANSFER panel (HQ → POS clients) — same UI as
  // the warehouse transfer panel, source = HQ stock
  // ════════════════════════════════════════════════════
  Alpine.data('storeTransferPanel', () => ({
    transfers: [], loading: true,
    transferPage: 1, transferPageSize: 30, transferTotal: 0, transferFilterDate: '', transferFilterSearch: '',
    stTransferModal: false, stTransferSaving: false, stTransferForm: { clientId: '', clientName: '', notes: '', storeId: '' }, stTransferFormItems: [],
    stTransferViewOpen: false, stTransferViewId: null, stTransferViewItems: [],
    async init() {
      window.addEventListener('load-stock', () => this.load());
      if (Alpine.store('app').section === 'stock' && Alpine.store('app').stockSubpage === 'transfer') await this.load();
      this.loadBadge();
      setInterval(() => { if (Alpine.store('app').section === 'stock' && Alpine.store('app').stockSubpage === 'transfer') this.load(); this.loadBadge() }, 30000);
    },
    async load() {
      this.loading = true;
      try {
        const q = [];
        if (this.transferFilterDate) q.push('date=' + this.transferFilterDate);
        if (this.transferFilterSearch) q.push('search=' + encodeURIComponent(this.transferFilterSearch));
        q.push('source=hq');
        q.push('page=' + this.transferPage);
        q.push('pageSize=' + this.transferPageSize);
        const d = await fetchJSON(API + '/warehouse/transfers?' + q.join('&'));
        this.transfers = d.items || [];
        this.transferTotal = d.total || 0;
      } catch (e) { this.transfers = []; this.transferTotal = 0 }
      this.loading = false;
    },
    applyTransferFilter() { this.transferPage = 1; this.load() },
    clearTransferFilter() { this.transferFilterDate = ''; this.transferFilterSearch = ''; this.transferPage = 1; this.load() },
    prevTransferPage() { if (this.transferPage > 1) { this.transferPage--; this.load() } },
    nextTransferPage() { if (this.transferPage < this.transferTotalPages) { this.transferPage++; this.load() } },
    gotoTransferPage(p) { this.transferPage = p; this.load() },
    get transferTotalPages() { return Math.max(1, Math.ceil(this.transferTotal / this.transferPageSize)) },
    get transferPageStart() { return this.transfers.length === 0 ? 0 : (this.transferPage - 1) * this.transferPageSize + 1 },
    get transferPageEnd() { return (this.transferPage - 1) * this.transferPageSize + this.transfers.length },
    get transferPageNumbers() {
      const total = this.transferTotalPages, cur = this.transferPage, out = [];
      if (total <= 7) { for (let i = 1; i <= total; i++) out.push(i); return out }
      out.push(1);
      if (cur > 3) out.push('...');
      for (let i = Math.max(2, cur - 1); i <= Math.min(total - 1, cur + 1); i++) out.push(i);
      if (cur < total - 2) out.push('...');
      out.push(total);
      return out;
    },
    get filtered() {
      if (this.transferFilterSearch) {
        const q = this.transferFilterSearch.toLowerCase();
        return this.transfers.filter(x => String(x.id).includes(q) || (x.clientName || '').toLowerCase().includes(q) || (x.notes || '').toLowerCase().includes(q));
      }
      return this.transfers;
    },
    openNewTransfer() { this.stTransferModal = true; this.stTransferForm = { clientId: '', clientName: '', notes: '', storeId: '' }; this.stTransferFormItems = [] },
    closeTransfer() { this.stTransferModal = false; this.stTransferFormItems = [] },
    addTransferItem(pid, pname, barcode, qty) { this.stTransferFormItems.push({ productId: pid, productName: pname, barcode: barcode || '', qty: parseInt(qty) || 1 }) },
    removeTransferItem(i) { this.stTransferFormItems.splice(i, 1) },
    get transferTotalQty() { return this.stTransferFormItems.reduce((s, x) => s + x.qty, 0) },
    async saveTransfer() {
      if (this.stTransferSaving) return;
      if (!this.stTransferForm.clientId) { toast('Select a POS client', 'error'); return }
      if (!this.stTransferFormItems.length) { toast('Add at least one product', 'error'); return }
      this.stTransferSaving = true;
      try {
        const r = await fetch(API + '/warehouse/transfers', {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            clientId: parseInt(this.stTransferForm.clientId),
            clientName: this.stTransferForm.clientName,
            notes: this.stTransferForm.notes,
            storeId: this.stTransferForm.storeId,
            source: 'hq',
            items: this.stTransferFormItems.map(x => ({ productId: x.productId, productName: x.productName, barcode: x.barcode, qty: x.qty }))
          })
        });
        const j = await r.json();
        if (!r.ok) throw new Error(j.error || 'Failed');
        toast('Transfer #' + j.id + ' created', 'success');
        this.stTransferModal = false;
        this.load(); this.loadBadge();
      } catch (e) { toast('Error: ' + e.message, 'error') }
      finally { this.stTransferSaving = false; }
    },
    async viewTransfer(id) {
      try {
        const items = await fetchJSON(API + '/warehouse/transfers/' + id + '/items');
        if (!items || !items.length) { toast('No items', 'info'); return }
        this.stTransferViewItems = items;
        this.stTransferViewId = id;
        this.stTransferViewOpen = true;
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    closeTransferView() { this.stTransferViewOpen = false; this.stTransferViewItems = [] },
    async cancelTransfer(id) {
      if (!confirm('Cancel transfer #' + id + '?')) return;
      try {
        const r = await fetch(API + '/warehouse/transfers/' + id + '/cancel', { method: 'PUT' });
        if (!r.ok) { const j = await r.json(); throw new Error(j.error || 'Failed') }
        toast('Transfer cancelled', 'success');
        this.load(); this.loadBadge();
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    async loadBadge() {
      try {
        const d = await fetchJSON(API + '/warehouse/transfers/pending-count?source=hq');
        Alpine.store('app')._stBadge = d ? d.pending || 0 : 0;
      } catch (e) { Alpine.store('app')._stBadge = 0 }
    }
  }));




  /* ΓöÇΓöÇ Customers ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('customersList', () => ({
    d: [], loading: true, orders: [], ordersOpen: false, ordersName: '', ordersLoading: false, ptsFilter: 'all',
    phoneOpen: false, phoneTarget: null, phoneInput: '', phoneSaving: false,
    upOpen: false, upTarget: null, upInput: '', upList: [], upSaving: false,
    async init() { window.addEventListener('load-customers', () => this.load()); await this.load() },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/customers?' + Alpine.store('app').storeParam.replace('&', '')) } catch (e) { this.d = [] }
      this.loading = false;
    },
    setPtsFilter(f) { this.ptsFilter = f; },
    get withStar() { return this.d.filter(x => !!x.qrCode).length },
    get withoutStar() { return this.d.filter(x => !x.qrCode).length },
    get noPhone() { return this.d.filter(x => !!x.googleSub && !x.phone).length },
    get filtered() {
      if (this.ptsFilter === 'star') return this.d.filter(x => !!x.qrCode);
      if (this.ptsFilter === 'nostar') return this.d.filter(x => !x.qrCode);
      if (this.ptsFilter === 'nophone') return this.d.filter(x => !!x.googleSub && !x.phone);
      return this.d;
    },
    addrText(x) {
      const parts = [];
      if (x.addrBlock) parts.push('Blk ' + x.addrBlock);
      if (x.addrLot) parts.push('Lot ' + x.addrLot);
      if (x.addrSubdivision) parts.push(x.addrSubdivision);
      if (x.addrDetails) parts.push(x.addrDetails);
      return parts.join(', ');
    },
    openPhone(x) { this.phoneTarget = x; this.phoneInput = x.phone || ''; this.phoneOpen = true; },
    closePhone() { this.phoneOpen = false; this.phoneTarget = null; },
    async savePhone() {
      const p = (this.phoneInput || '').trim();
      if (p.length < 10) { toast('Ilagay ang buong mobile number (09xx...)', 'error'); return; }
      if (!this.phoneTarget) return;
      this.phoneSaving = true;
      try {
        await fetchJSON(API + '/customers/' + this.phoneTarget.id + '/phone', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ phone: p }) });
        toast('Na-save ang phone ni ' + (this.phoneTarget.name || '') + ': ' + p, 'ok');
        this.phoneOpen = false; this.phoneTarget = null;
        await this.load();
      } catch (e) { toast(e.message || 'Hindi na-save', 'error'); }
      this.phoneSaving = false;
    },
    async viewOrders(x) {
      this.ordersName = x.name || '';
      this.orders = []; this.ordersOpen = true; this.ordersLoading = true;
      try { this.orders = await fetchJSON(API + '/customers/' + x.id + '/orders') } catch (e) { this.orders = [] }
      this.ordersLoading = false;
    },
    closeOrders() { this.ordersOpen = false; },
    // 📢 UPDATE sa account ng customer (makikita sa bell ng shop app)
    async openUpdates(x) {
      this.upTarget = x; this.upInput = ''; this.upList = []; this.upOpen = true;
      try { this.upList = await fetchJSON(API + '/customers/' + x.id + '/updates') } catch (e) { this.upList = [] }
    },
    closeUpdateModal() { this.upOpen = false; this.upTarget = null; },
    async saveUpdate() {
      const m = (this.upInput || '').trim();
      if (m.length < 3) { toast('Ilagay ang update message', 'error'); return; }
      if (!this.upTarget) return;
      this.upSaving = true;
      try {
        await fetchJSON(API + '/customers/' + this.upTarget.id + '/updates', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ message: m }) });
        toast('Na-post ang update kay ' + (this.upTarget.name || ''), 'ok');
        this.upInput = '';
        this.upList = await fetchJSON(API + '/customers/' + this.upTarget.id + '/updates');
      } catch (e) { toast(e.message || 'Hindi na-post', 'error'); }
      this.upSaving = false;
    },
    statusCls(s) {
      const m = { pending: 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300', confirmed: 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300', shipped: 'bg-violet-100 dark:bg-violet-900/30 text-violet-700 dark:text-violet-300', arrived: 'bg-cyan-100 dark:bg-cyan-900/30 text-cyan-700 dark:text-cyan-300', delivered: 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400', cancelled: 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300' };
      return m[s] || 'bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-300';
    }
  }));

  /* ΓöÇΓöÇ Users ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('usersList', () => ({
    d: [], loading: true, modalOpen: false, modalTitle: '', editingId: null, form: {},

    async init() { window.addEventListener('load-users', () => this.load()); await this.load() },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/users?' + Alpine.store('app').storeParam.replace('&', '')) } catch (e) { this.d = [] }
      this.loading = false;
    },

    openAdd() {
      this.editingId = null;
      this.form = { username: '', fullName: '', role: 'Cashier', passwordHash: '12345', storeIds: [], isActive: true, mobileAccess: false, webAccess: false };
      this.modalTitle = 'NEW USER';
      this.modalOpen = true;
    },

    openEdit(x) {
      this.editingId = x.posId;
      this.form = {
        username: x.username || '',
        fullName: x.fullName || '',
        role: x.role || 'Cashier',
        passwordHash: '',
        storeIds: (x.storeIds || []).slice(),
        isActive: x.isActive !== false,
        mobileAccess: !!x.mobileAccess,
        webAccess: !!x.webAccess
      };
      this.modalTitle = 'EDIT: ' + x.username;
      this.modalOpen = true;
    },

    closeModal() { this.modalOpen = false; this.editingId = null },

    toggleStore(sid) {
      const idx = this.form.storeIds.indexOf(sid);
      if (idx > -1) this.form.storeIds.splice(idx, 1);
      else this.form.storeIds.push(sid);
    },

    async save() {
      if (!this.form.username) { toast('Username is required', 'error'); return }
      if (!this.form.storeIds || !this.form.storeIds.length) { toast('Select at least one store', 'error'); return }
      try {
        const method = this.editingId ? 'PUT' : 'POST';
        const url = this.editingId ? API + '/users/' + this.editingId : API + '/users';
        const body = {
          username: this.form.username,
          fullName: this.form.fullName,
          role: this.form.role,
          storeIds: this.form.storeIds,
          isActive: this.form.isActive,
          mobileAccess: !!this.form.mobileAccess,
          webAccess: !!this.form.webAccess
        };
        if (this.form.passwordHash) body.passwordHash = this.form.passwordHash;
        const r = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (!r.ok) { const j = await r.json(); throw new Error(j.error || 'Failed') }
        toast((this.editingId ? 'Updated' : 'Created') + ' successfully', 'success');
        this.modalOpen = false;
        this.load();
      } catch (e) { toast('Save failed: ' + e.message, 'error') }
    },

    async deleteUser(x) {
      if (!confirm('Deactivate user "' + x.username + '"?')) return;
      try {
        await fetch(API + '/users/' + x.posId, { method: 'DELETE' });
        toast('User deactivated', 'success');
        this.load();
      } catch (e) { toast('Delete failed: ' + e.message, 'error') }
    },

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

  Alpine.data('storeSettings', () => ({
    pointsRate: 200, loading: true, saving: false, saved: false,
    async init() {
      try {
        const r = await fetchJSON(API + '/settings/' + (Alpine.store('app').storeId || 'STORE-DEV-0001') + '/PointsRate');
        this.pointsRate = parseInt(r.value) || 200;
      } catch (e) { this.pointsRate = 200 }
      this.loading = false;
    },
    async save() {
      this.saving = true; this.saved = false;
      try {
        await fetchJSON(API + '/settings/' + (Alpine.store('app').storeId || 'STORE-DEV-0001') + '/PointsRate', { method: 'PUT', body: JSON.stringify({ value: String(this.pointsRate) }), headers: { 'Content-Type': 'application/json' } });
        this.saved = true;
        setTimeout(() => this.saved = false, 2000);
      } catch (e) { toast('Failed to save: ' + e.message, 'error') }
      this.saving = false;
    }
  }));

  /* ΓöÇΓöÇ Missing End Shifts ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ */
  Alpine.data('missingShifts', () => ({
    stores: [], loading: true,
    async init() { await this.load(); window.addEventListener('refresh-data', () => this.load()) },
    async load() {
      this.loading = true;
      try {
        this.stores = await fetchJSON(API + '/missing-shifts');
      } catch (e) { this.stores = [] }
      this.loading = false;
    },
    get missing() { return this.stores.filter(x => x.missing) },
    get missingWithSales() { return this.missing.filter(x => x.todaySaleCount > 0) },
    get allGood() { return this.missing.length === 0 }
  }));

  // Inventory Cost Report
  Alpine.data('inventoryCostReport', () => ({
    d: [], loading: true,
    async init() { await this.load() },
    async load() {
      this.loading = true;
      try {
        const raw = await fetchJSON(API + '/shift-history?days=365');
        // Sort ascending by date for proper expected calculation
        const sorted = [...raw].sort((a, b) => new Date(a.closeDate) - new Date(b.closeDate));
        var prevInvByStore = {};
        var prevDateByStore = {};
        for (var i = 0; i < sorted.length; i++) {
          var x = sorted[i];
          var ic = x.totalInventoryCost || 0;
          var cs = x.totalCostSold || 0;
          var sr = x.totalStockReceivedCost || 0;
          var prevInvCost = prevInvByStore[x.storeId] || 0;
          x.prevInvCost = prevInvCost;
          x.expected = prevInvCost + sr - cs;
          x.variance = ic - x.expected;
          var prevDate = prevDateByStore[x.storeId];
          x.gapDays = prevDate ? Math.round((new Date(x.closeDate) - prevDate) / 86400000) : 0;
          prevInvByStore[x.storeId] = ic;
          prevDateByStore[x.storeId] = new Date(x.closeDate);
        }
        // Sort back to descending for display
        this.d = sorted.sort((a, b) => new Date(b.closeDate) - new Date(a.closeDate));
      } catch (e) { this.d = [] }
      this.loading = false;
    }
  }));

  // INVENTORY VALUE panel (current stock value per store + grand total, POS stores only)
  Alpine.data('inventoryValuePanel', () => ({
    stores: [], total: null, asOf: '', loading: true, _timer: null,
    async init() {
      if (Alpine.store('app').section === 'rpt-invval') { await this.load(); this._startTimer() }
      this.$watch('$store.app.section', v => { if (v === 'rpt-invval') { this.load(); this._startTimer() } });
      window.addEventListener('refresh-data', () => { if (Alpine.store('app').section === 'rpt-invval') this.load() });
    },
    _startTimer() {
      if (this._timer) clearInterval(this._timer);
      this._timer = setInterval(() => this.load(), 60000);
    },
    async load() {
      this.loading = true;
      try {
        const r = await fetchJSON(API + '/inventory-value');
        this.stores = r.stores || [];
        this.total = r.total || null;
        this.asOf = r.asOf ? new Date(r.asOf).toLocaleString('en-PH', { timeZone: 'Asia/Manila' }) : '';
      } catch (e) { this.stores = []; this.total = null }
      this.loading = false;
    },
    storeLabel(sid) { const m = Alpine.store('app').storeMap || {}; return m[sid] || sid },
    hasZero(x) { return (x.zeroCostItems || 0) > 0 }
  }));

  // RECEIPT AUDIT panel (anti-theft)
  Alpine.data('receiptAuditPanel', () => ({
    d: [], loading: true, detailsOpen: {},
    async init() {
      window.addEventListener('refresh-data', () => this.load());
      window.addEventListener('load-receipt-audit', () => this.load());
      if (Alpine.store('app').section === 'rpt-invcost') await this.load();
      this.$watch('$store.app.section', v => { if (v === 'rpt-invcost') this.load(); });
    },
    get alertsCount() { return this.d.filter(x => x.deletedCount > 0).length },
    safeParse(s) { try { const a = JSON.parse(s || '[]'); return Array.isArray(a) ? a : [] } catch (e) { return [] } },
    toggleDetails(i) { this.detailsOpen[i] = !this.detailsOpen[i] },
    async load() {
      this.loading = true;
      try { this.d = await fetchJSON(API + '/receipt-audit?limit=100'); } catch (e) { this.d = [] }
      this.loading = false;
    }
  }));

  // POS PROMO panel
  Alpine.data('posPromoPanel', () => ({
    promoMessage: '',
    saved: '',
    async init() {
      await this.load();
    },
    async load() {
      try {
        const res = await fetchJSON(API + '/pos-promo');
        this.promoMessage = res.message || '';
      } catch (e) { this.promoMessage = '' }
    },
    async save() {
      this.saved = '';
      try {
        await fetchJSON(API + '/pos-promo', { method: 'POST', body: JSON.stringify({ message: this.promoMessage }), headers: { 'Content-Type': 'application/json' } });
        this.saved = 'Promo message saved!';
        setTimeout(() => this.saved = '', 3000);
      } catch (e) { this.saved = 'Error saving promo message.' }
    }
  }));

  // Mobile app branding (splash/login colors, logo, title, launcher icon)
  Alpine.data('brandingPanel', () => ({
    appTitle: '',
    logoUrl: '',
    splashBg: '#10102a',
    primaryColor: '#06b6d4',
    iconKey: '',
    saved: '',
    async init() {
      if (Alpine.store('app').section === 'branding') await this.load();
      this.$watch('$store.app.section', v => { if (v === 'branding') this.load(); });
    },
    async load() {
      try {
        const res = await fetchJSON(API + '/branding');
        this.appTitle = res.appTitle || '';
        this.logoUrl = res.logoUrl || '';
        this.splashBg = res.splashBg || '#10102a';
        this.primaryColor = res.primaryColor || '#06b6d4';
        this.iconKey = res.iconKey || '';
      } catch (e) { }
    },
    async handleLogoUpload(evt) {
      const f = evt.target.files[0];
      if (!f) return;
      const fd = new FormData();
      fd.append('file', f);
      try {
        const res = await fetchJSON(API + '/branding/logo', { method: 'POST', body: fd });
        this.logoUrl = res.url || '';
        this.saved = 'Logo uploaded! Click SAVE BRANDING to commit.';
        setTimeout(() => this.saved = '', 4000);
      } catch (e) { this.saved = 'Logo upload failed: ' + e.message }
    },
    async save() {
      this.saved = '';
      try {
        await fetchJSON(API + '/branding', { method: 'POST', body: JSON.stringify({ appTitle: this.appTitle, logoUrl: this.logoUrl, splashBg: this.splashBg, primaryColor: this.primaryColor, iconKey: this.iconKey }), headers: { 'Content-Type': 'application/json' } });
        this.saved = 'Branding saved! Mobile app will apply it on next launch.';
        setTimeout(() => this.saved = '', 5000);
      } catch (e) { this.saved = 'Error saving branding.' }
    }
  }));

  // Online shop orders (admin management + delivery settings)
  Alpine.data('onlineOrdersPanel', () => ({
    data: [], search: '', filter: '', loading: false, pendingCount: 0,
    detailOpen: false, detail: null, detailItems: [], detailLoading: false,
    detailPayments: [], drivers: [], assignDriverId: '',
    detailTimeline: [], detailPick: { picked: 0, total: 0 },
    receiptOpen: false, editOpen: false, editItems: [], editQ: '', editResults: [], editTimer: null,
    remit: { shifts: [], payments: [] }, ecomShift: { shift: null, carriedOver: [] },
    settings: { deliveryFee: 0, freeDeliveryMin: 0 }, settingsSaved: '',
    init() {
      if (Alpine.store('app').section === 'online-orders') { this.load(); this.loadSettings(); this.loadRemittances(); this.loadEcomShift(); }
      this.$watch('$store.app.section', v => { if (v === 'online-orders') { this.load(); this.loadSettings(); this.loadRemittances(); this.loadEcomShift(); } });
      this.loadBadge();
      setInterval(() => this.loadBadge(), 30000);
    },
    async loadBadge() {
      try {
        const r = await fetchJSON(API + '/shop/orders/new-count');
        this.pendingCount = r.pending || 0;
        Alpine.store('app')._shopBadge = this.pendingCount;
      } catch (e) { }
    },
    async load() {
      this.loading = true;
      try {
        let url = API + '/shop/orders?limit=200';
        if (this.filter) url += '&status=' + this.filter;
        this.data = await fetchJSON(url);
      } catch (e) { this.data = [] }
      this.loading = false;
      this.loadBadge();
    },
    setFilter(s) { this.filter = s; this.load(); },
    get filtered() {
      const q = this.search.trim().toLowerCase();
      if (!q) return this.data;
      return this.data.filter(o => (o.orderNo + ' ' + o.customerName + ' ' + o.phone).toLowerCase().includes(q));
    },
    statusCls(s) {
      switch (s) {
        case 'pending': return 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400';
        case 'confirmed': return 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400';
        case 'shipped': return 'bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400';
        case 'delivered': return 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400';
        case 'cancelled': return 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400';
        default: return 'bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400';
      }
    },
    statusBtnCls(s) {
      switch (s) {
        case 'pending': return 'bg-yellow-500 text-white border-yellow-500';
        case 'confirmed': return 'bg-blue-600 text-white border-blue-600';
        case 'shipped': return 'bg-purple-600 text-white border-purple-600';
        case 'delivered': return 'bg-green-600 text-white border-green-600';
        case 'cancelled': return 'bg-red-600 text-white border-red-600';
        default: return '';
      }
    },
    async openDetail(o) {
      this.detail = o;
      this.detailOpen = true;
      this.detailItems = [];
      this.detailPayments = [];
      this.detailTimeline = [];
      this.detailPick = { picked: 0, total: 0 };
      this.detailLoading = true;
      this.assignDriverId = o.driverId || '';
      try {
        const r = await fetchJSON(API + '/shop/orders/' + o.id);
        this.detail = r.order;
        this.detailItems = r.items || [];
        this.detailPayments = r.payments || [];
        this.detailTimeline = r.timeline || [];
        this.detailPick = r.pickProgress || { picked: 0, total: 0 };
        this.assignDriverId = r.order.driverId || '';
        if (this.drivers.length === 0) this.loadDrivers();
      } catch (e) { this.detailItems = [] }
      this.detailLoading = false;
    },
    async togglePick(itemId, checked) {
      try {
        await fetchJSON(API + '/orders/' + this.detail.id + '/pick', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ itemId, picked: checked, pickedBy: 'Admin' }) });
        await this.openDetail(this.detail);
      } catch (e) { toast(e.message, 'error'); }
    },
    async loadRemittances() {
      try { this.remit = await fetchJSON(API + '/remittances'); } catch (e) { this.remit = { shifts: [], payments: [] }; }
    },
    async remitPayment(p) {
      try {
        await fetchJSON(API + '/payments/' + p.id + '/remit', { method: 'POST' });
        toast('Remitted: ' + p.orderNo + ' ' + p.method + ' ' + fmt(p.amount));
        await this.loadRemittances();
      } catch (e) { toast(e.message, 'error'); }
    },
    async loadEcomShift() {
      try { this.ecomShift = await fetchJSON(API + '/ecom-shift'); } catch (e) { this.ecomShift = { shift: null, carriedOver: [] }; }
    },
    async closeDay() {
      if (!confirm('Isara ang e-commerce day? Ang mga hindi nai-deliver ay dadalhin bukas.')) return;
      try {
        await fetchJSON(API + '/ecom-shift/close', { method: 'POST' });
        toast('CLOSED — bagong shift na bukas para bukas');
        await this.loadEcomShift();
      } catch (e) { toast(e.message, 'error'); }
    },
    async loadDrivers() {
      try { this.drivers = await fetchJSON(API + '/drivers'); } catch (e) { this.drivers = []; }
    },
    async assignDriver() {
      if (!this.assignDriverId) return;
      try {
        const r = await fetch(API + '/orders/' + this.detail.id + '/assign-driver', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ driverId: Number(this.assignDriverId) }) });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.error || 'Assign failed');
        const drv = this.drivers.find(d => d.id === Number(this.assignDriverId));
        this.detail.driverName = drv ? drv.name : 'Driver';
        this.detail.driverId = Number(this.assignDriverId);
        if (this.detail.status === 'confirmed') this.detail.status = 'shipped';
        toast('Driver assigned: ' + (drv ? drv.name : '') + ' — order marked SHIPPED');
        this.load();
      } catch (e) { toast(e.message || 'Assign failed', 'error'); }
    },
    subtotal() { return (this.detailItems || []).reduce((s, it) => s + it.total, 0) },
    get pickComplete() {
      return this.detailPick.total > 0 && this.detailPick.picked >= this.detailPick.total && (this.detail?.status || '') === 'confirmed';
    },
    get detailTotalPoints() { return Number(this.detail?.totalPoints || 0); },
    get detailPointsLabel() {
      const d = this.detail || {};
      return d.paidStatus === 'paid' ? '(earned)' : '(to earn)';
    },
    get detailPointsText() {
      const tp = Number(this.detail?.totalPoints || 0);
      const ap = Number(this.detail?.awardPoints || 0);
      return tp.toFixed(2) + ' pts → +' + ap.toFixed(2) + ' point' + (ap > 1 ? 's' : '');
    },
    get receiptText() {
      const o = this.detail || {};
      const items = this.detailItems || [];
      const withPts = items.filter(it => Number(it.points || 0) > 0);
      const noPts = items.filter(it => !(Number(it.points || 0) > 0));
      const tp = Number(o.totalPoints || 0);
      const ap = Number(o.awardPoints || 0);
      const verb = o.paidStatus === 'paid' ? 'earned' : 'to earn';
      const padN = (t, n) => { t = String(t); while (t.length < n) t += ' '; return t; };
      const dt = String(o.createdAt || '').slice(0, 16).replace('T', ' ');
      const itemLine = (it) => padN(String(it.productName || '').slice(0, 29), 30) + ' x' + it.qty + ' ' + Number(it.total).toFixed(2);
      const L = [];
      L.push('        ANDENGS SUPERSTORE');
      L.push('     --- Online Order Receipt ---');
      L.push('----------------------------');
      L.push('Order: ' + (o.orderNo || ''));
      if (dt) L.push('Date:  ' + dt);
      L.push('Customer: ' + (o.customerName || ''));
      if (o.phone) L.push('Phone: ' + o.phone);
      L.push('Address: Blk ' + (o.block || '-') + ' Lot ' + (o.lot || '-') + (o.subdivision ? ', ' + o.subdivision : ''));
      if (o.address) L.push('  ' + o.address);
      if (o.deliveryNote) L.push('Note: ' + o.deliveryNote);
      L.push('----------------------------');
      if (withPts.length) {
        L.push('-- WITH POINTS --');
        withPts.forEach(it => L.push(itemLine(it)));
        L.push('----------------------------');
        L.push('⭐ TOTAL: ' + tp.toFixed(2) + ' pts -> ' + (ap >= 1 ? '+' + ap.toFixed(2) + ' point' + (ap > 1 ? 's' : '') + ' (' + verb + ')' : '+0.00 (kulang pa para sa 1 point)'));
        L.push('----------------------------');
      }
      if (noPts.length) {
        L.push('-- WALANG POINTS --');
        noPts.forEach(it => L.push(itemLine(it)));
        L.push('----------------------------');
      }
      const sub = items.reduce((s, it) => s + Number(it.total || 0), 0);
      L.push(padN('SUBTOTAL', 38) + sub.toFixed(2));
      L.push(padN('DELIVERY FEE', 38) + Number(o.deliveryFee || 0).toFixed(2));
      L.push(padN('TOTAL', 38) + Number(o.total || 0).toFixed(2));
      if (withPts.length && ap < 1) L.push('⭐ Points ' + verb + ': ' + tp.toFixed(2) + ' pts -> +0.00 (P200 = 1 point)');
      L.push('Payment: ' + (o.paymentMethod || 'COD'));
      L.push('----------------------------');
      L.push('        Salamat po!');
      L.push('     Mag-order muli :)');
      return L.join('\n');
    },
    openReceipt() { this.receiptOpen = true; },
    closeReceipt() { this.receiptOpen = false; },
    openEdit() {
      this.editItems = (this.detailItems || []).map(it => ({ productId: it.productId, productName: it.productName, unitName: it.unitName || 'PC', qty: it.qty, price: Number(it.price || 0) }));
      this.editQ = ''; this.editResults = [];
      this.editOpen = true;
    },
    editQty(i, d) { this.editItems[i].qty = Math.max(1, (this.editItems[i].qty || 1) + d); },
    editRemove(i) { this.editItems.splice(i, 1); },
    get editSubtotal() { return this.editItems.reduce((s, it) => s + it.price * it.qty, 0) + Number(this.detail?.deliveryFee || 0); },
    async searchEdit() {
      clearTimeout(this.editTimer);
      const q = (this.editQ || '').trim();
      this.editTimer = setTimeout(async () => {
        if (!q) { this.editResults = []; return; }
        try { this.editResults = await fetchJSON(API + '/shop/catalog/search?q=' + encodeURIComponent(q) + '&limit=15'); }
        catch (e) { this.editResults = []; }
      }, 300);
    },
    addEdit(p) {
      if (this.editItems.some(x => x.productId === p.id)) { toast('Nasa listahan na ang item', 'error'); return; }
      this.editItems.push({ productId: p.id, productName: p.name, unitName: 'PC', qty: 1, price: Number(p.price || 0) });
      this.editQ = ''; this.editResults = [];
    },
    async saveEdit() {
      try {
        const items = this.editItems.map(it => ({ productId: it.productId, unitName: it.unitName, qty: it.qty }));
        const r = await fetch(API + '/shop/orders/' + this.detail.id + '/items', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ items }) });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.message || ('HTTP ' + r.status));
        toast('Items updated — bagong total: ' + fmt(j.total || 0) + ' ✓', 'success');
        this.editOpen = false;
        await this.openDetail(this.detail);
        this.load();
      } catch (e) { toast(e.message || 'Save failed', 'error'); }
    },
    async setStatus(st) {
      if (!this.detail) return;
      const no = this.detail.orderNo;
      const msg = st === 'cancelled' ? 'Cancel ' + no + '? Reserved stock will be returned to HQ.'
        : st === 'confirmed' ? 'Confirm ' + no + '? Items will be reserved from HQ stock.' : '';
      if (msg && !confirm(msg)) return;
      try {
        const r = await fetch(API + '/shop/orders/' + this.detail.id + '/status', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: st }) });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.message || ('HTTP ' + r.status));
        this.detail.status = st;
        toast(st.toUpperCase() + ' OK', 'success');
        if (st === 'confirmed') this.openReceipt();
        this.load();
      } catch (e) {
        toast(e.message || 'Error updating status', 'error');
      }
    },
    async loadSettings() {
      try {
        const r = await fetchJSON(API + '/shop/settings');
        this.settings.deliveryFee = r.deliveryFee || 0;
        this.settings.freeDeliveryMin = r.freeDeliveryMin || 0;
      } catch (e) { }
    },
    async saveSettings() {
      this.settingsSaved = '';
      try {
        await fetchJSON(API + '/shop/settings', { method: 'POST', body: JSON.stringify({ deliveryFee: this.settings.deliveryFee || 0, freeDeliveryMin: this.settings.freeDeliveryMin || 0 }), headers: { 'Content-Type': 'application/json' } });
        this.settingsSaved = 'Delivery settings saved!';
        setTimeout(() => this.settingsSaved = '', 3000);
      } catch (e) { this.settingsSaved = 'Error saving delivery settings.' }
    }
  }));

  // AGENTS remote diagnostic panel
  Alpine.data('agentsPanel', () => ({
    d: [], loading: false, selectedStore: '', cmdType: 'sql', cmdPayload: '', results: '', sending: false, pos: {},
    async init() {
      if (Alpine.store('app').section === 'agents') await this.load();
      this.$watch('$store.app.section', v => { if (v === 'agents') this.load(); });
      setInterval(() => { if (Alpine.store('app').section === 'agents') this.load(); }, 15000);
    },
    async load() { this.loading = true; try { this.d = await fetchJSON(API + '/agent/status') } catch (e) { this.d = [] }; try { const ps = await fetchJSON(API + '/pos-status'); this.pos = {}; (ps || []).forEach(x => this.pos[x.storeId] = x); } catch (e) { }; this.loading = false },
    syncChip(ps) {
      if (!ps) return { cls: 'bg-gray-200 dark:bg-[#222255] text-gray-500 dark:text-[#7878aa]', txt: '—' };
      var fresh = (Date.now() - new Date(ps.updatedAt).getTime()) < 90000;
      if (!fresh) return { cls: 'bg-gray-200 dark:bg-[#222255] text-gray-500 dark:text-[#7878aa]', txt: 'offline' };
      var total = 0, parts = [];
      for (var k in ps.pending) { if (ps.pending[k] > 0) { total += ps.pending[k]; parts.push(k + ':' + ps.pending[k]); } }
      if (total === 0) return { cls: 'bg-green-100 dark:bg-green-900/30 text-green-600 dark:text-green-400', txt: 'SYNC OK' };
      return { cls: 'bg-orange-100 dark:bg-orange-900/30 text-orange-600 dark:text-orange-400', txt: total + ' PENDING' };
    },
    async send() {
      if (!this.selectedStore || !this.cmdPayload.trim()) return;
      this.sending = true; this.results = 'Sending...';
      try {
        const r = await fetchJSON(API + '/agent/send/' + encodeURIComponent(this.selectedStore), { method: 'POST', body: JSON.stringify({ type: this.cmdType, payload: this.cmdPayload }), headers: { 'Content-Type': 'application/json' } });
        this.cmdId = r.commandId;
        this.results = 'Command sent (#' + r.commandId + '). Waiting for result...';
        await this.pollResults();
      } catch (e) { this.results = 'Error: ' + e.message }
      this.sending = false;
    },
    async pollResults() {
      var tries = 0;
      while (tries < 15) {
        await new Promise(r => setTimeout(r, 2000));
        try {
          const list = await fetchJSON(API + '/agent/results/' + encodeURIComponent(this.selectedStore));
          var found = list.find(x => x.commandId === this.cmdId);
          if (found) { this.results = found.error ? 'ERROR: ' + found.error : found.output; return }
        } catch (e) {}
        tries++;
        this.results = 'Waiting... (' + tries + '/15)';
      }
      this.results = 'Timeout — agent may be offline.';
    },
    uploadedUrl: '', pushing: false, pushStatus: '',
    async handleUpload(e) {
      var file = e.target.files[0];
      if (!file) return;
      this.pushStatus = 'Uploading to cloud...';
      try {
        var fd = new FormData();
        fd.append('file', file);
        var r = await fetch(API + '/agent/upload-file', { method: 'POST', body: fd });
        if (!r.ok) throw new Error('Upload failed');
        var data = await r.json();
        this.uploadedUrl = data.url;
        this.pushStatus = 'Uploaded! Select a store and click PUSH.';
      } catch (ex) { this.pushStatus = 'Upload error: ' + ex.message }
    },
    async pushToPos() {
      if (!this.selectedStore || !this.uploadedUrl) return;
      this.pushing = true;
      this.pushStatus = 'Pushing to ' + this.selectedStore + '...';
      try {
        var fullUrl = 'https://admin.jumongdev.com' + this.uploadedUrl;
        var fileName = this.uploadedUrl.split('/').pop();
        var r = await fetchJSON(API + '/agent/send/' + encodeURIComponent(this.selectedStore), { method: 'POST', body: JSON.stringify({ type: 'update', payload: fullUrl + '|assets/' + fileName }), headers: { 'Content-Type': 'application/json' } });
        var cmdId = r.commandId;
        var tries = 0;
        while (tries < 10) {
          await new Promise(r => setTimeout(r, 3000));
          var list = await fetchJSON(API + '/agent/results/' + encodeURIComponent(this.selectedStore));
          var found = list.find(x => x.commandId === cmdId);
          if (found) { this.pushStatus = found.error ? 'ERROR: ' + found.error : 'Pushed! ' + fileName; this.pushing = false; return }
          tries++;
        }
        this.pushStatus = 'Timeout — agent may be offline.';
      } catch (ex) { this.pushStatus = 'Push error: ' + ex.message }
      this.pushing = false;
    }
  }));

  Alpine.data('posQrPanel', () => ({
    d: [], sel: {}, header: '', imgUrl: '', imgPreview: '', fileName: '', pushing: false, restartPos: false,
    statuses: [], msg: '', cloud: [], cloudMsg: '', cloudMsgOk: true,
    async init() {
      if (Alpine.store('app').section === 'posqr') await this.load();
      this.$watch('$store.app.section', v => { if (v === 'posqr') { this.load(); this.loadCloud(); } });
    },
    async loadCloud() {
      try {
        const j = await fetchJSON(API + '/payment-qrs');
        this.cloud = (j.qrs || []).map(q => ({ id: q.id, header: q.header, file: q.file, isActive: q.isActive, sortOrder: q.sortOrder }));
      } catch (e) { }
    },
    async saveCloud() {
      try {
        const r = await fetch(API + '/payment-qrs', {
          method: 'POST', headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ qrs: this.cloud.map((q, i) => ({ id: q.id, header: q.header, file: q.file, isActive: q.isActive, sortOrder: i })) })
        });
        if (!r.ok) throw new Error('Save failed');
        this.cloudMsgOk = true;
        this.cloudMsg = 'Cloud QR list saved — makikita na ng driver app.';
        await this.loadCloud();
      } catch (e) { this.cloudMsgOk = false; this.cloudMsg = 'Save error: ' + e.message; }
    },
    removeCloud(q) {
      this.cloud = this.cloud.filter(x => x !== q);
    },
    async load() {
      try {
        const agents = await fetchJSON(API + '/agent/status');
        this.d = (agents || []).map(a => a.storeId);
        this.statuses = this.d.map(sid => ({ storeId: sid, state: 'idle', msg: '' }));
      } catch (e) { this.d = []; this.statuses = []; }
    },
    storeName(sid) { return Alpine.store('app').storeMap[sid] || sid; },
    toggleAll(on) { this.d.forEach(sid => this.sel[sid] = on); },
    async handleUpload(e) {
      const file = e.target.files[0];
      if (!file) return;
      if (!/\.(png|jpe?g|gif|bmp)$/i.test(file.name)) { this.msg = 'Only image files (png/jpg/gif/bmp) allowed'; return; }
      this.msg = 'Uploading to cloud...';
      try {
        const fd = new FormData();
        fd.append('file', file);
        const r = await fetch(API + '/agent/upload-file', { method: 'POST', body: fd });
        if (!r.ok) throw new Error('Upload failed');
        const d = await r.json();
        this.imgUrl = d.fullUrl || ('https://admin.jumongdev.com' + d.url);
        this.imgPreview = d.url;
        this.fileName = file.name;
        this.msg = 'Image ready — push to stores below.';
      } catch (ex) { this.msg = 'Upload error: ' + ex.message; }
    },
    async pushAll() {
      const targets = this.d.filter(sid => this.sel[sid]);
      if (!targets.length) { this.msg = 'Select at least one store'; return; }
      if (!this.imgUrl) { this.msg = 'Upload an image first'; return; }
      if (!this.header.trim()) { this.msg = 'Enter a title first'; return; }
      this.pushing = true;
      const sql = "UPDATE Settings SET Value='" +
        JSON.stringify([{ header: this.header.trim(), file: this.fileName }]).replace(/'/g, "''") +
        "' WHERE Key='StoreQrCodes';";
      const tgt = targets.map(sid => ({ storeId: sid, state: 'sending', msg: '', updateId: 0, sqlId: 0, restartId: 0 }));
      this.statuses = this.d.map(sid => {
        const t = tgt.find(x => x.storeId === sid);
        return t || { storeId: sid, state: 'skip', msg: '' };
      });
      for (const t of tgt) {
        try {
          const u = await fetchJSON(API + '/agent/send/' + encodeURIComponent(t.storeId), { method: 'POST', body: JSON.stringify({ type: 'update', payload: this.imgUrl + '|..\\assets\\' + this.fileName }), headers: { 'Content-Type': 'application/json' } });
          t.updateId = u.commandId;
          const s = await fetchJSON(API + '/agent/send/' + encodeURIComponent(t.storeId), { method: 'POST', body: JSON.stringify({ type: 'sql', payload: sql }), headers: { 'Content-Type': 'application/json' } });
          t.sqlId = s.commandId;
          if (this.restartPos) {
            const r = await fetchJSON(API + '/agent/send/' + encodeURIComponent(t.storeId), { method: 'POST', body: JSON.stringify({ type: 'restart', payload: '' }), headers: { 'Content-Type': 'application/json' } });
            t.restartId = r.commandId;
          }
          t.state = 'pushing';
        } catch (ex) { t.state = 'error'; t.msg = 'send failed: ' + ex.message; }
      }
      this.msg = 'Commands sent. Waiting for agents...';
      await this.poll(tgt);
      this.pushing = false;
      if (this.fileName) {
        const existing = this.cloud.find(q => q.file === this.fileName);
        if (existing) existing.header = this.header.trim() || existing.header;
        else this.cloud.push({ id: 0, header: this.header.trim(), file: this.fileName, isActive: true });
        await this.saveCloud();
      }
    },
    async poll(tgt) {
      for (let n = 0; n < 14; n++) {
        await new Promise(r => setTimeout(r, 2500));
        let pending = false;
        for (const t of tgt) {
          if (t.state !== 'pushing') continue;
          try {
            const list = await fetchJSON(API + '/agent/results/' + encodeURIComponent(t.storeId));
            const u = list.find(x => x.commandId === t.updateId);
            const s = list.find(x => x.commandId === t.sqlId);
            if (u && s) {
              t.state = (u.error || s.error) ? 'error' : 'done';
              t.msg = (u.error || 'Image OK') + ' / ' + (s.error || 'Title OK') + (t.restartId ? ' / restart sent' : '');
            } else pending = true;
          } catch (e) { pending = true; }
        }
        if (!pending) { this.msg = 'Done — check the statuses below.'; return; }
      }
      this.msg = 'Some stores timed out — check AGENTS panel for connectivity.';
    }
  }));

  Alpine.data('suspect1pcPanel', () => ({
    data: [],
    filterStore: '',
    filterStatus: 'pending',
    itemModalOpen: false,
    itemViewList: [],
    itemViewInvoice: '',

    async init() {
      window.addEventListener('load-suspect1pc', () => this.load());
      if (Alpine.store('app').section === 'suspect1pc') await this.load();
      this.$watch('$store.app.section', v => { if (v === 'suspect1pc') this.load(); });
    },

    async load() {
      try {
        var qs = [];
        if (this.filterStore) qs.push('store=' + encodeURIComponent(this.filterStore));
        if (this.filterStatus) qs.push('status=' + encodeURIComponent(this.filterStatus));
        var url = API + '/suspect-1pc' + (qs.length ? '?' + qs.join('&') : '');
        this.data = await fetchJSON(url);
      } catch (e) { this.data = []; }
    },

    async assign(id) {
      try {
        await fetchJSON(API + '/suspect-1pc/' + id + '/assign', { method: 'PUT', body: JSON.stringify({ checker: 'Admin' }), headers: { 'Content-Type': 'application/json' } });
        await this.load();
      } catch (e) {}
    },

    async resolve(id) {
      try {
        await fetchJSON(API + '/suspect-1pc/' + id + '/resolve', { method: 'PUT', body: JSON.stringify({ notes: '' }), headers: { 'Content-Type': 'application/json' } });
        await this.load();
      } catch (e) {}
    },

    showItems(r) {
      this.itemViewInvoice = r.invoiceNo;
      try { this.itemViewList = (JSON.parse(r.items || '[]') || []).map(x => ({
        productName: x.productName || x.ProductName || '',
        unitName: x.unitName || x.UnitName || '',
        quantity: x.quantity || x.Quantity || 0,
        price: x.price || x.Price || 0
      })); } catch (e) { this.itemViewList = []; }
      this.itemModalOpen = true;
    }
  }));

  Alpine.data('aiChatPanel', () => ({
    msgs: [], input: '', sending: false, typing: false, error: '',
    stats: { total: 0, ok: 0, fail: 0, avgMs: 0, maxMs: 0, recent: [] }, statsTimer: null, sources: [],
    async init() {
      await this.loadStats();
      this.statsTimer = setInterval(() => { if (Alpine.store('app').section === 'ai-chat') this.loadStats(); }, 15000);
    },
    async loadStats() {
      try {
        const s = await fetchJSON(API + '/chat/stats');
        this.stats = s || this.stats;
      } catch (e) { }
    },
    async send() {
      const m = this.input.trim();
      if (!m || this.sending) return;
      this.msgs.push({ role: 'user', content: m });
      this.input = '';
      this.sending = true;
      this.typing = true;
      this.error = '';
      this.sources = [];
      try {
        const hist = this.msgs.slice(0, -1).map(x => ({ role: x.role, content: x.content }));
        const r = await fetchJSON(API + '/chat', { method: 'POST', body: JSON.stringify({ message: m, history: hist }), headers: { 'Content-Type': 'application/json' } });
        this.msgs.push({ role: 'assistant', content: r.reply, reviewed: '', correctOpen: false, correctText: '' });
        this.sources = (r.sources || []).map(s => s.startsWith('kb:') ? 'KB#' + s.slice(3) : s.charAt(0).toUpperCase() + s.slice(1));
      } catch (e) {
        this.error = e.message || 'Failed to reach Ollama';
        this.msgs.push({ role: 'assistant', content: '(error: ' + this.error + ')', reviewed: '', correctOpen: false, correctText: '' });
      }
      this.sending = false;
      this.typing = false;
      await this.loadStats();
    },
    startCorrect(m) { m.correctOpen = true; m.correctText = m.correctText || ''; },
    async review(m, verdict) {
      if (verdict === 'corrected' && !(m.correctText || '').trim()) return;
      try {
        await fetchJSON(API + '/chat/kb/review', {
          method: 'POST',
          body: JSON.stringify({
            userMessage: this.msgs[this.msgs.indexOf(m) - 1]?.content || '',
            botReply: m.content,
            verdict,
            correctedAnswer: verdict === 'corrected' ? m.correctText.trim() : ''
          }),
          headers: { 'Content-Type': 'application/json' }
        });
        m.reviewed = verdict;
        m.correctOpen = false;
      } catch (e) {
        this.error = e.message || 'Review failed';
      }
    },
    clearChat() { this.msgs = []; this.error = ''; this.sources = []; }
  }));

  Alpine.data('kbPanel', () => ({
    entries: [], reviews: [], filterCategory: '', pendingOnly: false,
    answers: {}, editorOpen: false, editingId: null,
    form: { category: 'business', keywords: '', question: '', answer: '', active: true },
    async init() { await this.load(); await this.loadReviews(); await this.refreshBadge(); },
    get filtered() {
      let list = this.entries;
      if (this.pendingOnly) list = list.filter(k => k.source === 'auto-pending' && !k.answer && !k.active);
      return list;
    },
    async load() {
      try {
        const url = API + '/chat/kb' + (this.filterCategory ? '?category=' + encodeURIComponent(this.filterCategory) : '');
        this.entries = await fetchJSON(url);
      } catch (e) { this.entries = []; }
    },
    async refreshBadge() {
      try { const r = await fetchJSON(API + '/chat/kb/pending-count'); Alpine.store('app')._kbBadge = r.count || 0; } catch (e) {}
    },
    async quickAnswer(k) {
      const a = (this.answers[k.id] || '').trim();
      if (!a) { toast('Type an answer first'); return; }
      try {
        await fetchJSON(API + '/chat/kb/' + k.id, { method: 'PUT', body: JSON.stringify({ answer: a, active: true }), headers: { 'Content-Type': 'application/json' } });
        toast('Answer saved — alam na ng bot!');
        delete this.answers[k.id];
        await this.load();
        await this.refreshBadge();
      } catch (e) { toast(e.message || 'Save failed'); }
    },
    async loadReviews() {
      try { this.reviews = await fetchJSON(API + '/chat/kb/reviews?limit=50'); } catch (e) { this.reviews = []; }
    },
    openAdd() {
      this.editingId = null;
      this.form = { category: 'business', keywords: '', question: '', answer: '', active: true };
      this.editorOpen = true;
    },
    openEdit(k) {
      this.editingId = k.id;
      this.form = { category: k.category, keywords: k.keywords, question: k.question, answer: k.answer, active: k.active };
      this.editorOpen = true;
    },
    async save() {
      try {
        const body = JSON.stringify(this.form);
        if (this.editingId) {
          await fetchJSON(API + '/chat/kb/' + this.editingId, { method: 'PUT', body, headers: { 'Content-Type': 'application/json' } });
        } else {
          await fetchJSON(API + '/chat/kb', { method: 'POST', body, headers: { 'Content-Type': 'application/json' } });
        }
        this.editorOpen = false;
        await this.load();
      } catch (e) {
        toast(e.message || 'Save failed');
      }
    },
    async toggleActive(k) {
      try {
        await fetchJSON(API + '/chat/kb/' + k.id, { method: 'PUT', body: JSON.stringify({ active: !k.active }), headers: { 'Content-Type': 'application/json' } });
        await this.load();
      } catch (e) { toast(e.message || 'Toggle failed'); }
    },
    async remove(k) {
      if (!confirm('Delete KB entry: ' + (k.question || k.answer.slice(0, 50)) + '?')) return;
      try {
        await fetchJSON(API + '/chat/kb/' + k.id, { method: 'DELETE' });
        await this.load();
      } catch (e) { toast(e.message || 'Delete failed'); }
    },
    async ingestProject() {
      try {
        const r = await fetchJSON(API + '/chat/kb/ingest-project', { method: 'POST' });
        toast('Ingested ' + (r.sections || 0) + ' project sections');
        await this.load();
      } catch (e) { toast(e.message || 'Ingest failed'); }
    }
  }));

  /* ── Shop Content (landing page content editor) ─────────────────────── */
  Alpine.data('restockPanel', () => ({
    data: [], filter: 'pending', loading: false,
    async init() { await this.load(); },
    setFilter(f) { this.filter = f; this.load(); },
    async load() {
      this.loading = true;
      try {
        const url = API + '/restock-requests' + (this.filter ? '?status=' + this.filter : '');
        this.data = await fetchJSON(url);
        if (this.filter !== 'pending') await this.refreshBadge();
      } catch (e) { this.data = []; }
      this.loading = false;
    },
    async refreshBadge() {
      try { const r = await fetchJSON(API + '/restock-requests/pending-count'); Alpine.store('app')._restockBadge = r.count || 0; } catch (e) {}
    },
    async resolve(x, st) {
      try {
        await fetchJSON(API + '/restock-requests/' + x.id + '/resolve', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: st }) });
        toast(st === 'fulfilled' ? 'Restock request marked DONE' : 'Dismissed');
        await this.load();
      } catch (e) { toast(e.message || 'Failed', 'error'); }
    }
  }));

  Alpine.data('productSuggestPanel', () => ({
    data: [], filter: 'pending', loading: false,
    async init() { await this.load(); },
    setFilter(f) { this.filter = f; this.load(); },
    async load() {
      this.loading = true;
      try {
        const url = API + '/product-suggestions' + (this.filter ? '?status=' + this.filter : '');
        this.data = await fetchJSON(url);
        if (this.filter !== 'pending') await this.refreshBadge();
      } catch (e) { this.data = []; }
      this.loading = false;
    },
    async refreshBadge() {
      try { const r = await fetchJSON(API + '/product-suggestions/pending-count'); Alpine.store('app')._suggBadge = r.count || 0; } catch (e) {}
    },
    async review(x, approve) {
      try {
        await fetchJSON(API + '/product-suggestions/' + x.id + '/review', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ approve }) });
        toast(approve ? 'Suggestion approved' : 'Dismissed');
        await this.load();
      } catch (e) { toast(e.message || 'Failed', 'error'); }
    }
  }));

  Alpine.data('promoGroupsPanel', () => ({
    groups: [], products: [], loading: true, saving: false, msg: '', msgOk: true,
    form: { id: 0, name: '', buyQty: 60, freeQty: 1, freeProductId: 0, active: true, items: [{ productId: 0, q: '', open: false }, { productId: 0, q: '', open: false }, { productId: 0, q: '', open: false }] },
    freeQ: '', freeOpen: false,
    async init() { await this.load(); },
    async load() {
      this.loading = true;
      try { this.groups = await fetchJSON(API + '/promo-groups'); } catch (e) { this.groups = []; }
      if (!this.products.length) {
        try { this.products = await fetchJSON(API + '/products/master?noImages=true'); } catch (e) { this.products = []; }
      }
      this.loading = false;
    },
    searchResults(q) {
      const s = (q || '').toLowerCase().trim();
      if (!s) return [];
      return this.products.filter(p => p.isActive !== false && ((p.name || '').toLowerCase().includes(s) || (p.barcode || '').includes(s))).slice(0, 20);
    },
    // Resolve typed text -> product id (exact name, o kaya unique na match)
    resolveProduct(q) {
      const s = (q || '').trim().toLowerCase();
      if (!s) return 0;
      const exact = this.products.find(p => (p.name || '').toLowerCase() === s);
      if (exact) return exact.id;
      const matches = this.products.filter(p => (p.name || '').toLowerCase().includes(s));
      return matches.length === 1 ? matches[0].id : 0;
    },
    pickFirst(it) {
      const r = this.searchResults(it.q);
      if (r.length) { it.productId = r[0].id; it.q = r[0].name; }
      it.open = false;
    },
    pickFreeFirst() {
      const r = this.searchResults(this.freeQ);
      if (r.length) { this.form.freeProductId = r[0].id; this.freeQ = r[0].name; }
      this.freeOpen = false;
    },
    addItem() { this.form.items.push({ productId: 0, q: '', open: false }); },
    removeItem(i) { if (this.form.items.length > 2) this.form.items.splice(i, 1); },
    openEdit(g) {
      this.form = { id: g.id, name: g.name, buyQty: g.buyQty, freeQty: g.freeQty, freeProductId: g.freeProductId, active: g.active, items: [] };
      (g.items || []).forEach(x => this.form.items.push({ productId: x.productId, q: this.productName(x.productId), open: false }));
      while (this.form.items.length < 3) this.form.items.push({ productId: 0, q: '', open: false });
      const fp = this.productName(g.freeProductId);
      this.freeQ = fp; this.freeOpen = false;
    },
    newGroup() {
      this.form = { id: 0, name: '', buyQty: 60, freeQty: 1, freeProductId: 0, active: true, items: [{ productId: 0, q: '', open: false }, { productId: 0, q: '', open: false }, { productId: 0, q: '', open: false }] };
      this.freeQ = ''; this.freeOpen = false;
    },
    productName(id) { const p = this.products.find(x => x.id === id); return p ? p.name : ''; },
    selectedItems() { return this.form.items.filter(x => x.productId > 0); },
    async save() {
      // Auto-resolve ang mga nai-type na pangalan (exact o unique match) — para hindi ma-reject kahit hindi nag-click sa list
      this.form.items.forEach(it => { if (!it.productId) it.productId = this.resolveProduct(it.q); });
      if (!this.form.freeProductId) this.form.freeProductId = this.resolveProduct(this.freeQ);
      const items = this.selectedItems();
      if (!this.form.name || items.length < 2 || this.form.buyQty <= 0 || this.form.freeQty <= 0 || !this.form.freeProductId) {
        this.msg = 'Kumpletuhin: pangalan, 2+ items, buy qty, free qty, at free product — siguraduhing NAPILI mula sa listahan (i-type at pindutin ang result, o Enter)'; this.msgOk = false; return;
      }
      this.saving = true; this.msg = '';
      try {
        const body = { name: this.form.name, buyQty: Number(this.form.buyQty), freeQty: Number(this.form.freeQty), freeProductId: this.form.freeProductId, active: this.form.active, items: items.map((x, i) => ({ productId: x.productId, slot: i })) };
        const url = this.form.id ? API + '/promo-groups/' + this.form.id : API + '/promo-groups';
        const r = await fetch(url, { method: this.form.id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (!r.ok) { const j = await r.json().catch(() => ({})); throw new Error(j.error || 'Failed'); }
        this.msg = 'Promo group saved!'; this.msgOk = true;
        this.form.id = 0;
        await this.load();
      } catch (e) { this.msg = 'Save failed: ' + e.message; this.msgOk = false; }
      this.saving = false;
    },
    async del(g) {
      if (!confirm('Delete promo group "' + g.name + '"?')) return;
      try { await fetchJSON(API + '/promo-groups/' + g.id, { method: 'DELETE' }); await this.load(); } catch (e) { toast(e.message, 'error'); }
    }
  }));

  Alpine.data('promoFreeQueuePanel', () => ({
    rows: [], filter: 'queued', loading: false,
    async init() { await this.load(); await this.refreshBadge(); },
    setFilter(f) { this.filter = f; this.load(); },
    async load() {
      this.loading = true;
      try {
        const url = API + '/promo-free-queue' + (this.filter ? '?status=' + this.filter : '');
        this.rows = await fetchJSON(url);
        if (this.filter !== 'queued') await this.refreshBadge();
      } catch (e) { this.rows = []; }
      this.loading = false;
    },
    async refreshBadge() {
      try { const r = await fetchJSON(API + '/promo-free-queue?status=queued'); Alpine.store('app')._promoQBadge = (r || []).length; } catch (e) {}
    },
    async process(x) {
      try {
        const r = await fetch(API + '/promo-free-queue/' + x.id + '/process', { method: 'POST' });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) { toast(j.error || 'Process failed', 'error'); return; }
        toast('PROCESSED: ' + j.productName + ' x' + j.qty, 'success');
        await this.load(); await this.refreshBadge();
      } catch (e) { toast('Process failed: ' + e.message, 'error'); }
    }
  }));

  Alpine.data('googleAuthPanel', () => ({
    f: { clientId: '', clientSecret: '', enabled: false },
    hasSecret: false, masked: '', saving: false, msg: '', msgOk: true,    async init() { await this.load(); },
    async load() {
      try {
        const r = await fetchJSON(API + '/google-auth');
        this.f.clientId = r.clientId || '';
        this.f.clientSecret = '';
        this.f.enabled = !!r.enabled;
        this.hasSecret = !!r.hasSecret;
        this.masked = r.clientSecret || '';
      } catch (e) { this.msg = 'Load failed: ' + e.message; this.msgOk = false; }
    },
    async save() {
      this.saving = true; this.msg = '';
      try {
        await fetchJSON(API + '/google-auth', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(this.f) });
        this.msg = 'Google login settings saved' + (this.f.enabled ? ' - gumagana na ang Sign in sa shop!' : ' (disabled)');
        this.msgOk = true;
        await this.load();
      } catch (e) { this.msg = 'Save failed: ' + e.message; this.msgOk = false; }
      this.saving = false;
    }
  }));

  Alpine.data('promoBannersPanel', () => ({
    banners: [], pointsRate: 200, loading: false, saving: false, msg: '', msgOk: true,
    form: { id: 0, targetType: 'category', targetValue: '', sortOrder: 0, active: true, file: null },
    async init() { await this.load(); },
    async load() {
      try {
        const r = await fetchJSON(API + '/promo-banners');
        this.banners = r.banners || [];
        this.pointsRate = r.pointsRate || 200;
      } catch (e) { this.banners = []; }
    },
    openAdd() {
      this.form = { id: 0, targetType: 'category', targetValue: '', sortOrder: this.banners.length + 1, active: true, file: null };
      document.getElementById('pbFile').value = '';
    },
    openEdit(b) {
      this.form = { id: b.id, targetType: b.targetType, targetValue: b.targetValue, sortOrder: b.sortOrder, active: b.active, file: null };
      document.getElementById('pbFile').value = '';
    },
    onFile(e) { this.form.file = e.target.files[0] || null; },
    async save() {
      if (!this.form.id && !this.form.file) { toast('Mag-upload ng image para sa bagong banner (o gamitin ang edit)', 'error'); return; }
      if (this.form.targetType === 'category' && !this.form.targetValue) { toast('Ilagay ang category (at opsiyonal na search)', 'error'); return; }
      this.saving = true; this.msg = '';
      try {
        const fd = new FormData();
        if (this.form.id) fd.append('id', this.form.id);
        fd.append('targetType', this.form.targetType);
        fd.append('targetValue', this.form.targetValue);
        fd.append('sortOrder', this.form.sortOrder);
        fd.append('active', this.form.active ? 'true' : 'false');
        if (this.form.file) fd.append('image', this.form.file);
        const r = await fetch(API + '/promo-banners', { method: 'POST', body: fd });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.error || 'Save failed');
        this.msg = 'Banner saved!';
        this.msgOk = true;
        await this.load();
      } catch (e) { this.msg = e.message; this.msgOk = false; }
      this.saving = false;
    },
    async remove(b) {
      if (!confirm('Delete banner #' + b.id + '?')) return;
      try {
        await fetchJSON(API + '/promo-banners/' + b.id, { method: 'DELETE' });
        await this.load();
      } catch (e) { toast(e.message, 'error'); }
    }
  }));

  Alpine.data('shopContentPanel', () => ({
    c: {}, loaded: false, saving: false, suggestions: [],
    async init() { await this.load(); await this.loadSuggestions(); },
    fields: [
      { k: 'hero_title', label: 'Hero Title', g: 'HERO', t: 'text' },
      { k: 'hero_subtitle', label: 'Hero Subtitle', g: 'HERO', t: 'textarea' },
      { k: 'hero_cta', label: 'Hero Button Text', g: 'HERO', t: 'text' },
      { k: 'wholesale_banner', label: 'Wholesale Banner Text', g: 'WHOLESALE', t: 'textarea' },
      { k: 'trust_delivery', label: 'Badge: Fast Delivery (title)', g: 'TRUST BADGES', t: 'text' },
      { k: 'trust_delivery_detail', label: 'Badge: Fast Delivery (detail)', g: 'TRUST BADGES', t: 'text' },
      { k: 'trust_pickup', label: 'Badge: Store Pickup (title)', g: 'TRUST BADGES', t: 'text' },
      { k: 'trust_pickup_detail', label: 'Badge: Store Pickup (detail)', g: 'TRUST BADGES', t: 'text' },
      { k: 'trust_cod', label: 'Badge: COD (title)', g: 'TRUST BADGES', t: 'text' },
      { k: 'trust_cod_detail', label: 'Badge: COD (detail)', g: 'TRUST BADGES', t: 'text' },
      { k: 'trust_gcash', label: 'Badge: GCash (title)', g: 'TRUST BADGES', t: 'text' },
      { k: 'trust_gcash_detail', label: 'Badge: GCash (detail)', g: 'TRUST BADGES', t: 'text' },
      { k: 'delivery_coverage', label: 'Delivery Coverage', g: 'CONTACT / ABOUT', t: 'textarea' },
      { k: 'pickup_address', label: 'Pickup Address', g: 'CONTACT / ABOUT', t: 'textarea' },
      { k: 'phone', label: 'Phone Number', g: 'CONTACT / ABOUT', t: 'text' },
      { k: 'messenger_link', label: 'Messenger Link (m.me/...)', g: 'CONTACT / ABOUT', t: 'text' },
      { k: 'facebook_link', label: 'Facebook Link', g: 'CONTACT / ABOUT', t: 'text' },
      { k: 'about_text', label: 'About Us Text', g: 'CONTACT / ABOUT', t: 'textarea' },
      { k: 'subdivisions', label: 'Subdivisions (isang subdivision bawat linya — para sa locked picker)', g: 'DELIVERY', t: 'textarea' },
      { k: 'fb_embed_home', label: 'Facebook Post Embed (Home) — i-paste ang buong <iframe> code (FB post → ⋯ → Embed → Get Code)', g: 'FACEBOOK', t: 'textarea', rows: 6 },
      { k: 'fb_cta_label', label: 'Button Text (sa ilalim ng post; blangko = walang button)', g: 'FACEBOOK', t: 'text' },
      { k: 'fb_cta_target_type', label: 'Target ng Button', g: 'FACEBOOK', t: 'select', options: ['Wala', 'Product', 'Category', 'URL'] },
      { k: 'fb_cta_target_value', label: 'Target Value — Product: Product ID (hal. 310) · Category: category|search (hal. powdered drink|milo choco) · URL: buong link', g: 'FACEBOOK', t: 'text' }
    ],
    get groups() { return ['HERO', 'WHOLESALE', 'TRUST BADGES', 'CONTACT / ABOUT', 'DELIVERY', 'FACEBOOK'] },
    async load() {
      try { this.c = await fetchJSON(API + '/shop/content'); this.loaded = true; } catch (e) { this.loaded = false; }
    },
    async loadSuggestions() {
      try { this.suggestions = await fetchJSON(API + '/subdivision-suggestions?status=pending'); } catch (e) { this.suggestions = []; }
    },
    async approveSuggestion(s) {
      try {
        await fetchJSON(API + '/subdivision-suggestions/' + s.id + '/approve', { method: 'POST' });
        toast('Approved: ' + s.name + ' — nasa picker na!');
        await this.loadSuggestions();
        await this.load();
      } catch (e) { toast(e.message || 'Failed', 'error'); }
    },
    async dismissSuggestion(s) {
      try {
        await fetchJSON(API + '/subdivision-suggestions/' + s.id + '/dismiss', { method: 'POST' });
        await this.loadSuggestions();
      } catch (e) { toast(e.message || 'Failed', 'error'); }
    },
    async save() {
      this.saving = true;
      try {
        const r = await fetchJSON(API + '/shop-content', {
          method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(this.c)
        });
        toast('Shop content saved (' + (r.saved || 0) + ' fields)');
      } catch (e) { toast('Save failed: ' + e.message, 'error'); }
      this.saving = false;
    }
  }));

  Alpine.data('messengerBotPanel', () => ({
    f: { pageId: '', pageToken: '', verifyToken: '', enabled: false },
    hasToken: false, pageTokenMasked: '', webhookUrl: '',
    saving: false, testing: false, loaded: false, msg: '', msgOk: true,
    async load() {
      try {
        const r = await fetchJSON(API + '/messenger/config');
        this.f.pageId = r.pageId || '';
        this.f.pageToken = '';
        this.f.verifyToken = r.verifyToken || '';
        this.f.enabled = !!r.enabled;
        this.hasToken = !!r.hasToken;
        this.pageTokenMasked = r.pageToken || '';
        this.webhookUrl = r.webhookUrl || '';
        this.loaded = true;
      } catch (e) { this.msg = 'Load failed: ' + e.message; this.msgOk = false; }
    },
    async save() {
      this.saving = true; this.msg = '';
      try {
        const body = { pageId: this.f.pageId, pageToken: this.f.pageToken, verifyToken: this.f.verifyToken, enabled: this.f.enabled };
        await fetchJSON(API + '/messenger/config', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        this.msg = 'Messenger bot config saved. ' + (this.hasToken || this.f.pageToken ? 'Pumunta na sa Meta webhook setup (tingnan ang SETUP GUIDE).' : '');
        this.msgOk = true;
        await this.load();
      } catch (e) { this.msg = 'Save failed: ' + e.message; this.msgOk = false; }
      this.saving = false;
    },
    async test() {
      this.testing = true; this.msg = '';
      try {
        const r = await fetchJSON(API + '/messenger/test', { method: 'POST' });
        this.msg = r.ok ? '✅ ' + r.detail : '❌ ' + r.detail;
        this.msgOk = !!r.ok;
      } catch (e) { this.msg = 'Test failed: ' + e.message; this.msgOk = false; }
      this.testing = false;
    }
  }));

});