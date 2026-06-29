using System.Data.SQLite;
using JumongPosV1._01.Data;

namespace JumongPosV1._01.Helpers;

public class Theme
{
    public string Name { get; init; } = "";

    // ── Canvas / Backgrounds ──
    public Color CanvasBg { get; init; }      // main form background
    public Color PanelBg { get; init; }        // toolbar/card/panel background
    public Color InputBg { get; init; }        // textbox/combobox background
    public Color InputFg { get; init; }        // textbox/combobox text
    public Color CardBg { get; init; }         // white card in POS, same as PanelBg in mgmt
    public Color SurfaceBg { get; init; }      // form surface in POS, same as CanvasBg in mgmt

    // ── Text ──
    public Color TextPrimary { get; init; }    // main text
    public Color TextSecondary { get; init; }  // secondary text
    public Color TextMuted { get; init; }      // dim/muted text
    public Color TextHint { get; init; }       // hint text

    // ── Accents ──
    public Color AccentCyan { get; init; }     // neon titles / key emphasis
    public Color AccentBlue { get; init; }     // primary buttons
    public Color AccentGreen { get; init; }    // success / positive
    public Color AccentRed { get; init; }      // danger / negative
    public Color AccentOrange { get; init; }   // warning
    public Color AccentPurple { get; init; }   // secondary accent
    public Color AccentGrey { get; init; }     // neutral buttons

    // ── Borders ──
    public Color BorderColor { get; init; }    // main border
    public Color BorderLight { get; init; }    // subtle border

    // ── DataGridView ──
    public Color DgvHeaderBg { get; init; }
    public Color DgvHeaderFg { get; init; }
    public Color DgvRowAlt { get; init; }
    public Color DgvRowNormal { get; init; }
    public Color DgvGrid { get; init; }
    public Color DgvSelection { get; init; }

    // ── Topbar ──
    public Color TopbarBg { get; init; }
    public Color TopbarChip { get; init; }
    public Color TopbarBorder { get; init; }
    public Color TopbarText { get; init; }
    public Color TopbarAccent { get; init; }

    // ── Status / POS chip colors ──
    public Color StatusGreenDark { get; init; }
    public Color StatusGreenMid { get; init; }
    public Color StatusGreenLight { get; init; }
    public Color StatusBlueLight { get; init; }
    public Color StatusBlueMid { get; init; }
    public Color StatusBlueDark { get; init; }
    public Color StatusRedLight { get; init; }
    public Color StatusRedDark { get; init; }
    public Color StatusAmberLight { get; init; }
    public Color StatusAmberMid { get; init; }
    public Color StatusAmberDark { get; init; }

    // ── Sidebar (MainForm) ──
    public Color SidebarBg { get; init; }
    public Color SidebarCardBg { get; init; }
    public Color SidebarHoverBg { get; init; }
    public Color SidebarTitleAccent { get; init; }
    public Color SidebarUserInfo { get; init; }
    public Color SidebarDivider { get; init; }
    public Color SidebarLogoutBg { get; init; }
    public Color SidebarLogoutFg { get; init; }
    public Color SidebarLogoutHover { get; init; }
}

public static class ThemeManager
{
    public static Theme Dark { get; } = new Theme
    {
        Name = "Dark",

        CanvasBg  = Color.FromArgb(10, 10, 26),
        PanelBg   = Color.FromArgb(20, 20, 40),
        InputBg   = Color.FromArgb(30, 30, 55),
        InputFg   = Color.FromArgb(230, 230, 245),
        CardBg    = Color.FromArgb(20, 20, 40),
        SurfaceBg = Color.FromArgb(10, 10, 26),

        TextPrimary   = Color.FromArgb(230, 230, 245),
        TextSecondary = Color.FromArgb(200, 200, 220),
        TextMuted     = Color.FromArgb(140, 140, 170),
        TextHint      = Color.FromArgb(160, 160, 190),

        AccentCyan   = Color.FromArgb(0, 245, 255),
        AccentBlue   = Color.FromArgb(72, 126, 176),
        AccentGreen  = Color.FromArgb(46, 204, 113),
        AccentRed    = Color.FromArgb(231, 76, 60),
        AccentOrange = Color.FromArgb(243, 156, 18),
        AccentPurple = Color.FromArgb(155, 89, 182),
        AccentGrey   = Color.FromArgb(149, 165, 166),

        BorderColor  = Color.FromArgb(40, 40, 70),
        BorderLight  = Color.FromArgb(236, 237, 243),

        DgvHeaderBg  = Color.FromArgb(25, 25, 50),
        DgvHeaderFg  = Color.FromArgb(0, 245, 255),
        DgvRowAlt    = Color.FromArgb(15, 15, 32),
        DgvRowNormal = Color.FromArgb(22, 22, 45),
        DgvGrid      = Color.FromArgb(40, 40, 70),
        DgvSelection = Color.FromArgb(40, 40, 80),

        TopbarBg      = Color.FromArgb(26, 26, 46),
        TopbarChip    = Color.FromArgb(37, 37, 64),
        TopbarBorder  = Color.FromArgb(56, 56, 96),
        TopbarText    = Color.FromArgb(170, 170, 204),
        TopbarAccent  = Color.FromArgb(126, 184, 247),

        StatusGreenDark  = Color.FromArgb(39, 80, 10),
        StatusGreenMid   = Color.FromArgb(99, 153, 34),
        StatusGreenLight = Color.FromArgb(25, 45, 20),
        StatusBlueLight  = Color.FromArgb(20, 35, 50),
        StatusBlueMid    = Color.FromArgb(24, 95, 165),
        StatusBlueDark   = Color.FromArgb(12, 68, 124),
        StatusRedLight   = Color.FromArgb(50, 25, 25),
        StatusRedDark    = Color.FromArgb(163, 45, 45),
        StatusAmberLight = Color.FromArgb(50, 40, 15),
        StatusAmberMid   = Color.FromArgb(186, 117, 23),
        StatusAmberDark  = Color.FromArgb(99, 56, 6),

        SidebarBg           = Color.FromArgb(30, 30, 45),
        SidebarCardBg        = Color.FromArgb(40, 40, 58),
        SidebarHoverBg       = Color.FromArgb(55, 55, 78),
        SidebarTitleAccent   = Color.FromArgb(100, 180, 255),
        SidebarUserInfo      = Color.FromArgb(150, 150, 170),
        SidebarDivider       = Color.FromArgb(60, 60, 80),
        SidebarLogoutBg      = Color.FromArgb(50, 35, 35),
        SidebarLogoutFg      = Color.FromArgb(231, 76, 60),
        SidebarLogoutHover   = Color.FromArgb(70, 45, 45),
    };

    public static Theme Light { get; } = new Theme
    {
        Name = "Light",

        CanvasBg  = Color.FromArgb(244, 245, 250),
        PanelBg   = Color.White,
        InputBg   = Color.White,
        InputFg   = Color.FromArgb(30, 30, 46),
        CardBg    = Color.White,
        SurfaceBg = Color.FromArgb(244, 245, 250),

        TextPrimary   = Color.FromArgb(30, 30, 46),
        TextSecondary = Color.FromArgb(110, 110, 140),
        TextMuted     = Color.FromArgb(110, 110, 140),
        TextHint      = Color.FromArgb(160, 160, 190),

        AccentCyan   = Color.FromArgb(24, 95, 165),
        AccentBlue   = Color.FromArgb(72, 126, 176),
        AccentGreen  = Color.FromArgb(46, 204, 113),
        AccentRed    = Color.FromArgb(231, 76, 60),
        AccentOrange = Color.FromArgb(243, 156, 18),
        AccentPurple = Color.FromArgb(155, 89, 182),
        AccentGrey   = Color.FromArgb(149, 165, 166),

        BorderColor  = Color.FromArgb(220, 221, 230),
        BorderLight  = Color.FromArgb(236, 237, 243),

        DgvHeaderBg  = Color.FromArgb(235, 236, 240),
        DgvHeaderFg  = Color.FromArgb(24, 95, 165),
        DgvRowAlt    = Color.FromArgb(248, 249, 250),
        DgvRowNormal = Color.White,
        DgvGrid      = Color.FromArgb(220, 221, 230),
        DgvSelection = Color.FromArgb(200, 210, 230),

        TopbarBg      = Color.FromArgb(26, 26, 46),
        TopbarChip    = Color.FromArgb(37, 37, 64),
        TopbarBorder  = Color.FromArgb(56, 56, 96),
        TopbarText    = Color.FromArgb(170, 170, 204),
        TopbarAccent  = Color.FromArgb(126, 184, 247),

        StatusGreenDark  = Color.FromArgb(39, 80, 10),
        StatusGreenMid   = Color.FromArgb(99, 153, 34),
        StatusGreenLight = Color.FromArgb(234, 243, 222),
        StatusBlueLight  = Color.FromArgb(230, 241, 251),
        StatusBlueMid    = Color.FromArgb(24, 95, 165),
        StatusBlueDark   = Color.FromArgb(12, 68, 124),
        StatusRedLight   = Color.FromArgb(252, 235, 235),
        StatusRedDark    = Color.FromArgb(163, 45, 45),
        StatusAmberLight = Color.FromArgb(250, 238, 218),
        StatusAmberMid   = Color.FromArgb(186, 117, 23),
        StatusAmberDark  = Color.FromArgb(99, 56, 6),

        SidebarBg           = Color.FromArgb(244, 245, 250),
        SidebarCardBg        = Color.White,
        SidebarHoverBg       = Color.FromArgb(230, 231, 240),
        SidebarTitleAccent   = Color.FromArgb(24, 95, 165),
        SidebarUserInfo      = Color.FromArgb(110, 110, 140),
        SidebarDivider       = Color.FromArgb(200, 201, 210),
        SidebarLogoutBg      = Color.FromArgb(252, 235, 235),
        SidebarLogoutFg      = Color.FromArgb(163, 45, 45),
        SidebarLogoutHover   = Color.FromArgb(250, 225, 225),
    };

    public static Theme Current { get; private set; } = Dark;

    public static void LoadTheme()
    {
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'AppTheme'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            Current = val == "Light" ? Light : Dark;
        }
        catch
        {
            Current = Dark;
        }
    }

    public static void SwitchTheme(string themeName)
    {
        Current = themeName == "Light" ? Light : Dark;
        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('AppTheme', @v)", conn);
            cmd.Parameters.AddWithValue("@v", Current.Name);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }
}
