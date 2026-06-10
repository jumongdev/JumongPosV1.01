using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class ProductUnitsForm : Form
{
    private readonly int _productId;
    private ProductUnit? _selected;

    public ProductUnitsForm(int productId, string productName)
    {
        _productId = productId;
        InitializeComponent();
        lblTitle.Text = $"Units for: {productName}";
        LoadUnits();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadUnits()
    {
        _suppressChange = true;
        var list = ProductUnitService.GetByProduct(_productId);

        dgvUnits.AutoGenerateColumns = false;
        dgvUnits.Columns.Clear();
        dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", Width = 40 });
        dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitName", HeaderText = "Unit", Width = 100 });
        dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Price", HeaderText = "Price", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cost", HeaderText = "Cost", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "QtyPerUnit", HeaderText = "Qty/Unit", Width = 80 });
        dgvUnits.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsDefault", HeaderText = "Default", Width = 65 });
        dgvUnits.DataSource = list;
        dgvUnits.RowHeadersVisible = false;
        _suppressChange = false;

        if (dgvUnits.Rows.Count > 0)
            dgvUnits.Rows[0].Selected = true;
    }

    private bool _suppressChange;

    private void dgvUnits_SelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressChange) return;
        if (dgvUnits.CurrentRow?.DataBoundItem is ProductUnit u)
        {
            _selected = u;
            txtUnitName.Text = u.UnitName;
            txtPrice.Text = u.Price.ToString("N2");
            nudQtyPerUnit.Value = u.QtyPerUnit;
            chkDefault.Checked = u.IsDefault;
            btnDelete.Enabled = true;
        }
    }

    private void ClearForm()
    {
        _selected = null;
        txtUnitName.Clear();
        txtPrice.Clear();
        nudQtyPerUnit.Value = 1;
        chkDefault.Checked = false;
        btnDelete.Enabled = false;
    }

    private void btnNew_Click(object? sender, EventArgs e)
    {
        ClearForm();
        txtUnitName.Focus();
    }

    private void btnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtUnitName.Text))
        {
            MessageBox.Show("Unit name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var u = _selected ?? new ProductUnit();
        u.ProductId = _productId;
        u.UnitName = txtUnitName.Text.Trim();
        u.Price = decimal.TryParse(txtPrice.Text, out var p) ? p : 0;
        u.QtyPerUnit = (int)nudQtyPerUnit.Value;
        var product = ProductService.GetById(_productId);
        u.Cost = u.QtyPerUnit * (product?.Cost ?? 0);
        u.IsDefault = chkDefault.Checked;

        ProductUnitService.Save(u);
        LoadUnits();
        ClearForm();
    }

    private void btnDelete_Click(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        if (MessageBox.Show($"Delete unit '{_selected.UnitName}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            ProductUnitService.Delete(_selected.Id);
            LoadUnits();
            ClearForm();
        }
    }

    private void InitializeComponent()
    {
        var canvasBg = Color.FromArgb(10, 10, 26);
        var panelBg = Color.FromArgb(20, 20, 40);
        var inputBg = Color.FromArgb(30, 30, 55);
        var inputFg = Color.FromArgb(230, 230, 245);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var dimText = Color.FromArgb(140, 140, 170);

        Text = "Manage Product Units";
        ClientSize = new Size(600, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = canvasBg;

        lblTitle = new Label
        {
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = neonTitle,
            Location = new Point(15, 15),
            Size = new Size(570, 25)
        };

        dgvUnits = new DataGridView
        {
            Location = new Point(15, 50),
            Size = new Size(570, 180),
            BackgroundColor = panelBg,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(40, 40, 70),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9F),
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(25, 25, 50), ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold) },
            ColumnHeadersHeight = 30,
            EnableHeadersVisualStyles = false,
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(22, 22, 45), ForeColor = inputFg, SelectionBackColor = Color.FromArgb(40, 40, 80), SelectionForeColor = Color.White },
            RowTemplate = { Height = 28 },
            AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(15, 15, 32) }
        };
        dgvUnits.SelectionChanged += dgvUnits_SelectionChanged;

        // Edit fields
        var y = 245;
        AddField("Unit Name:", ref txtUnitName, ref y, inputBg, inputFg, dimText);
        AddField("Price:", ref txtPrice, ref y, inputBg, inputFg, dimText);
        var lblQty = new Label
        {
            Text = "Qty Per Unit:",
            Location = new Point(15, y),
            Size = new Size(85, 25),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = dimText
        };
        nudQtyPerUnit = new NumericUpDown
        {
            Location = new Point(105, y),
            Size = new Size(80, 25),
            Minimum = 1,
            Maximum = 999999,
            Value = 1,
            BackColor = inputBg,
            ForeColor = inputFg
        };
        Controls.Add(lblQty);
        Controls.Add(nudQtyPerUnit);
        y += 30;

        chkDefault = new CheckBox
        {
            Text = "Default unit",
            Location = new Point(15, y),
            Size = new Size(120, 25),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = inputFg,
            BackColor = canvasBg
        };
        Controls.Add(chkDefault);
        y += 35;

        btnNew = new Button
        {
            Text = "+ New",
            Location = new Point(15, y),
            Size = new Size(80, 35),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(72, 126, 176),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
        btnNew.Click += btnNew_Click;

        btnSave = new Button
        {
            Text = "Save",
            Location = new Point(100, y),
            Size = new Size(80, 35),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
        btnSave.Click += btnSave_Click;

        btnDelete = new Button
        {
            Text = "Delete",
            Location = new Point(185, y),
            Size = new Size(80, 35),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(231, 76, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,
            Enabled = false
        };
        btnDelete.Click += btnDelete_Click;

        btnClose = new Button
        {
            Text = "Close",
            Location = new Point(500, y),
            Size = new Size(85, 35),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(149, 165, 166),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
        btnClose.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { lblTitle, dgvUnits, btnNew, btnSave, btnDelete, btnClose });
    }

    private void AddField(string label, ref TextBox box, ref int y, Color inputBg, Color inputFg, Color labelColor)
    {
        var lbl = new Label
        {
            Text = label,
            Location = new Point(15, y),
            Size = new Size(85, 25),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = labelColor
        };
        box = new TextBox
        {
            Location = new Point(105, y),
            Size = new Size(150, 25),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = inputBg,
            ForeColor = inputFg,
            Font = new Font("Segoe UI", 9F)
        };
        Controls.Add(lbl);
        Controls.Add(box);
        y += 30;
    }

    private Label lblTitle = null!;
    private DataGridView dgvUnits = null!;
    private TextBox txtUnitName = null!;
    private TextBox txtPrice = null!;
    private NumericUpDown nudQtyPerUnit = null!;
    private CheckBox chkDefault = null!;
    private Button btnNew = null!, btnSave = null!, btnDelete = null!, btnClose = null!;
}
