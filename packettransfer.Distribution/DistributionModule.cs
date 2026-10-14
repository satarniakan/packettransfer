using Microsoft.EntityFrameworkCore;
using packettransfer.Distribution.Entities;
using packettransfer.Infrastructure.Data;

namespace packettransfer.Distribution;

/// <summary>
/// ماژول دامنهٔ توزیع بسته: جدول‌های خودش را به مدل EF اضافه می‌کند،
/// بدون آنکه پایه ویرایش شود. سازندهٔ بدون‌پارامتر برای کشف design-time لازم است.
/// </summary>
public sealed class DistributionModule : IpackettransferModule
{
    public string Name => "Distribution";

    public void ConfigureModel(ModelBuilder builder)
    {
        builder.Entity<PackageDelivery>(e =>
        {
            e.ToTable("PackageDeliveries");
            e.HasKey(x => x.Id);

            e.Property(x => x.City).HasMaxLength(100).IsRequired();
            e.Property(x => x.Neighborhood).HasMaxLength(100);
            e.Property(x => x.Zone).HasMaxLength(100);
            e.Property(x => x.District).HasMaxLength(100);

            e.Property(x => x.DeliveryCode).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.DeliveryCode).IsUnique();

            e.Property(x => x.NationalCode).HasMaxLength(16).IsRequired();
            e.HasIndex(x => x.NationalCode);

            e.Property(x => x.AssigneeUserId).HasMaxLength(450).IsRequired();
            e.HasIndex(x => new { x.AssigneeUserId, x.Status });

            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.City);
        });
    }
}
