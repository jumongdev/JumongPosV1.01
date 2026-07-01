  /* ── Warehouse ──────────────────────────────────────── */
  Alpine.data('warehousePanel', () => ({
    tab: 'product', subTab: 'orders', data: [], loading: true, catFilter: '', search: '',
    async init() { window.addEventListener('load-warehouse', () => this.load()); await this.load(); this.startPoll() },
    get endpoint() {
      if (this.tab === 'product' || this.tab === 'inventory') return '/warehouse/products';
      if (this.subTab === 'orders') return '/warehouse/orders';
      if (this.subTab === 'transfers') return '/warehouse/transfers/pending';
      if (this.subTab === 'clients') return '/warehouse/clients';
      return '/warehouse/products';
    },
    async load() {
      this.loading = true;
      try { this.data = await fetchJSON(API + this.endpoint) }
      catch (e) { this.data = [] }
      this.loading = false;
    },
    switchTab(t) { this.tab = t; this.subTab = 'orders'; this.catFilter = ''; this.load() },
    switchSubTab(t) { this.subTab = t; this.catFilter = ''; this.load() },
    get isTransfer() { return this.tab === 'transfer' },
    get categories() { if (this.tab !== 'product') return []; const c = []; this.data.forEach(x => { if (x.category && !c.includes(x.category)) c.push(x.category) }); return c.sort() },
    get filtered() {
      let items = this.data;
      if (this.catFilter && this.tab === 'product') items = items.filter(x => x.category === this.catFilter);
      if (this.search) { const q = this.search.toLowerCase(); items = items.filter(x => JSON.stringify(x).toLowerCase().includes(q)) }
      return items;
    },
    setFilter(cat) { this.catFilter = cat },

    modalOpen: false, modalTitle: '', modalMode: 'add', modalId: null, form: {},

    openAdd() {
      this.modalMode = 'add'; this.modalId = null; this.modalOpen = true;
      if (this.tab === 'product') this.form = { name: '', barcode: '', category: '', boxPrice: 0, boxCost: 0, boxQty: 1, piecePrice: 0, stockQty: 0 };
      else if (this.isTransfer && this.subTab === 'clients') this.form = { name: '', contact: '', address: '', storeType: 'pos', storeId: '' };
      this.modalTitle = this.tab === 'product' ? 'Add Product' : 'Add Client';
    },
    openEdit(id) {
      const p = this.data.find(x => x.id === id); if (!p) return;
      this.modalMode = 'edit'; this.modalId = id; this.modalOpen = true;
      if (this.tab === 'product') {
        this.form = { name: p.name, barcode: p.barcode || '', category: p.category || '', boxPrice: p.boxPrice, boxCost: p.boxCost, boxQty: p.boxQty, piecePrice: p.boxQty > 0 ? (p.boxPrice / p.boxQty).toFixed(2) : p.piecePrice, stockQty: p.stockQty };
      } else if (this.isTransfer && this.subTab === 'clients') {
        this.form = { name: p.name, contact: p.contact || '', address: p.address || '', storeType: p.storeType || 'pos', storeId: p.storeId || '' };
      }
      this.modalTitle = this.tab === 'product' ? 'Edit: ' + p.name : 'Edit: ' + p.name;
      this._computePiecePrice();
    },
    closeModal() { this.modalOpen = false },
    _computePiecePrice() {
      const bp = parseFloat(this.form.boxPrice) || 0, bq = parseInt(this.form.boxQty) || 1;
      if (bp && bq) this.form.piecePrice = (bp / bq).toFixed(2);
    },
    get _entityType() {
      if (this.tab === 'product') return 'products';
      if (this.isTransfer && this.subTab === 'clients') return 'clients';
      return 'products';
    },
    async save() {
      try {
        const baseUrl = API + '/warehouse/' + this._entityType;
        const method = this.modalId ? 'PUT' : 'POST';
        const url = this.modalId ? baseUrl + '/' + this.modalId : baseUrl;
        const body = this._entityType === 'products'
          ? { name: this.form.name, barcode: this.form.barcode, category: this.form.category, boxPrice: parseFloat(this.form.boxPrice) || 0, boxCost: parseFloat(this.form.boxCost) || 0, boxQty: parseInt(this.form.boxQty) || 1, piecePrice: parseFloat(this.form.piecePrice) || 0 }
          : { name: this.form.name, contact: this.form.contact, address: this.form.address, storeType: this.form.storeType, storeId: this.form.storeId };
        const r = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (!r.ok) throw new Error('Failed');
        if (this._entityType === 'products' && !this.modalId) {
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
        const baseUrl = API + '/warehouse/' + this._entityType;
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
    masterImportOpen: false, masterImportList: [], masterSearch: '', importBoxQty: 12,
    closeImport() { this.masterImportOpen = false; this.masterSearch = '' },
    async doImport(mid) {
      try {
        const r = await fetch(API + '/warehouse/products/from-master/' + mid + '?boxQty=' + this.importBoxQty, { method: 'POST' });
        const j = await r.json();
        if (j.id) { toast('Imported (ID: ' + j.id + ')', 'success'); this.masterImportOpen = false; this.load() }
        else toast('Failed to import', 'error');
      } catch (e) { toast('Error: ' + e.message, 'error') }
    },
    async doBulkImport(category) {
      if (!confirm('Import all products from "' + category + '" (boxQty=' + this.importBoxQty + ')?\nAlready imported products will be skipped.')) return;
      try {
        const r = await fetch(API + '/warehouse/products/from-master/category/' + encodeURIComponent(category) + '?boxQty=' + this.importBoxQty, { method: 'POST' });
        const j = await r.json();
        toast('Imported ' + (j.imported || 0) + ' product(s)', 'success');
        this.masterImportOpen = false;
        this.load();
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
      setInterval(() => { if (this.tab === 'transfer' && (this.subTab === 'orders' || this.subTab === 'transfers')) this.load(); this.updateBadge() }, 30000);
    }
  }));
