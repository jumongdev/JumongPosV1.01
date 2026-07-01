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
        btnProcess.Text = "Loading items...";

        try
        {
            // Fetch order items from cloud (product name, barcode, base_qty)
            var allItems = await SyncService.GetTransferItemsAsync(transfer.OrderId);
            if (allItems == null || allItems.Count == 0)
            {
                MessageBox.Show("Could not load items for this order.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Show item picker with checkboxes
            var (checkedItems, hasUnmatched) = ShowItemPicker(allItems);
            if (checkedItems == null) return; // user cancelled

            if (checkedItems.Count == 0)
            {
                MessageBox.Show("No items selected. Receiving cancelled.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Send checked items to cloud (restocks shortages)
            var result = await SyncService.MarkTransferReceivedAsync(transfer.OrderId, checkedItems);
            if (result == null || !result.Success)
            {
                MessageBox.Show("Failed to confirm transfer on cloud.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Add stock locally for checked items
            var userName = _currentUser?.FullName ?? _currentUser?.Username ?? "System";
            var receivingItems = new List<(int ProductId, string ProductName, string Barcode, int StockBefore, int Qty)>();
            foreach (var ci in checkedItems)
            {
                var found = ProductService.GetByBarcode(ci.Barcode)
                         ?? ProductService.GetAll().FirstOrDefault(p =>
                            p.Name.Equals(ci.ProductName, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    receivingItems.Add((found.Id, found.Name, found.Barcode ?? "", found.StockQty, ci.BaseQty));
            }

            if (receivingItems.Count > 0)
            {
                var error = StockService.ConfirmReceiving(receivingItems, _currentUser?.Id ?? 0, userName, $"WH-Transfer #{transfer.OrderId}");
                if (error != null)
                    MessageBox.Show($"Stock receiving error: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            var shortageMsg = (result.Shortages != null && result.Shortages.Count > 0)
                ? $"\n{result.Shortages.Count} item(s) reported as shortage."
                : "";
            MessageBox.Show($"Transfer #{transfer.OrderId} received — {receivingItems.Count} item(s) added to stock.{shortageMsg}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private (List<TransferItem>? Checked, bool HasUnmatched) ShowItemPicker(List<TransferItem> items)
    {
        var checkedItems = new List<TransferItem>();
        var hasUnmatched = false;

        using var picker = new Form
        {
            Text = "Receive Warehouse Transfer",
            Size = new Size(650, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            BackColor = CSurface
        };

        var lbl = new Label
        {
            Text = "Uncheck items that did not arrive:",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = CAccent,
            Location = new Point(12, 12),
            Size = new Size(600, 22)
        };

        var panel = new Panel
        {
            Location = new Point(12, 42),
            Size = new Size(600, 370),
            AutoScroll = true,
            BackColor = CCard,
            BorderStyle = BorderStyle.FixedSingle
        };

        var checkboxes = new Dictionary<int, CheckBox>();
        var y = 5;
        foreach (var item in items)
        {
            var found = ProductService.GetByBarcode(item.Barcode)
                     ?? ProductService.GetAll().FirstOrDefault(p =>
                        p.Name.Equals(item.ProductName, StringComparison.OrdinalIgnoreCase));
            if (found == null)
            {
                hasUnmatched = true;
                var lblUnmatched = new Label
                {
                    Text = $"\u26A0 {item.ProductName} — {item.BaseQty} {item.BaseUnitName} (not found in POS)",
                    Location = new Point(8, y),
                    Size = new Size(570, 24),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = ThemeManager.Current.AccentRed
                };
                panel.Controls.Add(lblUnmatched);
                y += 28;
                continue;
            }

            var cb = new CheckBox
            {
                Text = $"{item.ProductName} — {item.BaseQty} {item.BaseUnitName} (barcode: {item.Barcode})",
                Location = new Point(5, y),
                Size = new Size(580, 24),
                Font = new Font("Segoe UI", 9F),
                ForeColor = CText,
                Checked = true,
                Tag = item
            };
            panel.Controls.Add(cb);
            checkboxes[item.ProductId] = cb;
            y += 28;
        }

        if (y < panel.Height) panel.Height = y + 10;

        var btnOk = new Button
        {
            Text = "RECEIVE CHECKED",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = CGreen,
            ForeColor = Color.White,
            Location = new Point(12, panel.Bottom + 12),
            Size = new Size(200, 40),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };

        var btnCancel = new Button
        {
            Text = "CANCEL",
            Font = new Font("Segoe UI", 10F),
            FlatStyle = FlatStyle.Flat,
            BackColor = CCard,
            ForeColor = CText,
            Location = new Point(220, panel.Bottom + 12),
            Size = new Size(120, 40),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };

        picker.Controls.AddRange(new Control[] { lbl, panel, btnOk, btnCancel });
        picker.AcceptButton = btnOk;
        picker.CancelButton = btnCancel;

        if (picker.ShowDialog() == DialogResult.OK)
        {
            foreach (var cb in checkboxes.Values)
            {
                if (cb.Checked && cb.Tag is TransferItem ti)
                    checkedItems.Add(ti);
            }
        }

        return (checkedItems.Count > 0 ? checkedItems : null, hasUnmatched);
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
