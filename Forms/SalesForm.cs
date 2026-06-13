using System.ComponentModel;
using System.Data.SQLite;
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
    private readonly CustomerDisplayForm _customerDisplay;
    private bool _displayVisible = true;
    private System.Windows.Forms.Timer _barcodeTimer = null!;
    private DateTime _lastBarcodeKeystroke = DateTime.MinValue;

    private static readonly Color CTopbar      = Color.FromArgb(26, 26, 46);
    private static readonly Color CTopbarChip  = Color.FromArgb(37, 37, 64);
    private static readonly Color CTopbarBorder= Color.FromArgb(56, 56, 96);
    private static readonly Color CTopbarText  = Color.FromArgb(170, 170, 204);
    private static readonly Color CTopbarAccent= Color.FromArgb(126, 184, 247);
    private static readonly Color CSurface     = Color.FromArgb(244, 245, 250);
    private static readonly Color CCard        = Color.White;
    private static readonly Color CBorder      = Color.FromArgb(220, 221, 230);
    private static readonly Color CBorderLight = Color.FromArgb(236, 237, 243);
    private static readonly Color CText        = Color.FromArgb(30, 30, 46);
    private static readonly Color CTextMuted   = Color.FromArgb(110, 110, 140);
    private static readonly Color CTextHint    = Color.FromArgb(160, 160, 190);
    private static readonly Color CGreenDark   = Color.FromArgb(39, 80, 10);
    private static readonly Color CGreenMid    = Color.FromArgb(99, 153, 34);
    private static readonly Color CGreenLight  = Color.FromArgb(234, 243, 222);
    private static readonly Color CBlueLight   = Color.FromArgb(230, 241, 251);
    private static readonly Color CBlueMid     = Color.FromArgb(24, 95, 165);
    private static readonly Color CBlueDark    = Color.FromArgb(12, 68, 124);
    private static readonly Color CRedLight    = Color.FromArgb(252, 235, 235);
    private static readonly Color CRedDark     = Color.FromArgb(163, 45, 45);
    private static readonly Color CAmberLight  = Color.FromArgb(250, 238, 218);
    private static readonly Color CAmberDark   = Color.FromArgb(99, 56, 6);
    private static readonly Color CAmberMid    = Color.FromArgb(186, 117, 23);

    public SalesForm(User? user, CustomerDisplayForm customerDisplay)
    {
        _currentUser = user;
        _customerDisplay = customerDisplay;
        _customerDisplay.SetIdleMode(false);
        _displayVisible = customerDisplay.Visible;
        InitializeComponent();
        UpdateTotals();
        txtBarcode.Focus();

        _barcodeTimer = new System.Windows.Forms.Timer { Interval = 180 };
        _barcodeTimer.Tick += (_, _) =>
        {
            _barcodeTimer.Stop();
            ProcessBarcodeInput();
        };
        DebugHelper.AddFormLabel(this);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var screen = Screen.FromControl(this);
        Location = screen.WorkingArea.Location;
        PromptNextTransaction();
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
        using var otForm = new OrderTypeForm();
        if (otForm.ShowDialog() != DialogResult.OK) return false;

        _orderType = otForm.SelectedType;
        if (_orderType == "Walk-in") return true;

        using var custForm = new SelectCustomerForm(_orderType);
        if (custForm.ShowDialog() != DialogResult.OK) return false;

        _selectedCustomer = custForm.SelectedCustomer;
        UpdateCustomerDisplay();
        return true;
    }

    private void UpdateCustomerDisplay()
    {
        if (_selectedCustomer != null)
        {
            var c = _selectedCustomer;
            lblCustomerInfo.Text = $"{c.Name}  ·  {c.DisplayPhone}  ·  Credit: \u20b1{c.CreditBalance:N2}  ·  Points: {c.LoyaltyPoints}";
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
        }
        PushToCustomerDisplay();
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
        if (prod != null && totalPieces > prod.StockQty)
        {
            MessageBox.Show(
                $"Insufficient stock for '{prod.Name}'.\nRequested: {totalPieces} pcs\nAvailable: {prod.StockQty} pcs",
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
            _lastBarcodeKeystroke = DateTime.Now;
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

        var cartPieces = _cart
            .Where(x => x.ProductId == product.Id)
            .Sum(x => x.Quantity * x.QtyPerUnit);
        var newPieces = cartPieces + quantity * qtyPerUnit;

        if (newPieces > product.StockQty)
        {
            MessageBox.Show(
                $"Insufficient stock for '{product.Name}'.\nRequested: {newPieces} pcs\nAvailable: {product.StockQty} pcs",
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
                UnitCost = product.Cost * qtyPerUnit
            });
        }
        RefreshCart();
    }

    private void txtSearch_TextChanged(object? sender, EventArgs e)
    {
        if (txtSearch.Text.Length >= 2)
        {
            var results = ProductService.Search(txtSearch.Text);
            RebuildSearchPanel(results);
            _pnlSearchResults.Visible = results.Count > 0;
            if (_pnlSearchResults.Visible) _pnlSearchResults.BringToFront();
        }
        else
        {
            _pnlSearchResults.Visible = false;
        }
    }

    private void RebuildSearchPanel(List<Product> results)
    {
        _pnlSearchResults.SuspendLayout();
        while (_pnlSearchResults.Controls.Count > 1)
            _pnlSearchResults.Controls.RemoveAt(1);

        if (_pnlSearchResults.Controls[0] is Panel hdr && hdr.Controls.Count >= 2)
            ((Label)hdr.Controls[1]).Text = $"{results.Count} result{(results.Count == 1 ? "" : "s")}";

        var threshold = ProductService.GetLowStockThreshold();
        var rowH = 46;
        var top = 28;
        foreach (var prod in results.Take(8))
        {
            var isOut = prod.StockQty <= 0;
            var isLow = !isOut && prod.StockQty <= threshold;
            var canAdd = !isOut;
            var row = new Panel
            {
                Location = new Point(0, top),
                Size = new Size(_pnlSearchResults.Width, rowH),
                BackColor = CCard,
                Tag = prod,
                Cursor = canAdd ? Cursors.Hand : Cursors.Default
            };

            Color iconBg, iconFg;
            if (isOut) { iconBg = CRedLight; iconFg = CRedDark; }
            else if (isLow) { iconBg = Color.FromArgb(255, 243, 205); iconFg = Color.FromArgb(243, 156, 18); }
            else { iconBg = CBlueLight; iconFg = CBlueMid; }
            var icon = new Panel
            {
                Location = new Point(10, 8),
                Size = new Size(30, 30),
                BackColor = iconBg
            };
            var iconLbl = new Label
            {
                Text = "\u25a0",
                Font = new Font("Segoe UI", 8F),
                ForeColor = iconFg,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            icon.Controls.Add(iconLbl);

            var stockLabel = isOut ? "Out of stock" : (isLow ? $"Low stock ({prod.StockQty})" : $"{prod.StockQty} in stock");
            var lblName = new Label
            {
                Text = prod.Name,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = canAdd ? CText : CTextMuted,
                Location = new Point(48, 6), Size = new Size(200, 18),
                AutoEllipsis = true
            };
            var lblSub = new Label
            {
                Text = $"{prod.Barcode}  ·  {stockLabel}",
                Font = new Font("Segoe UI", 8F),
                ForeColor = CTextHint,
                Location = new Point(48, 25), Size = new Size(200, 15)
            };

            var lblPrice = new Label
            {
                Text = $"\u20b1{prod.Price:N2}",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = canAdd ? CGreenDark : CTextHint,
                Location = new Point(row.Width - 120, 13), Size = new Size(72, 20),
                TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var btnAdd = new Button
            {
                Text = "+",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Location = new Point(row.Width - 42, 9), Size = new Size(32, 28),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = canAdd ? CGreenMid : CBorder },
                BackColor = canAdd ? CGreenLight : CSurface,
                ForeColor = canAdd ? CGreenDark : CTextHint,
                Cursor = canAdd ? Cursors.Hand : Cursors.Default,
                Enabled = canAdd,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Tag = prod
            };
            if (canAdd)
                btnAdd.Click += (_, _) => AddSearchedProduct((Product)btnAdd.Tag!);

            row.Paint += (s, ev) =>
            {
                using var pen = new Pen(CBorderLight, 1);
                ev.Graphics.DrawLine(pen, 0, rowH - 1, row.Width, rowH - 1);
            };

            row.Controls.AddRange(new Control[] { icon, lblName, lblSub, lblPrice, btnAdd });
            if (canAdd)
                row.Click += (_, _) => AddSearchedProduct(prod);

            _pnlSearchResults.Controls.Add(row);
            top += rowH;
        }

        _pnlSearchResults.Height = top;
        _pnlSearchResults.ResumeLayout();
    }

    private void AddSearchedProduct(Product product)
    {
        var unit = GetUnitForProduct(product);
        if (unit != null || ProductUnitService.GetByProduct(product.Id).Count == 0)
        {
            AddToCart(product, unit);
            _pnlSearchResults.Visible = false;
            txtSearch.Clear();
            _searchHighlightIdx = -1;
            txtBarcode.Focus();
        }
    }

    private void HighlightSearchRow()
    {
        var rows = _pnlSearchResults.Controls.Cast<Control>().Skip(1).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var isHl = i == _searchHighlightIdx;
            rows[i].BackColor = isHl ? Color.FromArgb(40, 80, 140) : CCard;
            foreach (Control c in rows[i].Controls)
                if (c is Label l) l.ForeColor = isHl ? Color.White : CText;
        }
    }

    private void RefreshCart()
    {
        _cartSource.ResetBindings(false);
        UpdateTotals();
        PushToCustomerDisplay();

        BeginInvoke(new Action(() =>
        {
            if (dgvCart.Rows.Count > 0)
            {
                dgvCart.FirstDisplayedScrollingRowIndex = 0;
                dgvCart.ClearSelection();
            }
        }));
    }

    private void PushToCustomerDisplay()
    {
        if (!_displayVisible) return;
        var grandTotal = _cart.Sum(x => x.TotalPrice);
        _customerDisplay.UpdateOrder(_selectedCustomer?.Name ?? "", _orderType, _cart.ToList(), grandTotal);
    }

    private void ToggleCustomerDisplay()
    {
        if (_customerDisplay.Visible)
        {
            _customerDisplay.Hide();
            _displayVisible = false;
        }
        else
        {
            _customerDisplay.Show();
            _customerDisplay.BringToFront();
            _displayVisible = true;
            PushToCustomerDisplay();
        }
    }

    private void UpdateTotals()
    {
        var grandTotal = _cart.Sum(x => x.TotalPrice);
        var totalQty   = _cart.Sum(x => x.Quantity);
        lblSubTotal.Text    = $"\u20b1{grandTotal:N2}";
        lblGrandTotal.Text  = $"\u20b1{grandTotal:N2}";
        btnPay.Text         = $"Charge  \u20b1{grandTotal:N2}";
        lblCartMeta.Text    = $"{_cart.Count} item(s)  ·  {totalQty} pcs  ·  click qty to edit";
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
                var msg = $"'{prod.Name}' — needs {totalPieces} pcs, only {prod.StockQty} available";
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

        var grandTotal = _cart.Sum(x => x.TotalPrice);
        using var payForm = new PaymentForm(grandTotal, _selectedCustomer);
        if (payForm.ShowDialog() != DialogResult.OK) return;

        var sale = new Sale
        {
            InvoiceNo     = SaleService.GenerateInvoiceNo(),
            SaleDate      = DateTime.Now,
            SubTotal      = grandTotal,
            Discount      = 0,
            Tax           = 0,
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
        _pnlCashierChip = new Panel { BackColor = CTopbarChip };
        _pnlCashierChip.Paint += (s, e) =>
        {
            using var pen = new Pen(CTopbarBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, _pnlCashierChip.Width - 1, _pnlCashierChip.Height - 1);
        };
        var lblCashierChip = new Label
        {
            Text = cashierName,
            Font = new Font("Segoe UI", 9F),
            ForeColor = CTopbarText,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _pnlCashierChip.Controls.Add(lblCashierChip);

        _lblTime = new Label
        {
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(100, 100, 130),
            TextAlign = ContentAlignment.MiddleRight
        };

        var btnDisplay = new Button
        {
            Text = "Display",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CTopbarBorder },
            BackColor = CTopbarChip,
            ForeColor = CTopbarText,
            Cursor = Cursors.Hand
        };
        btnDisplay.Click += (_, _) => ToggleCustomerDisplay();

        _pnlTopbar.Controls.AddRange(new Control[] { lblBrand, _pnlCashierChip, _lblTime, btnDisplay });

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

        _pnlCustomerBar.Controls.AddRange(new Control[] { lblCustIcon, lblCustomerInfo, lblOrderChip });

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
            BackColor = Color.FromArgb(245, 246, 250),
            ForeColor = CText
        };
        txtBarcode.KeyDown += txtBarcode_KeyDown;

        var lblSearchHint = new Label
        {
            Text = "Search product",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint
        };
        txtSearch = new TextBox
        {
            Font = new Font("Segoe UI", 13F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(245, 246, 250),
            ForeColor = CText
        };
        txtSearch.TextChanged += txtSearch_TextChanged;
        txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                _pnlSearchResults.Visible = false;
                txtSearch.Clear();
                txtBarcode.Focus();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down && _pnlSearchResults.Visible)
            {
                e.SuppressKeyPress = true;
                _searchHighlightIdx = Math.Min(_searchHighlightIdx + 1, _pnlSearchResults.Controls.Count - 2);
                HighlightSearchRow();
            }
            else if (e.KeyCode == Keys.Up && _pnlSearchResults.Visible)
            {
                e.SuppressKeyPress = true;
                _searchHighlightIdx = Math.Max(_searchHighlightIdx - 1, 0);
                HighlightSearchRow();
            }
            else if (e.KeyCode == Keys.Enter && _pnlSearchResults.Visible && _searchHighlightIdx >= 0)
            {
                e.SuppressKeyPress = true;
                var rows = _pnlSearchResults.Controls.Cast<Control>().Skip(1).ToList();
                if (_searchHighlightIdx < rows.Count && rows[_searchHighlightIdx].Tag is Product p)
                    AddSearchedProduct(p);
            }
        };

        _pnlSearch.Controls.AddRange(new Control[] { lblBarcodeHint, txtBarcode, lblSearchHint, txtSearch });

        _pnlSearchResults = new Panel
        {
            BackColor = CCard,
            Visible = false,
            BorderStyle = BorderStyle.FixedSingle
        };

        var srHeader = new Panel { Location = new Point(0, 0), Size = new Size(400, 28), BackColor = CSurface };
        srHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLight, 1);
            e.Graphics.DrawLine(pen, 0, 27, srHeader.Width, 27);
        };
        var srHeaderIcon = new Label
        {
            Text = "\u25ba",
            Font = new Font("Segoe UI", 9F),
            ForeColor = CTextHint,
            Location = new Point(10, 6), Size = new Size(16, 16)
        };
        var srHeaderCount = new Label
        {
            Text = "Results",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextMuted,
            Location = new Point(28, 6), Size = new Size(120, 16)
        };
        srHeader.Controls.AddRange(new Control[] { srHeaderIcon, srHeaderCount });
        _pnlSearchResults.Controls.Add(srHeader);

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
        var lblDiscountVal = new Label
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

        _pnlTotals.Controls.AddRange(new Control[]
        {
            lblTotalDueHint, lblGrandTotal, lblInvoiceHint,
            sep1,
            lblSubTotalLbl, lblSubTotal,
            lblDiscountLbl, lblDiscountVal,
            sep2,
            btnPay
        });

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F1) { txtBarcode.Focus(); txtBarcode.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F2) { txtSearch.Focus(); txtSearch.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F3)
            {
                if (dgvCart.Rows.Count > 0 && dgvCart.CurrentCell != null)
                    EditCartItemQuantity(dgvCart.CurrentCell.RowIndex);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F4) { btnPay_Click(null!, e); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F5) { btnHold_Click(null!, e); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F8) { btnClear_Click(null!, e); e.SuppressKeyPress = true; }
        };

        Controls.AddRange(new Control[]
        {
            _pnlTopbar, _pnlCustomerBar, _pnlSearch,
            _pnlCart, _pnlTotals, _pnlSearchResults
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
        var custH     = 36;
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
        _pnlCashierChip.Location = new Point(184, 8);
        _pnlCashierChip.Size     = new Size(140, 28);
        _lblTime.Text     = DateTime.Now.ToString("MMM dd, yyyy  h:mm tt");
        _lblTime.Location = new Point(w - 310, 0);
        _lblTime.Size     = new Size(190, topH);
        tbControls[3].Location = new Point(w - 110, 10);
        tbControls[3].Size     = new Size(90, 24);

        _pnlCustomerBar.Location = new Point(0, topH);
        _pnlCustomerBar.Size     = new Size(leftW, custH);
        var cc = _pnlCustomerBar.Controls;
        cc[0].Location = new Point(12, 9);   cc[0].Size = new Size(18, 18);
        cc[1].Location = new Point(34, 8);   cc[1].Size = new Size(leftW - 160, 20);
        cc[2].Location = new Point(leftW - 120, 9); cc[2].Size = new Size(100, 18);

        _pnlSearch.Location = new Point(0, topH + custH);
        _pnlSearch.Size     = new Size(leftW, searchH);
        var half = leftW / 2;
        var sc   = _pnlSearch.Controls;
        sc[0].Location = new Point(gap, 4);          sc[0].Size = new Size(80, 14);
        txtBarcode.Location = new Point(gap, 18);    txtBarcode.Size = new Size(half - gap * 2, 28);
        sc[2].Location = new Point(half + gap, 4);   sc[2].Size = new Size(120, 14);
        txtSearch.Location  = new Point(half + gap, 18); txtSearch.Size = new Size(half - gap * 2, 28);

        _pnlSearchResults.Location = new Point(half + gap, topH + custH + searchH);
        _pnlSearchResults.Size     = new Size(half - gap * 2, _pnlSearchResults.Height);
        foreach (Control c in _pnlSearchResults.Controls)
        {
            c.Width = _pnlSearchResults.Width - 2;
            if (c is Panel row)
            {
                foreach (Control rc in row.Controls)
                {
                    if (rc.Anchor.HasFlag(AnchorStyles.Right))
                        rc.Left = row.Width - rc.Width - 8;
                }
            }
        }

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

        rcs[0].Location = new Point(m, ry);       rcs[0].Size = new Size(pw, 16); ry += 18;
        lblGrandTotal.Location = new Point(m, ry); lblGrandTotal.Size = new Size(pw, 40); ry += 42;
        rcs[2].Location = new Point(m, ry);        rcs[2].Size = new Size(pw, 16); ry += 22;
        rcs[3].Location = new Point(m, ry);        rcs[3].Size = new Size(pw, 1);  ry += 12;
        rcs[4].Location = new Point(m, ry);        rcs[4].Size = new Size(pw / 2, 22);
        lblSubTotal.Location = new Point(m + pw / 2, ry); lblSubTotal.Size = new Size(pw / 2, 22); ry += 24;
        rcs[6].Location = new Point(m, ry);        rcs[6].Size = new Size(pw / 2, 22);
        rcs[7].Location = new Point(m + pw / 2, ry); rcs[7].Size = new Size(pw / 2, 22); ry += 26;
        rcs[8].Location = new Point(m, ry);        rcs[8].Size = new Size(pw, 1); ry += 16;
        btnPay.Location = new Point(m, ry);       btnPay.Size = new Size(pw, 52);
    }

    private Panel _pnlTopbar = null!;
    private Panel _pnlCashierChip = null!;
    private Panel _pnlCustomerBar = null!;
    private Panel _pnlSearch = null!;
    private Panel _pnlSearchResults = null!;
    private int _searchHighlightIdx = -1;
    private Panel _pnlCart = null!;
    private Panel _pnlTotals = null!;
    private Label _lblTime = null!;
    private Label lblCustomerInfo = null!;
    private Label lblOrderChip = null!;
    private Label lblCartMeta = null!;
    private TextBox txtBarcode = null!;
    private TextBox txtSearch = null!;
    private DataGridView dgvCart = null!;
    private Label lblSubTotal = null!;
    private Label lblGrandTotal = null!;
    private Button btnRemove = null!;
    private Button btnClear = null!;
    private Button btnHold = null!;
    private Button btnRetrieve = null!;
    private Button btnPay = null!;
}
