using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class InventoryHistoryForm : Form
{
    private readonly User? _currentUser;
    private DataGridView _dgv = null!;
    private Panel _pnlDetails = null!;
    private Label _lblDetailTitle = null!;
    private DataGridView _dgvDetail = null!;
    private RichTextBox _txtReport = null!;
    private Button _btnCopy = null!;

    public InventoryHistoryForm(User? currentUser = null)
    {
        _currentUser = currentUser;
        InitializeComponent();
        LoadSessions();
        DebugHelper.AddFormLabel(this);
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;
        var canvasBg = t.CanvasBg;
        var panelBg = t.PanelBg;
        var inputFg = t.InputFg;
        var neonTitle = t.AccentCyan;
        var dimText = t.TextMuted;
        var borderColor = t.BorderColor;

        BackColor = canvasBg;
        Text = "Inventory Count History";
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };

        var lblTitle = new Label { Text = "  \uD83D\uDCCA INVENTORY COUNT SESSIONS", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(400, 28) };

        var btnClose = new Button { Text = "\u2716 CLOSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(0, 8), Size = new Size(110, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentGrey, ForeColor = Color.White, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnClose.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlToolbar.Controls.AddRange([lblTitle, btnClose]);

        // Sessions grid
        _dgv = new DataGridView
        {
            Location = new Point(10, 60),
            Size = new Size(450, 400),
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = panelBg,
            BorderStyle = BorderStyle.None,
            GridColor = t.DgvGrid,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = t.DgvHeaderBg, ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter },
            ColumnHeadersHeight = 30,
            EnableHeadersVisualStyles = false,
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = t.DgvRowNormal, ForeColor = inputFg, SelectionBackColor = t.DgvSelection, SelectionForeColor = Color.White, Padding = new Padding(4, 2, 4, 2) },
            RowTemplate = { Height = 28 },
            AlternatingRowsDefaultCellStyle = { BackColor = t.DgvRowAlt }
        };

        _dgv.Columns.Add("SessionId", "Session ID");
        _dgv.Columns.Add("CountedBy", "Counted By");
        _dgv.Columns.Add("StartedAt", "Started");
        _dgv.Columns.Add("EndedAt", "Ended");
        _dgv.Columns.Add("Status", "Status");
        _dgv.Columns.Add("TotalItems", "Items");
        _dgv.Columns.Add("WithVariance", "Variance");

        _dgv.Columns["SessionId"]!.Width = 120;
        _dgv.Columns["CountedBy"]!.Width = 100;
        _dgv.Columns["StartedAt"]!.Width = 150;
        _dgv.Columns["EndedAt"]!.Width = 150;
        _dgv.Columns["Status"]!.Width = 80;
        _dgv.Columns["TotalItems"]!.Width = 60;
        _dgv.Columns["WithVariance"]!.Width = 70;

        _dgv.SelectionChanged += (_, _) => ShowSessionDetails();

        // Details panel
        _pnlDetails = new Panel { Location = new Point(470, 60), Size = new Size(100, 100), BackColor = panelBg };

        _lblDetailTitle = new Label { Text = "Select a session", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = dimText, Location = new Point(10, 10), Size = new Size(400, 25) };

        _dgvDetail = new DataGridView
        {
            Location = new Point(10, 40),
            Size = new Size(100, 200),
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = panelBg,
            BorderStyle = BorderStyle.None,
            GridColor = t.DgvGrid,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = t.DgvHeaderBg, ForeColor = neonTitle, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter },
            ColumnHeadersHeight = 28,
            EnableHeadersVisualStyles = false,
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = t.DgvRowNormal, ForeColor = inputFg, SelectionBackColor = t.DgvSelection, SelectionForeColor = Color.White, Padding = new Padding(4, 2, 4, 2) },
            RowTemplate = { Height = 26 },
            AlternatingRowsDefaultCellStyle = { BackColor = t.DgvRowAlt }
        };

        _dgvDetail.Columns.Add("Product", "Product");
        _dgvDetail.Columns.Add("Barcode", "Barcode");
        _dgvDetail.Columns.Add("System", "System");
        _dgvDetail.Columns.Add("Actual", "Actual");
        _dgvDetail.Columns.Add("Variance", "Var");
        _dgvDetail.Columns.Add("Adjusted", "Adj");

        _dgvDetail.Columns["Product"]!.Width = 180;
        _dgvDetail.Columns["Barcode"]!.Width = 100;
        _dgvDetail.Columns["System"]!.Width = 60;
        _dgvDetail.Columns["Actual"]!.Width = 60;
        _dgvDetail.Columns["Variance"]!.Width = 60;
        _dgvDetail.Columns["Adjusted"]!.Width = 50;

        _txtReport = new RichTextBox
        {
            Location = new Point(10, 250),
            Size = new Size(100, 200),
            BackColor = canvasBg,
            ForeColor = inputFg,
            Font = new Font("Consolas", 9F),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };

        _btnCopy = new Button { Text = "COPY REPORT", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentGreen, ForeColor = Color.White, Location = new Point(10, 460), Size = new Size(150, 30), Cursor = Cursors.Hand };
        _btnCopy.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_txtReport.Text))
            {
                Clipboard.SetText(_txtReport.Text);
                MessageBox.Show("Report copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        _pnlDetails.Controls.AddRange([_lblDetailTitle, _dgvDetail, _txtReport, _btnCopy]);

        Controls.AddRange([pnlToolbar, _dgv, _pnlDetails]);

        Resize += (_, _) =>
        {
            _dgv.Size = new Size(450, Height - 70);
            _pnlDetails.Location = new Point(470, 60);
            _pnlDetails.Size = new Size(Width - 490, Height - 70);
            _dgvDetail.Size = new Size(_pnlDetails.Width - 20, 180);
            _txtReport.Size = new Size(_pnlDetails.Width - 20, _pnlDetails.Height - 270);
        };
    }

    private void LoadSessions()
    {
        var sessions = InventoryService.GetAllSessions();
        _dgv.Rows.Clear();
        foreach (var s in sessions)
        {
            _dgv.Rows.Add(s.SessionId, s.CountedBy, s.StartedAt, s.EndedAt ?? "—", s.Status, s.TotalItems, s.ItemsWithVariance);
        }
    }

    private void ShowSessionDetails()
    {
        if (_dgv.SelectedRows.Count == 0) return;

        var row = _dgv.SelectedRows[0];
        var sessionId = row.Cells["SessionId"].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(sessionId)) return;

        var session = InventoryService.GetSession(sessionId);
        var counts = InventoryService.GetSessionCounts(sessionId);
        var report = InventoryService.GetSessionReport(sessionId);

        _lblDetailTitle.Text = $"Session: {sessionId}  |  {session?.CountedBy}  |  {counts.Count} items";

        _dgvDetail.Rows.Clear();
        foreach (var c in counts)
        {
            var varStr = c.Variance >= 0 ? $"+{c.Variance}" : c.Variance.ToString();
            _dgvDetail.Rows.Add(c.ProductName, c.Barcode, c.SystemQty, c.ActualQty, varStr, c.Adjusted ? "✓" : "");
        }

        _txtReport.Text = report;
    }

    public void ApplyTheme()
    {
        // Theme colors refresh on next show
    }
}
