using System.ComponentModel.DataAnnotations;

namespace packettransfer.Application.DTOs;

public class CreateUserDto
{
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

    /// <summary>
    /// رمز عبور کاربر — الزامی است چون ورود فقط با نام کاربری و رمز انجام می‌شود.
    /// سیاست رمز Identity (حداقل ۶ کاراکتر و…) روی همان اعمال می‌شود.
    /// </summary>
    [Required(ErrorMessage = "رمز عبور الزامی است.")]
    public string? Password { get; set; }

    [MinLength(1, ErrorMessage = "انتخاب حداقل یک نقش الزامی است.")]
    public List<string> RoleNames { get; set; } = new();
}
