using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public class ProductSearchForm : Form
{
    public Product? SelectedProduct { get; private set; }

    public string InitialSearchText
    {
        get => _txtSearch.Text;
        set
        {
            _txtSearch.Text = value;
            if (value.Length >= 2) DoSearch();
        }
    }

    private readonly TextBox _txtSearch;
    private readonly DataGridView _dgv;
    private List<Product> _results = new();

    private static Color CHeaderBg    => ThemeManager.Current.StatusBlueMid;
    private static Color CHeaderText  => Color.White;
    private static Color CSurface     => ThemeManager.Current.SurfaceBg;
    private static Color CCard        => ThemeManager.Current.CardBg;
    private static Color CBorderLight => ThemeManager.Current.BorderLight;
    private static Color CText        => ThemeManager.Current.TextPrimary;
    private static Color CTextMuted   => ThemeManager.Current.TextSecondary;
    private static Color CTextHint    => ThemeManager.Current.TextHint;
    private static Color CInputBg     => ThemeManager.Current.InputBg;
    private static Color CInputFg     => ThemeManager.Current.InputFg;
    private static Color CBlueMid     => ThemeManager.Current.StatusBlueMid;
    private static Color CGreenDark   => ThemeManager.Current.StatusGreenDark;
    private static Color CGreenMid    => ThemeManager.Current.StatusGreenMid;
    private static Color CRedDark     => ThemeManager.Current.StatusRedDark;
    private static Color CAmberDark   => ThemeManager.Current.StatusAmberDark;

    public ProductSearchForm()
    {
        Text = "Search Product";
        Size = new Size(820, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = CSurface;
        Font = new Font("Segoe UI", 10F);

        var clientW = 764;
        var leftM  = 24;

        var pnlHeader = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(820, 44),
            BackColor = CHeaderBg
        };
        var lblHeader = new Label
        {
            Text = "  \U0001F50D  Search Product",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = CHeaderText,
            Location = new Point(leftM, 0),
            Size = new Size(clientW, 44),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblHeader);

        var lblHint = new Label
        {
            Text = "Type product name or barcode",
            Location = new Point(leftM, 56),
            Size = new Size(clientW, 16),
            Font = new Font("Segoe UI", 9F),
            ForeColor = CTextHint
        };

        _txtSearch = new TextBox
        {
            Location = new Point(leftM, 76),
            Size = new Size(clientW, 30),
            Font = new Font("Segoe UI", 12F),
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = CInputFg,
            BackColor = CInputBg
        };
        _txtSearch.TextChanged += (_, _) => DoSearch();
        _txtSearch.KeyDown += OnSearchKeyDown;
        _txtSearch.GotFocus += (_, _) => _txtSearch.BackColor = CCard;
        _txtSearch.LostFocus += (_, _) => _txtSearch.BackColor = CInputBg;
        _txtSearch.LostFocus += (_, _) => _txtSearch.BackColor = CCard;

        _dgv = new DataGridView
        {
            Location = new Point(leftM, 114),
            Size = new Size(clientW, 446),
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = CCard,
            GridColor = CBorderLight,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            RowTemplate = { Height = 32 },
            EnableHeadersVisualStyles = false,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = CBlueMid,
                ForeColor = ThemeManager.Current.DgvHeaderFg,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                SelectionBackColor = CBlueMid,
                SelectionForeColor = CHeaderText
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                ForeColor = CText,
                SelectionBackColor = CBlueMid,
                SelectionForeColor = CCard,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(6, 0, 0, 0)
            }
        };

        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "ProductName",
            HeaderText = "Product Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 65,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(8, 0, 0, 0)
            }
        });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Barcode",
            HeaderText = "Barcode",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 16,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                ForeColor = CTextMuted,
                Padding = new Padding(6, 0, 0, 0)
            }
        });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "PriceDisplay",
            HeaderText = "Price",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 10,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = CGreenDark,
                Format = "N2",
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 6, 0)
            }
        });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "StockDisplay",
            HeaderText = "Stock",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 9,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(0)
            }
        });

        _dgv.CellDoubleClick += (_, _) => SelectCurrent();
        _dgv.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { SelectCurrent(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };

        _dgv.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex == 3 && e.RowIndex >= 0 && e.RowIndex < _results.Count)
            {
                var prod = _results[e.RowIndex];
                var threshold = ProductService.GetLowStockThreshold();
                if (prod.StockQty <= 0)
                {
                    e.Value = "OUT";
                    e.CellStyle.ForeColor = CRedDark;
                }
                else if (prod.StockQty <= threshold)
                {
                    e.Value = prod.StockQty.ToString();
                    e.CellStyle.ForeColor = CAmberDark;
                }
                else
                {
                    e.Value = prod.StockQty.ToString();
                    e.CellStyle.ForeColor = CGreenDark;
                }
                e.FormattingApplied = true;
            }
        };

        var pnlFooter = new Panel
        {
            Location = new Point(leftM, 560),
            Size = new Size(clientW, 4),
            BackColor = CBlueMid
        };

        Controls.AddRange(new Control[] { pnlHeader, lblHint, _txtSearch, _dgv, pnlFooter });

        Shown += (_, _) => _txtSearch.Focus();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            if (_dgv.Rows.Count > 0)
            {
                _dgv.Focus();
                _dgv.CurrentCell = _dgv.Rows[0].Cells[0];
            }
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        else if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SelectCurrent();
        }
    }

    private void DoSearch()
    {
        var text = _txtSearch.Text.Trim();
        if (text.Length < 2)
        {
            _results.Clear();
            _dgv.DataSource = null;
            return;
        }

        _results = ProductService.Search(text);
        var defaultUnits = ProductUnitService.GetDefaultsByProductIds(_results.Select(p => p.Id).ToList());
        var display = _results.Select(p =>
        {
            var defaultUnit = defaultUnits.TryGetValue(p.Id, out var u) ? u : null;
            var displayPrice = defaultUnit?.Price ?? p.Price;
            return new
            {
                ProductName = p.Name,
                Barcode = p.Barcode ?? "",
                PriceDisplay = displayPrice,
                StockDisplay = (object)p.StockQty
            };
        }).ToList();
        _dgv.DataSource = display;
    }

    private void SelectCurrent()
    {
        if (_dgv.CurrentRow == null || _dgv.CurrentRow.Index < 0 || _dgv.CurrentRow.Index >= _results.Count)
            return;
        SelectedProduct = _results[_dgv.CurrentRow.Index];
        DialogResult = DialogResult.OK;
        Close();
    }
}
