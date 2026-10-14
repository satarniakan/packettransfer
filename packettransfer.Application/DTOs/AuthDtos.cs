namespace packettransfer.Application.DTOs;

public record PasswordLoginResult(bool Succeeded);

public enum ProfileUpdateStatus
{
    Success,
    UserNotFound,
    FullNameTooLong,
    EmailAlreadyExists,
    EmailUpdateFailed
}

public record ProfileUpdateResult(ProfileUpdateStatus Status);

public enum ChangePasswordStatus
{
    Success,
    UserNotFound,

    /// <summary>رمز فعلی اشتباه وارد شده است.</summary>
    WrongCurrentPassword,
    PasswordMismatch,
    ChangeFailed,

    /// <summary>نشست جانشین (ادمینِ به‌جای کاربر) حق تغییر رمز ندارد.</summary>
    ImpersonationBlocked
}

public record ChangePasswordResult(ChangePasswordStatus Status);

/// <summary>
/// پروفایل پایهٔ کاربر. عمداً فقط شماره، ایمیل، نام و وضعیت رمز است؛ هر فیلد دامنه‌ای
/// (آدرس، سازمان، …) باید در ماژول پروژهٔ مصرف‌کننده تعریف شود.
/// </summary>
public record UserProfileDto(
    string? UserName,
    string? PhoneNumber,
    string? FullName,
    string? Email,
    bool HasPassword);