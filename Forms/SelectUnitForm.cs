using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Forms;

public partial class SelectUnitForm : Form
{
    public ProductUnit? SelectedUnit { get; private set; }

    public SelectUnitForm(string productName, List<ProductUnit> units)
    {
        InitializeComponent();
        lblProduct.Text = productName;
        lstUnits.Items.Clear();
        foreach (var u in units)
        {
            var text = $"{u.UnitName}  —  ₱{u.Price:N2}  (x{u.QtyPerUnit} pcs)";
            lstUnits.Items.Add(text);
        }
        if (units.Count > 0)
            lstUnits.SelectedIndex = units.FindIndex(u => u.IsDefault);
        if (lstUnits.SelectedIndex < 0)
            lstUnits.SelectedIndex = 0;
        _units = units;
        DebugHelper.AddFormLabel(this);
    }

    private readonly List<ProductUnit> _units;

    private void SelectUnit()
    {
        if (lstUnits.SelectedIndex >= 0)
        {
            SelectedUnit = _units[lstUnits.SelectedIndex];
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void lstUnits_DoubleClick(object? sender, EventArgs e) => SelectUnit();
    private void btnSelect_Click(object? sender, EventArgs e) => SelectUnit();

    private void InitializeComponent()
    {
        Text = "Select Unit";
        ClientSize = new Size(320, 220);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 249, 252);

        lblProduct = new Label
        {
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 44, 44),
            Location = new Point(15, 12),
            Size = new Size(290, 20)
        };

        var lblHint = new Label
        {
            Text = "Select a unit (\u2191\u2193 + Enter to select):",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(120, 120, 130),
            Location = new Point(15, 35),
            Size = new Size(290, 18)
        };

        lstUnits = new ListBox
        {
            Font = new Font("Segoe UI", 10F),
            Location = new Point(15, 58),
            Size = new Size(290, 90),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        lstUnits.DoubleClick += lstUnits_DoubleClick;
        lstUnits.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SelectUnit(); } };

        btnSelect = new Button
        {
            Text = "SELECT",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Location = new Point(105, 160),
            Size = new Size(110, 40),
            Cursor = Cursors.Hand
        };
        btnSelect.Click += btnSelect_Click;

        Controls.AddRange(new Control[] { lblProduct, lblHint, lstUnits, btnSelect });
    }

    private Label lblProduct = null!;
    private ListBox lstUnits = null!;
    private Button btnSelect = null!;
}
