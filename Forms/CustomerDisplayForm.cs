using System.Data.SQLite;
using JumongPosV1._01.Data;
using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;

namespace JumongPosV1._01.Forms;

public partial class CustomerDisplayForm : Form
{
    private Label lblHeader = null!, lblTotal = null!, lblOrderType = null!;
    private FlowLayoutPanel pnlItems = null!;
    private PictureBox pbPromo = null!, pbGcash = null!;
    private PictureBox _slideshowPb = null!;
    private System.Windows.Forms.Timer _slideshowTimer = null!;
    private List<Image> _adsImages = new();
    private int _currentAdIndex;
    private bool _idleMode = true;

    private readonly int _targetScreenIdx;

    public CustomerDisplayForm()
    {
        _targetScreenIdx = ReadScreenSetting();

        var sameScreen = _targetScreenIdx == 0 || _targetScreenIdx >= Screen.AllScreens.Length;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Text = "Customer Display";
        BackColor = Color.FromArgb(250, 250, 252);
        FormBorderStyle = sameScreen ? FormBorderStyle.Sizable : FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = sameScreen;
        Font = new Font("Segoe UI", 11F);
        BuildLayout();
        BuildSlideshow();
        LoadGcashQr();
        FormClosing += (_, e) => { e.Cancel = true; Hide(); };
        SetIdleMode(true);
        Resize += (_, _) => LayoutPanels();
        DebugHelper.AddFormLabel(this);
    }

    private static int ReadScreenSetting()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'CustomerScreenIndex'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            if (int.TryParse(val, out var idx) && idx >= 0 && idx < Screen.AllScreens.Length)
                return idx;
        }
        catch { }
        return Screen.AllScreens.Length > 1 ? 1 : 0;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var screen = Screen.AllScreens[_targetScreenIdx];
        var sameScreen = _targetScreenIdx == 0 || _targetScreenIdx >= Screen.AllScreens.Length;

        if (!sameScreen)
        {
            Location = screen.WorkingArea.Location;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            var wa = screen.WorkingArea;
            var dw = Math.Min(480, wa.Width / 3);
            Location = new Point(wa.Right - dw, wa.Top);
            Size = new Size(dw, wa.Height);
            WindowState = FormWindowState.Normal;
            BringToFront();
        }
        LayoutPanels();
    }

    public void SetIdleMode(bool idle)
    {
        _idleMode = idle;
        _slideshowTimer.Stop();

        if (idle)
        {
            _left.Visible = false;
            _right.Visible = false;
            _slideshowPb.Visible = true;
            _slideshowPb.Dock = DockStyle.Fill;
            _slideshowPb.SizeMode = PictureBoxSizeMode.Zoom;

            if (_adsImages.Count > 0)
            {
                _currentAdIndex = 0;
                _slideshowPb.Image = _adsImages[0];
                _slideshowTimer.Start();
            }
            else
            {
                TryLoadImage(_slideshowPb, "assets\\promo.png");
                if (_slideshowPb.Image == null)
                    _slideshowPb.BackColor = Color.FromArgb(30, 30, 45);
            }
        }
        else
        {
            _slideshowPb.Visible = false;
            _left.Visible = true;
            _right.Visible = true;

            if (_adsImages.Count > 0)
            {
                _currentAdIndex = 0;
                pbPromo.Image = _adsImages[0];
                _slideshowTimer.Start();
            }
            else
            {
                TryLoadImage(pbPromo, "assets\\promo.png");
            }

            LayoutPanels();
        }
    }

    private void BuildSlideshow()
    {
        _slideshowPb = new PictureBox
        {
            Location = new Point(0, 0),
            Size = ClientSize,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };
        Controls.Add(_slideshowPb);

        var assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
        if (Directory.Exists(assetsDir))
            LoadImagesFromDir(assetsDir);

        _slideshowTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _slideshowTimer.Tick += (_, _) =>
        {
            if (_adsImages.Count == 0) return;
            _currentAdIndex = (_currentAdIndex + 1) % _adsImages.Count;
            var img = _adsImages[_currentAdIndex];
            if (_idleMode)
                _slideshowPb.Image = img;
            else
                pbPromo.Image = img;
        };
    }

    private void LoadImagesFromDir(string dir)
    {
        foreach (var f in Directory.GetFiles(dir))
        {
            var ext = Path.GetExtension(f).ToLower();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
            {
                try { _adsImages.Add(Image.FromFile(f)); } catch { }
            }
        }
    }

    public void UpdateOrder(string customerName, string orderType, List<SaleItem> cart, decimal grandTotal)
    {
        SuspendLayout();
        lblOrderType.Text = orderType.ToUpper();
        lblHeader.Text = string.IsNullOrEmpty(customerName) ? "Walk-in" : $"CUSTOMER: {customerName}";
        lblTotal.Text = $"TOTAL: \u20b1{grandTotal:N2}";

        pnlItems.Controls.Clear();

        if (cart.Count == 0)
        {
            pnlItems.Controls.Add(new Label
            {
                Text = "No items yet.",
                Font = new Font("Segoe UI", 14F),
                ForeColor = Color.FromArgb(180, 180, 190),
                Size = new Size(pnlItems.Width - 25, 30),
                Margin = new Padding(0, 20, 0, 0),
                TextAlign = ContentAlignment.MiddleCenter
            });
            return;
        }

        foreach (var item in cart)
        {
            var row = new Panel
            {
                Size = new Size(pnlItems.Width - 25, 38),
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.FromArgb(248, 249, 252)
            };

            var qtyLabel = new Label
            {
                Text = $"{item.Quantity}x",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(72, 126, 176),
                Location = new Point(5, 4),
                Size = new Size(55, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var nameLabel = new Label
            {
                Text = item.ProductName,
                Font = new Font("Segoe UI", 13F),
                ForeColor = Color.FromArgb(44, 44, 44),
                Location = new Point(65, 4),
                Size = new Size(row.Width - 330, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var priceLabel = new Label
            {
                Text = $"\u20b1{item.Price:N2}",
                Font = new Font("Segoe UI", 13F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(row.Width - 260, 4),
                Size = new Size(100, 30),
                TextAlign = ContentAlignment.MiddleRight
            };

            var totalLabel = new Label
            {
                Text = $"\u20b1{item.TotalPrice:N2}",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 44, 44),
                Location = new Point(row.Width - 150, 4),
                Size = new Size(140, 30),
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.AddRange(new Control[] { qtyLabel, nameLabel, priceLabel, totalLabel });
            pnlItems.Controls.Add(row);
        }
        ResumeLayout(true);
    }

    public void ClearOrder()
    {
        SuspendLayout();
        lblOrderType.Text = "WALK-IN";
        lblHeader.Text = "Walk-in";
        lblTotal.Text = "TOTAL: \u20b10.00";
        pnlItems.Controls.Clear();
        ResumeLayout(true);
    }

    private void LoadGcashQr()
    {
        TryLoadImage(pbGcash, "assets\\gcash_qr.png");
        if (pbGcash.Image == null)
        {
            _lblGcashTitle.Visible = false;
            pbGcash.Visible = false;
        }
    }

    private static void TryLoadImage(PictureBox pb, string filename)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
        if (File.Exists(path))
        {
            try { pb.Image = Image.FromFile(path); } catch { }
        }
    }

    private Panel _left = null!, _right = null!, _sepPanel = null!;
    private Label _colHeaders = null!, _lblPromoTitle = null!, _lblGcashTitle = null!;
    private int _leftW, _rightW;

    private void BuildLayout()
    {
        var w = Screen.PrimaryScreen!.WorkingArea.Width;
        var h = Screen.PrimaryScreen!.WorkingArea.Height;
        _leftW = w / 2;
        _rightW = w / 2;

        _left = new Panel
        {
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        lblOrderType = new Label
        {
            Text = "WALK-IN",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 180, 255),
            TextAlign = ContentAlignment.MiddleLeft
        };

        lblHeader = new Label
        {
            Text = "CUSTOMER: Walk-in",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 44, 44),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _sepPanel = new Panel
        {
            BackColor = Color.FromArgb(72, 126, 176)
        };

        _colHeaders = new Label
        {
            Text = "QTY   ITEM                                                      PRICE          TOTAL",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(140, 140, 160)
        };

        pnlItems = new FlowLayoutPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.White
        };

        lblTotal = new Label
        {
            Text = "TOTAL: \u20b10.00",
            Font = new Font("Segoe UI", 28F, FontStyle.Bold),
            ForeColor = Color.FromArgb(46, 204, 113),
            TextAlign = ContentAlignment.MiddleRight
        };

        _left.Controls.AddRange(new Control[] { lblOrderType, lblHeader, _sepPanel, _colHeaders, pnlItems, lblTotal });

        _right = new Panel
        {
            BackColor = Color.FromArgb(248, 249, 252),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblPromoTitle = new Label
        {
            Text = "PROMOS",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(72, 126, 176),
            TextAlign = ContentAlignment.MiddleCenter
        };

        pbPromo = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(235, 236, 240),
            BorderStyle = BorderStyle.FixedSingle
        };
 
        _lblGcashTitle = new Label
        {
            Text = "SCAN TO PAY VIA GCASH",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 44, 44),
            TextAlign = ContentAlignment.MiddleCenter
        };
 
        pbGcash = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _right.Controls.AddRange(new Control[] { _lblPromoTitle, pbPromo, _lblGcashTitle, pbGcash });

        Controls.AddRange(new Control[] { _left, _right });
    }

    private void LayoutPanels()
    {
        var h = ClientSize.Height;
        var w = ClientSize.Width;
        _leftW = w / 2;
        _rightW = w / 2;

        _left.Location = new Point(0, 0);
        _left.Size = new Size(_leftW, h);

        lblOrderType.Location = new Point(30, 20);
        lblOrderType.Size = new Size(_leftW - 60, 25);

        lblHeader.Location = new Point(30, 50);
        lblHeader.Size = new Size(_leftW - 60, 35);

        _sepPanel.Location = new Point(30, 100);
        _sepPanel.Size = new Size(_leftW - 60, 2);

        _colHeaders.Location = new Point(30, 115);
        _colHeaders.Size = new Size(_leftW - 60, 22);

        pnlItems.Location = new Point(30, 145);
        pnlItems.Size = new Size(_leftW - 60, h - 300);

        lblTotal.Location = new Point(30, h - 130);
        lblTotal.Size = new Size(_leftW - 60, 60);

        _right.Location = new Point(_leftW, 0);
        _right.Size = new Size(_rightW, h);

        var promoH = h / 2;
        var m = 10;

        _lblPromoTitle.Location = new Point(m, m);
        _lblPromoTitle.Size = new Size(_rightW - m * 2, 25);

        if (pbGcash.Visible)
        {
            _lblGcashTitle.Visible = true;

            pbPromo.Location = new Point(m, _lblPromoTitle.Bottom + 5);
            pbPromo.Size = new Size(_rightW - m * 2, promoH - pbPromo.Top - m);

            _lblGcashTitle.Location = new Point(m, promoH + m);
            _lblGcashTitle.Size = new Size(_rightW - m * 2, 25);

            pbGcash.Location = new Point(m, _lblGcashTitle.Bottom + 5);
            pbGcash.Size = new Size(_rightW - m * 2, h - pbGcash.Top - m);
        }
        else
        {
            pbPromo.Location = new Point(m, _lblPromoTitle.Bottom + 5);
            pbPromo.Size = new Size(_rightW - m * 2, h - pbPromo.Top - m);
        }
    }
}
