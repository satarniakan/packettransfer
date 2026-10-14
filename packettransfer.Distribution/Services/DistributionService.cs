using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using packettransfer.Application.DTOs;
using packettransfer.Application.Imports;
using packettransfer.Application.Services;
using packettransfer.Distribution.DTOs;
using packettransfer.Distribution.Entities;
using packettransfer.Distribution.Repositories;
using packettransfer.Domain.Identity;
using packettransfer.Domain.Queries;

namespace packettransfer.Distribution.Services;

/// <summary>
/// سرویس دامنهٔ توزیع بسته: ایمپورت اکسل داشبورد، گزارش، فهرست توزیع‌کننده و ثبت تحویل.
/// </summary>
public interface IDistributionService
{
    Task<DistributionImportResult> ImportAsync(Stream excelStream);
    Task<PagedResult<PackageDeliveryDto>> GetReportPagedAsync(DistributionReportFilter filter);
    Task<(int Total, int Pending, int Delivered)> GetSummaryAsync();
    Task<List<string>> GetCitiesAsync();
    Task<List<PackageDeliveryDto>> GetPendingForAssigneeAsync(string assigneeUserId);
    Task<DeliveryVerifyResult> VerifyDeliveryAsync(
        string deliveryCode, string nationalCode, string distributorUserId);
    Task<IdentityResult> ReassignAsync(int packageId, string newAssigneeUserName);
}

public sealed class DistributionService : IDistributionService
{
    private readonly IDistributionUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpContext;
    private readonly TimeProvider _clock;
    private readonly ILogger<DistributionService> _logger;

    public DistributionService(
        IDistributionUnitOfWork uow,
        UserManager<ApplicationUser> userManager,
        IAuditService audit,
        IHttpContextAccessor httpContext,
        TimeProvider? clock,
        ILogger<DistributionService> logger)
    {
        _uow = uow;
        _userManager = userManager;
        _audit = audit;
        _httpContext = httpContext;
        _clock = clock ?? TimeProvider.System;
        _logger = logger;
    }

    private string? ActorName => _httpContext.HttpContext?.User.Identity?.Name;

    // ---------- ایمپورت ----------

    /// <summary>
    /// ستون‌ها: شهر (الزامی)، محله/ناحیه/منطقه (اختیاری)، کد تحویل (الزامی، یکتا)،
    /// کد ملی (الزامی، ۱۰ رقم)، تاریخ تحویل (اختیاری، شمسی)، کاربر تحویل‌دهنده
    /// (الزامی، نام کاربری موجود). هر سطر خراب فقط خودش را می‌سوزاند.
    /// </summary>
    public async Task<DistributionImportResult> ImportAsync(Stream excelStream)
    {
        var parsed = ExcelImporter.Read(excelStream, row => new ImportedPackageRow(
            ExcelImporter.Required(row, "City", "شهر").Trim(),
            ExcelImporter.Get(row, "Neighborhood", "محله")?.Trim(),
            ExcelImporter.Get(row, "Zone", "ناحیه")?.Trim(),
            ExcelImporter.Get(row, "District", "منطقه")?.Trim(),
            ExcelImporter.Required(row, "DeliveryCode", "کد تحویل", "کدتحویل").Trim(),
            PersianSearch.NormalizePhone(ExcelImporter.Required(row, "NationalCode", "کد ملی", "کدملی")),
            ExcelImporter.Get(row, "ScheduledDate", "تاریخ تحویل", "تاریخ")?.Trim(),
            ExcelImporter.Required(row, "Assignee", "کاربر تحویل‌دهنده", "تحویل‌دهنده", "نام کاربری").Trim()));

        var errors = new List<ImportRowErrorDto>(
            parsed.RowErrors.Select(e => new ImportRowErrorDto(e.RowNumber, e.Column, e.Message)));
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;

        foreach (var item in parsed.Items)
        {
            var (rowNumber, row) = (item.RowNumber, item.Value);

            if (!seenCodes.Add(row.DeliveryCode))
            {
                errors.Add(new(rowNumber, "DeliveryCode", $"کد تحویل «{row.DeliveryCode}» در فایل تکراری است."));
                continue;
            }

            if (row.NationalCode.Length != 10 || !row.NationalCode.All(char.IsDigit))
            {
                errors.Add(new(rowNumber, "NationalCode", $"کد ملی «{row.NationalCode}» معتبر نیست (۱۰ رقم)."));
                continue;
            }

            if (await _uow.PackageDeliveries.ExistsDeliveryCodeAsync(row.DeliveryCode))
            {
                errors.Add(new(rowNumber, "DeliveryCode", $"کد تحویل «{row.DeliveryCode}» قبلاً وارد شده است."));
                continue;
            }

            var assignee = await _userManager.FindByNameAsync(row.AssigneeUserName);
            if (assignee is null)
            {
                errors.Add(new(rowNumber, "Assignee", $"کاربر «{row.AssigneeUserName}» یافت نشد."));
                continue;
            }

            DateTime? scheduled = null;
            if (!string.IsNullOrWhiteSpace(row.ScheduledDateText))
            {
                if (!PersianDateParser.TryParse(row.ScheduledDateText, out var date))
                {
                    errors.Add(new(rowNumber, "ScheduledDate", $"تاریخ «{row.ScheduledDateText}» معتبر نیست (مثل ۱۴۰۳/۰۵/۱۲)."));
                    continue;
                }
                scheduled = date;
            }

            await _uow.PackageDeliveries.AddAsync(new PackageDelivery
            {
                City = row.City,
                Neighborhood = row.Neighborhood,
                Zone = row.Zone,
                District = row.District,
                DeliveryCode = row.DeliveryCode,
                NationalCode = row.NationalCode,
                ScheduledDate = scheduled,
                AssigneeUserId = assignee.Id,
                Status = DeliveryStatus.Pending
            });
            imported++;
        }

        if (imported > 0)
        {
            await _uow.CompleteAsync();
            await _audit.LogEventAsync("distribution.imported", ActorName,
                $"{imported} بسته از اکسل وارد شد ({errors.Count} خطا).");
        }

        errors.Sort((a, b) => a.RowNumber.CompareTo(b.RowNumber));
        return new DistributionImportResult(imported, errors, parsed.TotalRows);
    }

    // ---------- گزارش ----------

    public async Task<PagedResult<PackageDeliveryDto>> GetReportPagedAsync(DistributionReportFilter filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = _uow.PackageDeliveries.Query();

        if (filter.Status is not null)
            query = query.Where(p => p.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim();
            query = query.Where(p => p.City == city);
        }

        if (!string.IsNullOrWhiteSpace(filter.AssigneeUserName))
        {
            var assignee = await _userManager.FindByNameAsync(filter.AssigneeUserName.Trim());
            query = assignee is null
                ? query.Where(p => false)
                : query.Where(p => p.AssigneeUserId == assignee.Id);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = PersianSearch.Normalize(filter.Search);
            if (term.Length > 0)
            {
                query = query.Where(PersianSearch.ContainsNormalized<PackageDelivery>(
                    term, p => p.DeliveryCode, p => p.NationalCode, p => p.City,
                    p => p.Neighborhood, p => p.Zone, p => p.District));
            }
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<PackageDeliveryDto>
        {
            Items = await ToDtosAsync(items),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(int Total, int Pending, int Delivered)> GetSummaryAsync()
    {
        var query = _uow.PackageDeliveries.Query();
        var total = await query.CountAsync();
        var delivered = await query.CountAsync(p => p.Status == DeliveryStatus.Delivered);
        return (total, total - delivered, delivered);
    }

    public Task<List<string>> GetCitiesAsync() =>
        _uow.PackageDeliveries.Query()
            .Select(p => p.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    // ---------- توزیع‌کننده ----------

    /// <summary>همهٔ بسته‌های تحویل‌نشدهٔ منتسب به کاربر (بدون توجه به تاریخ).</summary>
    public async Task<List<PackageDeliveryDto>> GetPendingForAssigneeAsync(string assigneeUserId)
    {
        var items = await _uow.PackageDeliveries.Query()
            .Where(p => p.AssigneeUserId == assigneeUserId && p.Status == DeliveryStatus.Pending)
            .OrderBy(p => p.ScheduledDate == null)
            .ThenBy(p => p.ScheduledDate)
            .ThenBy(p => p.Id)
            .ToListAsync();

        return await ToDtosAsync(items);
    }

    /// <summary>
    /// ثبت تحویل: بسته با کد تحویل پیدا می‌شود؛ باید منتسب به همین توزیع‌کننده باشد
    /// و کد ملی اسکن‌شده با کد ملی پنل یکی باشد. تحویل تکراری خطا نیست، فقط گزارش می‌شود.
    /// </summary>
    public async Task<DeliveryVerifyResult> VerifyDeliveryAsync(
        string deliveryCode, string nationalCode, string distributorUserId)
    {
        var code = (deliveryCode ?? string.Empty).Trim();
        var national = PersianSearch.NormalizePhone(nationalCode);
        if (code.Length == 0 || national.Length == 0)
            return new DeliveryVerifyResult(DeliveryVerifyStatus.NotFound);

        var package = await _uow.PackageDeliveries.FindByDeliveryCodeAsync(code);
        if (package is null)
            return new DeliveryVerifyResult(DeliveryVerifyStatus.NotFound);

        if (package.Status == DeliveryStatus.Delivered)
        {
            var dto = (await ToDtosAsync([package]))[0];
            return new DeliveryVerifyResult(DeliveryVerifyStatus.AlreadyDelivered, dto, dto.DeliveredByUserName);
        }

        if (!string.Equals(package.AssigneeUserId, distributorUserId, StringComparison.Ordinal))
            return new DeliveryVerifyResult(DeliveryVerifyStatus.NotAssigned);

        if (!string.Equals(package.NationalCode, national, StringComparison.Ordinal))
        {
            _logger.LogWarning("Delivery national-code mismatch for {DeliveryCode}", code);
            return new DeliveryVerifyResult(DeliveryVerifyStatus.NationalCodeMismatch);
        }

        package.Status = DeliveryStatus.Delivered;
        package.DeliveredAtUtc = _clock.GetUtcNow().UtcDateTime;
        package.DeliveredByUserId = distributorUserId;
        await _uow.CompleteAsync();

        await _audit.LogEventAsync("distribution.delivered", ActorName,
            $"بسته {code} تحویل شد.");

        var delivered = (await ToDtosAsync([package]))[0];
        return new DeliveryVerifyResult(
            DeliveryVerifyStatus.Success, delivered, delivered.DeliveredByUserName);
    }

    /// <summary>انتساب مجدد بسته به توزیع‌کنندهٔ دیگر (ادمین).</summary>
    public async Task<IdentityResult> ReassignAsync(int packageId, string newAssigneeUserName)
    {
        var package = await _uow.PackageDeliveries.Query()
            .FirstOrDefaultAsync(p => p.Id == packageId);
        if (package is null)
            return IdentityResult.Failed(new IdentityError { Description = "بسته یافت نشد." });

        var assignee = await _userManager.FindByNameAsync((newAssigneeUserName ?? string.Empty).Trim());
        if (assignee is null)
            return IdentityResult.Failed(new IdentityError { Description = "کاربر تحویل‌دهنده یافت نشد." });

        package.AssigneeUserId = assignee.Id;
        await _uow.CompleteAsync();

        await _audit.LogEventAsync("distribution.reassigned", ActorName,
            $"بسته {package.DeliveryCode} به {assignee.UserName} منتسب شد.");
        return IdentityResult.Success;
    }

    // ---------- کمکی ----------

    private async Task<List<PackageDeliveryDto>> ToDtosAsync(List<PackageDelivery> items)
    {
        var ids = items
            .SelectMany(p => new[] { p.AssigneeUserId, p.DeliveredByUserId })
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var names = await _userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Id);

        return items.Select(p => new PackageDeliveryDto(
            p.Id, p.City, p.Neighborhood, p.Zone, p.District,
            p.DeliveryCode, p.NationalCode, p.ScheduledDate,
            p.AssigneeUserId,
            names.TryGetValue(p.AssigneeUserId, out var a) ? a : p.AssigneeUserId,
            p.Status, p.DeliveredAtUtc,
            p.DeliveredByUserId is not null && names.TryGetValue(p.DeliveredByUserId, out var d) ? d : null
        )).ToList();
    }

    private sealed record ImportedPackageRow(
        string City, string? Neighborhood, string? Zone, string? District,
        string DeliveryCode, string NationalCode, string? ScheduledDateText, string AssigneeUserName);
}
