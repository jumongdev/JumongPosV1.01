using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class OrderTypeForm : Form
{
    public string SelectedType { get; private set; } = "Walk-in";

    public OrderTypeForm()
    {
        InitializeComponent();
        txtBarcodeScan.Focus();

        var onlineEnabled = true;
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'EnableOnlineOrders'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            onlineEnabled = val != "false";
        }
        catch { }

        if (!onlineEnabled)
        {
            btnOnline.Visible = false;
            var shift = 60;
            sep.Location = new Point(sep.Location.X, sep.Location.Y - shift);
            lblScan.Location = new Point(lblScan.Location.X, lblScan.Location.Y - shift);
            txtBarcodeScan.Location = new Point(txtBarcodeScan.Location.X, txtBarcodeScan.Location.Y - shift);
            lblPriceCheck.Location = new Point(lblPriceCheck.Location.X, lblPriceCheck.Location.Y - shift);
            ClientSize = new Size(380, 310);
        }

        DebugHelper.AddFormLabel(this);
    }

    private void SelectType(string type)
    {
        SelectedType = type;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void txtBarcodeScan_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            var code = txtBarcodeScan.Text.Trim();
            if (string.IsNullOrEmpty(code)) return;

            var product = ProductService.GetByBarcode(code);
            if (product != null)
            {
                var unit = ProductUnitService.GetDefault(product.Id);
                var price = unit?.Price ?? product.Price;
                var unitName = unit?.UnitName ?? "pc";
                lblPriceCheck.Text = $"{product.Name}  —  \u20b1{price:N2} / {unitName}";
                lblPriceCheck.ForeColor = Color.FromArgb(46, 204, 113);
            }
            else
            {
                lblPriceCheck.Text = $"Barcode '{code}' not found.";
                lblPriceCheck.ForeColor = Color.FromArgb(231, 76, 60);
            }
            txtBarcodeScan.Clear();
        }
    }

    private void InitializeComponent()
    {
        var accent = Color.FromArgb(72, 126, 176);
        var darkHeader = Color.FromArgb(30, 30, 45);
        var panelBg = Color.FromArgb(248, 249, 252);
        var textColor = Color.FromArgb(44, 44, 44);

        Text = "Order Type";
        ClientSize = new Size(380, 370);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = panelBg;

        var pnlHeader = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(380, 60),
            BackColor = darkHeader
        };

        var lblHeader = new Label
        {
            Text = "SELECT ORDER TYPE",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 180, 255),
            Location = new Point(20, 15),
            Size = new Size(340, 35),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblHeader);

        var btnWalkIn = new Button
        {
            Text = "WALK-IN",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(39, 174, 96) },
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Location = new Point(30, 85),
            Size = new Size(320, 45),
            Cursor = Cursors.Hand
        };
        btnWalkIn.Click += (s, e) => SelectType("Walk-in");

        var btnCounter = new Button
        {
            Text = "COUNTER (Track Customer)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(30, 100, 180) },
            BackColor = accent,
            ForeColor = Color.White,
            Location = new Point(30, 145),
            Size = new Size(320, 45),
            Cursor = Cursors.Hand
        };
        btnCounter.Click += (s, e) => SelectType("Counter");

        btnOnline = new Button
        {
            Text = "ONLINE ORDER",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(180, 100, 30) },
            BackColor = Color.FromArgb(230, 126, 34),
            ForeColor = Color.White,
            Location = new Point(30, 205),
            Size = new Size(320, 45),
            Cursor = Cursors.Hand
        };
        btnOnline.Click += (s, e) => SelectType("Online");

        sep = new Panel
        {
            Location = new Point(30, 265),
            Size = new Size(320, 1),
            BackColor = Color.FromArgb(220, 220, 225)
        };

        lblScan = new Label
        {
            Text = "SCAN BARCODE TO CHECK PRICE",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(140, 140, 160),
            Location = new Point(30, 278),
            Size = new Size(200, 15)
        };

        txtBarcodeScan = new TextBox
        {
            Font = new Font("Segoe UI", 14F),
            Location = new Point(30, 295),
            Size = new Size(320, 30),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = textColor
        };
        txtBarcodeScan.KeyDown += txtBarcodeScan_KeyDown;

        lblPriceCheck = new Label
        {
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(46, 204, 113),
            Location = new Point(30, 330),
            Size = new Size(320, 25),
            TextAlign = ContentAlignment.MiddleLeft
        };

        Controls.AddRange(new Control[] { pnlHeader, btnWalkIn, btnCounter, btnOnline, sep, lblScan, txtBarcodeScan, lblPriceCheck });
    }

    private TextBox txtBarcodeScan = null!;
    private Label lblPriceCheck = null!;
    private Label lblScan = null!;
    private Button btnOnline = null!;
    private Panel sep = null!;
}