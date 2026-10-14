# AGENTS.md — راهنمای ایجنت برای پروژه packettransfer

> این فایل زنده است: با هر تغییر ساختاری (پروژه/نقطه اتصال/قرارداد جدید) همین فایل را هم به‌روز کن.

## نمای کلی

- استک: ‎.NET 10 (global.json)‎، Blazor Server تعاملی، EF Core + SQL Server (`localhost,1433`، دیتابیس `packettransferDev`)، Serilog.
- انتخاب پروایدر دیتابیس در `packettransfer.Infrastructure/Data/DatabaseProvider.cs` است (`Database:Provider=SqlServer/Sqlite`)؛ مایگریشن‌های `Migrations/` مخصوص SQL Serverاند و SQLite با `EnsureCreated` ساخته می‌شود.
- همهٔ متن‌ها فارسی راست‌چین؛ زمان‌ها در دیتابیس UTC، نمایش شمسی با `PersianDateHelper`.
- ساخته‌شده از قالب `empty-platform`؛ فضای‌نام ریشه `packettransfer`.

## ساختار (تخت — فقط یک پروژه اجرایی)

| مسیر | نقش | دست می‌زنی؟ |
|---|---|---|
| `packettransfer.App` | **تنها پروژه اجرایی (startup)** — `Program.cs`، همهٔ UI (صفحات، `Shared/App*`، `Routes`)، endpointها، `PlatformSetup`، سرویس‌های وب، `wwwroot/`، مایگریشن‌ها، فکتوری design-time، سه نقطه اتصال دامنه (`AppSetup.cs`) | بله — فقط همین |
| `packettransfer.Domain` | انتیتی‌ها، enumها، اکسپشن‌ها، قراردادها (`IPlatformUnitOfWork`، `IPermissionCatalog`، `IPlatformModule`…)، هویت | نه (مگر قرارداد عمومی جدید) |
| `packettransfer.Application` | سرویس‌ها، DTOها، Jobs، ایمپورت/اکسپورت، Helpers | نه (مگر سرویس پایه‌ای جدید) |
| `packettransfer.Infrastructure` | `DbContext`، ریپازیتوری‌ها، `RoleSeeder`، فرستنده‌ها، درگاه‌ها | نه |
| `packettransfer.Tests` | تست‌ها (xunit + Moq + InMemory) | بله — برای هر فیچر تست اضافه کن |
| `packettransfer.Distribution` | ماژول دامنهٔ توزیع بسته: انتیتی `PackageDelivery` + `DistributionModule` + ریپو/سرویس خودش + `IDistributionUnitOfWork`؛ مجوزها `distribution.manage`/`distribution.deliver`، نقش `Distributor`، صفحات `/admin/distribution` و `/deliver` | بله — دامنه جدید = پروژه جدا |

## قانون طلایی

دامنهٔ جدید (حسابداری، فروش، …) = **پروژه جدا** با `IPlatformModule` + ثبت در `Program.cs`، مجوز در `IPermissionCatalog`، منو در `INavProvider`.
پایه را برای نیاز دامنه ویرایش نکن؛ اگر نقطه اتصال کم است، همان را به پایه اضافه کن.

## دستورها (از ریشه)

```bash
dotnet build packettransfer.slnx
dotnet test packettransfer.Tests/packettransfer.Tests.csproj
dotnet run --project packettransfer.App
dotnet ef migrations add <Name> --project packettransfer.App --startup-project packettransfer.App
```

- `appsettings.json` در گیت نیست؛ از `appsettings.json.example` کپی کن. رشته اتصال واقعی و Secrets هرگز کامیت نشود.
- ورود فقط با نام کاربری/ایمیل و رمز است؛ ثبت‌نام خودکار و ورود موبایلی حذف شده. اولین ادمین با `tools/create-admin.sql` ساخته می‌شود؛ بعد بقیه کاربران از `/admin/users`.

## قراردادهای کد

- انتیتی دامنه از `AuditableEntity` ارث ببرد (رد پیگیری + حذف نرم خودکار) و در ماژول با `DeleteBehavior.Cascade` به والد وصل شود.
- رابطه‌های الزامی فرزند هم باید نرم‌حذفی باشند تا رکورد یتیم نماند.
- کلید مجوز = نام policy (`[Authorize(Policy = "...")]`)؛ نقش `Admin` از همه‌چیز عبور می‌کند.
- صفحه‌ها تک‌فرم؛ `DbContext` اسکوپ‌دار مدارها را قاطی نکن.
- استاتیک‌ها در `packettransfer.App/wwwroot` در ریشه سرو می‌شوند (بدون پیشوند `_content`).
- بوت‌استرپ vendored است (`wwwroot/lib` + `libman.json`)؛ هم‌زمان با ارتقای ‎.NET به‌روز نکن.

## تعریف «تمام‌شده»

`dotnet build` با ۰ هشدار و ۰ خطا + `dotnet test` سبز + بدون فایل اضافی در `git status`.
قبل از push، اسکن ردپا: هیچ ارجاعی به دامنه/پروژهٔ دیگری (نام، کامنت، سید، دیتای تستی) نماند.
