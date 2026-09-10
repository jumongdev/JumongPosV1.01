using JumongPosV1._01.Helpers;
using JumongPosV1._01.Models;
using JumongPosV1._01.Services;

namespace JumongPosV1._01.Forms;

/// <summary>
/// 💬 HQ POS messenger — e-commerce customer chat (customer ↔ cashier/admin).
/// HQ store lang ito ginagamit; cloud shop_chat_* tables ang pinagmulan.
/// </summary>
public class CustomerChatForm : Form
{
    private readonly string _replyBy;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly Panel _pnlConvList = null!;
    private readonly Label _lblConvCount = null!;
    private readonly Label _lblEmpty = null!;
    private readonly Panel _pnlThread = null!;
    private readonly Label _lblThreadInfo = null!;
    private readonly Panel _pnlMsgs = null!;
    private readonly TextBox _txtReply = null!;
    private readonly Button _btnSend = null!;
    private readonly Label _lblThreadEmpty = null!;

    private CloudChatConversation? _open;
    private List<CloudChatMessage> _msgs = new();
    private long _lastMsgId;
    private bool _busy;

    private static Color CHeaderBg => ThemeManager.Current.StatusBlueMid;
    private static Color CHeaderText => Color.White;
    private static Color CSurface => ThemeManager.Current.SurfaceBg;
    private static Color CCard => ThemeManager.Current.CardBg;
    private static Color CBorderLight => ThemeManager.Current.BorderLight;
    private static Color CText => ThemeManager.Current.TextPrimary;
    private static Color CTextMuted => ThemeManager.Current.TextSecondary;
    private static Color CTextHint => ThemeManager.Current.TextHint;
    private static Color CInputBg => ThemeManager.Current.InputBg;
    private static Color CInputFg => ThemeManager.Current.InputFg;
    private static Color CViolet => Color.FromArgb(124, 92, 230);
    private static Color CVioletDark => Color.FromArgb(96, 70, 190);
    private static Color CRedBadge => Color.FromArgb(220, 53, 69);

    public CustomerChatForm(string replyBy)
    {
        _replyBy = string.IsNullOrWhiteSpace(replyBy) ? "HQ" : replyBy;
        Text = "Customer Chat — Online Shop";
        Size = new Size(1040, 640);
        MinimumSize = new Size(860, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CSurface;
        Font = new Font("Segoe UI", 10F);

        // Header
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = CViolet };
        pnlHeader.Paint += (s, e) => { using var pen = new Pen(CVioletDark, 1); e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1); };
        var lblTitle = new Label { Text = "  \U0001F4AC  CUSTOMER CHAT — ONLINE SHOP", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = CHeaderText, Location = new Point(14, 0), Size = new Size(560, 46), TextAlign = ContentAlignment.MiddleLeft };
        var lblWho = new Label { Text = $"Sumasagot: {_replyBy}", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(225, 220, 255), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(760, 15) };
        var btnClose = new Button { Text = "\u2716 CLOSE", Font = new Font("Segoe UI", 9F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = ThemeManager.Current.AccentRed, ForeColor = Color.White, Size = new Size(104, 32), Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(900, 7) };
        btnClose.Click += (_, _) => Close();
        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblWho, btnClose });
        Controls.Add(pnlHeader);

        // Split: conversations (left) + thread (right)
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 330, FixedPanel = FixedPanel.Panel1, BackColor = CSurface };
        Controls.Add(split);

        // LEFT: conversation list (dock order: fill first, top last — reverse z-order layout)
        var pnlConv = new Panel { Dock = DockStyle.Fill, BackColor = CSurface };
        _pnlConvList = new Panel { Dock = DockStyle.Fill, BackColor = CSurface, AutoScroll = true };
        pnlConv.Controls.Add(_pnlConvList);
        _lblEmpty = new Label { Text = "Wala pang customer na nag-message.", Font = new Font("Segoe UI", 9F), ForeColor = CTextHint, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        _pnlConvList.Controls.Add(_lblEmpty);
        var pnlConvHeader = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = CCard };
        _lblConvCount = new Label { Text = "CUSTOMERS (0)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = CTextMuted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
        pnlConvHeader.Controls.Add(_lblConvCount);
        pnlConv.Controls.Add(pnlConvHeader);
        split.Panel1.Controls.Add(pnlConv);

        // RIGHT: thread (dock order: fill, bottom, top)
        _pnlThread = new Panel { Dock = DockStyle.Fill, BackColor = CCard };
        _pnlMsgs = new Panel { Dock = DockStyle.Fill, BackColor = CSurface, AutoScroll = true };
        _pnlMsgs.Resize += (_, _) => RenderMsgs();
        _pnlThread.Controls.Add(_pnlMsgs);
        _lblThreadEmpty = new Label { Text = "👈 Pumili ng conversation sa kaliwa", Font = new Font("Segoe UI", 10F), ForeColor = CTextHint, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        _pnlMsgs.Controls.Add(_lblThreadEmpty);
        var pnlComposer = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = CCard };
        var sepTop = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = CBorderLight };
        _txtReply = new TextBox
        {
            Location = new Point(12, 16),
            Size = new Size(0, 60),
            Multiline = true,
            MaxLength = 1000,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = CInputBg,
            ForeColor = CInputFg,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
        };
        _btnSend = new Button { Text = "SEND", Font = new Font("Segoe UI", 10F, FontStyle.Bold), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, BackColor = CViolet, ForeColor = Color.White, Size = new Size(92, 34), Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnSend.Click += async (_, _) => await SendReplyAsync();
        _txtReply.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; await SendReplyAsync(); } };
        pnlComposer.Resize += (s, e) =>
        {
            _txtReply.Size = new Size(pnlComposer.ClientSize.Width - 120, 34);
            _btnSend.Location = new Point(pnlComposer.ClientSize.Width - 100, 14);
        };
        pnlComposer.Controls.AddRange(new Control[] { sepTop, _txtReply, _btnSend });
        _pnlThread.Controls.Add(pnlComposer);
        var pnlThreadHeader = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = CViolet };
        _lblThreadInfo = new Label { Text = "Pumili ng customer sa kaliwa", Font = new Font("Segoe UI", 9.5F), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0) };
        pnlThreadHeader.Controls.Add(_lblThreadInfo);
        _pnlThread.Controls.Add(pnlThreadHeader);
        split.Panel2.Controls.Add(_pnlThread);

        _pollTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _pollTimer.Tick += async (_, _) => await RefreshConversationsAsync(keepOpen: true);
        Shown += async (_, _) => { await RefreshConversationsAsync(false); _pollTimer.Start(); };
        FormClosing += (_, _) => _pollTimer.Stop();
        DebugHelper.AddFormLabel(this);
    }

    private async Task RefreshConversationsAsync(bool keepOpen)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var convs = await SyncService.GetChatConversationsAsync() ?? new List<CloudChatConversation>();
            // refresh open conv object
            if (_open != null)
            {
                var updated = convs.FirstOrDefault(c => c.Id == _open.Id);
                if (updated != null) _open = updated;
                else { _open = null; _msgs = new List<CloudChatMessage>(); }
            }
            if (IsDisposed) return;
            RenderConvList(convs);
            if (_open != null)
            {
                var fresh = await SyncService.GetChatMessagesAsync(_open.Id) ?? new List<CloudChatMessage>();
                if (fresh.Count != _msgs.Count || fresh.Any(m => !_msgs.Any(x => x.Id == m.Id)))
                {
                    _msgs = fresh;
                    RenderMsgs();
                }
                if (_msgs.Any(m => m.Sender == "customer" && !m.SeenByAdmin))
                    await SyncService.MarkChatSeenAsync(_open.Id);
            }
        }
        catch { }
        finally { _busy = false; }
    }

    private void RenderConvList(List<CloudChatConversation> convs)
    {
        _lblConvCount.Text = $"CUSTOMERS ({convs.Count})";
        _lblEmpty.Visible = convs.Count == 0;
        _lblEmpty.BringToFront();
        _pnlConvList.Controls.Clear();
        if (convs.Count == 0) return;
        int y = 4;
        foreach (var c in convs)
        {
            var card = MakeConvCard(c, _open?.Id == c.Id);
            card.Location = new Point(6, y);
            y += card.Height + 5;
            _pnlConvList.Controls.Add(card);
        }
        _pnlConvList.AutoScrollMinSize = new Size(0, y);
    }

    private Panel MakeConvCard(CloudChatConversation c, bool selected)
    {
        var t = ThemeManager.Current;
        var width = _pnlConvList.ClientSize.Width - 18;
        var card = new Panel
        {
            Width = Math.Max(260, width),
            Height = 88,
            BackColor = selected ? Color.FromArgb(124, 92, 230) : CCard,
            Cursor = Cursors.Hand,
            Tag = c
        };
        card.Paint += (s, e) =>
        {
            var p = (Panel)s;
            using var pen = new Pen(selected ? Color.FromArgb(124, 92, 230) : CBorderLight, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        var fg = selected ? Color.White : CText;
        var fgSub = selected ? Color.FromArgb(235, 230, 255) : CTextMuted;
        var timeStr = c.LastAt.HasValue ? c.LastAt.Value.ToLocalTime().ToString("MM/dd HH:mm") : "";
        var lblName = new Label { Text = c.CustomerName, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = fg, AutoSize = false, Location = new Point(10, 6), Size = new Size(width - 92, 18) };
        var lblTime = new Label { Text = timeStr, Font = new Font("Segoe UI", 7.5F), ForeColor = fgSub, AutoSize = false, Location = new Point(width - 74, 8), Size = new Size(64, 14), TextAlign = ContentAlignment.MiddleRight };
        var contact = $"\U0001F4DE {(string.IsNullOrEmpty(c.Phone) ? "—" : c.Phone)}";
        if (!string.IsNullOrEmpty(c.Subdivision) || !string.IsNullOrEmpty(c.Block))
            contact += $"  \U0001F3D8\ufe0f {(string.IsNullOrEmpty(c.Subdivision) ? $"Blk {c.Block} Lot {c.Lot}" : c.Subdivision)}";
        var lblContact = new Label { Text = contact, Font = new Font("Segoe UI", 8F), ForeColor = fgSub, AutoSize = false, Location = new Point(10, 26), Size = new Size(width - 20, 16) };
        var last = (c.LastSender == "admin" ? "→ " : "") + c.LastMessage;
        var lblLast = new Label { Text = last, Font = new Font("Segoe UI", 8.5F), ForeColor = fgSub, AutoSize = false, Location = new Point(10, 48), Size = new Size(width - (c.Unread > 0 ? 46 : 20), 16) };
        var lblBadge = new Label { Text = c.Unread > 0 ? c.Unread.ToString() : "", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.White, BackColor = CRedBadge, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(width - 34, 50), Size = new Size(24, 16), Visible = c.Unread > 0 };
        card.Controls.AddRange(new Control[] { lblName, lblTime, lblContact, lblLast, lblBadge });
        card.Click += (_, _) => OpenConversation(c);
        foreach (Control ch in card.Controls) ch.Click += (_, _) => OpenConversation(c);
        return card;
    }

    private async void OpenConversation(CloudChatConversation c)
    {
        _open = c;
        _msgs = await SyncService.GetChatMessagesAsync(c.Id) ?? new List<CloudChatMessage>();
        if (IsDisposed) return;
        // info sa header
        var info = $"{c.CustomerName}   \U0001F4DE {(string.IsNullOrEmpty(c.Phone) ? "—" : c.Phone)}";
        if (!string.IsNullOrEmpty(c.Subdivision) || !string.IsNullOrEmpty(c.Block))
            info += $"   \U0001F3D8\ufe0f {(string.IsNullOrEmpty(c.Subdivision) ? $"Blk {c.Block} Lot {c.Lot}" : c.Subdivision)}";
        if (!string.IsNullOrEmpty(c.Address)) info += $"   • {c.Address}";
        _lblThreadInfo.Text = info;
        _lblThreadInfo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        RenderMsgs();
        if (_msgs.Any(m => m.Sender == "customer" && !m.SeenByAdmin))
            await SyncService.MarkChatSeenAsync(c.Id);
        RenderConvList(await SyncService.GetChatConversationsAsync() ?? new List<CloudChatConversation>());
    }

    private void RenderMsgs()
    {
        if (IsDisposed) return;
        _pnlMsgs.Controls.Clear();
        if (_open == null) { _lblThreadEmpty.Visible = true; return; }
        _lblThreadEmpty.Visible = false;
        _lblThreadEmpty.SendToBack();
        int y = 8;
        foreach (var m in _msgs)
        {
            var isMine = m.Sender == "admin";
            var bubble = MakeBubble(m, isMine);
            bubble.Location = isMine
                ? new Point(_pnlMsgs.ClientSize.Width - bubble.Width - 10, y)
                : new Point(10, y);
            _pnlMsgs.Controls.Add(bubble);
            y += bubble.Height + 7;
        }
        _pnlMsgs.AutoScrollMinSize = new Size(0, y);
        _pnlMsgs.AutoScrollPosition = new Point(0, Math.Max(0, y - _pnlMsgs.ClientSize.Height));
    }

    private Panel MakeBubble(CloudChatMessage m, bool isMine)
    {
        var font = new Font("Segoe UI", 9.5F);
        var metaFont = new Font("Segoe UI", 7.5F);
        var maxTextW = Math.Max(180, _pnlMsgs.ClientSize.Width - 150);
        var pad = 10;
        var time = m.CreatedAt.ToLocalTime().ToString("hh:mm tt");
        var who = isMine ? (string.IsNullOrEmpty(m.ReplyBy) ? "Ikaw" : m.ReplyBy) : "Customer";
        var meta = isMine ? $"{who} · {time}" : $"{time}";
        var isDark = ThemeManager.Current.TextPrimary.R > 200;
        var bubbleBg = isMine ? CViolet : (isDark ? Color.FromArgb(45, 45, 80) : Color.White);
        var bubbleFg = isMine ? Color.White : CText;
        var bubbleBorder = isMine ? CViolet : CBorderLight;

        // Text label: AutoSize + MaximumSize para talagang mag-wrap at maipakita nang buo
        // (ang Label na AutoSize=false ay HINDI nag-wrap — pinuputol ang mahabang mensahe).
        var lblText = new Label
        {
            Text = m.Message,
            Font = font,
            ForeColor = bubbleFg,
            BackColor = bubbleBg,
            AutoSize = true,
            MaximumSize = new Size(maxTextW, 0),
            Location = new Point(pad, 6),
            UseMnemonic = false
        };
        var textSize = lblText.GetPreferredSize(new Size(maxTextW, 0));
        lblText.Size = textSize;

        var lblMeta = new Label
        {
            Text = meta,
            Font = metaFont,
            ForeColor = isMine ? Color.FromArgb(230, 225, 255) : CTextHint,
            BackColor = bubbleBg,
            AutoSize = true,
            Location = new Point(pad, 6 + textSize.Height + 2)
        };
        var metaSize = lblMeta.GetPreferredSize(new Size(maxTextW, 0));

        var w = Math.Min(maxTextW, textSize.Width) + pad * 2 + 4;
        var h = 6 + textSize.Height + 2 + metaSize.Height + 6;
        var panel = new Panel { Width = Math.Max(60, w), Height = Math.Max(30, h) };

        panel.Paint += (s, e) =>
        {
            var p = (Panel)s;
            using var path = RoundRect(0, 0, p.Width - 1, p.Height - 1, 10);
            using var fill = new SolidBrush(bubbleBg);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
            if (!isMine) { using var pen = new Pen(bubbleBorder, 1); e.Graphics.DrawPath(pen, path); }
        };
        panel.Controls.AddRange(new Control[] { lblText, lblMeta });
        return panel;
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundRect(int x, int y, int w, int h, int r)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(x, y, r, r, 180, 90);
        p.AddArc(x + w - r, y, r, r, 270, 90);
        p.AddArc(x + w - r, y + h - r, r, r, 0, 90);
        p.AddArc(x, y + h - r, r, r, 90, 90);
        p.CloseFigure();
        return p;
    }

    private async Task SendReplyAsync()
    {
        var msg = _txtReply.Text.Trim();
        if (string.IsNullOrEmpty(msg) || _open == null) return;
        _btnSend.Enabled = false;
        try
        {
            var ok = await SyncService.ReplyChatAsync(_open.Id, msg, _replyBy);
            if (ok)
            {
                _txtReply.Clear();
                await RefreshConversationsAsync(true);
            }
            else
            {
                MessageBox.Show("Hindi maipadala ang reply — check ang connection sa cloud.", "Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex) { ErrorLogger.Log("CustomerChatForm.SendReply", ex); }
        finally { _btnSend.Enabled = true; _txtReply.Focus(); }
    }
}
