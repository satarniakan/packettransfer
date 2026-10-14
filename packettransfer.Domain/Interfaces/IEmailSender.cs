// packettransfer.Domain/Interfaces/IEmailSender.cs
namespace packettransfer.Domain.Interfaces;

/// <summary>ارسال ایمیل تراکنشی — پیاده‌سازی SMTP در Infrastructure</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}
