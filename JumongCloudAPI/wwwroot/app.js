/* ═══════════════════════════════════════════════════════
   Jumong POS Dashboard — App.js
   Alpine.js 3.x Components + Utilities
   ═══════════════════════════════════════════════════════ */

const API = '/api/dashboard';

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
    stock: ['Product,Barcode,Category,Store,Stock,Price,Cost', d => d.map(x => [x.name, x.barcode, x.category, window.shortStore(x.storeId, Alpine.store('app').storeMap[x.storeId]), x.stockQty, x.price, x.cost])],
    analytics: ['Product,Barcode,Category,Unit,Qty Sold,Revenue,Cost,Profit,Margin%', d => d.map(x => [x.productName, x.barcode, x.category, x.unitName || 'pc', x.totalQty, x.totalRevenue, x.totalCost, x.totalProfit, x.marginPct + '%'])]
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


