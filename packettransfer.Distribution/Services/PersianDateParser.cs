using System.Globalization;
using packettransfer.Domain.Queries;

namespace packettransfer.Distribution.Services;

/// <summary>
/// پارس تاریخ شمسی «1403/05/12» (ارقام فارسی/عربی هم قبول) به تاریخ UTC.
/// جداکنندهٔ `/`، `-`، `.` و فاصله قبول است.
/// </summary>
public static class PersianDateParser
{
    private static readonly PersianCalendar Calendar = new();

    public static bool TryParse(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = PersianSearch.NormalizePhone(text).Trim();
        var parts = normalized.Split(['/', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var year)
            || !int.TryParse(parts[1], out var month)
            || !int.TryParse(parts[2], out var day))
            return false;

        // سال دو‌رقمی (مثل ۰۳) یعنی همین قرن شمسی
        if (year < 100)
            year += 1400;

        try
        {
            date = DateTime.SpecifyKind(
                Calendar.ToDateTime(year, month, day, 0, 0, 0, 0), DateTimeKind.Utc);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
