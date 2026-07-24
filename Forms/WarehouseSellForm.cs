using System.Data.SQLite;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public class WarehouseSellForm : Form
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static Color CTopbar => ThemeManager.Current.TopbarBg;
    private static Color CTopbarChip => ThemeManager.Current.TopbarChip;
    private static Color CTopbarBorder => ThemeManager.Current.TopbarBorder;
    private static Color CTopbarText => ThemeManager.Current.TopbarText;
    private static Color CTopbarAccent => ThemeManager.Current.TopbarAccent;
    private static Color CSurface => ThemeManager.Current.SurfaceBg;
    private static Color CCard => ThemeManager.Current.CardBg;
    private static Color CBorder => ThemeManager.Current.BorderColor;
    private static Color CBorderLight => ThemeManager.Current.BorderLight;
    private static Color CText => ThemeManager.Current.TextPrimary;
    private static Color CTextMuted => ThemeManager.Current.TextSecondary;
    private static Color CTextHint => ThemeManager.Current.TextHint;
    private static Color CGreenDark => ThemeManager.Current.StatusGreenDark;
    private static Color CGreenMid => ThemeManager.Current.StatusGreenMid;
    private static Color CBlueLight => ThemeManager.Current.StatusBlueLight;
    private static Color CBlueMid => ThemeManager.Current.StatusBlueMid;
    private static Color CBlueDark => ThemeManager.Current.StatusBlueDark;
    private static Color CInputBg => ThemeManager.Current.InputBg;
    private static Color CInputFg => ThemeManager.Current.InputFg;
    private static Color CRedLight => ThemeManager.Current.StatusRedLight;
    private static Color CRedDark => ThemeManager.Current.StatusRedDark;
    private static Color CAmberLight => ThemeManager.Current.StatusAmberLight;
    private static Color CAmberDark => ThemeManager.Current.StatusAmberDark;
    private static Color CAmberMid => ThemeManager.Current.StatusAmberMid;

    private readonly BindingSource _cartSource = new();
    private readonly BindingList<WhCartItem> _cart = new();
    private readonly User? _currentUser;
    private JsonElement _selectedCustomer;
    private string _customerName = "";

    private Panel _pnlTopbar = null!;
    private Panel _pnlCustomerBar = null!;
    private Panel _pnlSearch = null!;
    private Panel _pnlCart = null!;
    private Panel _pnlTotals = null!;
    private Label _lblTime = null!;
    private Label lblCustomerInfo = null!;
    private Label lblOrderChip = null!;
    private Label lblCartMeta = null!;
    private TextBox txtSearch = null!;
    private Button btnSearch = null!;
    private Button btnCustomer = null!;
    private DataGridView dgvCart = null!;
    private Label lblTotalDueHint = null!;
    private Label lblSubTotal = null!;
    private Label lblGrandTotal = null!;
    private Panel sep1 = null!;
    private Label lblSubTotalLbl = null!;
    private Panel sep2 = null!;
    private Button btnClear = null!;
    private Button btnSell = null!;

    public void ApplyTheme()
    {
        BackColor = CSurface;
        ForeColor = CText;
    }

    public WarehouseSellForm(User user)
    {
        try
        {
            ErrorLogger.Log("WhSellForm", "Constructor start");
            _currentUser = user;
            InitializeComponent();
            ApplyTheme();
            ErrorLogger.Log("WhSellForm", "Constructor end");
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("WhSellForm", ex);
            throw;
        }
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

        dgvCart.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == dgvCart.Columns["RowNum"]?.Index && e.RowIndex >= 0)
            {
                e.Value = (dgvCart.Rows.Count - e.RowIndex).ToString();
                e.FormattingApplied = true;
            }
        };
        dgvCart.CellContentClick += (_, e) =>
        {
            if (e.ColumnIndex == 1 && e.RowIndex >= 0 &&
                dgvCart.Rows[e.RowIndex].DataBoundItem is WhCartItem item)
            {
                if (MessageBox.Show($"Remove '{item.ProductName}' from cart?", "Confirm Remove",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _cart.Remove(item);
                    RefreshCart();
                }
            }
        };
        dgvCart.CellClick += (_, e) =>
        {
            if (e.ColumnIndex != 5 || e.RowIndex < 0) return;
            if (dgvCart.Rows[e.RowIndex].DataBoundItem is not WhCartItem item) return;
            ShowQtyDialog(item);
        };
    }

    private void ShowQtyDialog(WhCartItem item)
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

        item.Quantity = (int)nud.Value;
        item.TotalPrice = item.Price * item.Quantity;
        RefreshCart();
    }

    private void InitializeComponent()
    {
        Text = "Wholesale";
        StartPosition = FormStartPosition.Manual;
        WindowState = FormWindowState.Maximized;
        BackColor = CSurface;
        Font = new Font("Segoe UI", 10F);
        KeyPreview = true;

        // ── Topbar ──
        _pnlTopbar = new Panel { Height = 44, Dock = DockStyle.None, BackColor = CTopbar };

        var lblBrand = new Label
        {
            Text = "WHOLESALE",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = CTopbarAccent,
            Location = new Point(16, 0), Size = new Size(160, 44),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblCashier = new Label
        {
            Text = _currentUser?.FullName ?? _currentUser?.Username ?? "Admin",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 200, 255),
            Location = new Point(180, 0), Size = new Size(180, 44),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblTime = new Label
        {
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(100, 100, 130),
            Size = new Size(190, 44),
            TextAlign = ContentAlignment.MiddleRight
        };

        _pnlTopbar.Controls.AddRange(new Control[] { lblBrand, lblCashier, _lblTime });

        // ── Customer Bar ──
        _pnlCustomerBar = new Panel { Height = 42, Dock = DockStyle.None, BackColor = CCard };
        _pnlCustomerBar.Paint += (_, e) =>
        {
            using var pen = new Pen(CBorderLight);
            e.Graphics.DrawLine(pen, 0, _pnlCustomerBar.Height - 1, _pnlCustomerBar.Width, _pnlCustomerBar.Height - 1);
        };

        var lblCustIcon = new Label
        {
            Text = "\u25cf", Font = new Font("Segoe UI", 16F),
            ForeColor = CBlueMid,
            Location = new Point(12, 7), Size = new Size(28, 28),
            TextAlign = ContentAlignment.MiddleCenter
        };

        lblCustomerInfo = new Label
        {
            Text = "Select customer...",
            Font = new Font("Segoe UI", 9F),
            ForeColor = CTextMuted,
            Location = new Point(38, 11), Size = new Size(200, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };

        btnCustomer = new Button
        {
            Text = "SELECT CUSTOMER",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
            BackColor = CBlueLight,
            ForeColor = CBlueDark,
            Cursor = Cursors.Hand,
            Size = new Size(140, 28),
            Location = new Point(250, 7)
        };
        btnCustomer.Click += async (_, _) => await ShowCustomerPickerAsync();

        lblOrderChip = new Label
        {
            Text = "WHOLESALE",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CBlueDark,
            BackColor = CBlueLight,
            Padding = new Padding(6, 2, 6, 2),
            AutoSize = true,
            Visible = true
        };

        _pnlCustomerBar.Controls.AddRange(new Control[] { lblCustIcon, lblCustomerInfo, btnCustomer, lblOrderChip });

        // ── Search Bar ──
        _pnlSearch = new Panel { Height = 52, Dock = DockStyle.None, BackColor = CCard };
        _pnlSearch.Paint += (_, e) =>
        {
            using var pen = new Pen(CBorderLight);
            e.Graphics.DrawLine(pen, 0, _pnlSearch.Height - 1, _pnlSearch.Width, _pnlSearch.Height - 1);
        };

        var lblSearchHint = new Label
        {
            Text = "Search product",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint,
            Location = new Point(12, 4), Size = new Size(120, 14)
        };

        txtSearch = new TextBox
        {
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = CInputBg,
            ForeColor = CInputFg,
            Location = new Point(12, 18), Size = new Size(200, 28)
        };
        txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ShowSearchPopup(txtSearch.Text.Trim());
            }
        };

        btnSearch = new Button
        {
            Text = "Search  (F2)",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
            BackColor = CBlueLight,
            ForeColor = CBlueDark,
            Cursor = Cursors.Hand,
            Size = new Size(130, 32),
            Location = new Point(220, 16)
        };
        btnSearch.Click += (_, _) => ShowSearchPopup("");

        _pnlSearch.Controls.AddRange(new Control[] { lblSearchHint, txtSearch, btnSearch });

        // ── Cart ──
        _pnlCart = new Panel { Dock = DockStyle.None, BackColor = CSurface };

        var pnlCartHeader = new Panel { Height = 28, Dock = DockStyle.Top, BackColor = CCard };
        pnlCartHeader.Paint += (_, e) =>
        {
            using var pen = new Pen(CBorderLight);
            e.Graphics.DrawLine(pen, 0, pnlCartHeader.Height - 1, pnlCartHeader.Width, pnlCartHeader.Height - 1);
        };
        var lblCartTitle = new Label
        {
            Text = "Cart items", Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CTextMuted,
            Location = new Point(12, 4), Size = new Size(120, 18)
        };
        lblCartMeta = new Label
        {
            Font = new Font("Segoe UI", 8F),
            ForeColor = CTextHint,
            Size = new Size(300, 18),
            TextAlign = ContentAlignment.MiddleRight
        };
        pnlCartHeader.Controls.AddRange(new Control[] { lblCartTitle, lblCartMeta });

        dgvCart = new DataGridView { Dock = DockStyle.Fill };
        SetupCartGrid();

        var pnlActions = new Panel { Height = 38, Dock = DockStyle.Bottom, BackColor = CCard };
        pnlActions.Paint += (_, e) =>
        {
            using var pen = new Pen(CBorderLight);
            e.Graphics.DrawLine(pen, 0, 0, pnlActions.Width, 0);
        };
        btnClear = new Button
        {
            Text = "Clear all",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
            BackColor = CCard, ForeColor = CTextMuted, Cursor = Cursors.Hand,
            Size = new Size(90, 28), Location = new Point(12, 5)
        };
        btnClear.Click += (_, _) => { _cart.Clear(); RefreshCart(); };
        pnlActions.Controls.Add(btnClear);

        var pnlShortcuts = new Panel { Height = 24, Dock = DockStyle.Bottom, BackColor = CSurface };
        var shortcutKeys = new[] { "F1  Search", "F2  Customer", "F3  Clear", "F5  Focus" };
        for (var i = 0; i < shortcutKeys.Length; i++)
        {
            var s = new Label
            {
                Text = shortcutKeys[i],
                Font = new Font("Segoe UI", 8F), ForeColor = CTextHint,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(12 + i * 94, 3), Size = new Size(90, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlShortcuts.Controls.Add(s);
        }

        _pnlCart.Controls.AddRange(new Control[] { dgvCart, pnlCartHeader, pnlActions, pnlShortcuts });

        // ── Totals ──
        _pnlTotals = new Panel { Dock = DockStyle.None, BackColor = CCard };
        _pnlTotals.Paint += (_, e) =>
        {
            using var pen = new Pen(CBorderLight);
            e.Graphics.DrawLine(pen, 0, 0, 0, _pnlTotals.Height);
        };

        lblTotalDueHint = new Label
        {
            Text = "Total due",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CTextMuted
        };
        lblGrandTotal = new Label
        {
            Text = "\u20b10.00",
            Font = new Font("Segoe UI", 28F, FontStyle.Bold),
            ForeColor = CGreenDark
        };
        sep1 = new Panel { Height = 1, BackColor = CBorderLight };
        lblSubTotalLbl = new Label
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
        sep2 = new Panel { Height = 1, BackColor = CBorderLight };
        btnSell = new Button
        {
            Text = "SELL  \u20b10.00",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            BackColor = CGreenMid, ForeColor = Color.White, Cursor = Cursors.Hand
        };
        btnSell.Click += async (_, _) => await DoSellAsync();

        btnEndShiftWh = new Button
        {
            Text = "\uD83D\uDD14 END SHIFT",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(200, 160, 0), ForeColor = Color.White, Cursor = Cursors.Hand
        };
        btnEndShiftWh.Click += async (_, _) => await DoWholesaleEndShiftAsync();

        var btnVoid = new Button
        {
            Text = "\u2716 VOID RECEIPT",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(255, 60, 60), ForeColor = Color.White, Cursor = Cursors.Hand
        };
        btnVoid.Click += async (_, _) => await ShowVoidPopupAsync();

        _pnlTotals.Controls.AddRange(new Control[] {
            lblTotalDueHint, lblGrandTotal, sep1,
            lblSubTotalLbl, lblSubTotal,
            sep2, btnSell, btnEndShiftWh, btnVoid
        });

        Controls.AddRange(new Control[] { _pnlTopbar, _pnlCustomerBar, _pnlSearch, _pnlCart, _pnlTotals });

        Resize += (_, _) => LayoutControls();

        KeyDown += (_, e) =>
        {
            switch (e.KeyCode)
            {
                case Keys.F1: ShowSearchPopup(""); break;
                case Keys.F2: _ = ShowCustomerPickerAsync(); break;
                case Keys.F3: btnClear.PerformClick(); break;
                case Keys.F5: txtSearch.Focus(); txtSearch.SelectAll(); break;
                case Keys.Escape: Close(); break;
            }
        };

        var t = new System.Windows.Forms.Timer { Interval = 1000 };
        t.Tick += (_, _) => _lblTime.Text = DateTime.Now.ToString("MMM dd, yyyy  hh:mm:ss tt");
        t.Start();

        RefreshCart();
    }

    private void LayoutControls()
    {
        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var gap = 12;

        var topH = 44;
        var custH = 42;
        var searchH = 52;
        var rightW = Math.Max(280, Math.Min(320, (int)(w * 0.27)));
        var leftW = w - rightW - 1;
        var cartTop = topH + custH + searchH;
        var cartH = h - cartTop;

        _pnlTopbar.Location = new Point(0, 0);
        _pnlTopbar.Width = w;

        _lblTime.Location = new Point(w - 310, 0);

        _pnlCustomerBar.Location = new Point(0, topH);
        _pnlCustomerBar.Width = leftW;

        lblOrderChip.Location = new Point(leftW - 130, 11);

        _pnlSearch.Location = new Point(0, topH + custH);
        _pnlSearch.Width = leftW;

        var half = leftW / 2;
        txtSearch.Size = new Size(half - 24, 28);
        btnSearch.Location = new Point(half + 12, 16);
        btnSearch.Size = new Size(half - 24, 32);

        _pnlCart.Location = new Point(0, cartTop);
        _pnlCart.Width = leftW;
        _pnlCart.Height = cartH;

        lblCartMeta.Location = new Point(leftW - 320, 4);
        lblCartMeta.Width = 300;

        _pnlTotals.Location = new Point(leftW, topH);
        _pnlTotals.Width = rightW;
        _pnlTotals.Height = h - topH;

        var m = 18;
        var pw = rightW - 36;
        var ry = m;
        lblTotalDueHint.Location = new Point(m, ry); lblTotalDueHint.Size = new Size(pw, 18); ry += 22;
        lblGrandTotal.Location = new Point(m, ry); lblGrandTotal.Size = new Size(pw, 54); ry += 58;
        sep1.Location = new Point(m, ry); sep1.Width = pw; ry += 12;
        lblSubTotalLbl.Location = new Point(m, ry); lblSubTotalLbl.Size = new Size(pw / 2, 22);
        lblSubTotal.Location = new Point(m + pw / 2, ry); lblSubTotal.Size = new Size(pw / 2, 22); ry += 28;
        sep2.Location = new Point(m, ry); sep2.Width = pw; ry += 14;
        btnSell.Location = new Point(m, ry); btnSell.Size = new Size(pw, 52); ry += 58;
        btnEndShiftWh.Location = new Point(m, ry); btnEndShiftWh.Size = new Size(pw, 40); ry += 46;
        btnVoid.Location = new Point(m, ry); btnVoid.Size = new Size(pw, 36);
    }

    private async Task ShowCustomerPickerAsync()
    {
        using var form = new Form
        {
            Text = "Select Customer",
            Size = new Size(500, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = CSurface
        };

        var txtCustSearch = new TextBox
        {
            Font = new Font("Segoe UI", 12F),
            Location = new Point(12, 12), Size = new Size(360, 30),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = CInputBg, ForeColor = CInputFg
        };

        var dgv = new DataGridView
        {
            AllowUserToAddRows = false, ReadOnly = true,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = CCard, ForeColor = CText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10F),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = CBorder, ColumnHeadersHeight = 30,
            Location = new Point(12, 50), Size = new Size(460, 380)
        };
        dgv.Columns.Add("Name", "Name");
        dgv.Columns.Add("Phone", "Phone");
        dgv.Columns[0].FillWeight = 70;
        dgv.Columns[1].FillWeight = 30;

        var btnSelect = new Button
        {
            Text = "SELECT",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = CGreenMid, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,
            Location = new Point(12, 440), Size = new Size(100, 28),
            DialogResult = DialogResult.OK
        };

        form.Controls.AddRange(new Control[] { txtCustSearch, dgv, btnSelect });
        form.AcceptButton = btnSelect;

        JsonDocument? _custDoc = null;
        List<JsonElement> customers = new();

        async Task LoadCust(string search)
        {
            try
            {
                var url = SyncService.ApiUrl.TrimEnd('/') + "/dashboard/warehouse/customers?search=" + Uri.EscapeDataString(search);
                var json = await _http.GetStringAsync(url);
                _custDoc?.Dispose();
                _custDoc = JsonDocument.Parse(json);
                customers = _custDoc.RootElement.EnumerateArray().ToList();
                dgv.Rows.Clear();
                foreach (var c in customers)
                {
                    var idx = dgv.Rows.Add(c.GetProperty("name").GetString(), c.GetProperty("phone").GetString());
                    dgv.Rows[idx].Tag = c;
                }
            }
            catch (Exception ex) { ErrorLogger.Log("WhSellForm.LoadCust", ex); }
        }

        txtCustSearch.TextChanged += async (_, _) => await LoadCust(txtCustSearch.Text);
        await LoadCust("");

        if (form.ShowDialog() != DialogResult.OK || dgv.SelectedRows.Count == 0) return;

        var cust = (JsonElement)dgv.SelectedRows[0].Tag;
        _selectedCustomer = cust;
        _customerName = cust.GetProperty("name").GetString() ?? "";
        lblCustomerInfo.Text = _customerName;
        lblCustomerInfo.ForeColor = CBlueMid;
    }

    private void ShowSearchPopup(string searchText)
    {
        var popup = new Form
        {
            Text = "Search Product",
            Size = new Size(700, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = CSurface
        };

        var txtProdSearch = new TextBox
        {
            Font = new Font("Segoe UI", 12F),
            Location = new Point(12, 12), Size = new Size(660, 30),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = CInputBg, ForeColor = CInputFg,
            Text = searchText
        };

        var dgv = new DataGridView
        {
            AllowUserToAddRows = false, ReadOnly = true,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = CCard, ForeColor = CText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10F),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = CBorder, ColumnHeadersHeight = 30,
            Location = new Point(12, 50), Size = new Size(660, 340)
        };
        dgv.Columns.Add("Name", "Name");
        dgv.Columns.Add("Price", "Price");
        dgv.Columns.Add("Stock", "Stock");
        dgv.Columns[0].FillWeight = 50;
        dgv.Columns[1].FillWeight = 25;
        dgv.Columns[2].FillWeight = 25;

        JsonDocument? _prodDoc = null;
        List<JsonElement> products = new();

        async Task LoadProds(string search)
        {
            try
            {
                var url = SyncService.ApiUrl.TrimEnd('/') + "/dashboard/warehouse/products?search=" + Uri.EscapeDataString(search);
                var json = await _http.GetStringAsync(url);
                _prodDoc?.Dispose();
                _prodDoc = JsonDocument.Parse(json);
                products = _prodDoc.RootElement.EnumerateArray().Where(p =>
                {
                    var stock = p.GetProperty("stockQty").GetInt32();
                    if (stock <= 0) return false;
                    return true;
                }).ToList();

                dgv.Rows.Clear();
                foreach (var p in products)
                {
                    var price = GetDisplayPrice(p);
                    var stock = p.GetProperty("stockQty").GetInt32();
                    var idx = dgv.Rows.Add(p.GetProperty("name").GetString(), "₱" + price.ToString("N2"), stock.ToString());
                    dgv.Rows[idx].Tag = p;
                }
                if (dgv.Rows.Count > 0) dgv.ClearSelection();
            }
            catch (Exception ex) { ErrorLogger.Log("WhSellForm.LoadProds", ex); }
        }

        txtProdSearch.TextChanged += async (_, _) => await LoadProds(txtProdSearch.Text);
        _ = LoadProds(searchText);

        // Bottom controls: unit, qty, add button
        var cmbUnit = new ComboBox
        {
            Font = new Font("Segoe UI", 11F),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(12, 400), Size = new Size(180, 28),
            BackColor = CInputBg, ForeColor = CInputFg
        };

        var numQty = new NumericUpDown
        {
            Font = new Font("Segoe UI", 11F),
            Minimum = 1, Maximum = 99999, Value = 1,
            Location = new Point(200, 400), Size = new Size(80, 28),
            BackColor = CInputBg, ForeColor = CInputFg
        };

        var btnAdd = new Button
        {
            Text = "+ ADD TO CART",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = CGreenMid, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand, Enabled = false,
            Location = new Point(290, 398), Size = new Size(140, 32)
        };

        JsonElement? selectedProduct = null;
        List<JsonElement> currentUnits = new();

        dgv.SelectionChanged += (_, _) =>
        {
            if (dgv.SelectedRows.Count > 0 && dgv.SelectedRows[0].Tag is JsonElement p)
            {
                selectedProduct = p;
                currentUnits = GetUnits(p);
                cmbUnit.Items.Clear();
                if (currentUnits.Count > 0)
                {
                    foreach (var u in currentUnits)
                        cmbUnit.Items.Add(u.GetProperty("unitName").GetString());
                    cmbUnit.SelectedIndex = 0;
                    cmbUnit.Enabled = true;
                }
                else
                {
                    cmbUnit.Items.Add("Piece");
                    cmbUnit.SelectedIndex = 0;
                    cmbUnit.Enabled = false;
                }
                btnAdd.Enabled = true;
                numQty.Value = 1;
            }
        };

        dgv.DoubleClick += (_, _) =>
        {
            if (selectedProduct != null) btnAdd.PerformClick();
        };

        btnAdd.Click += (_, _) =>
        {
            if (selectedProduct == null) return;
            var p = selectedProduct.Value;
            var qty = (int)numQty.Value;
            if (qty <= 0) return;

            string unitName = "Piece";
            decimal unitPrice;
            int unitIndex = cmbUnit.SelectedIndex;

            if (unitIndex >= 0 && unitIndex < currentUnits.Count)
            {
                var u = currentUnits[unitIndex];
                unitName = u.GetProperty("unitName").GetString() ?? "Piece";
                unitPrice = u.GetProperty("price").GetDecimal();
            }
            else
            {
                unitPrice = p.GetProperty("piecePrice").GetDecimal();
            }

            var subtotal = qty * unitPrice;

            var existing = _cart.FirstOrDefault(x => x.ProductId == p.GetProperty("id").GetInt32() && x.UnitName == unitName);
            if (existing != null)
            {
                existing.Quantity += qty;
                existing.TotalPrice = existing.Price * existing.Quantity;
            }
            else
            {
                _cart.Add(new WhCartItem
                {
                    ProductId = p.GetProperty("id").GetInt32(),
                    ProductName = p.GetProperty("name").GetString() ?? "",
                    UnitName = unitName,
                    UnitIndex = unitIndex,
                    Quantity = qty,
                    Price = unitPrice,
                    TotalPrice = subtotal
                });
            }

            RefreshCart();
            popup.Close();
        };

        popup.Controls.AddRange(new Control[] { txtProdSearch, dgv, cmbUnit, numQty, btnAdd });
        popup.ShowDialog(this);
    }

    private decimal GetDisplayPrice(JsonElement p)
    {
        var units = GetUnits(p);
        if (units.Count > 0) return units[0].GetProperty("price").GetDecimal();
        return p.GetProperty("piecePrice").GetDecimal();
    }

    private List<JsonElement> GetUnits(JsonElement p)
    {
        if (p.TryGetProperty("units", out var u) && u.ValueKind == JsonValueKind.Array)
            return u.EnumerateArray().ToList();
        return new List<JsonElement>();
    }

    private void RefreshCart()
    {
        _cartSource.ResetBindings(false);
        var total = _cart.Sum(x => x.TotalPrice);
        var qty = _cart.Sum(x => x.Quantity);

        lblSubTotal.Text = "\u20b1" + total.ToString("N2");
        lblGrandTotal.Text = "\u20b1" + total.ToString("N2");
        btnSell.Text = "SELL  \u20b1" + total.ToString("N2");
        lblCartMeta.Text = $"{_cart.Count} item(s)  \u00b7  {qty} pcs";

        if (IsHandleCreated)
            BeginInvoke(new Action(() =>
            {
                if (dgvCart.Rows.Count > 0)
                {
                    dgvCart.FirstDisplayedScrollingRowIndex = 0;
                    dgvCart.ClearSelection();
                }
            }));
    }

    private async Task DoSellAsync()
    {
        if (_cart.Count == 0) { MessageBox.Show("Cart is empty.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (string.IsNullOrEmpty(_customerName)) { MessageBox.Show("Please select a customer.", "No Customer", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        btnSell.Enabled = false;
        btnSell.Text = "SELLING...";

        try
        {
            var customerId = _selectedCustomer.ValueKind == JsonValueKind.Object
                ? _selectedCustomer.GetProperty("id").GetInt32()
                : 0;

            var totalDue = _cart.Sum(x => x.TotalPrice);

            // Payment dialog
            var payMethod = "Cash";
            var cashReceived = totalDue;
            using (var payForm = new Form
            {
                Text = "Payment",
                Size = new Size(380, 260),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = ThemeManager.Current.SurfaceBg,
                ForeColor = ThemeManager.Current.TextPrimary
            })
            {
                var cartTotal = _cart.Sum(x => x.TotalPrice);
                var lblTotal = new Label { Text = $"Total: ₱{cartTotal:N2}", Font = new Font("Segoe UI", 16F, FontStyle.Bold), Location = new Point(20, 15), Size = new Size(340, 30), TextAlign = ContentAlignment.MiddleCenter, ForeColor = ThemeManager.Current.AccentCyan };
                var lblMethod = new Label { Text = "Payment Method:", Location = new Point(20, 60), Size = new Size(120, 25), ForeColor = ThemeManager.Current.TextSecondary };
                var cmbMethod = new ComboBox { Location = new Point(150, 58), Size = new Size(190, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = ThemeManager.Current.InputBg, ForeColor = ThemeManager.Current.TextPrimary };
                cmbMethod.Items.AddRange(new[] { "Cash", "E-Wallet", "Credit" });
                cmbMethod.SelectedIndex = 0;
                var lblCash = new Label { Text = "Amount Received:", Location = new Point(20, 100), Size = new Size(120, 25), ForeColor = ThemeManager.Current.TextSecondary };
                var txtCash = new TextBox { Text = totalDue.ToString("N2"), Location = new Point(150, 98), Size = new Size(190, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = ThemeManager.Current.InputBg, ForeColor = ThemeManager.Current.TextPrimary, TextAlign = HorizontalAlignment.Right };
                var lblChange = new Label { Text = "Change: ₱0.00", Location = new Point(20, 135), Size = new Size(320, 25), ForeColor = Color.FromArgb(0, 200, 83), Font = new Font("Segoe UI", 10F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
                txtCash.TextChanged += (_, _) =>
                {
                    decimal.TryParse(txtCash.Text, out var amt);
                    lblChange.Text = $"Change: ₱{Math.Max(0, amt - cartTotal):N2}";
                    lblChange.ForeColor = amt >= cartTotal ? Color.FromArgb(0, 200, 83) : Color.FromArgb(255, 82, 82);
                };
                cmbMethod.SelectedIndexChanged += (_, _) =>
                {
                    var isCash = cmbMethod.SelectedItem?.ToString() == "Cash";
                    txtCash.Visible = isCash;
                    lblCash.Visible = isCash;
                    if (!isCash) lblChange.Text = "Change: ₱0.00";
                };
                var btnOk = new Button { Text = "CONFIRM PAYMENT", Location = new Point(30, 175), Size = new Size(150, 40), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand };
                var btnCancel = new Button { Text = "CANCEL", Location = new Point(200, 175), Size = new Size(130, 40), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, 82, 82), ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand };
                string? resultMethod = null;
                decimal resultCash = 0;
                btnOk.Click += (_, _) =>
                {
                    if (cmbMethod.SelectedItem?.ToString() == "Cash" && (!decimal.TryParse(txtCash.Text, out var amt) || amt < cartTotal))
                    { MessageBox.Show("Amount must cover the total.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    resultMethod = cmbMethod.SelectedItem?.ToString();
                    resultCash = decimal.TryParse(txtCash.Text, out var a) ? a : cartTotal;
                    payForm.DialogResult = DialogResult.OK;
                };
                btnCancel.Click += (_, _) => payForm.DialogResult = DialogResult.Cancel;
                payForm.Controls.AddRange(new Control[] { lblTotal, lblMethod, cmbMethod, lblCash, txtCash, lblChange, btnOk, btnCancel });
                if (payForm.ShowDialog() != DialogResult.OK) { btnSell.Enabled = true; btnSell.Text = "SELL  ₱0.00"; return; }
                payMethod = resultMethod ?? "Cash";
                cashReceived = resultCash;
            }

            var items = _cart.Select(c => new
            {
                productId = c.ProductId,
                productName = c.ProductName,
                unitIndex = c.UnitIndex,
                qty = c.Quantity
            }).ToList();

            var body = new { customerId, customerName = _customerName, paymentMethod = payMethod, cashReceived, items };

            var url = SyncService.ApiUrl.TrimEnd('/') + "/dashboard/warehouse/sell";
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            var respBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(respBody);
                var result = doc.RootElement;
                var grandTotal = result.GetProperty("grandTotal").GetDecimal();
                var saleId = result.GetProperty("saleId").GetInt32();

                // Wholesale stays in cloud only — no local save

                try
                {
                    var printItems = _cart.Select(c => (c.ProductName, c.UnitName, c.Quantity, c.Price, c.TotalPrice)).ToList();
                    PrinterService.PrintWhReceipt(saleId, _customerName, printItems, grandTotal, _currentUser?.FullName ?? "Admin");
                }
                catch (Exception printEx)
                {
                    MessageBox.Show("Sale completed but receipt printing failed: " + printEx.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                MessageBox.Show($"Sale complete!\nCustomer: {_customerName}\nTotal: \u20b1{grandTotal:N2}\nSale #{saleId}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                try
                {
                    using var doc = JsonDocument.Parse(respBody);
                    var err = doc.RootElement.GetProperty("error").GetString();
                    MessageBox.Show("Sale failed: " + err, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { MessageBox.Show("Sale failed: " + respBody, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Connection error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnSell.Enabled = true;
            btnSell.Text = "SELL  " + (_cart.Count > 0 ? "\u20b1" + _cart.Sum(x => x.TotalPrice).ToString("N2") : "\u20b10.00");
        }
    }

    private async Task DoWholesaleEndShiftAsync()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            var since = "";
            using (var getLast = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'WholesaleLastClose'", conn))
                since = getLast.ExecuteScalar()?.ToString() ?? "";

            var cond = string.IsNullOrEmpty(since) ? "1=1" : "s.SaleDate > @since";
            using var cmd = new SQLiteCommand($@"SELECT COUNT(*), COALESCE(SUM(s.GrandTotal), 0)
                FROM Sales s WHERE s.OrderType = 'Wholesale' AND {cond}", conn);
            if (!string.IsNullOrEmpty(since))
                cmd.Parameters.AddWithValue("@since", since);
            using var rdr = cmd.ExecuteReader();
            rdr.Read();
            var count = rdr.GetInt32(0);
            var total = rdr.GetDecimal(1);

            var msg = $"Wholesale End Shift\n━━━━━━━━━━━━━━━\n" +
                      $"Transactions: {count}\n" +
                      $"Total Sales: ₱{total:N2}\n" +
                      $"Since: {(string.IsNullOrEmpty(since) ? "Beginning" : since)}\n\n" +
                      $"End shift and reset counter?";

            if (MessageBox.Show(msg, "Wholesale End Shift", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var now = TimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss");
                using var ups = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('WholesaleLastClose', @val)", conn);
                ups.Parameters.AddWithValue("@val", now);
                ups.ExecuteNonQuery();

                MessageBox.Show($"Wholesale shift ended.\n{count} transaction(s)\nTotal: ₱{total:N2}", "Shift Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex) { ErrorLogger.Log("WholesaleEndShift", ex); MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ShowVoidPopupAsync()
    {
        var popup = new Form { Text = "Void Wholesale Receipt", Size = new Size(600, 450), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = ThemeManager.Current.SurfaceBg };
        var lbl = new Label { Text = "Select a sale to void:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(12, 12), Size = new Size(560, 24), ForeColor = ThemeManager.Current.TextPrimary };
        var dgv = new DataGridView { Location = new Point(12, 42), Size = new Size(560, 300), ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = ThemeManager.Current.CardBg, Font = new Font("Segoe UI", 9F), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeight = 28, EnableHeadersVisualStyles = false };
        dgv.Columns.Add("Id", "#"); dgv.Columns.Add("Customer", "Customer"); dgv.Columns.Add("Total", "Total"); dgv.Columns.Add("Date", "Date");
        dgv.Columns[0].Width = 50; dgv.Columns[2].Width = 80; dgv.Columns[3].Width = 140;

        var btnDoVoid = new Button { Text = "✖ VOID SELECTED SALE", Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = Color.FromArgb(255, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand, Enabled = false, Location = new Point(12, 352), Size = new Size(200, 36) };
        var btnClose = new Button { Text = "CLOSE", Location = new Point(370, 352), Size = new Size(200, 36), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = ThemeManager.Current.CardBg, ForeColor = ThemeManager.Current.TextSecondary, Cursor = Cursors.Hand };
        btnClose.Click += (_, _) => popup.Close();

        dgv.SelectionChanged += (_, _) => btnDoVoid.Enabled = dgv.SelectedRows.Count > 0;

        btnDoVoid.Click += async (_, _) =>
        {
            if (dgv.SelectedRows.Count == 0) return;
            var saleId = (int)dgv.SelectedRows[0].Cells[0].Value;
            if (MessageBox.Show($"Void wholesale sale #{saleId}?\nThis will restore stock to the warehouse.", "Confirm Void", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                var url = SyncService.ApiUrl.TrimEnd('/') + "/dashboard/warehouse/sales/" + saleId + "/void";
                var response = await _http.PostAsync(url, new StringContent("{}", Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode) { MessageBox.Show("Sale #" + saleId + " voided. Stock restored.", "Voided", MessageBoxButtons.OK, MessageBoxIcon.Information); popup.Close(); }
                else { var err = await response.Content.ReadAsStringAsync(); MessageBox.Show("Failed: " + err, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };

        popup.Controls.AddRange(new Control[] { lbl, dgv, btnDoVoid, btnClose });

        // Load recent sales
        try
        {
            var url = SyncService.ApiUrl.TrimEnd('/') + "/dashboard/warehouse/sales?limit=200";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            foreach (var s in doc.RootElement.EnumerateArray())
            {
                if (s.TryGetProperty("isVoided", out var iv) && iv.GetBoolean()) continue;
                var sid = s.GetProperty("id").GetInt32();
                var cn = s.GetProperty("customerName").GetString();
                var total = s.GetProperty("total").GetDecimal();
                var dt = s.GetProperty("createdAt").GetDateTime().ToString("MMM dd hh:mm tt");
                dgv.Rows.Add(sid, cn, "₱" + total.ToString("N2"), dt);
            }
        }
        catch { dgv.Rows.Add(0, "Failed to load", "", ""); }

        popup.ShowDialog(this);
    }

    private Button btnEndShiftWh = null!;
    private Button btnVoid = null!;
}

public class WhCartItem : INotifyPropertyChanged
{
    private int _quantity;
    private decimal _totalPrice;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "Piece";
    public int UnitIndex { get; set; }
    public decimal Price { get; set; }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = value; OnPropertyChanged(); }
    }

    public decimal TotalPrice
    {
        get => _totalPrice;
        set { _totalPrice = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
