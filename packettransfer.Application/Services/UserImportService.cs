using Microsoft.AspNetCore.Http;
using packettransfer.Application.DTOs;
using packettransfer.Application.Imports;
using packettransfer.Domain.Identity;
using packettransfer.Domain.Queries;

namespace packettransfer.Application.Services;

/// <summary>نتیجهٔ ایمپورت کاربران: تعداد ساخته‌شده + خطاهای سطری (شماره سطر واقعی اکسل).</summary>
public record UserImportResult(int ImportedCount, List<ImportRowError> RowErrors, int TotalRows);

/// <summary>
/// ایمپورت گروهی کاربران از اکسل (فهرست پرسنل و…). ستون‌ها:
/// نام کاربری (UserName/نام کاربری/username — الزامی، یکتا)،
/// نام (FullName/نام/نام کامل — الزامی)، رمز (Password/رمز — الزامی)،
/// شماره موبایل (Phone/PhoneNumber/موبایل/شماره موبایل/شماره — اختیاری، فقط اطلاعاتی)،
/// ایمیل (Email/ایمیل — اختیاری).
/// <para>
/// هر سطر خراب فقط خودش را می‌سوزاند. نام‌های کاربری تکراریِ داخل فایل در همان‌جا
/// رد می‌شوند. نقش همهٔ واردشده‌ها «کاربر» است.
/// </para>
/// </summary>
public interface IUserImportService
{
    Task<UserImportResult> ImportAsync(Stream excelStream);
}

public partial class UserImportService : IUserImportService
{
    private readonly IUserAdminService _users;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;

    public UserImportService(IUserAdminService users, IAuditService audit, IHttpContextAccessor httpContext)
    {
        _users = users;
        _audit = audit;
        _httpContext = httpContext;
    }

    public async Task<UserImportResult> ImportAsync(Stream excelStream)
    {
        var parsed = ExcelImporter.Read(excelStream, row => new ImportedUserRow(
            ExcelImporter.Required(row, "UserName", "نام کاربری", "username").Trim(),
            ExcelImporter.Required(row, "FullName", "نام", "نام کامل").Trim(),
            ExcelImporter.Required(row, "Password", "رمز").Trim(),
            ExcelImporter.Get(row, "Phone", "PhoneNumber", "موبایل", "شماره موبایل", "شماره")?.Trim(),
            ExcelImporter.Get(row, "Email", "ایمیل")?.Trim()));

        var errors = new List<ImportRowError>(parsed.RowErrors);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;

        foreach (var item in parsed.Items)
        {
            var (rowNumber, row) = (item.RowNumber, item.Value);

            if (row.UserName.Length < 3 || row.UserName.Any(char.IsWhiteSpace))
            {
                errors.Add(new ImportRowError(rowNumber, "UserName", $"نام کاربری «{row.UserName}» معتبر نیست (حداقل ۳ کاراکتر بدون فاصله)."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.FullName))
            {
                errors.Add(new ImportRowError(rowNumber, "FullName", "نام الزامی است."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Password))
            {
                errors.Add(new ImportRowError(rowNumber, "Password", "رمز الزامی است."));
                continue;
            }

            string? phone = null;
            if (!string.IsNullOrWhiteSpace(row.Phone))
            {
                phone = PersianSearch.NormalizePhone(row.Phone);
            }

            if (!seenNames.Add(row.UserName))
            {
                errors.Add(new ImportRowError(rowNumber, "UserName", $"نام کاربری «{row.UserName}» در فایل تکراری است."));
                continue;
            }

            var result = await _users.CreateUserAsync(new CreateUserDto
            {
                UserName = row.UserName,
                FullName = row.FullName,
                PhoneNumber = phone,
                Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email,
                Password = row.Password,
                RoleNames = [Roles.User]
            });

            if (result.Succeeded)
            {
                imported++;
            }
            else
            {
                errors.Add(new ImportRowError(rowNumber, "UserName",
                    string.Join("، ", result.Errors.Select(e => e.Description))));
            }
        }

        errors.Sort((a, b) => a.RowNumber.CompareTo(b.RowNumber));

        if (imported > 0)
        {
            var actor = _httpContext.HttpContext?.User.Identity?.Name;
            await _audit.LogEventAsync("user.imported", actor,
                $"{imported} کاربر از اکسل وارد شدند ({errors.Count} خطا).");
        }

        return new UserImportResult(imported, errors, parsed.TotalRows);
    }

    private sealed record ImportedUserRow(
        string UserName, string FullName, string Password, string? Phone, string? Email);
}
