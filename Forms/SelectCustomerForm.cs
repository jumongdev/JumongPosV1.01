using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class SelectCustomerForm : Form
{
    public Customer? SelectedCustomer { get; private set; }

    public SelectCustomerForm(string orderType)
    {
        InitializeComponent();
        lblHeader.Text = orderType == "Online"
            ? "SELECT CUSTOMER FOR ONLINE ORDER"
            : "SELECT CUSTOMER — ⭐ online-registered lang · TOP = WALK-IN";
        // I-load ang list kapag may handle na ang form (DataGridView DataSource sa ctor = NRE sa band layout)
        Shown += (_, _) => RefreshGrid("");
        txtSearch.Focus();
        DebugHelper.AddFormLabel(this);
    }

    private void RefreshGrid(string keyword)
    {
        // ⭐ ONLY: mga online-registered customers (may QR code) lang ang pwedeng i-pick;
        // ang mga walang star (manual POS customers, walang points) ay HINDI na lalabas dito.
        var results = keyword.Length < 1
            ? CustomerService.GetAll().Where(c => c.IsActive && !string.IsNullOrEmpty(c.QrCode)).OrderBy(c => c.Name).ToList()
            : CustomerService.Search(keyword).Where(c => c.IsActive && !string.IsNullOrEmpty(c.QrCode)).ToList();
        _list = results;

        var rows = new List<object>
        {
            new { Id = 0, Name = "🧾 WALK-IN (no points)", Phone = "", Address = "—", Credit = "—", Points = 0 }
        };
        rows.AddRange(results.Select(c => new
        {
            c.Id,
            Name = string.IsNullOrEmpty(c.QrCode) ? c.Name : "⭐ " + c.Name,
            Phone = c.Phone,
            Address = string.IsNullOrEmpty(c.Address) ? "—" : c.Address,
            Credit = c.CreditBalance.ToString("N2"),
            Points = c.LoyaltyPoints
        }));
        dgvCustomers.DataSource = rows;
        dgvCustomers.Visible = true;
        // Explicit widths para LAGING kita ang buong pangalan at points (hindi napi-putol)
        if (dgvCustomers.Columns["Id"] != null) dgvCustomers.Columns["Id"].Width = 35;
        if (dgvCustomers.Columns["Name"] != null) dgvCustomers.Columns["Name"].Width = 270;
        if (dgvCustomers.Columns["Phone"] != null) dgvCustomers.Columns["Phone"].Width = 100;
        if (dgvCustomers.Columns["Address"] != null) dgvCustomers.Columns["Address"].Width = 110;
        if (dgvCustomers.Columns["Credit"] != null) dgvCustomers.Columns["Credit"].Width = 75;
        if (dgvCustomers.Columns["Points"] != null) dgvCustomers.Columns["Points"].Width = 70;
        // Tooltip para sa mahabang pangalan/address
        dgvCustomers.CellToolTipTextNeeded -= OnCellToolTip;
        dgvCustomers.CellToolTipTextNeeded += OnCellToolTip;
    }

    private void OnCellToolTip(object? s, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.RowIndex < 0) return;
        var col = dgvCustomers.Columns[e.ColumnIndex].Name;
        if (col == "Name" || col == "Address")
        {
            var v = dgvCustomers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
            if (!string.IsNullOrEmpty(v)) e.ToolTipText = v;
        }
    }

    private void SelectCurrent()
    {
        if (dgvCustomers.SelectedRows.Count > 0)
        {
            var id = Convert.ToInt32(dgvCustomers.SelectedRows[0].Cells["Id"].Value);
            SelectedCustomer = id == 0 ? null : _list.FirstOrDefault(c => c.Id == id);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private List<Customer> _list = new();

    private void InitializeComponent()
    {
        var accent = Color.FromArgb(72, 126, 176);
        var darkHeader = Color.FromArgb(30, 30, 45);
        var panelBg = Color.FromArgb(248, 249, 252);
        var textColor = Color.FromArgb(44, 44, 44);

        Text = "Select Customer";
        ClientSize = new Size(700, 450);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = panelBg;

        var pnlHeader = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(700, 55),
            BackColor = darkHeader
        };

        lblHeader = new Label
        {
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 180, 255),
            Location = new Point(15, 12),
            Size = new Size(660, 30),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblHeader);

        var lblSearch = new Label
        {
            Text = "SEARCH CUSTOMER",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(140, 140, 160),
            Location = new Point(15, 65),
            Size = new Size(200, 15)
        };

        txtSearch = new TextBox
        {
            Font = new Font("Segoe UI", 14F),
            Location = new Point(15, 82),
            Size = new Size(665, 32),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = textColor
        };
        txtSearch.TextChanged += (_, _) => RefreshGrid(txtSearch.Text.Trim());
        txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down && dgvCustomers.Visible && dgvCustomers.Rows.Count > 0)
            {
                e.SuppressKeyPress = true;
                dgvCustomers.Focus();
                if (dgvCustomers.Rows.Count > 0)
                    dgvCustomers.Rows[0].Selected = true;
            }
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        dgvCustomers = new DataGridView
        {
            Font = new Font("Segoe UI", 10F),
            Location = new Point(15, 122),
            Size = new Size(665, 250),
            Visible = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(230, 230, 235),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        dgvCustomers.DoubleClick += (_, _) => SelectCurrent();
        dgvCustomers.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SelectCurrent();
            }
            if (e.KeyCode == Keys.Up && dgvCustomers.SelectedRows.Count > 0 &&
                dgvCustomers.SelectedRows[0].Index == 0)
            {
                e.SuppressKeyPress = true;
                txtSearch.Focus();
                txtSearch.Select(txtSearch.Text.Length, 0);
            }
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        var btnCancel = new Button
        {
            Text = "CANCEL",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(215, 215, 220) },
            BackColor = Color.FromArgb(149, 165, 166),
            ForeColor = Color.White,
            Location = new Point(300, 390),
            Size = new Size(100, 35),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[] { pnlHeader, lblSearch, txtSearch, dgvCustomers, btnCancel });
    }

    private Label lblHeader = null!;
    private TextBox txtSearch = null!;
    private DataGridView dgvCustomers = null!;
}