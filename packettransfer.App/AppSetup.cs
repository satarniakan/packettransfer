using Microsoft.Extensions.DependencyInjection;
using packettransfer.Domain.Identity;
using packettransfer.App.Components.Layout;

namespace packettransfer.App;

/// <summary>
/// کاتالوگ مجوزهای میزبان: مجوزهای پایه (خالی) + مجوزهای ماژول توزیع بسته.
/// </summary>
public sealed class AppPermissionCatalog : IPermissionCatalog
{
    public IReadOnlyList<PermissionDescriptor> All { get; } =
    [
        new(packettransfer.Distribution.DistributionPermissions.Manage,
            packettransfer.Distribution.DistributionPermissions.Title(packettransfer.Distribution.DistributionPermissions.Manage),
            packettransfer.Distribution.DistributionPermissions.Group),
        new(packettransfer.Distribution.DistributionPermissions.Deliver,
            packettransfer.Distribution.DistributionPermissions.Title(packettransfer.Distribution.DistributionPermissions.Deliver),
            packettransfer.Distribution.DistributionPermissions.Group),
    ];
}

/// <summary>
/// کاتالوگ تنظیمات میزبان: کلیدهای پایه + همهٔ تنظیمات اتصال‌ها (پیامک، ایمیل،
/// پرداخت، کپچا، وب‌پوش، توکن). مقادیر محرمانه رمزنگاری‌شده ذخیره می‌شوند.
/// ترتیب مقدار مؤثر هر کلید: ردیف دیتابیس ← appsettings ← پیش‌فرض؛ پس خالی
/// گذاشتن یعنی «از appsettings بخوان».
/// </summary>
public sealed class AppSettingCatalog : ISettingCatalog
{
    public IReadOnlyList<SettingDescriptor> All { get; } =
    [
        new(FrameworkSettingKeys.SiteName, "نام سامانه", SettingDefaults.Get(FrameworkSettingKeys.SiteName), "عمومی"),

        new(IntegrationSettingKeys.SmsKavenegarApiKey, "کلید API کاوه‌نگار (Sms:Kavenegar:ApiKey)",
            "", "پیامک", IsSecret: true),
        new(IntegrationSettingKeys.SmsKavenegarSender, "شماره خط ارسال‌کننده (Sms:Kavenegar:Sender)",
            "", "پیامک"),

        new(IntegrationSettingKeys.SmtpHost, "هاست SMTP (Email:Smtp:Host)", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpPort, "پورت SMTP (پیش‌فرض 587)", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpUsername, "نام کاربری SMTP", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpPassword, "رمز SMTP", "", "ایمیل", IsSecret: true),
        new(IntegrationSettingKeys.SmtpFromAddress, "فرستنده (FromAddress)", "", "ایمیل"),
        new(IntegrationSettingKeys.SmtpFromName, "نام فرستنده (FromName)", "", "ایمیل"),

        new(IntegrationSettingKeys.ZarinPalMerchantId, "مرچنت زرین‌پال (Payment:ZarinPal:MerchantId)",
            "", "پرداخت", IsSecret: true),
        new(IntegrationSettingKeys.ZarinPalSandbox, "حالت sandbox زرین‌پال (true/false)", "", "پرداخت"),

        new(IntegrationSettingKeys.VapidSubject, "آدرس تماس VAPID (مثل mailto:admin@example.com)", "", "وب‌پوش"),
        new(IntegrationSettingKeys.VapidPublicKey, "کلید عمومی VAPID", "", "وب‌پوش"),
        new(IntegrationSettingKeys.VapidPrivateKey, "کلید خصوصی VAPID", "", "وب‌پوش", IsSecret: true),

        new(IntegrationSettingKeys.ApiTokenLifetimeDays, "عمر توکن موبایل به روز (پیش‌فرض ۱۸۰)", "", "توکن")
    ];
}

/// <summary>
/// منوی میزبان نمونه: فقط بخش‌های پایه. آیتم‌های دامنه را پروژه به همین فهرست اضافه می‌کند.
/// </summary>
public sealed class AppNavProvider : INavProvider
{
    public IReadOnlyList<NavItem> GetRootItems() =>
    [
        // توزیع بسته: داشبورد ادمین + اپ توزیع‌کننده
        NavItem.Group("توزیع بسته", "bi-box-seam",
            NavItem.Link("داشبورد توزیع", "admin/distribution", "bi-clipboard-data",
                packettransfer.Distribution.DistributionPermissions.Manage),
            NavItem.Link("اپ توزیع‌کننده", "deliver", "bi-bicycle",
                packettransfer.Distribution.DistributionPermissions.Deliver)),

        // مدیریت کاربران، مجوزها و پیام‌رسانی
        NavItem.Group("مدیریت", "bi-shield-lock",
            NavItem.Link("کاربران", "admin/users", "bi-people", Roles.Admin),
            NavItem.Link("مجوز نقش‌ها", "admin/roles", "bi-key", Roles.Admin),
            NavItem.Link("اعلان سراسری", "admin/broadcast", "bi-megaphone", Roles.Admin),
            NavItem.Link("صف پیام‌ها", "admin/outbox", "bi-send", Roles.Admin),
            NavItem.Link("پرداخت‌ها", "admin/payments", "bi-credit-card", Roles.Admin),
            NavItem.Link("تنظیمات", "admin/settings", "bi-sliders", Roles.Admin)),

        // سیستم — لاگ‌ها و تنظیمات حساب
        NavItem.Group("سیستم", "bi-gear",
            NavItem.Link("گزارش رویدادها", "audit-logs", "bi-journal-text", Roles.Admin),
            NavItem.Link("اعلان‌ها", "notifications", "bi-bell"),
            NavItem.Link("نشست‌های فعال", "sessions", "bi-phone"),
            NavItem.Link("پروفایل", "profile", "bi-person"))
    ];
}

/// <summary>
/// راه‌اندازی میزبان.
/// </summary>
public static class AppSetup
{
    /// <summary>
    /// کاتالوگ مجوزها، کاتالوگ تنظیمات و منو: همین سه نقطه‌اند که پایه به دامنه وصل می‌شوند.
    /// </summary>
    public static IServiceCollection AddAppWeb(this IServiceCollection services)
    {
        services.AddSingleton<IPermissionCatalog, AppPermissionCatalog>();
        services.AddSingleton<ISettingCatalog, AppSettingCatalog>();
        services.AddSingleton<INavProvider, AppNavProvider>();
        return services;
    }
}
