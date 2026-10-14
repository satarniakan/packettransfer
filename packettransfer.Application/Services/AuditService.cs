using packettransfer.Application.DTOs;
using packettransfer.Application.Queries;
using packettransfer.Domain.Entities;
using packettransfer.Domain.Interfaces;

namespace packettransfer.Application.Services;

/// <summary>
/// ثبت رویدادهای سامانه. ساختار داده‌اش کاملاً عمومی است (چه کسی، چه وقت، چه کرد)؛
/// متن رویداد را فراخواننده می‌دهد، پس هیچ وابستگی به دامنه ندارد.
/// </summary>
public interface IAuditService
{
    Task LogEventAsync(string eventType, string? userEmail, string details);
    Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100);
    Task<PagedResult<AuditLog>> GetEventsPagedAsync(int page, int pageSize, string? search = null);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repository;
    private readonly IpackettransferUnitOfWork _unitOfWork;

    public AuditService(IAuditLogRepository repository, IpackettransferUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task LogEventAsync(string eventType, string? userEmail, string details)
    {
        await _repository.AddAsync(new AuditLog(eventType, userEmail, details));
        await _unitOfWork.CompleteAsync();
    }

    public Task<IEnumerable<AuditLog>> GetRecentEventsAsync(int count = 100) =>
        _repository.GetRecentAsync(count);

    public async Task<PagedResult<AuditLog>> GetEventsPagedAsync(int page, int pageSize, string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        // سقف مشترک با ToPagedAsync: ?pageSize=10000000 نباید کل جدول را به حافظه بکشد
        if (pageSize > PagedQueryExtensions.MaxPageSize) pageSize = PagedQueryExtensions.MaxPageSize;

        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, search);

        return new PagedResult<AuditLog>
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}