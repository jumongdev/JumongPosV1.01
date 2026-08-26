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
        DebugHelper.AddFormLabel(this);
    }

    private void SelectType(string type)
    {
        SelectedType = type;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void InitializeComponent()
    {
        var accent = Color.FromArgb(72, 126, 176);
        var darkHeader = Color.FromArgb(30, 30, 45);
        var panelBg = Color.FromArgb(248, 249, 252);
        var textColor = Color.FromArgb(44, 44, 44);

        Text = "Order Type";
        ClientSize = new Size(380, 225);
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

        Controls.AddRange(new Control[] { pnlHeader, btnWalkIn, btnCounter });
    }
}