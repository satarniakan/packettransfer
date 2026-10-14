using Microsoft.Extensions.Configuration;

namespace packettransfer.Infrastructure.Data;

/// <summary>
/// انتخاب پروایدر دیتابیس: SQLite فایلی (پیش‌فرض محلی، بدون نیاز به SQL Server)
/// یا SQL Server. فایل SQLite داخل خود پروژه (<c>App_Data/app.db</c>) زندگی می‌کند
/// و همیشه همراه پروژه است.
/// </summary>
public static class DatabaseProvider
{
    /// <summary>
    /// true یعنی SQLite فایلی. ترتیب: ۱) کلید صریح Database:Provider، ۲) حدس از رشته اتصال.
    /// </summary>
    public static bool IsSqlite(string? connectionString, IConfiguration configuration)
    {
        var explicit_ = configuration["Database:Provider"];
        if (!string.IsNullOrWhiteSpace(explicit_))
        {
            if (explicit_.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
                || explicit_.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
                return true;
            if (explicit_.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                || explicit_.Equals("SQLServer", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return IsSqliteConnectionString(connectionString);
    }

    public static bool IsSqliteConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;
        var cs = connectionString.Trim();
        // رشته‌های SQL Server همیشه یکی از این‌ها را دارند
        if (cs.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || cs.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase)
            || cs.Contains("User Id=", StringComparison.OrdinalIgnoreCase)
            || cs.Contains("UserID=", StringComparison.OrdinalIgnoreCase))
            return false;
        // SQLite: مسیر فایل .db/.sqlite یا Data Source=
        if (cs.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || cs.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            || cs.Contains(".db;", StringComparison.OrdinalIgnoreCase)
            || cs.Contains(".sqlite;", StringComparison.OrdinalIgnoreCase))
            return true;
        if (cs.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            || cs.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>
    /// پوشهٔ فایل SQLite را می‌سازد تا EnsureCreated/Migrate روی مسیر نسبی خطا ندهد.
    /// ورودی: رشته اتصال SQLite مثل "Data Source=App_Data/app.db".
    /// خروجی: مسیر پوشه ساخته‌شده یا null.
    /// </summary>
    public static string? EnsureSqliteDirectory(string? connectionString, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;
        string? path = null;
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim();
            if (p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                path = p["Data Source=".Length..].Trim().Trim('"');
            else if (p.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
                path = p["DataSource=".Length..].Trim().Trim('"');
        }
        if (string.IsNullOrWhiteSpace(path) || path == ":memory:")
            return null;
        if (!Path.IsPathRooted(path))
            path = Path.Combine(baseDirectory, path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }
}
