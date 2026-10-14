using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using packettransfer.Application.DTOs;
using packettransfer.Domain.Identity;

namespace packettransfer.Application.Services;

public interface IAuthService
{
    Task<PasswordLoginResult> LoginWithPasswordAsync(string userName, string password);
    Task LogoutAsync();
    Task<UserProfileDto?> GetProfileAsync(string userId);
    Task<ProfileUpdateResult> UpdateProfileAsync(string userId, string? fullName, string? email);

    /// <summary>
    /// تغییر رمز توسط خودِ کاربر با دانستن رمز فعلی. نشست جانشین (ادمینِ به‌جای
    /// کاربر) حق تغییر رمز ندارد — وگرنه ادمین پشتیبانی می‌توانست برای همیشه راه
    /// ورود رمزی به حساب کاربر باز کند.
    /// </summary>
    Task<ChangePasswordResult> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, string? confirmPassword);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthService> _logger;
    private readonly ILoginHistoryService _history;
    private readonly IHttpContextAccessor _httpContext;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILoginHistoryService history,
        IHttpContextAccessor httpContext,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _history = history;
        _httpContext = httpContext;
        _logger = logger;
    }

    private (string? Ip, string? UserAgent) RequestInfo()
    {
        var http = _httpContext.HttpContext;
        return (http?.Connection.RemoteIpAddress?.ToString(),
            http?.Request.Headers.UserAgent.ToString());
    }

    public async Task<PasswordLoginResult> LoginWithPasswordAsync(string userName, string password)
    {
        var (ip, userAgent) = RequestInfo();
        var user = await FindByUserNameOrEmailAsync(userName);

        if (user is null)
        {
            _logger.LogWarning("Login failed: no user found for {UserName}", userName);
            await _history.RecordAsync(null, userName, false, "Password", ip, userAgent, "invalid-credentials");
            return new PasswordLoginResult(false);
        }

        // lockoutOnFailure:true (پیش‌فرض Identity خودش FALSE است) قفل موقت حساب را می‌خواهد.
        var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            var reason = result.IsLockedOut ? "locked-out" : "invalid-credentials";
            _logger.LogWarning("Login failed for {UserName}: {Reason}", userName,
                result.IsLockedOut ? "locked out" : result.IsNotAllowed ? "not allowed" : "invalid password");
            await _history.RecordAsync(user.Id, userName, false, "Password", ip, userAgent, reason);
        }
        else
        {
            await _history.RecordAsync(user.Id, userName, true, "Password", ip, userAgent);
        }

        return new PasswordLoginResult(result.Succeeded);
    }

    public Task LogoutAsync() => _signInManager.SignOutAsync();

    public async Task<UserProfileDto?> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        return new UserProfileDto(
            user.UserName,
            user.PhoneNumber,
            user.FullName,
            user.Email,
            await _userManager.HasPasswordAsync(user));
    }

    public async Task<ProfileUpdateResult> UpdateProfileAsync(
        string userId, string? fullName, string? email)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return new ProfileUpdateResult(ProfileUpdateStatus.UserNotFound);

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var trimmed = fullName.Trim();
            if (trimmed.Length > 100)
                return new ProfileUpdateResult(ProfileUpdateStatus.FullNameTooLong);

            user.FullName = trimmed;
            await _userManager.UpdateAsync(user);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser is not null && existingUser.Id != user.Id)
                return new ProfileUpdateResult(ProfileUpdateStatus.EmailAlreadyExists);

            var emailResult = await _userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
            {
                _logger.LogWarning("SetEmail failed for user {UserId}: {Errors}",
                    userId, string.Join(" | ", emailResult.Errors.Select(e => e.Description)));
                return new ProfileUpdateResult(ProfileUpdateStatus.EmailUpdateFailed);
            }
        }

        return new ProfileUpdateResult(ProfileUpdateStatus.Success);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, string? confirmPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return new ChangePasswordResult(ChangePasswordStatus.UserNotFound);

        if (_httpContext.HttpContext?.User.HasClaim(c => c.Type == ImpersonationClaims.Impersonator) == true)
            return new ChangePasswordResult(ChangePasswordStatus.ImpersonationBlocked);

        if (newPassword != confirmPassword)
            return new ChangePasswordResult(ChangePasswordStatus.PasswordMismatch);

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            // کد PasswordMismatch یعنی رمز فعلی اشتباه است؛ بقیهٔ کدها (طول،
            // پیچیدگی و…) یعنی رمز جدید سیاست رمز را پاس نکرده است.
            var wrongCurrent = result.Errors.Any(e => e.Code == "PasswordMismatch");
            _logger.LogWarning("ChangePassword failed for user {UserId}: {Errors}",
                userId, string.Join(" | ", result.Errors.Select(e => e.Description)));
            return new ChangePasswordResult(wrongCurrent
                ? ChangePasswordStatus.WrongCurrentPassword
                : ChangePasswordStatus.ChangeFailed);
        }

        return new ChangePasswordResult(ChangePasswordStatus.Success);
    }

    private async Task<ApplicationUser?> FindByUserNameOrEmailAsync(string userName)
    {
        var byEmail = await _userManager.FindByEmailAsync(userName);
        return byEmail ?? await _userManager.FindByNameAsync(userName);
    }
}
