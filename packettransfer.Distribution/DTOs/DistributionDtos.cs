using packettransfer.Distribution.Entities;

namespace packettransfer.Distribution.DTOs;

/// <summary>یک ردیف گزارش/فهرست توزیع.</summary>
public record PackageDeliveryDto(
    int Id,
    string City,
    string? Neighborhood,
    string? Zone,
    string? District,
    string DeliveryCode,
    string NationalCode,
    DateTime? ScheduledDate,
    string AssigneeUserId,
    string? AssigneeUserName,
    DeliveryStatus Status,
    DateTime? DeliveredAtUtc,
    string? DeliveredByUserName);

/// <summary>فیلتر گزارش داشبورد.</summary>
public sealed class DistributionReportFilter
{
    public string? Search { get; set; }
    public string? City { get; set; }
    public DeliveryStatus? Status { get; set; }
    public string? AssigneeUserName { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>نتیجهٔ ایمپورت اکسل توزیع.</summary>
public record DistributionImportResult(int ImportedCount, List<ImportRowErrorDto> RowErrors, int TotalRows);

public record ImportRowErrorDto(int RowNumber, string Column, string Message);

/// <summary>نتیجهٔ ثبت تحویل با بارکد.</summary>
public enum DeliveryVerifyStatus
{
    Success,
    NotFound,
    AlreadyDelivered,
    NotAssigned,
    NationalCodeMismatch
}

public record DeliveryVerifyResult(
    DeliveryVerifyStatus Status,
    PackageDeliveryDto? Package = null,
    string? DeliveredByUserName = null);
