using System.ComponentModel.DataAnnotations;

namespace packettransfer.Application.DTOs;

public record UserListItemDto(
    string UserId,
    string? UserName,
    string? PhoneNumber,
    string? FullName,
    string? Email,
    List<string> Roles);

/// <summary>ویرایش اطلاعات یک کاربر توسط ادمین (نقش‌ها از صفحهٔ جداگانه مدیریت می‌شوند).</summary>
public class UpdateUserDto
{
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام کاربری الزامی است.")]
    [StringLength(64, MinimumLength = 3, ErrorMessage = "نام کاربری باید بین ۳ تا ۶۴ کاراکتر باشد.")]
    [RegularExpression(@"^\S+$", ErrorMessage = "نام کاربری نباید فاصله داشته باشد.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام کامل الزامی است.")]
    [StringLength(100, ErrorMessage = "نام کامل نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست.")]
    public string? PhoneNumber { get; set; }

    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
    public string? Email { get; set; }
}
