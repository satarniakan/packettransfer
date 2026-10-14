namespace packettransfer.Application.DTOs;

/// <summary>ورودی‌های JSON مسیرهای <c>/api/v1</c> (کلاینت‌های غیرمرورگری): ورود با نام کاربری/ایمیل و رمز + صدور توکن.</summary>
public record PasswordLoginApiDto(string Username, string Password, string? DeviceName = null);

/// <summary>پاسخ صدور توکن — متن خام فقط همین‌بار دیده می‌شود.</summary>
public record TokenResponseDto(string Token, DateTime ExpiresAtUtc);

public record ApiTokenDto(
    int Id,
    string? DeviceName,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime ExpiresAtUtc);
