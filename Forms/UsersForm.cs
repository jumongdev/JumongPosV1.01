using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class UsersForm : Form
{
    private User? _selected;
    private readonly User? _currentUser;

    public UsersForm(User? currentUser = null)
    {
        _currentUser = currentUser;
        InitializeComponent();
        LoadUsers();
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

    private void btnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtUsername.Text))
        {
            MessageBox.Show("Username is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_selected == null && string.IsNullOrWhiteSpace(txtPassword.Text))
        {
            MessageBox.Show("Password is required for new users.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var u = _selected ?? new User();
        u.Username = txtUsername.Text.Trim();
        u.FullName = txtFullName.Text.Trim();
        if (!string.IsNullOrWhiteSpace(txtPassword.Text))
            u.PasswordHash = txtPassword.Text.Trim();
        u.Role = cmbRole.Text;
        u.IsActive = chkActive.Checked;

        var modifiedBy = _currentUser != null && !string.IsNullOrEmpty(_currentUser.FullName)
            ? _currentUser.FullName : _currentUser?.Username ?? "";
        var error = UserService.Save(u, modifiedBy);
        if (error != null)
        {
            MessageBox.Show(error, "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        LoadUsers();
        ClearForm();
    }

    private void btnDelete_Click(object? sender, EventArgs e)
    {
        if (_selected == null) return;
        if (_selected.Username == "admin")
        {
            MessageBox.Show("Cannot delete the admin user.", "Denied", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }
        if (MessageBox.Show($"Delete user '{_selected.Username}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        UserService.Delete(_selected.Id);
        LoadUsers();
        ClearForm();
    }

    private void ClearForm()
    {
        _selected = null;
        txtUsername.Clear();
        txtFullName.Clear();
        txtPassword.Clear();
        cmbRole.SelectedIndex = 0;
        chkActive.Checked = true;
        lblFormTitle.Text = "NEW USER";
        lblFormTitle.ForeColor = ThemeManager.Current.AccentCyan;
    }

    private void InitializeComponent()
    {
        var t = ThemeManager.Current;

        BackColor = t.CanvasBg;
        Text = "Manage Users";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        // ── TOP TOOLBAR ──
        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = t.PanelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(t.BorderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };

        var lblPageTitle = new Label { Text = "\uD83D\uDC64 USER MANAGEMENT", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(20, 12), Size = new Size(300, 28) };
        pnlToolbar.Controls.Add(lblPageTitle);

        // ── MAIN SPLIT ──
        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = t.CanvasBg };

        // LEFT - Grid
        var pnlLeft = new Panel { Location = new Point(10, 10), Size = new Size(600, 400), BackColor = t.PanelBg };
        pnlLeft.Paint += (s, e) => { using var pen = new Pen(t.BorderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlLeft.Width - 1, pnlLeft.Height - 1); };

        var lblGridTitle = new Label { Text = "USER ROSTER", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = t.TextMuted, Location = new Point(12, 8), Size = new Size(200, 20) };
        dgvUsers = new DataGridView
        {
            Location = new Point(8, 32),
            Size = new Size(584, 360),
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        dgvUsers.SelectionChanged += (s, e) =>
        {
            if (dgvUsers.CurrentRow?.DataBoundItem is User u)
            {
                _selected = u;
                txtUsername.Text = u.Username;
                txtFullName.Text = u.FullName;
                txtPassword.Text = "";
                cmbRole.Text = u.Role;
                chkActive.Checked = u.IsActive;
                lblFormTitle.Text = $"EDIT: {u.FullName}";
                lblFormTitle.ForeColor = t.AccentGreen;
            }
        };
        pnlLeft.Controls.AddRange(new Control[] { lblGridTitle, dgvUsers });

        // RIGHT - Entry Card
        var pnlRight = new Panel { Location = new Point(625, 10), Size = new Size(340, 400), BackColor = t.PanelBg };
        pnlRight.Paint += (s, e) => { using var pen = new Pen(t.BorderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlRight.Width - 1, pnlRight.Height - 1); };

        lblFormTitle = new Label { Text = "NEW USER", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = t.AccentCyan, Location = new Point(15, 10), Size = new Size(310, 25), TextAlign = ContentAlignment.MiddleLeft };

        var y = 42;
        AddField("USERNAME", ref txtUsername, ref y, pnlRight, t.InputBg, t.InputFg, t.TextMuted);
        AddField("FULL NAME", ref txtFullName, ref y, pnlRight, t.InputBg, t.InputFg, t.TextMuted);
        AddField("PASSWORD", ref txtPassword, ref y, pnlRight, t.InputBg, t.InputFg, t.TextMuted, HorizontalAlignment.Left, true);

        var lblRole = new Label { Text = "ROLE", Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = t.TextMuted, Location = new Point(15, y), Size = new Size(80, 16), TextAlign = ContentAlignment.MiddleLeft };
        cmbRole = new ComboBox { Location = new Point(15, y + 16), Size = new Size(150, 26), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = t.InputBg, ForeColor = t.InputFg, Font = new Font("Segoe UI", 10F) };
        cmbRole.Items.AddRange(new[] { "Admin", "Cashier" });
        cmbRole.SelectedIndex = 0;
        pnlRight.Controls.AddRange(new Control[] { lblRole, cmbRole });
        y += 48;

        chkActive = new CheckBox { Text = "Active", Location = new Point(15, y), Size = new Size(100, 26), ForeColor = t.InputFg, Font = new Font("Segoe UI", 10F), BackColor = Color.Transparent, Checked = true };
        pnlRight.Controls.Add(chkActive);
        y += 35;

        var btnNew = new Button { Text = "+ NEW", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(15, y), Size = new Size(95, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentBlue, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnNew.Click += (_, _) => { ClearForm(); txtUsername.Focus(); };

        btnSave = new Button { Text = "\u2714 SAVE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(115, y), Size = new Size(100, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentGreen, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnSave.Click += btnSave_Click;

        btnDelete = new Button { Text = "\u2716 DELETE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(220, y), Size = new Size(95, 34), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = t.AccentRed, ForeColor = Color.White, Cursor = Cursors.Hand };
        btnDelete.Click += btnDelete_Click;

        pnlRight.Controls.AddRange(new Control[] { lblFormTitle, btnNew, btnSave, btnDelete });

        pnlMain.Controls.AddRange(new Control[] { pnlLeft, pnlRight });
        Controls.Clear();
        Controls.AddRange(new Control[] { pnlMain, pnlToolbar });

        Shown += (_, _) => ResizeLayout(pnlLeft, pnlRight, dgvUsers);
        Resize += (_, _) => ResizeLayout(pnlLeft, pnlRight, dgvUsers);
    }

    private void AddField(string label, ref TextBox box, ref int y, Panel parent, Color inputBg, Color inputFg, Color labelColor, HorizontalAlignment align = HorizontalAlignment.Left, bool password = false)
    {
        var lbl = new Label { Text = label, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = labelColor, Location = new Point(15, y), Size = new Size(80, 16), TextAlign = ContentAlignment.MiddleLeft };
        box = new TextBox { Location = new Point(15, y + 16), Size = new Size(310, 26), BorderStyle = BorderStyle.FixedSingle, BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 10F), TextAlign = align, UseSystemPasswordChar = password };
        parent.Controls.AddRange(new Control[] { lbl, box });
        y += 48;
    }

    private void ResizeLayout(Panel pnlLeft, Panel pnlRight, DataGridView dgv)
    {
        var margin = 10;
        var availH = ClientSize.Height - 50 - margin * 4;
        var availW = ClientSize.Width - margin * 3;
        var leftW = (int)(availW * 0.64);
        var rightW = availW - leftW;

        pnlLeft.Location = new Point(margin, margin);
        pnlLeft.Size = new Size(leftW, availH);
        pnlRight.Location = new Point(leftW + margin * 2, margin);
        pnlRight.Size = new Size(rightW, availH);

        dgv.Location = new Point(8, 32);
        dgv.Size = new Size(leftW - 16, availH - 40);
    }

    public void ApplyTheme()
    {
        var t = ThemeManager.Current;
        BackColor = t.CanvasBg;
        ForeColor = t.TextPrimary;
    }

    private DataGridView dgvUsers = null!;
    private TextBox txtUsername = null!, txtFullName = null!, txtPassword = null!;
    private ComboBox cmbRole = null!;
    private CheckBox chkActive = null!;
    private Button btnSave = null!, btnDelete = null!;
    private Label lblFormTitle = null!;
}
