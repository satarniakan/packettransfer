namespace packettransfer.Distribution;

/// <summary>
/// کلیدهای مجوز ماژول توزیع (= نام policy). نقش <c>Admin</c> از همه عبور می‌کند.
/// در <c>AppPermissionCatalog</c> میزبان ثبت می‌شوند تا در صفحه نقش‌ها قابل اعطا باشند.
/// </summary>
public static class DistributionPermissions
{
    /// <summary>داشبورد توزیع: ایمپورت اکسل + گزارش + انتساب مجدد.</summary>
    public const string Manage = "distribution.manage";

    /// <summary>اپ توزیع‌کننده: مشاهده بسته‌های خود + ثبت تحویل با بارکد.</summary>
    public const string Deliver = "distribution.deliver";

    public static string Title(string key) => key switch
    {
        Manage => "مدیریت توزیع (ایمپورت و گزارش)",
        Deliver => "تحویل بسته (اپ توزیع‌کننده)",
        _ => key
    };

    public const string Group = "توزیع بسته";
}
