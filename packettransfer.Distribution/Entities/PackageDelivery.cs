using packettransfer.Domain.Common;

namespace packettransfer.Distribution.Entities;

/// <summary>وضعیت بسته: در انتظار تحویل یا تحویل‌شده.</summary>
public enum DeliveryStatus
{
    Pending = 0,
    Delivered = 1
}

/// <summary>
/// یک ردیف توزیع بسته: از ایمپورت اکسل داشبورد می‌آید و با اسکن بارکد
/// (کد تحویل + کد ملی) توسط توزیع‌کننده، تحویل‌شده می‌شود.
/// </summary>
public class PackageDelivery : AuditableEntity
{
    /// <summary>شهر.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>محله (اختیاری).</summary>
    public string? Neighborhood { get; set; }

    /// <summary>ناحیه (اختیاری).</summary>
    public string? Zone { get; set; }

    /// <summary>منطقه (اختیاری).</summary>
    public string? District { get; set; }

    /// <summary>کد تحویل — یکتا در کل جدول؛ روی بارکد گیرنده چاپ شده.</summary>
    public string DeliveryCode { get; set; } = string.Empty;

    /// <summary>کد ملی گیرنده — ۱۰ رقم انگلیسی (نرمال‌شده).</summary>
    public string NationalCode { get; set; } = string.Empty;

    /// <summary>تاریخ مقرر تحویل (فقط تاریخ، UTC)؛ null یعنی بدون موعد.</summary>
    public DateTime? ScheduledDate { get; set; }

    /// <summary>کاربر تحویل‌دهندهٔ منتسب (<c>ApplicationUser.Id</c>).</summary>
    public string AssigneeUserId { get; set; } = string.Empty;

    /// <summary>وضعیت.</summary>
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    /// <summary>زمان واقعی تحویل (UTC)؛ null یعنی هنوز تحویل نشده.</summary>
    public DateTime? DeliveredAtUtc { get; set; }

    /// <summary>کاربری که تحویل را ثبت کرد (معمولاً همان منتسب).</summary>
    public string? DeliveredByUserId { get; set; }
}
