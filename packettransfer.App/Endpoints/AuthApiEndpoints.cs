using System.Security.Claims;
using packettransfer.Application.DTOs;
using packettransfer.Application.Services;

namespace packettransfer.App.Endpoints;

/// <summary>
/// API نسخه۱: ورود با نام کاربری/ایمیل و رمز + مدیریت توکن‌ها/نشست‌ها با JSON.
/// احراز هویت با <c>Authorization: Bearer pat_...</c> و policy مخصوص «Api» است، نه کوکی.
/// </summary>
public static class AuthApiEndpoints
{
    public static IEndpointRouteBuilder MapAuthApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", async (
            HttpContext http,
            IApiTokenService tokens,
            ILoginHistoryService history,
            PasswordLoginApiDto dto) =>
        {
            var result = await tokens.LoginWithPasswordAndIssueTokenAsync(
                dto.Username, dto.Password, dto.DeviceName);

            await history.RecordAsync(
                userId: result.UserId,
                userName: dto.Username,
                succeeded: result.Succeeded,
                method: "ApiToken",
                ipAddress: http.Connection.RemoteIpAddress?.ToString(),
                userAgent: http.Request.Headers.UserAgent.ToString(),
                failureReason: result.Succeeded ? null : "invalid-credentials");

            return result.Succeeded
                ? Results.Ok(new TokenResponseDto(result.Token!, result.ExpiresAtUtc!.Value))
                : Results.Unauthorized();
        }).RequireRateLimiting("login");

        group.MapGet("/tokens", async (ClaimsPrincipal user, IApiTokenService tokens) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            var list = await tokens.GetActiveAsync(userId);
            return Results.Ok(list.Select(t => new ApiTokenDto(
                t.Id, t.DeviceName, t.CreatedAtUtc, t.LastUsedAtUtc, t.ExpiresAtUtc)).ToList());
        }).RequireAuthorization("Api");

        group.MapDelete("/tokens/{id:int}", async (int id, ClaimsPrincipal user, IApiTokenService tokens) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            return await tokens.RevokeAsync(id, userId) ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("Api");

        // «خروج از همهٔ دستگاه‌ها»
        group.MapDelete("/tokens", async (ClaimsPrincipal user, IApiTokenService tokens) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            var count = await tokens.RevokeAllAsync(userId);
            return Results.Ok(new { revoked = count });
        }).RequireAuthorization("Api");

        app.MapGet("/api/v1/profile", async (ClaimsPrincipal user, IAuthService auth) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            var profile = await auth.GetProfileAsync(userId);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        }).RequireAuthorization("Api");

        return app;
    }
}
