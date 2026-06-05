using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class LoginForm : Form
{
    public User? CurrentUser { get; private set; }

    public LoginForm()
    {
        InitializeComponent();
        DebugHelper.AddFormLabel(this);
    }

    private void btnLogin_Click(object? sender, EventArgs e)
    {
        var username = txtUsername.Text.Trim();
        var password = txtPassword.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            lblError.Text = "Enter username and password.";
            return;
        }

        var user = AuthService.Login(username, password);
        if (user != null)
        {
            if (!ShiftSessionService.IsShiftActive() && user.Role != "Admin")
            {
                var openingBalance = PromptOpeningBalance(user);
                if (openingBalance == null) return;
                var cashierName = string.IsNullOrEmpty(user.FullName) ? user.Username : user.FullName;
                ShiftSessionService.StartSession(openingBalance.Value, cashierName);
                AuditLogService.Log("ShiftSessionStarted", "ShiftOpeningBalance", "0", openingBalance.Value.ToString("F2"), cashierName);
            }

            CurrentUser = user;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            lblError.Text = "Invalid username or password.";
            txtPassword.Clear();
            txtPassword.Focus();
        }
    }

    private static decimal? PromptOpeningBalance(User user)
    {
        var canvasBg = Color.FromArgb(10, 10, 26);
        var panelBg = Color.FromArgb(20, 20, 40);
        var inputBg = Color.FromArgb(30, 30, 55);
        var inputFg = Color.FromArgb(230, 230, 245);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var dimText = Color.FromArgb(140, 140, 170);
        var borderColor = Color.FromArgb(40, 40, 70);
        var accentGreen = Color.FromArgb(46, 204, 113);
        var accentGrey = Color.FromArgb(149, 165, 166);

        var cashierName = string.IsNullOrEmpty(user.FullName) ? user.Username : user.FullName;
        decimal? result = null;

        using var form = new Form
        {
            Text = "Opening Balance — New Shift",
            ClientSize = new Size(460, 340),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = canvasBg,
            ShowInTaskbar = false,
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        };

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblTitle = new Label { Text = "\uD83D\uDCB0 OPENING DRAWER BALANCE", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(420, 28) };
        pnlToolbar.Controls.Add(lblTitle);

        var pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };

        var lblWelcome = new Label
        {
            Text = $"Welcome, {cashierName}",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 230, 245),
            Location = new Point(30, 15),
            Size = new Size(400, 24)
        };
        pnlContent.Controls.Add(lblWelcome);

        var lblInfo = new Label
        {
            Text = "A new shift session requires an opening\ncash drawer balance before you can proceed.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = dimText,
            Location = new Point(30, 42),
            Size = new Size(400, 36)
        };
        pnlContent.Controls.Add(lblInfo);

        var pnlInput = new Panel
        {
            Location = new Point(15, 95),
            Size = new Size(415, 85),
            BackColor = panelBg
        };
        pnlInput.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlInput.Width - 1, pnlInput.Height - 1); };

        var lblAmount = new Label
        {
            Text = "Amount in Drawer (\u20b1):",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = neonTitle,
            Location = new Point(15, 10),
            Size = new Size(200, 22)
        };
        pnlInput.Controls.Add(lblAmount);

        var numAmount = new NumericUpDown
        {
            DecimalPlaces = 2,
            Maximum = 9999999,
            Minimum = 0,
            Value = 0,
            Location = new Point(15, 38),
            Size = new Size(385, 30),
            BackColor = inputBg,
            ForeColor = inputFg,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Center,
            BorderStyle = BorderStyle.None
        };
        pnlInput.Controls.Add(numAmount);

        pnlContent.Controls.Add(pnlInput);

        var btnConfirm = new Button
        {
            Text = "\u2705 CONFIRM & ENTER POS",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = accentGreen,
            ForeColor = Color.White,
            Location = new Point(30, 200),
            Size = new Size(400, 44),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };
        pnlContent.Controls.Add(btnConfirm);

        var btnCancel = new Button
        {
            Text = "\u2716 CANCEL",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = accentGrey,
            ForeColor = Color.White,
            Location = new Point(30, 250),
            Size = new Size(400, 34),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        pnlContent.Controls.Add(btnCancel);

        form.Controls.AddRange(new Control[] { pnlContent, pnlToolbar });
        form.AcceptButton = btnConfirm;
        form.CancelButton = btnCancel;

        if (form.ShowDialog() == DialogResult.OK)
        {
            result = numAmount.Value;
        }

        return result;
    }

    private void InitializeComponent()
    {
        var darkBg = Color.FromArgb(30, 30, 45);
        var accent = Color.FromArgb(100, 180, 255);

        Text = "Jumong POS - Login";
        ClientSize = new Size(380, 320);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 249, 252);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var pnlHeader = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(380, 80),
            BackColor = darkBg
        };

        var lblTitle = new Label
        {
            Text = "JUMONG POS",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = accent,
            Location = new Point(0, 20),
            Size = new Size(380, 40),
            TextAlign = ContentAlignment.MiddleCenter
        };
        pnlHeader.Controls.Add(lblTitle);

        var lblUser = new Label
        {
            Text = "Username",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 80, 90),
            Location = new Point(40, 105),
            Size = new Size(300, 18)
        };

        txtUsername = new TextBox
        {
            Font = new Font("Segoe UI", 12F),
            Location = new Point(40, 125),
            Size = new Size(300, 30),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

        var lblPass = new Label
        {
            Text = "Password",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 80, 90),
            Location = new Point(40, 165),
            Size = new Size(300, 18)
        };

        txtPassword = new TextBox
        {
            Font = new Font("Segoe UI", 12F),
            Location = new Point(40, 185),
            Size = new Size(300, 30),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            UseSystemPasswordChar = true
        };
        txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnLogin.PerformClick(); } };

        lblError = new Label
        {
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(231, 76, 60),
            Location = new Point(40, 225),
            Size = new Size(300, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        btnLogin = new Button
        {
            Text = "LOG IN",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Location = new Point(100, 255),
            Size = new Size(180, 45),
            Cursor = Cursors.Hand
        };
        btnLogin.Click += btnLogin_Click;

        Controls.AddRange(new Control[] { pnlHeader, lblUser, txtUsername, lblPass, txtPassword, lblError, btnLogin });
    }

    private TextBox txtUsername = null!;
    private TextBox txtPassword = null!;
    private Label lblError = null!;
    private Button btnLogin = null!;
}
