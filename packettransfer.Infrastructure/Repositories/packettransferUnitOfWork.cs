using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using packettransfer.Domain.Interfaces;
using packettransfer.Infrastructure.Data;

namespace packettransfer.Infrastructure.Repositories;

/// <summary>
/// واحد کارِ پایه. عمداً فقط چهار ریپازیتوری زیرساختی دارد؛ پروژهٔ مصرف‌کننده
/// یک واحد کار دامنه‌ای جدا می‌سازد که از این ارث می‌برد.
/// <para>
/// سازنده فقط <c>DbContext</c> و <c>IServiceProvider</c> می‌گیرد تا با اضافه‌شدن
/// ریپازیتوری جدید به پایه، سازنده عوض نشود و کلاس‌های مشتق‌شدهٔ مصرف‌کننده نشکنند.
/// </para>
/// </summary>
public class packettransferUnitOfWork : IpackettransferUnitOfWork
{
    private readonly packettransferDbContext _context;
    private int _transactionDepth;

    public packettransferUnitOfWork(packettransferDbContext context, IServiceProvider services)
    {
        _context = context;
        AuditLogs = services.GetRequiredService<IAuditLogRepository>();
        Notifications = services.GetRequiredService<INotificationRepository>();
        Outbox = services.GetRequiredService<IOutboxRepository>();
        Payments = services.GetRequiredService<IPaymentRepository>();
    }

    public IAuditLogRepository AuditLogs { get; }
    public INotificationRepository Notifications { get; }
    public IOutboxRepository Outbox { get; }
    public IPaymentRepository Payments { get; }

    public async Task<int> CompleteAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        // استثنای همزمانی (RowVersion) باید عیناً به تماس‌گیرنده برسد تا
        // حلقه‌های retry در سرویس‌ها کار کنند؛ DbUpdateConcurrencyException
        // زیرکلاس DbUpdateException است و اگر اول گرفته نشود، گم می‌شود.
        catch (DbUpdateConcurrencyException)
        {
            throw;
        }
        // خطای «یکتایی» و برخورد فیلدها یک قاعدهٔ کسب‌وکار نیست؛ یک خطای فنی/داده‌ای است.
        catch (DbUpdateException ex)
        {
            throw new packettransfer.Domain.Exceptions.DataIntegrityException(
                "این عملیات با یک محدودیت داده‌ای دیتابیس برخورد کرد (مثلاً مقدار تکراری در یک فیلد یکتا یا طول فیلد بیش از حد مجاز).", ex);
        }
    }

    /// <summary>
    /// اجرای عملیات در یک تراکنش دیتابیس، سازگار با EnableRetryOnFailure:
    /// با استراتژی retry، «همه‌ی» دستورات تراکنش باید داخل ExecutionStrategy اجرا شوند —
    /// فقط BeginTransaction را داخل strategy گذاشتن کافی نیست و EF روی اولین کوئری
    /// InvalidOperationException می‌اندازد. فراخوانی تو‌در‌تو در همان تراکنش بیرونی ادغام می‌شود.
    /// </summary>
    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        if (_transactionDepth > 0)
        {
            await action();
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        var attempt = 0;
        await strategy.ExecuteAsync(async () =>
        {
            // تلاش مجددِ strategy (خطای موقت SQL/failover) همان delegate را از اول اجرا می‌کند،
            // ولی change tracker هنوز سطرهای Added/Modified و افزایش‌های in-memory تلاش قبلی را
            // نگه داشته — بدون پاک‌کردن، تغییرات دوباره اعمال می‌شد.
            // تلاش اول پاک نمی‌شود: فراخوانی‌کننده ممکن است پیش از ورود به تراکنش چیزی
            // بارگذاری/تغییر داده باشد که هنوز commit نشده است.
            if (attempt++ > 0)
                _context.ChangeTracker.Clear();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            _transactionDepth = 1;
            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                _transactionDepth = 0;
            }
        });
    }

    public void ClearChangeTracker() => _context.ChangeTracker.Clear();
}