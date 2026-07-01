using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public class PendingOrdersForm : Form
{
    private readonly User _currentUser;
    private readonly CustomerDisplayForm? _customerDisplay;
    private List<PendingTransfer> _transfers = new();
    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private DataGridView dgvOrders = null!;
    private Button btnProcess = null!;
    private Button btnRefresh = null!;
    private Label lblStatus = null!;

    private static Color CSurface => ThemeManager.Current.CanvasBg;
    private static Color CCard => ThemeManager.Current.PanelBg;
    private static Color CText => ThemeManager.Current.TextPrimary;
    private static Color CTextMuted => ThemeManager.Current.TextMuted;
    private static Color CAccent => ThemeManager.Current.AccentBlue;
    private static Color CGreen => ThemeManager.Current.AccentGreen;
    private static Color CBorder => ThemeManager.Current.BorderColor;

    public PendingOrdersForm(User user, CustomerDisplayForm? customerDisplay = null)
    {
        _currentUser = user;
        _customerDisplay = customerDisplay;
        InitializeComponent();
        _ = LoadTransfers();
    }

    private async Task LoadTransfers()
    {
        lblStatus.Text = "Loading...";
        var result = await SyncService.GetPendingTransfersAsync();
        _transfers = result ?? new List<PendingTransfer>();
        dgvOrders.Rows.Clear();
        foreach (var t in _transfers)
        {
            var idx = dgvOrders.Rows.Add();
            dgvOrders.Rows[idx].Cells[0].Value = t.OrderId;
            dgvOrders.Rows[idx].Cells[1].Value = t.ClientName;
            dgvOrders.Rows[idx].Cells[2].Value = $"\u20b1{t.TotalAmount:N2}";
            dgvOrders.Rows[idx].Cells[3].Value = t.Items.Count;
            dgvOrders.Rows[idx].Cells[4].Value = t.CreatedAt.ToString("MMM dd, hh:mm tt");
            dgvOrders.Rows[idx].Tag = t;
        }
        lblStatus.Text = _transfers.Count == 0 ? "No pending orders" : $"{_transfers.Count} order(s) pending";
        btnProcess.Enabled = _transfers.Count > 0;
    }

    private async void btnProcess_Click(object? sender, EventArgs e)
    {
        if (dgvOrders.SelectedRows.Count == 0) return;
        var transfer = dgvOrders.SelectedRows[0].Tag as PendingTransfer;
        if (transfer == null) return;

        btnProcess.Enabled = false;
        btnProcess.Text = "Processing...";

        try
        {
            // Match items to local products
            var items = new List<(int ProductId, string ProductName, string Barcode, int StockBefore, int Qty)>();
            var unmatched = new List<TransferItem>();

            foreach (var ti in transfer.Items)
            {
                var found = ProductService.GetByBarcode(ti.Barcode)
                         ?? ProductService.GetAll().FirstOrDefault(p =>
                            p.Name.Equals(ti.ProductName, StringComparison.OrdinalIgnoreCase));
                if (found == null)
                {
                    unmatched.Add(ti);
                    continue;
                }
                items.Add((found.Id, found.Name, found.Barcode ?? "", found.StockQty, ti.BaseQty));
            }

            if (unmatched.Count > 0 && items.Count == 0)
            {
                MessageBox.Show("No items could be matched to local products.\nPlease add these products first.", "Cannot Process", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (unmatched.Count > 0)
            {
                var msg = "Some items could not be matched to local products:\n";
                foreach (var u in unmatched)
                    msg += $"\n  • {u.ProductName} ({u.BaseQty} {u.BaseUnitName})";
                msg += "\n\nDo you want to receive the matched items only?";
                if (MessageBox.Show(msg, "Unmatched Items", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            if (items.Count == 0) return;

            // Mark transfer as received on cloud
            var received = await SyncService.MarkTransferReceivedAsync(transfer.OrderId);
            if (received == null)
            {
                MessageBox.Show("Failed to confirm transfer on cloud.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Add stock locally
            var userName = _currentUser?.FullName ?? _currentUser?.Username ?? "System";
            var error = StockService.ConfirmReceiving(items, _currentUser?.Id ?? 0, userName, $"WH-Transfer #{transfer.OrderId}");
            if (error != null)
            {
                MessageBox.Show($"Stock receiving error: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show($"Transfer #{transfer.OrderId} received — {items.Count} item(s) added to stock.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = LoadTransfers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error processing transfer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnProcess.Enabled = true;
            btnProcess.Text = "\uD83D\uDCCB Process Order";
        }
    }

    private void InitializeComponent()
    {
        Text = "Online Orders";
        ClientSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = CSurface;
        ForeColor = CText;
        Font = new Font("Segoe UI", 10F);

        var lblTitle = new Label
        {
            Text = "\uD83D\uDCE6 Pending Online Orders",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = CAccent,
            Location = new Point(20, 15),
            Size = new Size(500, 36)
        };

        lblStatus = new Label
        {
            Text = "Loading...",
            Font = new Font("Segoe UI", 10F),
            ForeColor = CTextMuted,
            Location = new Point(20, 52),
            Size = new Size(400, 22)
        };

        btnRefresh = new Button
        {
            Text = "\uD83D\uDD04 Refresh",
            Location = new Point(740, 15),
            Size = new Size(130, 36),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = CBorder },
            BackColor = CCard,
            ForeColor = CText,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += async (_, _) => await LoadTransfers();

        dgvOrders = new DataGridView
        {
            Location = new Point(20, 85),
            Size = new Size(860, 400),
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = CSurface,
            BorderStyle = BorderStyle.None,
            GridColor = CBorder,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Segoe UI", 10F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = CCard,
                ForeColor = CAccent,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            },
            ColumnHeadersHeight = 36,
            EnableHeadersVisualStyles = false,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = CSurface,
                ForeColor = CText,
                SelectionBackColor = ThemeManager.Current.DgvSelection,
                SelectionForeColor = Color.White
            },
            RowTemplate = { Height = 36 }
        };
        dgvOrders.Columns.Add("OrderId", "ORDER #");
        dgvOrders.Columns.Add("Client", "CLIENT");
        dgvOrders.Columns.Add("Total", "TOTAL");
        dgvOrders.Columns.Add("Items", "ITEMS");
        dgvOrders.Columns.Add("Date", "DATE");
        dgvOrders.Columns[0].FillWeight = 10;
        dgvOrders.Columns[1].FillWeight = 30;
        dgvOrders.Columns[2].FillWeight = 20;
        dgvOrders.Columns[3].FillWeight = 10;
        dgvOrders.Columns[4].FillWeight = 30;
        dgvOrders.DoubleClick += btnProcess_Click;

        btnProcess = new Button
        {
            Text = "\uD83D\uDCCB Process Order",
            Location = new Point(20, 505),
            Size = new Size(240, 48),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = CGreen,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnProcess.Click += btnProcess_Click;

        Controls.AddRange(new Control[] { lblTitle, lblStatus, btnRefresh, dgvOrders, btnProcess });
    }
}
