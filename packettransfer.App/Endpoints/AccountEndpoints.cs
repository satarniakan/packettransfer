// packettransfer.App/Endpoints/AccountEndpoints.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using packettransfer.Application.DTOs;
using packettransfer.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace packettransfer.App.Endpoints;

/// <summary>
/// Endpointهای احراز هویت. هر کدام فقط IAuthService را صدا می‌زنند، بدون منطق تجاری.
/// (این‌ها Razor Page نیستند چون باید کوکی ست کنند و این کار داخل Blazor Circuit ممکن نیست.)
/// ورود فقط با نام کاربری/ایمیل و رمز عبور است.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // سازگاری با نشانی قدیمی ورود رمزی
        app.MapGet("/login-password", () => Results.Redirect("/login"));

        app.MapPost("/Account/Login", async (
            IAuthService authService,
            [FromForm] string username,
            [FromForm] string password) =>
        {
            var result = await authService.LoginWithPasswordAsync(username, password);

            return result.Succeeded
                ? Results.Redirect("/")
                : Results.Redirect("/login?error=1");
        }).WithMetadata(new RequireAntiforgeryTokenAttribute()).RequireRateLimiting("login");

        app.MapPost("/Account/UpdateProfile", async (
            HttpContext httpContext,
            IAuthService authService,
            [FromForm] string? fullName,
            [FromForm] string? email) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
            {
                return Results.Redirect("/login");
            }

            var result = await authService.UpdateProfileAsync(userId, fullName, email);

            return result.Status switch
            {
                ProfileUpdateStatus.Success => Results.Redirect("/profile?success=1"),
                ProfileUpdateStatus.UserNotFound => Results.Redirect("/login"),
                _ => Results.Redirect($"/profile?error={result.Status}")
            };
        }).WithMetadata(new RequireAntiforgeryTokenAttribute()).RequireRateLimiting("profile");

        app.MapPost("/Account/ChangePassword", async (
            HttpContext httpContext,
            IAuthService authService,
            [FromForm] string currentPassword,
            [FromForm] string newPassword,
            [FromForm] string? confirmPassword) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
            {
                return Results.Redirect("/login");
            }

            var result = await authService.ChangePasswordAsync(
                userId, currentPassword, newPassword, confirmPassword);

            return result.Status switch
            {
                ChangePasswordStatus.Success => Results.Redirect("/profile?password=success"),
                ChangePasswordStatus.UserNotFound => Results.Redirect("/login"),
                _ => Results.Redirect($"/profile?passworderror={result.Status}")
            };
        }).WithMetadata(new RequireAntiforgeryTokenAttribute()).RequireRateLimiting("profile");

        app.MapPost("/logout", async (IAuthService authService) =>
        {
            await authService.LogoutAsync();
            return Results.Redirect("/login");
        }).WithMetadata(new RequireAntiforgeryTokenAttribute());

        return app;
    }
}
