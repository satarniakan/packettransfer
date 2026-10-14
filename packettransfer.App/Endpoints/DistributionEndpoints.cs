using System.Security.Claims;
using packettransfer.Distribution.DTOs;
using packettransfer.Distribution.Services;

namespace packettransfer.App.Endpoints;

/// <summary>
/// API توزیع بسته (نسخه۱) برای اپ توزیع‌کننده: فهرست بسته‌های خود + ثبت تحویل.
/// احراز هویت با <c>Authorization: Bearer pat_...</c> و policy مخصوص «Api» است، نه کوکی.
/// توکن از <c>POST /api/v1/auth/login</c> (نام کاربری/رمز) گرفته می‌شود.
/// </summary>
public static class DistributionEndpoints
{
    public static IEndpointRouteBuilder MapDistributionApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/distribution");

        group.MapGet("/pending", async (ClaimsPrincipal user, IDistributionService distribution) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            var items = await distribution.GetPendingForAssigneeAsync(userId);
            return Results.Ok(items);
        }).RequireAuthorization("Api");

        group.MapPost("/deliver", async (
            ClaimsPrincipal user,
            IDistributionService distribution,
            DeliverApiDto dto) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            var result = await distribution.VerifyDeliveryAsync(
                dto.DeliveryCode, dto.NationalCode, userId);

            return result.Status switch
            {
                DeliveryVerifyStatus.Success => Results.Ok(new
                {
                    message = "تحویل ثبت شد.",
                    deliveredAt = result.Package!.DeliveredAtUtc,
                    deliveredBy = result.DeliveredByUserName
                }),
                DeliveryVerifyStatus.NotFound =>
                    Results.NotFound(new { message = "بسته‌ای با این کد تحویل یافت نشد." }),
                DeliveryVerifyStatus.AlreadyDelivered => Results.Conflict(new
                {
                    message = "این بسته قبلاً تحویل شده است.",
                    deliveredAt = result.Package!.DeliveredAtUtc,
                    deliveredBy = result.DeliveredByUserName
                }),
                DeliveryVerifyStatus.NotAssigned =>
                    Results.Json(new { message = "این بسته به شما منتسب نیست." },
                        statusCode: StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new { message = "کد ملی با اطلاعات پنل یکسان نیست." })
            };
        }).RequireAuthorization("Api");

        return app;
    }
}

/// <summary>ورودی ثبت تحویل: هر دو از اسکن بارکد گیرنده می‌آیند (یا ورود دستی).</summary>
public record DeliverApiDto(string DeliveryCode, string NationalCode);
