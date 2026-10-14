using Microsoft.Extensions.DependencyInjection;
using packettransfer.Distribution.Repositories;
using packettransfer.Domain.Interfaces;
using packettransfer.Infrastructure.Repositories;

namespace packettransfer.Distribution;

/// <summary>
/// واحد کار دامنهٔ توزیع: همان تراکنش پایه + ریپوی بسته‌ها.
/// سازنده فقط context و provider می‌گیرد تا با به‌روزرسانی پایه نشکند.
/// </summary>
public interface IDistributionUnitOfWork : IpackettransferUnitOfWork
{
    IPackageDeliveryRepository PackageDeliveries { get; }
}

public sealed class DistributionUnitOfWork : packettransferUnitOfWork, IDistributionUnitOfWork
{
    public DistributionUnitOfWork(
        packettransfer.Infrastructure.Data.packettransferDbContext context,
        IServiceProvider services)
        : base(context, services)
    {
        PackageDeliveries = services.GetRequiredService<IPackageDeliveryRepository>();
    }

    public IPackageDeliveryRepository PackageDeliveries { get; }
}
