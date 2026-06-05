using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

public partial class VoidLogForm : Form
{
    public VoidLogForm()
    {
        InitializeComponent();
        LoadLog();
        DebugHelper.AddFormLabel(this);
    }

    private void LoadLog()
    {
        var logs = SaleService.GetVoidLogs();
        dgvLog.AutoGenerateColumns = false;
        dgvLog.Columns.Clear();
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "DATE", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Action", HeaderText = "ACTION", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(0, 245, 255) } });
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceNo", HeaderText = "INVOICE", Width = 130, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "ITEM", Width = 160, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(230, 230, 245) } });
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Quantity", HeaderText = "QTY", Width = 50, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(230, 230, 245) } });
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "AMOUNT", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(0, 245, 255) } });
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UserName", HeaderText = "CASHIER", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(243, 156, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold) } });
        dgvLog.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Reason", HeaderText = "REASON", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(200, 200, 220) } });
        dgvLog.DataSource = logs;
        dgvLog.RowHeadersVisible = false;
        dgvLog.BackgroundColor = Color.FromArgb(20, 20, 40);
        dgvLog.BorderStyle = BorderStyle.None;
        dgvLog.GridColor = Color.FromArgb(40, 40, 70);
        dgvLog.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 25, 50);
        dgvLog.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 245, 255);
        dgvLog.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgvLog.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvLog.ColumnHeadersHeight = 30;
        dgvLog.EnableHeadersVisualStyles = false;
        dgvLog.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 80);
        dgvLog.DefaultCellStyle.SelectionForeColor = Color.White;
        dgvLog.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvLog.RowTemplate.Height = 28;
        dgvLog.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(15, 15, 32);
        dgvLog.DefaultCellStyle.BackColor = Color.FromArgb(22, 22, 45);
        dgvLog.DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 245);

        dgvLog.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || dgvLog.Rows[e.RowIndex].DataBoundItem is not VoidLog log) return;
            if (e.ColumnIndex == dgvLog.Columns["Action"]?.Index)
            {
                e.CellStyle!.ForeColor = log.Action == "VoidItem" ? Color.FromArgb(243, 156, 18) : Color.FromArgb(231, 76, 60);
                e.CellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            }
        };

        lblCount.Text = $"Total void records: {logs.Count}";
        lblCount.ForeColor = Color.FromArgb(140, 140, 170);
        lblCount.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        var voidByUser = logs.GroupBy(v => v.UserName)
            .Select(g => new { User = g.Key, Count = g.Count(), Total = g.Sum(v => v.Amount) })
            .Where(x => !string.IsNullOrEmpty(x.User))
            .OrderByDescending(x => x.Count)
            .ToList();
        if (voidByUser.Count > 0)
        {
            var maxCount = voidByUser.Max(x => x.Count);
            var alertColor = maxCount > 10 ? Color.FromArgb(231, 76, 60) : maxCount > 5 ? Color.FromArgb(243, 156, 18) : Color.FromArgb(140, 140, 170);
            var prefix = maxCount > 10 ? "\u26A0\uFE0F " : "";
            lblByUser.Text = prefix + "BY CASHIER: " + string.Join("  |  ", voidByUser.Select(x => $"{x.User}: {x.Count} (\u20b1{x.Total:N2})"));
            lblByUser.ForeColor = alertColor;
            lblByUser.Font = new Font("Segoe UI", 9F, maxCount > 5 ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private void InitializeComponent()
    {
        var canvasBg = Color.FromArgb(10, 10, 26);
        var panelBg = Color.FromArgb(20, 20, 40);
        var neonTitle = Color.FromArgb(0, 245, 255);
        var borderColor = Color.FromArgb(40, 40, 70);
        var dimText = Color.FromArgb(140, 140, 170);

        BackColor = canvasBg;
        Text = "Void History";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        Size = new Size(920, 540);

        var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = panelBg };
        pnlToolbar.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1); };
        var lblPageTitle = new Label { Text = "\uD83D\uDDD1\uFE0F VOID HISTORY LOG", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = neonTitle, Location = new Point(20, 12), Size = new Size(250, 28) };
        pnlToolbar.Controls.Add(lblPageTitle);

        var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = canvasBg };

        var pnlGrid = new Panel { Location = new Point(10, 10), Size = new Size(830, 390), BackColor = panelBg };
        pnlGrid.Paint += (s, e) => { using var pen = new Pen(borderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, pnlGrid.Width - 1, pnlGrid.Height - 1); };
        var lblGridTitle = new Label { Text = "VOID RECORDS", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(12, 8), Size = new Size(200, 20) };
        dgvLog = new DataGridView
        {
            Location = new Point(8, 32),
            Size = new Size(814, 350),
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9F),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };
        pnlGrid.Controls.AddRange(new Control[] { lblGridTitle, dgvLog });

        var pnlFooter = new Panel { Location = new Point(10, 410), Size = new Size(830, 55), BackColor = canvasBg };
        lblCount = new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(5, 3), Size = new Size(300, 18), TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
        lblByUser = new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = dimText, Location = new Point(5, 24), Size = new Size(820, 28), TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
        pnlFooter.Controls.AddRange(new Control[] { lblCount, lblByUser });

        pnlMain.Controls.AddRange(new Control[] { pnlGrid, pnlFooter });
        Controls.AddRange(new Control[] { pnlToolbar, pnlMain });

        Resize += (_, _) =>
        {
            var margin = 10;
            var toolbarH = 50;
            var footerH = 55;
            var availH = ClientSize.Height - toolbarH - footerH - margin * 4;
            var availW = ClientSize.Width - margin * 3;
            pnlGrid.Location = new Point(margin, margin);
            pnlGrid.Size = new Size(availW, availH);
            pnlFooter.Location = new Point(margin, availH + margin * 2);
            pnlFooter.Size = new Size(availW, footerH);
            dgvLog.Location = new Point(8, 32);
            dgvLog.Size = new Size(availW - 16, availH - 40);
        };
    }

    private DataGridView dgvLog = null!;
    private Label lblCount = null!;
    private Label lblByUser = null!;
}
