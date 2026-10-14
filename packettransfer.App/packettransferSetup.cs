using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using packettransfer.Application;
using packettransfer.Infrastructure;
using packettransfer.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using packettransfer.Application.Jobs;
using packettransfer.App.Authentication;
using packettransfer.App.Authorization;
using packettransfer.App.Endpoints;
using packettransfer.App.Services;

namespace packettransfer.App;

/// <summary>
/// متدهای راه‌اندازی مشترک برای هر برنامه‌ای که روی packettransfer می‌نشیند.
/// هر کدام فقط به پایه اشاره دارند؛ هیچ تنظیم دامنه‌ای اینجا نیست.
/// ترتیب فراخوانی در <c>Program.cs</c> مهم است و در README مستند شده.
/// </summary>
public static class packettransferSetup
{
    /// <summary>
    /// ثبت سرویس‌ها: Application + Infrastructure + پیام‌رسانی + OTP + Outbox.
    /// </summary>
    public static IServiceCollection Addpackettransfer(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment, string? migrationsAssembly = null)
    {
        services.AddpackettransferApplication();
        services.AddpackettransferInfrastructure(configuration, isDevelopment, migrationsAssembly);

        services.AddCascadingAuthenticationState();

        // پایه خودش سرویس‌های مجوزدهی را ثبت می‌کند تا به «تصادفی» بودنِ
        // AddRazorComponents در میزبان وابسته نباشد (وگرنه هر میزبانی که
        // Razor Components اضافه نکند، هنگام UseAuthorization خطا می‌گیرد).
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(
                ApiTokenAuthenticationHandler.SchemeName, displayName: "API Token", configureOptions: _ => { });

        services.AddAuthorizationBuilder()
            // endpointهای API موبایل: فقط توکن، نه کوکی
            .AddPolicy("Api", policy => policy
                .AddAuthenticationSchemes(ApiTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser())
            // endpointهای مشترک وب و موبایل (اعلان‌ها، شروع پرداخت): کوکی یا توکن
            .AddPolicy("App", policy => policy
                .AddAuthenticationSchemes(
                    IdentityConstants.ApplicationScheme,
                    ApiTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser());

        // سیاست‌ساز خودکار: هر کلید IPermissionCatalog خودش یک policy است.
        // باید بعد از AddAuthorization بیاید تا جایگزین provider پیش‌فرض شود.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        services.AddScoped<ToastService>();
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<RecurringJobRunner>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // ورود با رمز: حداکثر ۱۰ تلاش در هر ۱۵ دقیقه برای جلوگیری از حدس زدن رمز.
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0
                    }));

            // تکمیل پروفایل/گذاشتن رمز — عملیات گرانِ bcrypt است و قبلاً هیچ محدودیتی نداشت،
            // پس یک اسکریپت می‌توانست با درخواست‌های مکرر، CPU سرور را بسوزاند.
            options.AddPolicy("profile", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0
                    }));

            // شروع پرداخت: هر درخواست یک فراخوانی واقعی به درگاه + یک ردیف دیتابیس است،
            // پس کاربر لاگین‌کرده هم نباید بتواند با حلقه درگاه را بمباران کند.
            options.AddPolicy("payment-start", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    /// <summary>
    /// مسیریابی پیش‌فرض پایه: احراز هویت + اعلان‌ها. مسیریابی دامنه بعد از این می‌آید.
    /// </summary>
    public static IEndpointRouteBuilder Mappackettransfer(this IEndpointRouteBuilder app)
    {
        app.MapAccountEndpoints();
        app.MapNotificationsEndpoints();
        app.MapNotificationsApiEndpoints();
        app.MapAuthApiEndpoints();
        app.MapPaymentEndpoints();
        app.MapPushEndpoints();
        app.MapImpersonationEndpoints();
        return app;
    }

    /// <summary>
    /// تنظیمات پایه که باید در <c>Program.cs</c> میزبان اعمال شود، به‌ترتیب:
    /// هدرهای forwarded، فرهنگ فارسی، مدیریت خطا، middlewareهای امنیتی.
    /// </summary>
    public static RequestLocalizationOptions PersianLocalization() => new()
    {
        DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("fa-IR"),
        SupportedCultures = [new CultureInfo("fa-IR")],
        SupportedUICultures = [new CultureInfo("fa-IR")]
    };
}