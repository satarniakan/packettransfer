using Microsoft.EntityFrameworkCore;
using packettransfer.Distribution.DTOs;
using packettransfer.Distribution.Entities;

namespace packettransfer.Distribution.Repositories;

/// <summary>ریپازیتوری جدول توزیع بسته.</summary>
public interface IPackageDeliveryRepository
{
    Task AddAsync(PackageDelivery package);
    Task<PackageDelivery?> FindByDeliveryCodeAsync(string deliveryCode);
    Task<bool> ExistsDeliveryCodeAsync(string deliveryCode);
    IQueryable<PackageDelivery> Query();
}

public sealed class PackageDeliveryRepository : IPackageDeliveryRepository
{
    private readonly packettransfer.Infrastructure.Data.packettransferDbContext _context;

    public PackageDeliveryRepository(packettransfer.Infrastructure.Data.packettransferDbContext context)
        => _context = context;

    public Task AddAsync(PackageDelivery package)
        => _context.Set<PackageDelivery>().AddAsync(package).AsTask();

    public Task<PackageDelivery?> FindByDeliveryCodeAsync(string deliveryCode)
        => _context.Set<PackageDelivery>()
            .FirstOrDefaultAsync(p => p.DeliveryCode == deliveryCode);

    public Task<bool> ExistsDeliveryCodeAsync(string deliveryCode)
        => _context.Set<PackageDelivery>().AnyAsync(p => p.DeliveryCode == deliveryCode);

    public IQueryable<PackageDelivery> Query() => _context.Set<PackageDelivery>().AsQueryable();
}
