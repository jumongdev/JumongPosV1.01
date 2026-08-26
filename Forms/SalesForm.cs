using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class SalesForm : Form
{
    private readonly BindingSource _cartSource = new();
    private readonly BindingList<SaleItem> _cart = new();
    private readonly User? _currentUser;
    private Customer? _selectedCustomer;
    private string _orderType = "Walk-in";
    private bool _displayVisible;
    private System.Windows.Forms.Timer _barcodeTimer = null!;
    private decimal _discountPercent;
    private decimal _taxRate;
    private Label _lblUpdateBanner = null!;
    private Label _lblMasterBanner = null!;
    private Label _lblCustomerBanner = null!;

    private static Color CTopbar       => ThemeManager.Current.TopbarBg;
    private static Color CTopbarChip   => ThemeManager.Current.TopbarChip;
    private static Color CTopbarBorder => ThemeManager.Current.TopbarBorder;
    private static Color CTopbarText   => ThemeManager.Current.TopbarText;
    private static Color CTopbarAccent => ThemeManager.Current.TopbarAccent;
    private static Color CSurface      => ThemeManager.Current.SurfaceBg;
    private static Color CCard         => ThemeManager.Current.CardBg;
    private static Color CBorder       => ThemeManager.Current.BorderColor;
    private static Color CBorderLight  => ThemeManager.Current.BorderLight;
    private static Color CText         => ThemeManager.Current.TextPrimary;
    private static Color CTextMuted    => ThemeManager.Current.TextSecondary;
    private static Color CTextHint     => ThemeManager.Current.TextHint;
    private static Color CGreenDark    => ThemeManager.Current.StatusGreenDark;
    private static Color CGreenMid     => ThemeManager.Current.StatusGreenMid;
    private static Color CBlueLight    => ThemeManager.Current.StatusBlueLight;
    private static Color CBlueMid      => ThemeManager.Current.StatusBlueMid;
    private static Color CBlueDark     => ThemeManager.Current.StatusBlueDark;
    private static Color CInputBg      => ThemeManager.Current.InputBg;
    private static Color CInputFg      => ThemeManager.Current.InputFg;
    private static Color CRedLight     => ThemeManager.Current.StatusRedLight;
    private static Color CRedDark      => ThemeManager.Current.StatusRedDark;
    private static Color CAmberLight   => ThemeManager.Current.StatusAmberLight;
    private static Color CAmberDark    => ThemeManager.Current.StatusAmberDark;
    private static Color CAmberMid     => ThemeManager.Current.StatusAmberMid;

    public SalesForm(User? user)
    {
        _currentUser = user;
        InitializeComponent();
        UpdateTotals();
        txtBarcode.Focus();

        _barcodeTimer = new System.Windows.Forms.Timer { Interval = 180 };
        _barcodeTimer.Tick += (_, _) =>
        {
            _barcodeTimer.Stop();
            ProcessBarcodeInput();
        };
        var promoCheckTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        promoCheckTimer.Tick += async (_, _) => await FetchCloudPromoAsync();
        promoCheckTimer.Start();
        DebugHelper.AddFormLabel(this);
        try
        {
            using var trConn = DatabaseHelper.GetConnection();
            trConn.Open();
            using var trCmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'TaxRate'", trConn);
            var val = trCmd.ExecuteScalar()?.ToString();
            if (decimal.TryParse(val, out var tr) && tr > 0) _taxRate = tr;
        }
        catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var screen = Screen.FromControl(this);
        Location = screen.WorkingArea.Location;
        PromptNextTransaction();

        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var (available, _, _, _) = await UpdateService.CheckUpdate();
            if (available && !IsDisposed && IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    _lblUpdateBanner.Text = "APP UPDATE";
                    _lblUpdateBanner.Visible = true;
                    ResizeTopbar();
                });
            }
        }
        catch { }

        try
        {
            var count = await SyncService.CountPendingMasterUpdates();
            if (count > 0 && !IsDisposed && IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    _lblMasterBanner.Text = $"MASTER: {count} NEW";
                    _lblMasterBanner.Tag = count;
                    _lblMasterBanner.Visible = true;
                    ResizeTopbar();
                });
            }
        }
        catch { }

        try
        {
            var custCount = await SyncService.CountPendingCustomerUpdates();
            if (custCount > 0 && !IsDisposed && IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    _lblCustomerBanner.Text = $"CUSTOMERS: {custCount} NEW";
                    _lblCustomerBanner.Tag = custCount;
                    _lblCustomerBanner.Visible = true;
                    ResizeTopbar();
                });
            }
        }
        catch { }
    }

    private bool PromptNextTransaction()
    {
        if (!PromptOrderType())
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return false;
        }
        return true;
    }

    private bool PromptOrderType()
    {
        while (true)
        {
            using var otForm = new OrderTypeForm();
            if (otForm.ShowDialog() != DialogResult.OK) return false;

            _orderType = otForm.SelectedType;
            if (_orderType == "Walk-in") return true;

            using var custForm = new SelectCustomerForm(_orderType);
            if (custForm.ShowDialog() != DialogResult.OK) continue;

            _selectedCustomer = custForm.SelectedCustomer;
            UpdateCustomerDisplay();
            return true;
        }
    }

    private void UpdateCustomerDisplay()
    {
        if (_selectedCustomer != null)
        {
            var c = _selectedCustomer;
            var eligible = !string.IsNullOrEmpty(c.QrCode) ? "  ·  ⭐EARN" : "  ·  ⛔no points";
            lblCustomerInfo.Text = $"{c.Name}  ·  {c.DisplayPhone}  ·  Credit: \u20b1{c.CreditBalance:N2}  ·  Points: {c.LoyaltyPoints}{eligible}";
            lblCustomerInfo.ForeColor = _orderType == "Online" ? CAmberMid : CBlueMid;
            lblOrderChip.Text = _orderType;
            lblOrderChip.Visible = true;
        }
        else
        {
            lblCustomerInfo.Text = "Walk-in customer";
            lblCustomerInfo.ForeColor = CTextMuted;
            lblOrderChip.Text = "Walk-in";
            lblOrderChip.Visible = false;
            lblOrderChip.ForeColor = CTextMuted;
        }
        // SCAN QR ay para sa customer track lang — nakatago sa walk-in (walang customer)
        if (_btnScanQr != null) _btnScanQr.Visible = _selectedCustomer != null;
    }

    // 📱 Customer QR scan (barcode scanner input) — online-registered customers earn points
    private void ScanCustomerQr()
    {
        using var qrForm = new Form
        {
            Text = "📱 Scan Customer QR",
            Width = 380,
            Height = 160,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = CCard
        };
        var lbl = new Label { Text = "I-scan ang QR code ng customer (o i-type ang Customer ID):", Top = 14, Left = 16, Width = 330, ForeColor = CText };
        var txt = new TextBox { Top = 44, Left = 16, Width = 330, BackColor = CInputBg, ForeColor = CText, BorderStyle = BorderStyle.FixedSingle };
        var ok = new Button { Text = "ATTACH", Top = 82, Left = 200, Width = 140, BackColor = CBlueMid, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Cancel", Top = 82, Left = 16, Width = 120, FlatStyle = FlatStyle.Flat };
        qrForm.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        ok.Click += (s, e) => { qrForm.DialogResult = DialogResult.OK; };
        cancel.Click += (s, e) => { qrForm.DialogResult = DialogResult.Cancel; };
        txt.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { qrForm.DialogResult = DialogResult.OK; } };
        if (qrForm.ShowDialog(this) != DialogResult.OK) return;
        var code = txt.Text.Trim();
        if (code.Length == 0) return;
        var customer = CustomerService.GetByQrCode(code);
        if (customer == null)
        {
            MessageBox.Show("Hindi nahanap ang customer. Siguraduhin na ang customer ay naka-register online (may QR) at na-download ang customer list (Settings → SYNC FROM CLOUD).", "Customer Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _selectedCustomer = customer;
        _orderType = "Walk-in";
        UpdateCustomerDisplay();
        MessageBox.Show($"{customer.Name} attached — {(string.IsNullOrEmpty(customer.QrCode) ? "⛔ hindi mag-e-earn ng points" : "⭐ eligible sa points")}", "Customer Attached", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetupCartGrid()
    {
        dgvCart.AutoGenerateColumns = false;
        dgvCart.Columns.Clear();

        dgvCart.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RowNum", HeaderText = "#", ReadOnly = true,
            Width = 32, AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 10F),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                ForeColor = CTextHint
            }
        });

        dgvCart.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = "", Width = 30,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            FlatStyle = FlatStyle.Flat,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = CRedLight,
                ForeColor = CRedDark,
                SelectionBackColor = Color.FromArgb(240, 180, 180),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Padding = new Padding(0), Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            UseColumnTextForButtonValue = true, Text = "\u00d7"
        });

        dgvCart.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "ProductName", HeaderText = "Item",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 50,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = CText,
                Padding = new Padding(6, 0, 0, 0)
            }
        });

        dgvCart.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "UnitName", HeaderText = "Unit", ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 8,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 10F), ForeColor = CTextMuted,
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        });

        dgvCart.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Price", HeaderText = "Price", ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 12,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = "N2", Font = new Font("Segoe UI", 10F), ForeColor = CTextMuted,
                Alignment = DataGridViewContentAlignment.MiddleRight
            }
        });

        dgvCart.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Quantity", HeaderText = "Qty", ReadOnly = false,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 9,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = CBlueDark,
                BackColor = CBlueLight, SelectionBackColor = Color.FromArgb(180, 210, 245),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        });

        dgvCart.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "TotalPrice", HeaderText = "Total", ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 21,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = "N2", Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = CText, Alignment = DataGridViewContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            }
        });

        _cartSource.DataSource = _cart;
        dgvCart.DataSource = _cartSource;

        dgvCart.BackgroundColor = CCard;
        dgvCart.BorderStyle = BorderStyle.None;
        dgvCart.GridColor = CBorderLight;
        dgvCart.DefaultCellStyle.ForeColor = CText;
        dgvCart.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 250);
        dgvCart.DefaultCellStyle.SelectionForeColor = CText;

        dgvCart.ColumnHeadersDefaultCellStyle.BackColor = CSurface;
        dgvCart.ColumnHeadersDefaultCellStyle.ForeColor = CTextMuted;
        dgvCart.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvCart.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvCart.ColumnHeadersHeight = 32;
        dgvCart.EnableHeadersVisualStyles = false;
        dgvCart.RowTemplate.Height = 40;
        dgvCart.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 249, 253);
        dgvCart.RowHeadersVisible = false;
        dgvCart.AllowUserToAddRows = false;
        dgvCart.AllowUserToDeleteRows = false;
        dgvCart.ReadOnly = false;
        dgvCart.EditMode = DataGridViewEditMode.EditProgrammatically;
        dgvCart.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

        dgvCart.CellFormatting += dgvCart_RowNumFormatting;
        dgvCart.CellContentClick += dgvCart_CellContentClick;
        dgvCart.CellClick += dgvCart_CellClick;
    }

    private void dgvCart_RowNumFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.ColumnIndex == dgvCart.Columns["RowNum"]?.Index && e.RowIndex >= 0)
        {
            e.Value = (dgvCart.Rows.Count - e.RowIndex).ToString();
            e.FormattingApplied = true;
        }
    }

    private void dgvCart_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex == 1 && e.RowIndex >= 0 &&
            dgvCart.Rows[e.RowIndex].DataBoundItem is SaleItem item)
        {
            if (MessageBox.Show($"Remove '{item.ProductName}' from cart?", "Confirm Remove",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _cart.Remove(item);
                RefreshCart();
            }
        }
    }

    private void dgvCart_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex != 5 || e.RowIndex < 0) return;
        if (dgvCart.Rows[e.RowIndex].DataBoundItem is not SaleItem item) return;
        ShowQtyDialog(item);
    }

    private void ShowQtyDialog(SaleItem item)
    {
        using var qtyForm = new Form
        {
            Text = "Edit Quantity",
            Size = new Size(300, 160),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(248, 249, 252)
        };

        var lbl = new Label
        {
            Text = item.ProductName,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CText,
            Location = new Point(16, 16),
            Size = new Size(260, 20)
        };
        var nud = new NumericUpDown
        {
            Location = new Point(16, 42), Size = new Size(110, 28),
            Minimum = 1, Maximum = 999999, Value = item.Quantity,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        var btnOk = new Button
        {
            Text = "Update", Location = new Point(136, 40), Size = new Size(72, 30),
            DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = CGreenMid, ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand
        };
        var btnCancel = new Button
        {
            Text = "Cancel", Location = new Point(214, 40), Size = new Size(68, 30),
            DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand
        };

        qtyForm.Shown += (_, _) => nud.Select(0, nud.Text.Length);
        qtyForm.Controls.AddRange(new Control[] { lbl, nud, btnOk, btnCancel });
        qtyForm.AcceptButton = btnOk;
        qtyForm.CancelButton = btnCancel;

        if (qtyForm.ShowDialog() != DialogResult.OK) return;

        var newQty = (int)nud.Value;
        var otherPieces = _cart
            .Where(x => x.ProductId == item.ProductId && x != item)
            .Sum(x => x.Quantity * x.QtyPerUnit);
        var totalPieces = otherPieces + newQty * item.QtyPerUnit;

        var prod = ProductService.GetById(item.ProductId);
        var available = prod != null ? AvailablePieces(prod.Id, prod.StockQty) : 0;
        if (prod != null && totalPieces > available)
        {
            MessageBox.Show(
                $"Insufficient stock for '{prod.Name}'.\nRequested: {totalPieces} pcs\nAvailable: {available} pcs{HeldNote(item.ProductId)}",
                "Out of Stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        item.Quantity = newQty;
        item.TotalPrice = item.Price * item.Quantity;
        RefreshCart();
    }

    private void txtBarcode_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
        {
            e.SuppressKeyPress = true;
            _barcodeTimer.Stop();
            ProcessBarcodeInput();
        }
        else if (!char.IsControl((char)Keys.None) &&
                 e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.ControlKey)
        {
            _barcodeTimer.Stop();
            _barcodeTimer.Start();
        }
    }

    private void ProcessBarcodeInput()
    {
        var input = txtBarcode.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        var qty = 1;
        var barcode = input;
        var star = input.IndexOf('*');
        if (star > 0 && int.TryParse(input[..star], out var parsed) && parsed > 0)
        {
            qty = parsed;
            barcode = input[(star + 1)..];
        }

        if (barcode.Length > 30 || barcode.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            txtBarcode.Clear();
            txtBarcode.Focus();
            return;
        }

        AddProductByBarcode(barcode, qty);
        txtBarcode.Clear();
        txtBarcode.Focus();
    }

    private ProductUnit? GetUnitForProduct(Product product)
    {
        var units = ProductUnitService.GetByProduct(product.Id);
        if (units.Count == 0) return null;
        if (units.Count == 1) return units[0];
        using var form = new SelectUnitForm(product.Name, units);
        return form.ShowDialog() == DialogResult.OK ? form.SelectedUnit : null;
    }

    private void AddProductByBarcode(string barcode, int quantity = 1)
    {
        if (string.IsNullOrEmpty(barcode)) return;

        var product = ProductService.GetByBarcode(barcode);
        if (product == null)
        {
            var result = MessageBox.Show(
                $"Product with barcode '{barcode}' not found.\nAdd new product?",
                "Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                using var form = new ProductsForm(_currentUser, barcode);
                form.ShowDialog();
            }
            return;
        }

        var unit = GetUnitForProduct(product);
        if (unit != null || ProductUnitService.GetByProduct(product.Id).Count == 0)
            AddToCart(product, unit, quantity);
    }

    private void AddToCart(Product product, ProductUnit? unit = null, int quantity = 1)
    {
        var price = unit?.Price ?? product.Price;
        var unitName = unit?.UnitName ?? "";
        var qtyPerUnit = unit?.QtyPerUnit ?? 1;
        var ptsExempt = product.PointsExempt;
        var ptsPerUnit = unit?.PointsPerUnit ?? product.PointsPerUnit;

        var cartPieces = _cart
            .Where(x => x.ProductId == product.Id)
            .Sum(x => x.Quantity * x.QtyPerUnit);
        var newPieces = cartPieces + quantity * qtyPerUnit;

        var available = AvailablePieces(product.Id, product.StockQty);
        if (newPieces > available)
        {
            MessageBox.Show(
                $"Insufficient stock for '{product.Name}'.\nRequested: {newPieces} pcs\nAvailable: {available} pcs{HeldNote(product.Id)}",
                "Out of Stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var existing = _cart.FirstOrDefault(x => x.ProductId == product.Id && x.UnitName == unitName);
        if (existing != null)
        {
            _cart.Remove(existing);
            existing.Quantity += quantity;
            existing.TotalPrice = existing.Price * existing.Quantity;
            _cart.Insert(0, existing);
        }
        else
        {
            _cart.Insert(0, new SaleItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Barcode = product.Barcode,
                Price = price,
                UnitName = unitName,
                QtyPerUnit = qtyPerUnit,
                Quantity = quantity,
                TotalPrice = price * quantity,
                UnitCost = product.Cost * qtyPerUnit,
                PointsExempt = ptsExempt,
                PointsPerUnit = ptsPerUnit
            });
        }
        RefreshCart();
    }

    /// <summary>
    /// HQ-only: live available pieces = min(local mirror, server live stock) so a concurrent
    /// mobile/ecommerce deduction AND the HQ's own not-yet-pushed sales are both respected.
    /// Non-HQ stores (and server failures) simply use the local stock.
    /// </summary>
    private int AvailablePieces(int productId, int localStock)
    {
        if (SyncService.StoreId != "STORE-20260602-7159") return localStock;
        var live = SyncService.GetLiveStockAsync(new[] { productId }).GetAwaiter().GetResult();
        return live.TryGetValue(productId, out var serverQty) ? Math.Min(localStock, serverQty) : localStock;
    }

    /// <summary>
    /// HQ-only: kung may aktibong online order na may hawak na stock ng product na 'to, ibalik ang
    /// note para sa guard message (e.g. " — 5 pcs naka hold sa online order"). Empty kung wala.
    /// </summary>
    private string HeldNote(int productId)
    {
        if (SyncService.StoreId != "STORE-20260602-7159") return "";
        try
        {
            var held = SyncService.GetHeldStockAsync(new[] { productId }).GetAwaiter().GetResult();
            if (held.TryGetValue(productId, out var pcs) && pcs > 0)
                return $" — {pcs} pcs naka hold sa online order";
        }
        catch { }
        return "";
    }

    private void ShowSearchPopup(string initialSearch)
    {
        using var form = new ProductSearchForm();
        form.InitialSearchText = initialSearch;
        if (form.ShowDialog() == DialogResult.OK && form.SelectedProduct != null)
        {
            var product = form.SelectedProduct;
            var unit = GetUnitForProduct(product);
            if (unit != null || ProductUnitService.GetByProduct(product.Id).Count == 0)
                AddToCart(product, unit);
        }
        txtBarcode.Focus();
    }

    

    private void RefreshCart()
    {
        _cartSource.ResetBindings(false);
        UpdateTotals();

        BeginInvoke(new Action(() =>
        {
            if (dgvCart.Rows.Count > 0)
            {
                dgvCart.FirstDisplayedScrollingRowIndex = 0;
                dgvCart.ClearSelection();
            }
        }));
    }

    private void UpdateTotals()
    {
        var subTotal = _cart.Sum(x => x.TotalPrice);
        _discountPercent = 0; // discount disabled — prices locked to master catalog
        var discountAmt = 0m;
        var afterDiscount = subTotal;
        var taxAmt = afterDiscount * _taxRate / 100;
        var grandTotal = afterDiscount + taxAmt;
        var totalQty = _cart.Sum(x => x.Quantity);
        lblSubTotal.Text = $"\u20b1{subTotal:N2}";
        lblDiscountVal.Text = "—";
        lblDiscountVal.ForeColor = CTextHint;
        lblTaxVal.Text = _taxRate > 0 ? $"\u20b1{taxAmt:N2}" : "—";
        lblTaxVal.ForeColor = _taxRate > 0 ? Color.FromArgb(243, 156, 18) : CTextHint;
        lblGrandTotal.Text = $"\u20b1{grandTotal:N2}";
        btnPay.Text = $"Charge  \u20b1{grandTotal:N2}";
        lblCartMeta.Text = $"{_cart.Count} item(s)  ·  {totalQty} pcs  ·  click qty to edit";
    }

    private void btnRemove_Click(object? sender, EventArgs e)
    {
        if (dgvCart.CurrentRow?.DataBoundItem is SaleItem item)
        {
            _cart.Remove(item);
            RefreshCart();
        }
    }

    private void btnClear_Click(object? sender, EventArgs e)
    {
        if (_cart.Count == 0) return;
        if (MessageBox.Show("Clear all items from cart?", "Clear Cart",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _cart.Clear();
        RefreshCart();
    }

    private void btnHold_Click(object? sender, EventArgs e)
    {
        if (_cart.Count == 0)
        {
            MessageBox.Show("Cart is empty.", "Hold Cart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand(
            "INSERT INTO HeldCarts (OrderType, CustomerId, CustomerName, ItemsJson) VALUES (@type, @custId, @custName, @json)", conn);
        cmd.Parameters.AddWithValue("@type", _orderType);
        cmd.Parameters.AddWithValue("@custId", (object?)_selectedCustomer?.Id ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@custName", _selectedCustomer?.Name ?? "");
        cmd.Parameters.AddWithValue("@json", Models.HeldCart.SerializeItems(_cart.ToList()));
        cmd.ExecuteNonQuery();

        _cart.Clear();
        RefreshCart();
        _selectedCustomer = null;
        _orderType = "Walk-in";
        UpdateCustomerDisplay();
        MessageBox.Show("Cart held successfully!", "Hold Cart", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void btnRetrieve_Click(object? sender, EventArgs e)
    {
        using var form = new RetrieveHeldCartForm();
        if (form.ShowDialog() != DialogResult.OK || form.SelectedCart == null) return;

        var held  = form.SelectedCart;
        var items = held.DeserializeItems();
        if (items.Count == 0)
        {
            MessageBox.Show("Held cart has no items.", "Retrieve", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var stockIssues = new List<string>();
        foreach (var item in items)
        {
            var prod = ProductService.GetById(item.ProductId);
            if (prod == null) { stockIssues.Add($"'{item.ProductName}' — product no longer exists"); continue; }
            var totalPieces = items.Where(x => x.ProductId == item.ProductId).Sum(x => x.Quantity * x.QtyPerUnit);
            if (totalPieces > prod.StockQty)
            {
                var msg = $"'{prod.Name}' — needs {totalPieces} pcs, only {prod.StockQty} available{HeldNote(item.ProductId)}";
                if (!stockIssues.Contains(msg)) stockIssues.Add(msg);
            }
        }

        if (stockIssues.Count > 0)
        {
            var warn = "Some items have stock issues:\n\n" + string.Join("\n", stockIssues) +
                       "\n\nRetrieve anyway? Adjust quantities in cart as needed.";
            if (MessageBox.Show(warn, "Stock Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
        }

        _cart.Clear();
        foreach (var item in items) _cart.Add(item);

        _selectedCustomer = held.CustomerId.HasValue
            ? CustomerService.GetById(held.CustomerId.Value) : null;
        _orderType = held.OrderType;
        UpdateCustomerDisplay();
        RefreshCart();

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var del = new SQLiteCommand("DELETE FROM HeldCarts WHERE Id = @id", conn);
        del.Parameters.AddWithValue("@id", held.Id);
        del.ExecuteNonQuery();

        var msgOk = $"Cart restored: {items.Count} item(s)";
        if (stockIssues.Count > 0) msgOk += "\nWarning: some items had insufficient stock. Adjust before checkout.";
        MessageBox.Show(msgOk, "Retrieve", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void btnPay_Click(object? sender, EventArgs e)
    {
        if (_cart.Count == 0)
        {
            MessageBox.Show("Cart is empty.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_paying) return;
        _paying = true;
        btnPay.Enabled = false;

        // HQ live stock guard: verify the whole cart against min(local, server) before payment
        var liveIssues = new List<string>();
        var liveStock = SyncService.GetLiveStockAsync(_cart.Select(x => x.ProductId)).GetAwaiter().GetResult();
        foreach (var g in _cart.GroupBy(x => x.ProductId))
        {
            var prod = ProductService.GetById(g.Key);
            if (prod == null) continue;
            var pieces = g.Sum(x => x.Quantity * x.QtyPerUnit);
            var available = Math.Min(prod.StockQty, liveStock.TryGetValue(g.Key, out var sv) ? sv : prod.StockQty);
            if (pieces > available)
                liveIssues.Add($"'{prod.Name}' — needs {pieces} pcs, only {available} available (live check){HeldNote(g.Key)}");
        }
        if (liveIssues.Count > 0)
        {
            var warn = "Stock changed on the server since adding to cart:\n\n" + string.Join("\n", liveIssues) +
                       "\n\nRemove/adjst quantities before paying.";
            MessageBox.Show(warn, "Stock Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _paying = false;
            btnPay.Enabled = true;
            return;
        }

        var subTotal = _cart.Sum(x => x.TotalPrice);
        _discountPercent = 0; // discount disabled — prices locked to master catalog
        var discountAmt = 0m;
        var afterDiscount = subTotal;
        var taxAmt = afterDiscount * _taxRate / 100;
        var grandTotal = afterDiscount + taxAmt;
        using var payForm = new PaymentForm(grandTotal, _selectedCustomer);
        if (payForm.ShowDialog() != DialogResult.OK) { _paying = false; btnPay.Enabled = true; return; }

        var sale = new Sale
        {
            InvoiceNo     = SaleService.GenerateInvoiceNo(),
            SaleDate      = TimeHelper.Now,
            SubTotal      = subTotal,
            Discount      = discountAmt,
            Tax           = taxAmt,
            GrandTotal    = grandTotal,
            AmountPaid    = payForm.AmountPaid,
            Change        = payForm.Change,
            PaymentMethod = payForm.PaymentMethod,
            ReferenceNo   = payForm.PaymentMethod == "Split" ? payForm.EwReferenceNo : payForm.ReferenceNo,
            CashPaid      = payForm.CashPaid,
            EwPaid        = payForm.EwPaid,
            OrderType     = _orderType,
            CustomerId    = _selectedCustomer?.Id,
            CustomerName  = _selectedCustomer?.Name ?? "",
            UserId        = _currentUser?.Id,
            CashierName   = _currentUser?.FullName ?? _currentUser?.Username ?? "",
            Items         = new List<SaleItem>(_cart)
        };

        var saleId = SaleService.SaveSale(sale);

        if (_selectedCustomer != null)
        {
            // Points ONLY for online-registered customers (may QR code); manual/walk-in accounts = zero points
            if (!string.IsNullOrEmpty(_selectedCustomer.QrCode))
            {
                var pointsRate = int.Parse(DatabaseHelper.GetSetting("PointsRate", "200"));
                var ptsEarned = 0;
                foreach (var item in _cart)
                {
                    if (item.PointsExempt) continue;
                    if (item.PointsPerUnit > 0)
                        ptsEarned += item.PointsPerUnit * item.Quantity;
                    else
                        ptsEarned += (int)(item.TotalPrice / pointsRate);
                }
                var ptsUsed = payForm.PointsUsed;
                var newPts = _selectedCustomer.LoyaltyPoints + ptsEarned - ptsUsed;
                if (newPts < 0) newPts = 0;
                _selectedCustomer.LoyaltyPoints = newPts;
                CustomerService.UpdateLoyaltyPoints(_selectedCustomer.Id, newPts);
            }
        }

        if (payForm.PaymentMethod == "Credit" && _selectedCustomer != null)
        {
            var cashierName = _currentUser?.FullName ?? _currentUser?.Username ?? "";
            CreditService.AddTransaction(
                _selectedCustomer.Id, saleId, "Sale",
                $"Invoice {sale.InvoiceNo} - {sale.Items.Count} item(s)",
                grandTotal, userId: _currentUser?.Id ?? 0, userName: cashierName);
            _selectedCustomer.CreditBalance += grandTotal;
        }

        {
            using var psConn = DatabaseHelper.GetConnection();
            psConn.Open();
            using var psCmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'PrintReceipt'", psConn);
            var printSetting = psCmd.ExecuteScalar()?.ToString() ?? "True";
            if (printSetting == "True")
            {
                var cashierName = _currentUser?.FullName ?? _currentUser?.Username ?? "Admin";
                PrinterService.PrintReceipt(sale, cashierName, _selectedCustomer);
            }
        }

        if (_selectedCustomer != null && !string.IsNullOrWhiteSpace(_selectedCustomer.Email)
            && MessageBox.Show("Email receipt to customer?", "Email Receipt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            var emailSvc = new EmailService();
            var emailError = emailSvc.SendReceipt(sale, _selectedCustomer, _cart.ToList());
            if (emailError != null)
                MessageBox.Show($"Failed to send email: {emailError}", "Email Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("Receipt sent to " + _selectedCustomer.Email, "Email Sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        _cart.Clear();
        RefreshCart();

        var msg = payForm.PaymentMethod == "Credit"
            ? $"Credit sale complete!\nInvoice: {sale.InvoiceNo}\nCharged to {_selectedCustomer!.Name}: \u20b1{grandTotal:N2}"
            : $"Sale complete!\nInvoice: {sale.InvoiceNo}\nChange: \u20b1{sale.Change:N2}";
        MessageBox.Show(msg, "Sale Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _selectedCustomer = null;
        _orderType = "Walk-in";
        UpdateCustomerDisplay();
        PromptNextTransaction();
        _paying = false;
        btnPay.Enabled = true;
    }

    private void InitializeComponent()
    {
        Text = "Jumong POS";
        StartPosition = FormStartPosition.Manual;
        WindowState = FormWindowState.Maximized;
        BackColor = CSurface;
        Font = new Font("Segoe UI", 10F);

        _pnlTopbar = new Panel { BackColor = CTopbar };

        var lblBrand = new Label
        {
            Text = "Jumong POS",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = CTopbarAccent,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var cashierName = _currentUser?.FullName ?? _currentUser?.Username ?? "Admin";
        var lblCashierName = new Label
        {
            Text = cashierName,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 200, 255),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblTime = new Label
        {
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(100, 100, 130),
            TextAlign = ContentAlignment.MiddleRight
        };

        _lblUpdateBanner = new Label
        {
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(231, 76, 60),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Visible = false,
            Padding = new Padding(6, 0, 6, 0)
        };
        _lblUpdateBanner.Click += async (_, _) =>
        {
            try
            {
                var (available, version, _, downloadUrl) = await UpdateService.CheckUpdate();
                if (!available)
                {
                    MessageBox.Show("You're on the latest version, or cannot check right now.", "Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (string.IsNullOrEmpty(downloadUrl)) return;
                var result = MessageBox.Show($"New version {version} available!\n\nDownload and install update?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    SettingsForm.ShowUpdateProgress(version ?? "", downloadUrl);
            }
            catch { }
        };

        _lblMasterBanner = new Label
        {
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(243, 156, 18),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Visible = false,
            Padding = new Padding(6, 0, 6, 0)
        };
        _lblMasterBanner.Click += async (_, _) =>
        {
            if (_lblMasterBanner.Tag is int count && count > 0)
            {
                var result = MessageBox.Show($"There are {count} new/updated products available.\nRun 'Update Master Catalog' now?", "Master Catalog", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _lblMasterBanner.Visible = false;
                    SettingsForm.ShowSyncProgress("Updating Master Catalog...", SyncService.DownloadUpdatedMasterCatalog);
                }
            }
        };

        _lblCustomerBanner = new Label
        {
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(155, 89, 182),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Visible = false,
            Padding = new Padding(6, 0, 6, 0)
        };
        _lblCustomerBanner.Click += async (_, _) =>
        {
            if (_lblCustomerBanner.Tag is int count && count > 0)
            {
                var result = MessageBox.Show($"There are {count} new customers available.\nSync from cloud now?", "Customers", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _lblCustomerBanner.Visible = false;
                    SettingsForm.ShowSyncProgress("Downloading Customers...", async p =>
                    {
                        p.Report("Downloading customer list from cloud...");
                        return await SyncService.DownloadCustomersAsync();
                    });
                }
            }
        };

        _pnlTopbar.Controls.AddRange(new Control[] { lblBrand, lblCashierName, _lblUpdateBanner, _lblMasterBanner, _lblCustomerBanner, _lblTime });

        _pnlCustomerBar = new Panel { BackColor = CCard };
        _pnlCustomerBar.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLight, 1);
            e.Graphics.DrawLine(pen, 0, _pnlCustomerBar.Height - 1, _pnlCustomerBar.Width, _pnlCustomerBar.Height - 1);
        };

        var lblCustIcon = new Label
        {
            Text = "\u25cf",
            Font = new Font("Segoe UI", 16F),
            ForeColor = CBlueMid,
            TextAlign = ContentAlignment.MiddleCenter
        };

        lblCustomerInfo = new Label
        {
            Text = "Walk-in customer",
            Font = new Font("Segoe UI", 9F),
            ForeColor = CTextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        lblOrderChip = new Label
        {
            Text = "Walk-in",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CBlueDark,
            BackColor = CBlueLight,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(6, 2, 6, 2),
            Visible = false,
            AutoSize = true
        };

        var btnScanQr = new Button
        {
            Text = "📱 SCAN QR",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = CBlueMid,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            AutoSize = false,
            Size = new Size(88, 26),
            Margin = new Padding(0),
            Visible = false
        };
        btnScanQr.FlatAppearance.BorderSize = 0;
        btnScanQr.Click += (s, e) => ScanCustomerQr();
        _btnScanQr = btnScanQr;
        UpdateCustomerDisplay();

        _pnlCustomerBar.Controls.AddRange(new Control[] { lblCustIcon, lblCustomerInfo, lblOrderChip, btnScanQr });

        btnScanQr.Dock = DockStyle.Right;
        lblOrderChip.Dock = DockStyle.Right;
        lblCustomerInfo.Dock = DockStyle.Fill;

        _pnlSearch = new Panel { BackColor = CCard };
        _pnlSearch.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLight, 1);
            e.Graphics.DrawLine(pen, 0, _pnlSearch.Height - 1, _pnlSearch.Width, _pnlSearch.Height - 1);
        };

        var lblBarcodeHint = new Label
        {
            Text = "Barcode",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint
        };
        txtBarcode = new TextBox
        {
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = CInputBg,
            ForeColor = CInputFg
        };
        txtBarcode.KeyDown += txtBarcode_KeyDown;

        var lblSearchHint = new Label
        {
            Text = "Search product",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint
        };
        btnSearch = new Button
        {
            Text = "🔍  Search  (F2)",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
            BackColor = CBlueLight,
            ForeColor = CBlueDark,
            Cursor = Cursors.Hand
        };
        btnSearch.Click += (_, _) => ShowSearchPopup("");

        _pnlSearch.Controls.AddRange(new Control[] { lblBarcodeHint, txtBarcode, lblSearchHint, btnSearch });

        _pnlCart = new Panel { BackColor = CSurface };

        var pnlCartHeader = new Panel { BackColor = CCard };
        pnlCartHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLight, 1);
            e.Graphics.DrawLine(pen, 0, pnlCartHeader.Height - 1, pnlCartHeader.Width, pnlCartHeader.Height - 1);
        };

        var lblCartTitle = new Label
        {
            Text = "Cart items",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CTextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        lblCartMeta = new Label
        {
            Text = "0 items",
            Font = new Font("Segoe UI", 8F),
            ForeColor = CTextHint,
            TextAlign = ContentAlignment.MiddleRight
        };

        pnlCartHeader.Controls.AddRange(new Control[] { lblCartTitle, lblCartMeta });

        dgvCart = new DataGridView();
        SetupCartGrid();

        var pnlActions = new Panel { BackColor = CCard };
        pnlActions.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLight, 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlActions.Width, 0);
        };

        btnClear = new Button
        {
            Text = "Clear all",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
            BackColor = CCard,
            ForeColor = CTextMuted,
            Cursor = Cursors.Hand
        };
        btnClear.Click += btnClear_Click;

        btnHold = new Button
        {
            Text = "Hold",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CAmberMid },
            BackColor = CAmberLight,
            ForeColor = CAmberDark,
            Cursor = Cursors.Hand
        };
        btnHold.Click += btnHold_Click;

        btnRetrieve = new Button
        {
            Text = "Retrieve",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(133, 183, 235) },
            BackColor = CBlueLight,
            ForeColor = CBlueDark,
            Cursor = Cursors.Hand
        };
        btnRetrieve.Click += btnRetrieve_Click;

        var pnlShortcuts = new Panel { BackColor = CSurface };
        var shortcuts = new[] { "F1  Scan", "F2  Search", "F3  Edit qty", "F4  Pay", "F5  Hold", "F8  Clear" };
        for (var i = 0; i < shortcuts.Length; i++)
        {
            pnlShortcuts.Controls.Add(new Label
            {
                Text = shortcuts[i],
                Font = new Font("Segoe UI", 8F),
                ForeColor = CTextHint,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = false,
                Tag = i
            });
        }

        pnlActions.Controls.AddRange(new Control[] { btnClear, btnHold, btnRetrieve });
        _pnlCart.Controls.AddRange(new Control[] { pnlCartHeader, dgvCart, pnlActions, pnlShortcuts });

        btnRemove = new Button { Visible = false };
        btnRemove.Click += btnRemove_Click;

        _pnlTotals = new Panel { BackColor = CCard };
        _pnlTotals.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLight, 1);
            e.Graphics.DrawLine(pen, 0, 0, 0, _pnlTotals.Height);
        };

        var lblTotalDueHint = new Label
        {
            Text = "Total due",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CTextMuted
        };

        lblGrandTotal = new Label
        {
            Text = "\u20b10.00",
            Font = new Font("Segoe UI", 28F, FontStyle.Bold),
            ForeColor = CGreenDark,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblInvoiceHint = new Label
        {
            Text = "Walk-in",
            Font = new Font("Segoe UI", 8F),
            ForeColor = CTextHint
        };

        var sep1 = new Panel { BackColor = CBorderLight, Height = 1 };

        var lblSubTotalLbl = new Label
        {
            Text = "Subtotal",
            Font = new Font("Segoe UI", 10F),
            ForeColor = CTextMuted
        };
        lblSubTotal = new Label
        {
            Text = "\u20b10.00",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = CText,
            TextAlign = ContentAlignment.MiddleRight
        };
        var lblDiscountLbl = new Label
        {
            Text = "Discount",
            Font = new Font("Segoe UI", 10F),
            ForeColor = CTextMuted
        };
        lblDiscountLbl.Click += (_, _) => { }; // discount disabled — prices locked to master catalog
        lblDiscountVal = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 10F),
            ForeColor = CTextHint,
            TextAlign = ContentAlignment.MiddleRight
        };
        lblDiscountVal.Click += (_, _) => { }; // discount disabled — prices locked to master catalog

        var lblTaxLbl = new Label
        {
            Text = "Tax",
            Font = new Font("Segoe UI", 10F),
            ForeColor = CTextMuted
        };
        lblTaxVal = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 10F),
            ForeColor = CTextHint,
            TextAlign = ContentAlignment.MiddleRight
        };

        var sep2 = new Panel { BackColor = CBorderLight, Height = 1 };

        btnPay = new Button
        {
            Text = "Charge  \u20b10.00",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = CGreenMid,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnPay.Click += btnPay_Click;

        _pbQr = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Visible = false
        };
        var qrTip = new ToolTip();
        qrTip.SetToolTip(_pbQr, "Click to enlarge");
        _pbQr.Click += (_, _) =>
        {
            if (_pbQr.Image == null) return;
            using var f = new Form
            {
                Text = _lblQrHeader.Text,
                WindowState = FormWindowState.Maximized,
                FormBorderStyle = FormBorderStyle.Sizable,
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.Black
            };
            var pb = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = (Image)_pbQr.Image.Clone() };
            f.Controls.Add(pb);
            f.ShowDialog();
            pb.Image?.Dispose();
        };

        _lblQrHeader = new Label
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = ThemeManager.Current.AccentCyan,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        _btnQrPrev = new Button
        {
            Text = "\u25C0",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(30, 30, 50),
            ForeColor = Color.FromArgb(0, 195, 255),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnQrPrev.Click += (_, _) => { _qrIndex = (_qrIndex - 1 + _qrEntries.Count) % _qrEntries.Count; ShowQrIndex(); };
        _btnQrNext = new Button
        {
            Text = "\u25B6",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(30, 30, 50),
            ForeColor = Color.FromArgb(0, 195, 255),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnQrNext.Click += (_, _) => { _qrIndex = (_qrIndex + 1) % _qrEntries.Count; ShowQrIndex(); };

        _lblPromo = new Label
        {
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(241, 196, 15),
            BackColor = Color.FromArgb(20, 20, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        LoadQrCodes();

        _pnlTotals.Controls.AddRange(new Control[]
        {
            lblTotalDueHint, lblGrandTotal, lblInvoiceHint,
            sep1,
            lblSubTotalLbl, lblSubTotal,
            lblDiscountLbl, lblDiscountVal,
            lblTaxLbl, lblTaxVal,
            sep2,
            btnPay,
            _lblQrHeader, _btnQrPrev, _pbQr, _btnQrNext, _lblPromo
        });

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F1) { txtBarcode.Focus(); txtBarcode.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F2) { ShowSearchPopup(""); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F3)
            {
                if (dgvCart.Rows.Count > 0)
                {
                    var row = dgvCart.CurrentCell != null ? dgvCart.CurrentCell.RowIndex : 0;
                    EditCartItemQuantity(row);
                }
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F4) { btnPay_Click(null!, e); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F5) { btnHold_Click(null!, e); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F8) { btnClear_Click(null!, e); e.SuppressKeyPress = true; }
        };

        Controls.AddRange(new Control[]
        {
            _pnlTopbar, _pnlCustomerBar, _pnlSearch,
            _pnlCart, _pnlTotals
        });

        Resize += (_, _) => LayoutControls();
        LayoutControls();
    }

    private void EditCartItemQuantity(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= dgvCart.Rows.Count) return;
        if (dgvCart.Rows[rowIndex].DataBoundItem is not SaleItem item) return;
        ShowQtyDialog(item);
    }

    private void LayoutControls()
    {
        var w   = ClientSize.Width;
        var h   = ClientSize.Height;
        var gap = 12;

        var topH      = 44;
        var custH     = 42;
        var searchH   = 52;
        var rightW    = Math.Max(280, Math.Min(320, (int)(w * 0.27)));
        var leftW     = w - rightW - 1;
        var cartTop   = topH + custH + searchH;
        var cartH     = h - cartTop;

        _pnlTopbar.Location = new Point(0, 0);
        _pnlTopbar.Size     = new Size(w, topH);

        var tbControls = _pnlTopbar.Controls;
        tbControls[0].Location = new Point(16, 0);
        tbControls[0].Size     = new Size(160, topH);
        tbControls[1].Location = new Point(180, 0);
        tbControls[1].Size     = new Size(180, topH);
        _lblTime.Text     = TimeHelper.Now.ToString("MMM dd, yyyy  h:mm tt");
        _lblTime.Location = new Point(w - 310, 0);
        _lblTime.Size     = new Size(190, topH);
        ResizeTopbar();

        _pnlCustomerBar.Location = new Point(0, topH);
        _pnlCustomerBar.Size     = new Size(leftW, custH);
        var cc = _pnlCustomerBar.Controls;
        cc[0].Location = new Point(12, (custH - 28) / 2); cc[0].Size = new Size(28, 28);
        cc[1].Location = new Point(38, (custH - 20) / 2); cc[1].Size = new Size(leftW - 220, 20);
        cc[2].Location = new Point(leftW - 140, (custH - 20) / 2); cc[2].Size = new Size(120, 20);

        _pnlSearch.Location = new Point(0, topH + custH);
        _pnlSearch.Size     = new Size(leftW, searchH);
        var half = leftW / 2;
        var sc   = _pnlSearch.Controls;
        sc[0].Location = new Point(gap, 4);          sc[0].Size = new Size(80, 14);
        txtBarcode.Location = new Point(gap, 18);    txtBarcode.Size = new Size(half - gap * 2, 28);
        sc[2].Location = new Point(half + gap, 4);   sc[2].Size = new Size(120, 14);
        btnSearch.Location  = new Point(half + gap, 16); btnSearch.Size = new Size(half - gap * 2, 32);

        _pnlCart.Location = new Point(0, cartTop);
        _pnlCart.Size     = new Size(leftW, cartH);

        var pnlCartHdr  = (Panel)_pnlCart.Controls[0];
        var gridCtrl    = dgvCart;
        var pnlActs     = (Panel)_pnlCart.Controls[2];
        var pnlShorts   = (Panel)_pnlCart.Controls[3];

        var actH   = 38;
        var shortH = 24;
        var hdrH   = 28;

        pnlCartHdr.Location = new Point(0, 0);
        pnlCartHdr.Size     = new Size(leftW, hdrH);
        pnlCartHdr.Controls[0].Location = new Point(gap, 6);  pnlCartHdr.Controls[0].Size = new Size(120, 18);
        lblCartMeta.Location = new Point(leftW - 320, 6);     lblCartMeta.Size = new Size(300, 18);

        gridCtrl.Location = new Point(0, hdrH);
        gridCtrl.Size     = new Size(leftW, cartH - hdrH - actH - shortH);

        pnlActs.Location = new Point(0, cartH - actH - shortH);
        pnlActs.Size     = new Size(leftW, actH);
        var btns = pnlActs.Controls;
        btns[0].Location = new Point(gap, 4);        btns[0].Size = new Size(90, 28);
        btns[1].Location = new Point(gap + 98, 4);   btns[1].Size = new Size(80, 28);
        btns[2].Location = new Point(gap + 186, 4);  btns[2].Size = new Size(92, 28);

        pnlShorts.Location = new Point(0, cartH - shortH);
        pnlShorts.Size     = new Size(leftW, shortH);
        var scBtns = pnlShorts.Controls;
        var scW    = 90;
        for (var i = 0; i < scBtns.Count; i++)
        {
            scBtns[i].Location = new Point(gap + i * (scW + 4), 3);
            scBtns[i].Size     = new Size(scW, 18);
        }

        _pnlTotals.Location = new Point(leftW, topH);
        _pnlTotals.Size     = new Size(rightW, h - topH);

        var m   = 18;
        var pw  = rightW - m * 2;
        var rcs = _pnlTotals.Controls;
        var ry  = m;

        rcs[0].Location = new Point(m, ry);        rcs[0].Size = new Size(pw, 18); ry += 22;
        lblGrandTotal.Location = new Point(m, ry); lblGrandTotal.Size = new Size(pw, 54); ry += 58;
        rcs[2].Location = new Point(m, ry);        rcs[2].Size = new Size(pw, 16); ry += 22;
        rcs[3].Location = new Point(m, ry);        rcs[3].Size = new Size(pw, 1);  ry += 12;
        rcs[4].Location = new Point(m, ry);        rcs[4].Size = new Size(pw / 2, 22);
        lblSubTotal.Location = new Point(m + pw / 2, ry); lblSubTotal.Size = new Size(pw / 2, 22); ry += 28;
        rcs[6].Location = new Point(m, ry);        rcs[6].Size = new Size(pw / 2, 22);
        rcs[7].Location = new Point(m + pw / 2, ry); rcs[7].Size = new Size(pw / 2, 22); ry += 28;
        rcs[8].Location = new Point(m, ry);        rcs[8].Size = new Size(pw / 2, 22);
        rcs[9].Location = new Point(m + pw / 2, ry); rcs[9].Size = new Size(pw / 2, 22); ry += 28;
        rcs[10].Location = new Point(m, ry);       rcs[10].Size = new Size(pw, 1); ry += 14;
        btnPay.Location = new Point(m, ry);       btnPay.Size = new Size(pw, 52); ry += 54;

        if (_qrVisible)
        {
            _lblQrHeader.Location = new Point(m, ry);
            _lblQrHeader.Size = new Size(pw, 14);
            _lblQrHeader.Visible = !string.IsNullOrEmpty(_lblQrHeader.Text);
            ry += 16;

            var navW = 22;
            var qrH = Math.Max(50, Math.Min(110, _pnlTotals.Height - ry - 46));
            _btnQrPrev.Location = new Point(m, ry);
            _btnQrPrev.Size = new Size(navW, qrH);
            _btnQrPrev.Visible = _qrEntries.Count > 1;

            _pbQr.Location = new Point(m + navW, ry);
            _pbQr.Size = new Size(pw - navW * 2, qrH);
            _pbQr.Visible = true;

            _btnQrNext.Location = new Point(m + pw - navW, ry);
            _btnQrNext.Size = new Size(navW, qrH);
            _btnQrNext.Visible = _qrEntries.Count > 1;

            ry += qrH + 2;
        }
        else
        {
            _lblQrHeader.Visible = false;
            _pbQr.Visible = false;
            _btnQrPrev.Visible = false;
            _btnQrNext.Visible = false;
        }

        if (!string.IsNullOrEmpty(_promoText))
        {
            _lblPromo.Text = _promoText;
            _lblPromo.Location = new Point(m, ry);
            var availH = Math.Max(36, _pnlTotals.Height - ry - 8);
            var neededH = TextRenderer.MeasureText(_promoText, _lblPromo.Font,
                new Size(pw, availH), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + 8;
            _lblPromo.Size = new Size(pw, Math.Min(neededH, availH));
            _lblPromo.Visible = true;
        }
        else _lblPromo.Visible = false;
    }

    private void LoadQrCodes()
    {
        _qrEntries.Clear();
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'StoreQrCodes'", conn);
            var json = cmd.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(json))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var h = e.TryGetProperty("header", out var hp) ? hp.GetString() ?? "" : "";
                    var f = e.TryGetProperty("file", out var fp) ? fp.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(h) || !string.IsNullOrEmpty(f))
                        _qrEntries.Add((h, f));
                }
            }
            using var promoCmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'PosPromoMessage'", conn);
            _promoText = promoCmd.ExecuteScalar()?.ToString() ?? "";
        }
        catch { }
        _qrIndex = 0;
        _qrVisible = _qrEntries.Count > 0;
        if (_qrVisible) ShowQrIndex();
        _ = FetchCloudPromoAsync();
    }

    private async Task FetchCloudPromoAsync()
    {
        try
        {
            var cloudMsg = await SyncService.FetchPromoMessageAsync();
            if (!string.IsNullOrEmpty(cloudMsg))
            {
                _promoText = cloudMsg;
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(() => LayoutControls());
            }
        }
        catch (Exception ex) { ErrorLogger.Log("FetchCloudPromo", ex); }
    }

    private void ShowQrIndex()
    {
        if (_qrEntries.Count == 0) return;
        var (header, file) = _qrEntries[_qrIndex];
        _lblQrHeader.Text = header;
        var qrPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", file);
        try
        {
            if (File.Exists(qrPath))
            {
                if (_pbQr.Image != null) { _pbQr.Image.Dispose(); _pbQr.Image = null; }
                _pbQr.Image = Image.FromFile(qrPath);
            }
            else
            {
                _pbQr.Image = null;
            }
        }
        catch { _pbQr.Image = null; }
    }

    private void ResizeTopbar()
    {
        var tbControls = _pnlTopbar.Controls;
        var bannerX = 330;
        if (_lblUpdateBanner.Visible)
        {
            _lblUpdateBanner.Location = new Point(bannerX, 8);
            _lblUpdateBanner.Size = new Size(140, 28);
            bannerX += 148;
        }
        if (_lblMasterBanner.Visible)
        {
            _lblMasterBanner.Location = new Point(bannerX, 8);
            _lblMasterBanner.Size = new Size(150, 28);
            bannerX += 156;
        }
        if (_lblCustomerBanner.Visible)
        {
            _lblCustomerBanner.Location = new Point(bannerX, 8);
            _lblCustomerBanner.Size = new Size(160, 28);
        }
    }

    private Panel _pnlTopbar = null!;
    private Panel _pnlCustomerBar = null!;
    private Button _btnScanQr = null!;
    private Panel _pnlSearch = null!;
    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.SurfaceBg;
        ForeColor = t.TextPrimary;
    }

    private Panel _pnlCart = null!;
    private Panel _pnlTotals = null!;
    private Label _lblTime = null!;
    private Label lblCustomerInfo = null!;
    private Label lblOrderChip = null!;
    private Label lblCartMeta = null!;
    private Label lblDiscountVal = null!;
    private Label lblTaxVal = null!;
    private TextBox txtBarcode = null!;
    private Button btnSearch = null!;
    private DataGridView dgvCart = null!;
    private Label lblSubTotal = null!;
    private Label lblGrandTotal = null!;
    private Button btnRemove = null!;
    private Button btnClear = null!;
    private Button btnHold = null!;
    private Button btnRetrieve = null!;
    private bool _paying;
    private Button btnPay = null!;
    private PictureBox _pbQr = null!;
    private Label _lblQrHeader = null!;
    private Button _btnQrPrev = null!;
    private Button _btnQrNext = null!;
    private List<(string header, string file)> _qrEntries = new();
    private int _qrIndex;
    private bool _qrVisible;
    private Label _lblPromo = null!;
    private string _promoText = "";
}
