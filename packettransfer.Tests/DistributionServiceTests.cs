using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using packettransfer.Distribution;
using packettransfer.Distribution.DTOs;
using packettransfer.Distribution.Entities;
using packettransfer.Distribution.Repositories;
using packettransfer.Distribution.Services;
using packettransfer.Domain.Identity;
using packettransfer.Application.Services;
using packettransfer.Infrastructure.Data;
using packettransfer.Infrastructure.Repositories;

namespace packettransfer.Tests;

/// <summary>
/// ماژول توزیع بسته: ایمپورت اکسل، گزارش، فهرست توزیع‌کننده و ثبت تحویل با بارکد.
/// </summary>
public class DistributionServiceTests
{
    private sealed class Harness : IAsyncDisposable
    {
        public packettransferDbContext Db { get; }
        public Mock<UserManager<ApplicationUser>> Users { get; }
        public DistributionService Service { get; }
        private readonly ServiceProvider _provider;

        public Harness()
        {
            var options = new DbContextOptionsBuilder<packettransferDbContext>()
                .ReplaceService<IModelCacheKeyFactory, packettransferModelCacheKeyFactory>()
                .UseInMemoryDatabase($"dist-{Guid.NewGuid()}")
                .Options;

            Db = new packettransferDbContext(options, [new DistributionModule()]);

            var store = new UserStore<ApplicationUser>(Db);
            Users = new Mock<UserManager<ApplicationUser>>(
                store, null!, null!, null!, null!, null!, null!, null!, null!);
            // Users واقعیِ store تا کوئری نام کاربری روی InMemory کار کند
            Users.Setup(u => u.Users).Returns(store.Users);
            Users.Setup(u => u.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((string name) => store.Users.FirstOrDefault(u => u.UserName == name));

            var services = new ServiceCollection();
            services.AddSingleton<IPackageDeliveryRepository>(
                new PackageDeliveryRepository(Db));
            services.AddSingleton(Mock.Of<packettransfer.Domain.Interfaces.IAuditLogRepository>());
            services.AddSingleton(Mock.Of<packettransfer.Domain.Interfaces.INotificationRepository>());
            services.AddSingleton(Mock.Of<packettransfer.Domain.Interfaces.IOutboxRepository>());
            services.AddSingleton(Mock.Of<packettransfer.Domain.Interfaces.IPaymentRepository>());
            _provider = services.BuildServiceProvider();
            var uow = new DistributionUnitOfWork(Db, _provider);

            Service = new DistributionService(
                uow, Users.Object, Mock.Of<IAuditService>(),
                Mock.Of<IHttpContextAccessor>(), TimeProvider.System,
                NullLogger<DistributionService>.Instance);
        }

        public async Task SeedUserAsync(string id, string userName)
        {
            Db.Users.Add(new ApplicationUser { Id = id, UserName = userName });
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private static MemoryStream Workbook(params string[][] rows)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Sheet1");
            for (var r = 0; r < rows.Length; r++)
                for (var c = 0; c < rows[r].Length; c++)
                    sheet.Cell(r + 1, c + 1).Value = rows[r][c];
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task Import_ValidRows_CreatesPackages()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");

        using var stream = Workbook(
            ["شهر", "محله", "ناحیه", "منطقه", "کد تحویل", "کد ملی", "تاریخ تحویل", "کاربر تحویل‌دهنده"],
            ["تهران", "نارمک", "۴", "۸", "PKG-001", "0023456789", "۱۴۰۳/۰۵/۱۲", "courier.1"]);

        var result = await h.Service.ImportAsync(stream);

        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.RowErrors);
        var saved = await h.Db.Set<PackageDelivery>().SingleAsync();
        Assert.Equal("PKG-001", saved.DeliveryCode);
        Assert.Equal("0023456789", saved.NationalCode);
        Assert.Equal("d1", saved.AssigneeUserId);
        Assert.Equal(new DateTime(2024, 8, 2, 0, 0, 0, DateTimeKind.Utc), saved.ScheduledDate);
        Assert.Equal(DeliveryStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task Import_BadRows_ReportErrors_WithoutBurningOthers()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");

        using var stream = Workbook(
            ["شهر", "کد تحویل", "کد ملی", "کاربر تحویل‌دهنده"],
            ["تهران", "PKG-001", "0023456789", "courier.1"],
            ["تهران", "PKG-001", "0023456789", "courier.1"],
            ["تهران", "PKG-002", "123", "courier.1"],
            ["تهران", "PKG-003", "0023456789", "ghost"]);

        var result = await h.Service.ImportAsync(stream);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(4, result.TotalRows);
        Assert.Equal(3, result.RowErrors.Count);
    }

    [Fact]
    public async Task VerifyDelivery_MatchingCodes_MarksDelivered()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");
        h.Db.Set<PackageDelivery>().Add(new PackageDelivery
        {
            City = "تهران",
            DeliveryCode = "PKG-010",
            NationalCode = "0023456789",
            AssigneeUserId = "d1",
            Status = DeliveryStatus.Pending
        });
        await h.Db.SaveChangesAsync();

        // ارقام فارسی اسکن‌شده هم باید بخواند
        var result = await h.Service.VerifyDeliveryAsync("PKG-010", "۰۰۲۳۴۵۶۷۸۹", "d1");

        Assert.Equal(DeliveryVerifyStatus.Success, result.Status);
        Assert.NotNull(result.Package!.DeliveredAtUtc);
        Assert.Equal("courier.1", result.DeliveredByUserName);
    }

    [Fact]
    public async Task VerifyDelivery_WrongNationalCode_ReturnsMismatch_WithoutDelivery()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");
        h.Db.Set<PackageDelivery>().Add(new PackageDelivery
        {
            City = "تهران",
            DeliveryCode = "PKG-011",
            NationalCode = "0023456789",
            AssigneeUserId = "d1",
            Status = DeliveryStatus.Pending
        });
        await h.Db.SaveChangesAsync();

        var result = await h.Service.VerifyDeliveryAsync("PKG-011", "1111111111", "d1");

        Assert.Equal(DeliveryVerifyStatus.NationalCodeMismatch, result.Status);
        Assert.Equal(DeliveryStatus.Pending,
            (await h.Db.Set<PackageDelivery>().SingleAsync()).Status);
    }

    [Fact]
    public async Task VerifyDelivery_UnknownCode_ReturnsNotFound()
    {
        await using var h = new Harness();

        var result = await h.Service.VerifyDeliveryAsync("NOPE", "0023456789", "d1");

        Assert.Equal(DeliveryVerifyStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task VerifyDelivery_OtherDistributorsPackage_ReturnsNotAssigned()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");
        await h.SeedUserAsync("d2", "courier.2");
        h.Db.Set<PackageDelivery>().Add(new PackageDelivery
        {
            City = "تهران",
            DeliveryCode = "PKG-012",
            NationalCode = "0023456789",
            AssigneeUserId = "d1",
            Status = DeliveryStatus.Pending
        });
        await h.Db.SaveChangesAsync();

        var result = await h.Service.VerifyDeliveryAsync("PKG-012", "0023456789", "d2");

        Assert.Equal(DeliveryVerifyStatus.NotAssigned, result.Status);
    }

    [Fact]
    public async Task VerifyDelivery_AlreadyDelivered_ReturnsInfo()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");
        h.Db.Set<PackageDelivery>().Add(new PackageDelivery
        {
            City = "تهران",
            DeliveryCode = "PKG-013",
            NationalCode = "0023456789",
            AssigneeUserId = "d1",
            Status = DeliveryStatus.Delivered,
            DeliveredAtUtc = DateTime.UtcNow,
            DeliveredByUserId = "d1"
        });
        await h.Db.SaveChangesAsync();

        var result = await h.Service.VerifyDeliveryAsync("PKG-013", "0023456789", "d1");

        Assert.Equal(DeliveryVerifyStatus.AlreadyDelivered, result.Status);
    }

    [Fact]
    public async Task PendingForAssignee_ReturnsOnlyMinePending()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");
        await h.SeedUserAsync("d2", "courier.2");
        h.Db.Set<PackageDelivery>().AddRange(
            new PackageDelivery
            {
                City = "تهران", DeliveryCode = "A", NationalCode = "0023456789",
                AssigneeUserId = "d1", Status = DeliveryStatus.Pending
            },
            new PackageDelivery
            {
                City = "تهران", DeliveryCode = "B", NationalCode = "0023456789",
                AssigneeUserId = "d1", Status = DeliveryStatus.Delivered,
                DeliveredAtUtc = DateTime.UtcNow, DeliveredByUserId = "d1"
            },
            new PackageDelivery
            {
                City = "مشهد", DeliveryCode = "C", NationalCode = "0023456789",
                AssigneeUserId = "d2", Status = DeliveryStatus.Pending
            });
        await h.Db.SaveChangesAsync();

        var items = await h.Service.GetPendingForAssigneeAsync("d1");

        var single = Assert.Single(items);
        Assert.Equal("A", single.DeliveryCode);
        Assert.Equal("courier.1", single.AssigneeUserName);
    }

    [Fact]
    public async Task Report_FiltersByStatusAndSearch()
    {
        await using var h = new Harness();
        await h.SeedUserAsync("d1", "courier.1");
        h.Db.Set<PackageDelivery>().AddRange(
            new PackageDelivery
            {
                City = "تهران", DeliveryCode = "PKG-100", NationalCode = "0023456789",
                AssigneeUserId = "d1", Status = DeliveryStatus.Pending
            },
            new PackageDelivery
            {
                City = "مشهد", DeliveryCode = "PKG-200", NationalCode = "0023456789",
                AssigneeUserId = "d1", Status = DeliveryStatus.Delivered,
                DeliveredAtUtc = DateTime.UtcNow, DeliveredByUserId = "d1"
            });
        await h.Db.SaveChangesAsync();

        var delivered = await h.Service.GetReportPagedAsync(new DistributionReportFilter
        {
            Status = DeliveryStatus.Delivered,
            Page = 1,
            PageSize = 20
        });
        Assert.Equal(1, delivered.TotalCount);
        Assert.Equal("PKG-200", delivered.Items[0].DeliveryCode);

        var searched = await h.Service.GetReportPagedAsync(new DistributionReportFilter
        {
            Search = "PKG-100",
            Page = 1,
            PageSize = 20
        });
        Assert.Equal(1, searched.TotalCount);

        var summary = await h.Service.GetSummaryAsync();
        Assert.Equal((2, 1, 1), summary);
    }

    [Fact]
    public void PersianDateParser_ParsesJalaliDates()
    {
        Assert.True(PersianDateParser.TryParse("1403/05/12", out var d1));
        Assert.Equal(new DateTime(2024, 8, 2, 0, 0, 0, DateTimeKind.Utc), d1);

        Assert.True(PersianDateParser.TryParse("۱۴۰۳-۰۵-۱۲", out var d2));
        Assert.Equal(d1, d2);

        Assert.False(PersianDateParser.TryParse("1403/13/01", out _));
        Assert.False(PersianDateParser.TryParse("نادرست", out _));
        Assert.False(PersianDateParser.TryParse(null, out _));
    }
}
