using System.Data.SQLite;
using JumongPosV1._01.Data;

namespace JumongPosV1._01.Services;

public static class TimeHelper
{
    private static int? _cachedOffset;
    private static DateTime _lastRead = DateTime.MinValue;

    public static DateTime Now
    {
        get
        {
            var offset = GetOffset();
            return DateTime.UtcNow.AddMinutes(offset);
        }
    }

    public static DateTime Today => Now.Date;

    private static int GetOffset()
    {
        var cacheAge = (DateTime.UtcNow - _lastRead).TotalSeconds;
        if (_cachedOffset.HasValue && cacheAge < 30)
            return _cachedOffset.Value;

        try
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key = 'AppTimezone'", conn);
            var val = cmd.ExecuteScalar()?.ToString();
            if (int.TryParse(val, out var minutes))
            {
                _cachedOffset = minutes;
                _lastRead = DateTime.UtcNow;
                return minutes;
            }
        }
        catch { }

        _cachedOffset = 480;
        _lastRead = DateTime.UtcNow;
        return 480;
    }
}
