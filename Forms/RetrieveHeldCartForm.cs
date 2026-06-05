using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Forms;

public partial class RetrieveHeldCartForm : Form
{
    private List<HeldCart> _carts = new();
    public HeldCart? SelectedCart { get; private set; }

    public RetrieveHeldCartForm()
    {
        InitializeComponent();
        LoadCarts();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadCarts()
    {
        _carts.Clear();
        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        var sql = "SELECT Id, OrderType, CustomerId, CustomerName, ItemsJson, CreatedAt FROM HeldCarts ORDER BY CreatedAt DESC";
        using var cmd = new SQLiteCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            _carts.Add(new HeldCart
            {
                Id = rdr.GetInt32(0),
                OrderType = rdr.GetString(1),
                CustomerId = rdr.IsDBNull(2) ? null : rdr.GetInt32(2),
                CustomerName = rdr.GetString(3),
                ItemsJson = rdr.GetString(4),
                CreatedAt = DateTime.Parse(rdr.GetString(5))
            });
        }

        dgvCarts.Rows.Clear();
        foreach (var c in _carts)
        {
            var items = c.DeserializeItems();
            dgvCarts.Rows.Add(c.Id, c.OrderType, c.CustomerName, items.Count, c.CreatedAt.ToString("MM/dd HH:mm"));
        }

        if (_carts.Count > 0)
        {
            dgvCarts.Rows[0].Selected = true;
            btnRetrieve.Enabled = true;
        }
    }

    private void dgvCarts_SelectionChanged(object? sender, EventArgs e)
    {
        btnRetrieve.Enabled = dgvCarts.SelectedRows.Count > 0;
    }

    private void dgvCarts_DoubleClick(object? sender, EventArgs e)
    {
        RetrieveSelected();
    }

    private void btnRetrieve_Click(object? sender, EventArgs e)
    {
        RetrieveSelected();
    }

    private void btnDelete_Click(object? sender, EventArgs e)
    {
        if (dgvCarts.SelectedRows.Count == 0) return;
        var id = (int)dgvCarts.SelectedRows[0].Cells[0].Value;
        if (MessageBox.Show("Delete this held cart?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        using var conn = DatabaseHelper.GetConnection();
        conn.Open();
        using var cmd = new SQLiteCommand("DELETE FROM HeldCarts WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();

        LoadCarts();
    }

    private void RetrieveSelected()
    {
        if (dgvCarts.SelectedRows.Count == 0) return;
        var id = (int)dgvCarts.SelectedRows[0].Cells[0].Value;
        SelectedCart = _carts.FirstOrDefault(c => c.Id == id);
        if (SelectedCart != null)
            DialogResult = DialogResult.OK;
    }

    private void InitializeComponent()
    {
        Text = "Retrieve Held Cart";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(550, 350);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(248, 249, 252);

        var lbl = new Label
        {
            Text = "Select a held cart to retrieve:",
            Location = new Point(15, 15),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 44, 44)
        };

        dgvCarts = new DataGridView
        {
            Location = new Point(15, 40),
            Size = new Size(520, 230),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Segoe UI", 10F)
        };
        dgvCarts.Columns.Add("Id", "ID");
        dgvCarts.Columns[0].Visible = false;
        dgvCarts.Columns.Add("OrderType", "Type");
        dgvCarts.Columns.Add("Customer", "Customer");
        dgvCarts.Columns.Add("Items", "Items");
        dgvCarts.Columns.Add("Date", "Date/Time");
        dgvCarts.Columns[1].FillWeight = 15;
        dgvCarts.Columns[2].FillWeight = 40;
        dgvCarts.Columns[3].FillWeight = 10;
        dgvCarts.Columns[4].FillWeight = 35;
        dgvCarts.SelectionChanged += dgvCarts_SelectionChanged;
        dgvCarts.DoubleClick += dgvCarts_DoubleClick;

        btnRetrieve = new Button
        {
            Text = "RETRIEVE",
            Location = new Point(15, 285),
            Size = new Size(120, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        btnRetrieve.Click += btnRetrieve_Click;

        btnDelete = new Button
        {
            Text = "DELETE",
            Location = new Point(145, 285),
            Size = new Size(100, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(231, 76, 60),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnDelete.Click += btnDelete_Click;

        var btnCancel = new Button
        {
            Text = "CANCEL",
            Location = new Point(415, 285),
            Size = new Size(120, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(149, 165, 166),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[] { lbl, dgvCarts, btnRetrieve, btnDelete, btnCancel });
    }

    private DataGridView dgvCarts = null!;
    private Button btnRetrieve = null!;
    private Button btnDelete = null!;
}
