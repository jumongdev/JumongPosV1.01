using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Forms;

public partial class PaymentForm : Form
{
    private readonly decimal _grandTotal;
    private readonly Customer? _customer;

    public decimal AmountPaid    { get; private set; }
    public decimal Change        { get; private set; }
    public string  PaymentMethod { get; private set; } = "Cash";
    public string  ReferenceNo   { get; private set; } = "";
    public decimal CashPaid      { get; private set; }
    public decimal EwPaid        { get; private set; }
    public string  EwReferenceNo { get; private set; } = "";

    private static readonly Color CSurface    = Color.FromArgb(244, 245, 250);
    private static readonly Color CCard       = Color.White;
    private static readonly Color CBorder     = Color.FromArgb(220, 221, 230);
    private static readonly Color CBorderLt   = Color.FromArgb(236, 237, 243);
    private static readonly Color CText       = Color.FromArgb(30, 30, 46);
    private static readonly Color CTextMuted  = Color.FromArgb(110, 110, 140);
    private static readonly Color CTextHint   = Color.FromArgb(160, 160, 190);
    private static readonly Color CTopbar     = Color.FromArgb(26, 26, 46);
    private static readonly Color CTopbarAccent = Color.FromArgb(126, 184, 247);
    private static readonly Color CGreenDark  = Color.FromArgb(39, 80, 10);
    private static readonly Color CGreenMid   = Color.FromArgb(99, 153, 34);
    private static readonly Color CGreenLight = Color.FromArgb(234, 243, 222);
    private static readonly Color CBlueLight  = Color.FromArgb(230, 241, 251);
    private static readonly Color CBlueMid    = Color.FromArgb(24, 95, 165);
    private static readonly Color CBlueDark   = Color.FromArgb(12, 68, 124);
    private static readonly Color CAmberLight = Color.FromArgb(250, 238, 218);
    private static readonly Color CAmberDark  = Color.FromArgb(99, 56, 6);
    private static readonly Color CAmberMid   = Color.FromArgb(186, 117, 23);

    private string _activeMethod = "Cash";

    public PaymentForm(decimal grandTotal, Customer? customer = null)
    {
        _grandTotal = grandTotal;
        _customer   = customer;
        InitializeComponent();
        lblTotalAmount.Text = $"\u20b1{grandTotal:N2}";
        SelectMethod("Cash");
        KeyPreview = true;
        KeyDown += PaymentForm_KeyDown;
        DebugHelper.AddFormLabel(this);
    }

    private void PaymentForm_KeyDown(object? sender, KeyEventArgs e)
    {
        var methods = new[] { "Cash", "E-Wallet", "Split", "Credit" };
        var idx = Array.IndexOf(methods, _activeMethod);
        if (idx < 0) idx = 0;

        if (e.KeyCode == Keys.Left)
        {
            if (_activeMethod == "Credit" && _customer == null) idx = 2;
            idx = (idx - 1 + methods.Length) % methods.Length;
            if (methods[idx] == "Credit" && _customer == null) idx = 2;
            SelectMethod(methods[idx]);
            FlashMethodButton();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            idx = (idx + 1) % methods.Length;
            if (methods[idx] == "Credit" && _customer == null) idx = 0;
            SelectMethod(methods[idx]);
            FlashMethodButton();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            btnConfirm.PerformClick();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            e.SuppressKeyPress = true;
        }
    }

    private async void FlashMethodButton()
    {
        var methods = new[] { "Cash", "E-Wallet", "Split", "Credit" };
        var methodBtns = new[] { btnMethodCash, btnMethodEwallet, btnMethodSplit, btnMethodCredit };
        var idx = Array.IndexOf(methods, _activeMethod);
        if (idx < 0) return;

        var btn = methodBtns[idx];
        var origBack = btn.BackColor;
        var origFore = btn.ForeColor;
        btn.BackColor = Color.FromArgb(255, 215, 0);
        btn.ForeColor = Color.Black;
        await Task.Delay(120);
        btn.BackColor = origBack;
        btn.ForeColor = origFore;
    }

    private void SelectMethod(string method)
    {
        _activeMethod = method;

        var methodBtns = new[] { btnMethodCash, btnMethodEwallet, btnMethodSplit, btnMethodCredit };
        var methods    = new[] { "Cash", "E-Wallet", "Split", "Credit" };
        for (var i = 0; i < methodBtns.Length; i++)
        {
            var active = methods[i] == method;
            methodBtns[i].BackColor  = active ? CBlueLight : CSurface;
            methodBtns[i].ForeColor  = active ? CBlueDark  : CTextMuted;
            methodBtns[i].FlatAppearance.BorderColor = active ? CBlueMid : CBorder;
            methodBtns[i].FlatAppearance.BorderSize  = active ? 2 : 1;
        }

        pnlCash.Visible    = method == "Cash";
        pnlEwallet.Visible = method == "E-Wallet";
        pnlSplit.Visible   = method == "Split";
        pnlCredit.Visible  = method == "Credit";

        switch (method)
        {
            case "Cash":
                txtCashAmount.Text = "";
                lblChange.Text     = "\u20b10.00";
                lblChangePill.BackColor = CGreenLight;
                lblChangePill.ForeColor = CGreenDark;
                btnConfirm.BackColor = CGreenMid;
                btnConfirm.Text = $"Charge  \u20b1{_grandTotal:N2}";
                break;

            case "E-Wallet":
                txtEwRef.Text = "";
                btnConfirm.BackColor = CBlueMid;
                btnConfirm.Text = $"Confirm e-wallet  \u20b1{_grandTotal:N2}";
                break;

            case "Split":
                txtSplitCash.Text = "0.00";
                txtSplitEw.Text   = "0.00";
                txtSplitEwRef.Text = "";
                UpdateSplitSummary();
                btnConfirm.Text = $"Confirm split  \u20b1{_grandTotal:N2}";
                break;

            case "Credit":
                lblCreditInfo.Text = $"Charge \u20b1{_grandTotal:N2} to {_customer?.Name ?? "Customer"}'s credit.";
                btnConfirm.BackColor = CAmberMid;
                btnConfirm.Text = $"Confirm credit  \u20b1{_grandTotal:N2}";
                break;
        }

        btnMethodCredit.Enabled = _customer != null;
        btnMethodCredit.ForeColor = _customer != null ? btnMethodCredit.ForeColor : CTextHint;
    }

    private void txtCashAmount_TextChanged(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(txtCashAmount.Text, out var paid))
        {
            lblChange.Text = "\u20b10.00";
            return;
        }
        var change = paid - _grandTotal;
        lblChange.Text = change >= 0
            ? $"\u20b1{change:N2}"
            : $"-\u20b1{(-change):N2}";
        lblChangePill.BackColor = change >= 0 ? CGreenLight : Color.FromArgb(252, 235, 235);
        lblChangePill.ForeColor = change >= 0 ? CGreenDark  : Color.FromArgb(163, 45, 45);
    }

    private void UpdateSplitSummary()
    {
        decimal.TryParse(txtSplitCash.Text, out var cash);
        decimal.TryParse(txtSplitEw.Text, out var ew);
        var total    = cash + ew;
        var isEnough = total >= _grandTotal && total > 0;

        if (isEnough)
        {
            lblSplitStatus.Text      = $"Change: \u20b1{(total - _grandTotal):N2}";
            lblSplitStatus.ForeColor = CGreenDark;
            lblSplitStatus.BackColor = CGreenLight;
        }
        else
        {
            var shortfall = _grandTotal - total;
            lblSplitStatus.Text      = total > 0 ? $"Short: \u20b1{shortfall:N2}" : $"Enter amounts";
            lblSplitStatus.ForeColor = Color.FromArgb(163, 45, 45);
            lblSplitStatus.BackColor = Color.FromArgb(252, 235, 235);
        }

        btnConfirm.BackColor = isEnough ? CGreenMid : Color.FromArgb(170, 175, 180);
    }

    private void btnConfirm_Click(object? sender, EventArgs e)
    {
        switch (_activeMethod)
        {
            case "Cash":
                if (!decimal.TryParse(txtCashAmount.Text, out var paid) || paid < _grandTotal)
                {
                    MessageBox.Show($"Amount must be at least \u20b1{_grandTotal:N2}.", "Invalid Amount",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                AmountPaid = paid;
                Change = paid - _grandTotal;
                PaymentMethod = "Cash";
                ReferenceNo = "";
                break;

            case "E-Wallet":
                if (string.IsNullOrWhiteSpace(txtEwRef.Text))
                {
                    MessageBox.Show("Please enter the e-wallet reference number.", "Reference Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                AmountPaid = _grandTotal;
                Change = 0;
                PaymentMethod = "E-Wallet";
                ReferenceNo = txtEwRef.Text.Trim();
                break;

            case "Split":
                if (!decimal.TryParse(txtSplitCash.Text, out var splitCash)) splitCash = 0;
                if (!decimal.TryParse(txtSplitEw.Text,   out var splitEw))   splitEw   = 0;
                var splitTotal = splitCash + splitEw;

                if (splitTotal <= 0)
                {
                    MessageBox.Show("Please enter a valid payment amount.", "Invalid Amount",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (splitTotal < _grandTotal)
                {
                    MessageBox.Show(
                        $"Insufficient payment.\nTotal entered: \u20b1{splitTotal:N2}\nBalance due: \u20b1{_grandTotal:N2}\nShort: \u20b1{(_grandTotal - splitTotal):N2}",
                        "Insufficient", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (splitEw > 0 && string.IsNullOrWhiteSpace(txtSplitEwRef.Text))
                {
                    MessageBox.Show("Please enter the e-wallet reference number for the e-wallet portion.",
                        "Reference Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                CashPaid      = splitCash;
                EwPaid        = splitEw;
                EwReferenceNo = txtSplitEwRef.Text.Trim();
                AmountPaid    = splitTotal;
                Change        = splitTotal - _grandTotal;
                PaymentMethod = "Split";
                ReferenceNo   = EwReferenceNo;
                break;

            case "Credit":
                AmountPaid    = _grandTotal;
                Change        = 0;
                PaymentMethod = "Credit";
                ReferenceNo   = "";
                break;

            default:
                return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void InitializeComponent()
    {
        Text = "Payment";
        ClientSize = new Size(420, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = CSurface;
        Font = new Font("Segoe UI", 10F);

        var pnlHeader = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(420, 48),
            BackColor = CTopbar
        };
        var lblHeaderTitle = new Label
        {
            Text = "Complete payment",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = CTopbarAccent,
            Location = new Point(16, 10),
            Size = new Size(300, 28),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblHeaderTitle);

        var pnlTotalBlock = new Panel
        {
            Location = new Point(0, 48),
            Size = new Size(420, 74),
            BackColor = CCard
        };
        pnlTotalBlock.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLt, 1);
            e.Graphics.DrawLine(pen, 0, 73, 420, 73);
        };

        var lblTotalHint = new Label
        {
            Text = "Total due",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CTextMuted,
            Location = new Point(18, 10),
            Size = new Size(120, 16)
        };
        lblTotalAmount = new Label
        {
            Font = new Font("Segoe UI", 26F, FontStyle.Bold),
            ForeColor = CGreenDark,
            Location = new Point(16, 26),
            Size = new Size(250, 38),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlTotalBlock.Controls.AddRange(new Control[] { lblTotalHint, lblTotalAmount });

        var pnlMethodBlock = new Panel
        {
            Location = new Point(0, 122),
            Size = new Size(420, 86),
            BackColor = CCard
        };
        pnlMethodBlock.Paint += (s, e) =>
        {
            using var pen = new Pen(CBorderLt, 1);
            e.Graphics.DrawLine(pen, 0, 85, 420, 85);
        };

        var lblMethodHint = new Label
        {
            Text = "Payment method",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint,
            Location = new Point(16, 8),
            Size = new Size(160, 14)
        };

        Button MakeMethodBtn(string text, string icon, int x) => new Button
        {
            Text = $"{icon}\n{text}",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(x, 24),
            Size = new Size(90, 52),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
            BackColor = CSurface,
            ForeColor = CTextMuted,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };

        btnMethodCash    = MakeMethodBtn("Cash",     "\u25a0", 16);
        btnMethodEwallet = MakeMethodBtn("E-Wallet", "\u25cb", 114);
        btnMethodSplit   = MakeMethodBtn("Split",    "\u25d6", 212);
        btnMethodCredit  = MakeMethodBtn("Credit",   "\u25c6", 310);

        btnMethodCash.Click    += (_, _) => SelectMethod("Cash");
        btnMethodEwallet.Click += (_, _) => SelectMethod("E-Wallet");
        btnMethodSplit.Click   += (_, _) => SelectMethod("Split");
        btnMethodCredit.Click  += (_, _) => SelectMethod("Credit");

        pnlMethodBlock.Controls.AddRange(new Control[]
            { lblMethodHint, btnMethodCash, btnMethodEwallet, btnMethodSplit, btnMethodCredit });

        pnlCash = new Panel
        {
            Location = new Point(0, 208),
            Size = new Size(420, 210),
            BackColor = CCard,
            Visible = true
        };

        var lblTenderHint = new Label
        {
            Text = "Quick tender",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint,
            Location = new Point(16, 10), Size = new Size(120, 14)
        };

        Button MakeTender(string t, int x, Action click) {
            var b = new Button
            {
                Text = t, Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(x, 26), Size = new Size(88, 32),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
                BackColor = CSurface, ForeColor = CText, Cursor = Cursors.Hand
            };
            b.Click += (_, _) => click();
            return b;
        }

        var btnExact = MakeTender("Exact", 16,  () => { txtCashAmount.Text = _grandTotal.ToString("0.00"); });
        var btn500   = MakeTender("\u20b1500",  112, () => { txtCashAmount.Text = "500"; });
        var btn1000  = MakeTender("\u20b11,000",208, () => { txtCashAmount.Text = "1000"; });

        var lblCashAmtHint = new Label
        {
            Text = "Cash tendered",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint,
            Location = new Point(16, 68), Size = new Size(160, 14)
        };
        txtCashAmount = new TextBox
        {
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            Location = new Point(16, 84), Size = new Size(386, 42),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 249, 252),
            ForeColor = CText, TextAlign = HorizontalAlignment.Right
        };
        txtCashAmount.TextChanged += txtCashAmount_TextChanged;

        lblChangePill = new Label
        {
            Text = "Change",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CGreenDark, BackColor = CGreenLight,
            Location = new Point(16, 138), Size = new Size(68, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(4, 0, 4, 0)
        };
        lblChange = new Label
        {
            Text = "\u20b10.00",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = CGreenDark,
            Location = new Point(92, 134), Size = new Size(310, 30),
            TextAlign = ContentAlignment.MiddleRight
        };

        pnlCash.Controls.AddRange(new Control[]
            { lblTenderHint, btnExact, btn500, btn1000, lblCashAmtHint, txtCashAmount, lblChangePill, lblChange });

        pnlEwallet = new Panel
        {
            Location = new Point(0, 208),
            Size = new Size(420, 120),
            BackColor = CCard,
            Visible = false
        };

        var lblEwAmtHint = new Label
        {
            Text = $"Amount (exact: \u20b1{_grandTotal:N2})",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint,
            Location = new Point(16, 10), Size = new Size(300, 14)
        };
        var txtEwAmt = new TextBox
        {
            Text = _grandTotal.ToString("0.00"),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point(16, 26), Size = new Size(386, 36),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(240, 242, 248),
            ForeColor = CTextMuted, TextAlign = HorizontalAlignment.Right,
            ReadOnly = true
        };
        var lblEwRefHint = new Label
        {
            Text = "Reference number",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = CTextHint,
            Location = new Point(16, 72), Size = new Size(200, 14)
        };
        txtEwRef = new TextBox
        {
            Font = new Font("Segoe UI", 12F),
            Location = new Point(16, 88), Size = new Size(386, 28),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 249, 252),
            ForeColor = CText
        };
        pnlEwallet.Controls.AddRange(new Control[] { lblEwAmtHint, txtEwAmt, lblEwRefHint, txtEwRef });

        pnlSplit = new Panel
        {
            Location = new Point(0, 208),
            Size = new Size(420, 220),
            BackColor = CCard,
            Visible = false
        };

        var lblSpCashHint = new Label
        {
            Text = "Cash amount",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = CTextHint,
            Location = new Point(16, 10), Size = new Size(160, 14)
        };
        txtSplitCash = new TextBox
        {
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point(16, 26), Size = new Size(386, 36),
            TextAlign = HorizontalAlignment.Right, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 249, 252), ForeColor = CText, Text = "0.00"
        };
        txtSplitCash.TextChanged += (_, _) => UpdateSplitSummary();

        var lblSpEwHint = new Label
        {
            Text = "E-wallet amount",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = CTextHint,
            Location = new Point(16, 72), Size = new Size(160, 14)
        };
        txtSplitEw = new TextBox
        {
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point(16, 88), Size = new Size(386, 36),
            TextAlign = HorizontalAlignment.Right, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 249, 252), ForeColor = CText, Text = "0.00"
        };
        txtSplitEw.TextChanged += (_, _) => UpdateSplitSummary();

        var lblSpRefHint = new Label
        {
            Text = "E-wallet reference #",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = CTextHint,
            Location = new Point(16, 134), Size = new Size(200, 14)
        };
        txtSplitEwRef = new TextBox
        {
            Font = new Font("Segoe UI", 11F),
            Location = new Point(16, 150), Size = new Size(386, 28),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 249, 252), ForeColor = CText
        };

        lblSplitStatus = new Label
        {
            Text = "Enter amounts",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(163, 45, 45),
            BackColor = Color.FromArgb(252, 235, 235),
            Location = new Point(16, 186), Size = new Size(386, 24),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 8, 0)
        };

        pnlSplit.Controls.AddRange(new Control[]
            { lblSpCashHint, txtSplitCash, lblSpEwHint, txtSplitEw, lblSpRefHint, txtSplitEwRef, lblSplitStatus });

        pnlCredit = new Panel
        {
            Location = new Point(0, 208),
            Size = new Size(420, 80),
            BackColor = CCard,
            Visible = false
        };

        lblCreditInfo = new Label
        {
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = CAmberDark, BackColor = CAmberLight,
            Location = new Point(16, 14), Size = new Size(386, 50),
            TextAlign = ContentAlignment.MiddleCenter
        };
        pnlCredit.Controls.Add(lblCreditInfo);

        btnConfirm = new Button
        {
            Text = $"Charge  \u20b1{_grandTotal:N2}",
            Location = new Point(16, 534),
            Size = new Size(388, 50),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = CGreenMid,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnConfirm.Click += btnConfirm_Click;

        Controls.AddRange(new Control[]
        {
            pnlHeader, pnlTotalBlock, pnlMethodBlock,
            pnlCash, pnlEwallet, pnlSplit, pnlCredit,
            btnConfirm
        });
    }

    private Label    lblTotalAmount = null!;
    private Button   btnMethodCash = null!, btnMethodEwallet = null!,
                     btnMethodSplit = null!, btnMethodCredit = null!;
    private Panel    pnlCash = null!, pnlEwallet = null!,
                     pnlSplit = null!, pnlCredit = null!;
    private TextBox  txtCashAmount = null!;
    private Label    lblChangePill = null!, lblChange = null!;
    private TextBox  txtEwRef = null!;
    private TextBox  txtSplitCash = null!, txtSplitEw = null!, txtSplitEwRef = null!;
    private Label    lblSplitStatus = null!;
    private Label    lblCreditInfo = null!;
    private Button   btnConfirm = null!;
}
