// packettransfer.App/Program.cs
// میزبان: پایه + ماژول توزیع بسته (داشبورد ادمین + اپ توزیع‌کننده).
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using packettransfer.Infrastructure;
using packettransfer.Infrastructure.Data;
using packettransfer.App;
using packettransfer.App.Endpoints;
using packettransfer.App.Middleware;
using packettransfer.Distribution;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Razor Components (Blazor Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(o => o.DetailedErrors = builder.Environment.IsDevelopment());

// لاگر نهایی از appsettings خوانده می‌شود؛ سطح لاگ بین Development و Production فرق می‌کند.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: Serilog.RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

// Persist Data Protection keys so cookies/antiforgery tokens survive app restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")));

// هر لایه تنظیمات سرویس‌های خودش را رجیستر می‌کند.
// مایگریشن‌ها در همین مونتاژ میزبان‌اند (پوشهٔ Migrations) تا اسکیمای دامنه وارد پایه نشود.
builder.Services.Addpackettransfer(builder.Configuration, builder.Environment.IsDevelopment(),
    migrationsAssembly: typeof(Program).Assembly.GetName().Name);
builder.Services.AddAppWeb();
builder.Services.AddDistribution();

// سیاست‌های دسترسی خودکارند: PermissionPolicyProvider پایه هر کلید IPermissionCatalog
// را به policy تبدیل می‌کند، پس ثبت دستی لازم نیست.

builder.Services.AddHealthChecks().AddDbContextCheck<packettransferDbContext>();

var app = builder.Build();

// Forwarded Headers — باید اولین middleware باشد تا RemoteIpAddress واقعیِ کاربر
// (پشت reverse proxy) برای Rate Limiter و لاگ در دسترس باشد.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
{
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
}
app.UseForwardedHeaders(forwardedHeadersOptions);

// هدرهای امنیتی (nosniff، SAMEORIGIN، Referrer-Policy) — روی همهٔ پاسخ‌ها، از جمله خطاها.
app.UsepackettransferSecurityHeaders();

// مدیریت خطای سراسری — باید اولِ پایپ‌لاین باشد تا همهٔ خطاها را ببیند.
// پیام فارسیِ امن + کد وضعیت درست؛ جزئیات فنی فقط در لاگ (و در Development در پاسخ).
app.UsepackettransferExceptionHandler();

// اعمال خودکار مایگریشن‌های EF هنگام استارتاپ — دیگر فراموش نمی‌شوند.
// SQLite فایلی (App_Data/app.db): بدون نیاز به SQL Server، با EnsureCreated ساخته می‌شود
// چون مایگریشن‌های موجود مخصوص SQL Server هستند. SQL Server: طبق معمول MigrateAsync.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<packettransferDbContext>();
    if (db.Database.IsSqlite())
    {
        packettransfer.Infrastructure.Data.DatabaseProvider.EnsureSqliteDirectory(
            app.Configuration.GetConnectionString("Default"), app.Environment.ContentRootPath);
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }
}

// مجوزها و نقش‌های پایه + نقش توزیع‌کنندهٔ ماژول توزیع
await app.Services.InitializepackettransferAsync(["Distributor"]);
app.UseSerilogRequestLogging();

app.UseRequestLocalization(packettransferSetup.PersianLocalization());

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// درخواست‌هایی که با هیچ صفحه‌ای مطابقت ندارند به صفحهٔ طراحی‌شدهٔ not-found هدایت می‌شوند.
app.UseStatusCodePagesWithReExecute("/not-found", "?from={0}");

// Antiforgery به هویت کاربر نیاز دارد، پس باید بعد از Authentication/Authorization بیاید
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// endpointهای پایه (احراز هویت و اعلان‌ها). endpoint دامنه بعد از این می‌آید.
app.Mappackettransfer();
app.MapDistributionApi();
app.MapHealthChecks("/health");

// مستندات خودکار API موبایل — فقط در Development (شامل کلید عمومی پوش و مسیرهای auth).
// مرور JSON در: /openapi/v1.json
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapRazorComponents<packettransfer.App.Components.App>()
    .AddInteractiveServerRenderMode();

try
{
    Log.Information("Starting packettransfer.App application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// نقطهٔ ورود قابل ارجاع برای WebApplicationFactory در تست‌های یکپارچگی
public partial class Program;
