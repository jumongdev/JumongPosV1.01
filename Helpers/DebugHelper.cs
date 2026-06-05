namespace JumongPosV1._01.Helpers;

public static class DebugHelper
{
    public static void AddFormLabel(Form form)
    {
        var label = new Label
        {
            Text = form.GetType().Name,
            Font = new Font("Consolas", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(60, 60, 90),
            BackColor = Color.Transparent,
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            TextAlign = ContentAlignment.BottomRight,
            Padding = new Padding(4)
        };
        form.Controls.Add(label);
        label.BringToFront();
        form.Resize += (_, _) =>
        {
            label.Location = new Point(
                form.ClientSize.Width - label.PreferredWidth - 8,
                form.ClientSize.Height - label.PreferredHeight - 4);
        };
        form.Shown += (_, _) =>
        {
            label.Location = new Point(
                form.ClientSize.Width - label.PreferredWidth - 8,
                form.ClientSize.Height - label.PreferredHeight - 4);
        };
    }
}
