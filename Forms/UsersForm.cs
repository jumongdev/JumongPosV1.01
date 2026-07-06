using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class UsersForm : Form
{
    private readonly User? _currentUser;
    private readonly bool _isAdmin;

    public UsersForm(User? currentUser = null)
    {
        _currentUser = currentUser;
        _isAdmin = _currentUser?.Role == "Admin";
        InitializeComponent();
        if (_isAdmin) LoadUsers();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadUsers()
    {
        var data = UserService.GetAll();
        dgvUsers.AutoGenerateColumns = false;
        dgvUsers.Columns.Clear();
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "ID", Width = 45, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = ThemeManager.Current.TextMuted } });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Username", HeaderText = "USERNAME", Width = 130, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextPrimary } });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FullName", HeaderText = "FULL NAME", Width = 180, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.TextSecondary } });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Role", HeaderText = "ROLE", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = ThemeManager.Current.AccentCyan, Font = new Font("Segoe UI", 9F, FontStyle.Bold) } });
        dgvUsers.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsActive", HeaderText = "ACTIVE", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgvUsers.DataSource = data;
        dgvUsers.RowHeadersVisible = false;
        dgvUsers.BackgroundColor = ThemeManager.Current.PanelBg;
        dgvUsers.BorderStyle = BorderStyle.None;
        dgvUsers.GridColor = ThemeManager.Current.BorderColor;
        dgvUsers.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.Current.DgvHeaderBg;
        dgvUsers.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.Current.AccentCyan;
        dgvUsers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvUsers.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvUsers.ColumnHeadersHeight = 30;
        dgvUsers.EnableHeadersVisualStyles = false;
        dgvUsers.DefaultCellStyle.SelectionBackColor = ThemeManager.Current.DgvSelection;
        dgvUsers.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvUsers.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvUsers.RowTemplate.Height = 28;
        dgvUsers.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.Current.DgvRowAlt;
        dgvUsers.DefaultCellStyle.BackColor = ThemeManager.Current.DgvRowNormal;
        dgvUsers.DefaultCellStyle.ForeColor = ThemeManager.Current.TextPrimary;
    }

    private async void btnRefreshCloud_Click(object? sender, EventArgs e)
    {
        var storeId = SyncService.StoreId;
        if (string.IsNullOrEmpty(storeId))
        {
            MessageBox.Show("Store ID not configured. Go to Settings to set up cloud sync.", "Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        btnRefreshCloud.Enabled = false;
        btnRefreshCloud.Text = "SYNCING...";
        try
        {
            var count = await SyncService.DownloadUsersAsync(storeId);
            LoadUsers();
            MessageBox.Show($"Synced {count} user(s) from cloud.", "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sync failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        btnRefreshCloud.Enabled = true;
        btnRefreshCloud.Text = "\u2B06 REFRESH FROM CLOUD";
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;

        BackColor = t.CanvasBg;
        Text = "Users";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = t.PanelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(t.BorderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };

        var lblPageTitle = new Label { Text = "\uD83D\uDC64 USERS", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(20, 12), Size = new Size(200, 28) };
        pnlToolbar.Controls.Add(lblPageTitle);

        var btnProfile = new Button { Text = "\uD83D\uDC64 MY PROFILE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentGreen, ForeColor = Color.White, Cursor = Cursors.Hand, Location = new Point(0, 8), Size = new Size(120, 34), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnProfile.Click += (_, _) =>
        {
            if (_currentUser == null) return;
            var inputBg = t.InputBg;
            var inputFg = t.InputFg;
            var neonTitle = t.AccentCyan;
            var dimText = t.TextMuted;
            using var pf = new Form { Text = "My Profile", Size = new Size(380, 320), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = t.CanvasBg };
            var lblUser = new Label { Text = $"User: {_currentUser.Username}", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 15), Size = new Size(340, 25) };
            var lblName = new Label { Text = $"Full Name: {_currentUser.FullName}", Font = new Font("Segoe UI", 10F), ForeColor = dimText, Location = new Point(20, 45), Size = new Size(340, 20) };
            var lblRole = new Label { Text = $"Role: {_currentUser.Role}", Font = new Font("Segoe UI", 10F), ForeColor = dimText, Location = new Point(20, 68), Size = new Size(340, 20) };
            var sep = new Panel { Location = new Point(20, 95), Size = new Size(340, 1), BackColor = t.BorderColor };
            var lblChangePin = new Label { Text = "CHANGE PIN", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 108), Size = new Size(200, 20) };
            var lblOld = new Label { Text = "Current PIN:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(20, 135), Size = new Size(100, 22) };
            var txtOld = new TextBox { Location = new Point(130, 133), Size = new Size(120, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9F) };
            var lblNew = new Label { Text = "New PIN:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(20, 165), Size = new Size(100, 22) };
            var txtNew = new TextBox { Location = new Point(130, 163), Size = new Size(120, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9F) };
            var lblConfirm = new Label { Text = "Confirm:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(20, 195), Size = new Size(100, 22) };
            var txtConfirm = new TextBox { Location = new Point(130, 193), Size = new Size(120, 25), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9F) };
            var lblStatus = new Label { Text = "", Font = new Font("Segoe UI", 9F), ForeColor = t.AccentRed, Location = new Point(20, 225), Size = new Size(340, 20) };
            var btnSavePin = new Button { Text = "CHANGE PIN", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentBlue, ForeColor = Color.White, Cursor = Cursors.Hand, Location = new Point(130, 245), Size = new Size(120, 30) };
            btnSavePin.Click += (_, __) =>
            {
                if (txtOld.Text != _currentUser.PasswordHash)
                {
                    lblStatus.Text = "Current PIN is incorrect!";
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtNew.Text) || txtNew.Text.Length < 4)
                {
                    lblStatus.Text = "New PIN must be at least 4 characters";
                    return;
                }
                if (txtNew.Text != txtConfirm.Text)
                {
                    lblStatus.Text = "PINs do not match!";
                    return;
                }
                _currentUser.PasswordHash = txtNew.Text;
                var error = UserService.Save(_currentUser, _currentUser.FullName);
                if (error != null)
                {
                    lblStatus.Text = error;
                    return;
                }
                MessageBox.Show("PIN changed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                pf.Close();
            };
            pf.Controls.AddRange(new Control[] { lblUser, lblName, lblRole, sep, lblChangePin, lblOld, txtOld, lblNew, txtNew, lblConfirm, txtConfirm, lblStatus, btnSavePin });
            pf.ShowDialog();
        };
        pnlToolbar.Controls.Add(btnProfile);

        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = t.CanvasBg };

        var pnlGrid = new Panel { Location = new Point(10, 10), Size = new Size(600, 400), BackColor = t.PanelBg };
        pnlGrid.Paint += (s, e) => { using var pen = new Pen(t.BorderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlGrid.Width - 1, pnlGrid.Height - 1); };

        var lblGridTitle = new Label { Text = "USERS (synced from cloud)", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.TextMuted, Location = new Point(12, 8), Size = new Size(350, 20) };
        dgvUsers = new DataGridView
        {
            Location = new Point(8, 32),
            Size = new Size(584, 310),
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        pnlGrid.Controls.AddRange(new Control[] { lblGridTitle, dgvUsers });

        if (_isAdmin)
        {
            btnRefreshCloud = new Button { Text = "\u2B06 REFRESH FROM CLOUD", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentBlue, ForeColor = Color.White, Cursor = Cursors.Hand, Location = new Point(12, 350), Size = new Size(200, 34) };
            btnRefreshCloud.Click += btnRefreshCloud_Click;
            pnlGrid.Controls.Add(btnRefreshCloud);

            btnCleanLocal = new Button { Text = "\uD83E\uDDF9 CLEAN LOCAL", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentRed, ForeColor = Color.White, Cursor = Cursors.Hand, Location = new Point(220, 350), Size = new Size(150, 34) };
            btnCleanLocal.Click += (_, _) =>
            {
                if (MessageBox.Show("Remove ALL non-admin users from this POS client?\n\nThis only affects this machine. Cloud users are not affected.", "Clean Local Users", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                if (MessageBox.Show("FINAL WARNING: This cannot be undone. All local non-admin users will be deleted permanently.", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes) return;
                UserService.DeleteAllNonAdmin();
                LoadUsers();
                MessageBox.Show("All non-admin users have been removed from this POS.", "Cleaned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            pnlGrid.Controls.Add(btnCleanLocal);
        }

        pnlMain.Controls.Add(pnlGrid);
        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlToolbar });

        Shown += (_, _) => ResizeLayout(pnlGrid, dgvUsers);
        Resize += (_, _) => ResizeLayout(pnlGrid, dgvUsers);
    }

    private void ResizeLayout(Panel pnlGrid, DataGridView dgv)
    {
        var margin = 10;
        var availW = ClientSize.Width - margin * 3;
        var availH = ClientSize.Height - 50 - margin * 4;

        pnlGrid.Location = new Point(margin, margin);
        pnlGrid.Size = new Size(availW, availH);

        dgv.Location = new Point(8, 32);
        dgv.Size = new Size(availW - 16, availH - 90);
        if (btnRefreshCloud != null)
        {
            btnRefreshCloud.Location = new Point(12, availH - 50);
            if (btnCleanLocal != null)
                btnCleanLocal.Location = new Point(220, availH - 50);
        }
    }

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private DataGridView dgvUsers = null!;
    private Button? btnRefreshCloud;
    private Button? btnCleanLocal;
}
