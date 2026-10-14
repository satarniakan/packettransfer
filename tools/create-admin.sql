-- ============================================================
-- ساخت دستی کاربر ادمین (بازیابی اضطراری)
-- ورود فقط با نام کاربری و رمز است؛ ثبت‌نام خودکار وجود ندارد.
-- این اسکریپت فقط وقتی لازم است که هیچ ادمینی در سیستم نمانده باشد
-- (در حالت عادی، ادمین از صفحه «مدیریت کاربران» کاربر و رمز می‌سازد).
-- ------------------------------------------------------------
-- طرز استفاده:
--   1) هش رمز را بسازید (رمز خام هرگز در دیتابیس ذخیره نمی‌شود).
--      ساده‌ترین راه: یک پروژه موقت دات‌نت با
--      new PasswordHasher<ApplicationUser>().HashPassword(user, "رمز")،
--      یا از ادمین دیگری بخواهید بعداً رمز را از صفحه ویرایش کاربر عوض کند.
--   2) مقادیر زیر را پر کنید و کل اسکریپت را اجرا کنید.
-- ============================================================

DECLARE @UserName NVARCHAR(256) = N'admin';          -- نام کاربری دلخواه
DECLARE @PasswordHash NVARCHAR(MAX) = N'__HASH__';   -- هش PasswordHasher (مرحله ۱)
DECLARE @UserId NVARCHAR(450) = NEWID();
DECLARE @Stamp NVARCHAR(MAX) = NEWID();

-- نقش‌های پایه (اگر RoleSeeder استارتاپ هنوز اجرا نشده باشد)
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'ADMIN')
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Admin', 'ADMIN', NEWID());

IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'USER')
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'User', 'USER', NEWID());

-- کاربر (اگر همین نام کاربری هست، INSERT خطای یکتایی می‌دهد — عمداً)
INSERT INTO AspNetUsers
    (Id, UserName, NormalizedUserName, EmailConfirmed, PhoneNumberConfirmed,
     TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
     PasswordHash, SecurityStamp, ConcurrencyStamp)
VALUES
    (@UserId, @UserName, UPPER(@UserName), 0, 0,
     0, 1, 0,
     @PasswordHash, @Stamp, NEWID());

-- عضویت در نقش ادمین
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT @UserId, Id FROM AspNetRoles WHERE NormalizedName = 'ADMIN';

-- تأیید
SELECT u.UserName, r.Name AS Role
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON ur.UserId = u.Id
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE u.Id = @UserId;
