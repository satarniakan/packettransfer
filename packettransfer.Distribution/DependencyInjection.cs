using Microsoft.Extensions.DependencyInjection;
using packettransfer.Distribution.Repositories;
using packettransfer.Distribution.Services;
using packettransfer.Infrastructure.Data;

namespace packettransfer.Distribution;

/// <summary>ثبت ماژول توزیع: مدل EF + واحد کار + ریپو + سرویس.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDistribution(this IServiceCollection services)
    {
        services.AddSingleton<IpackettransferModule, DistributionModule>();
        services.AddScoped<IPackageDeliveryRepository, PackageDeliveryRepository>();
        services.AddScoped<IDistributionUnitOfWork, DistributionUnitOfWork>();
        services.AddScoped<IDistributionService, DistributionService>();
        return services;
    }
}
