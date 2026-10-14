using Microsoft.Extensions.Logging;
using packettransfer.Domain.Interfaces;

namespace packettransfer.Infrastructure.Services;

/// <summary>
/// جایگزین موقت سرویس پیامک تا وقتی ارائه‌دهنده‌ی واقعی وصل نشده است.
/// متن پیام ممکن است محتوای حساس (مثل اطلاع‌رسانی) داشته باشد؛ پس فقط در
/// Development داخل لاگ نوشته می‌شود.
/// </summary>
public class FakeSmsSender : ISmsSender
{
    private readonly ILogger<FakeSmsSender> _logger;
    private readonly bool _logMessageBody;

    public FakeSmsSender(ILogger<FakeSmsSender> logger, bool logMessageBody)
    {
        _logger = logger;
        _logMessageBody = logMessageBody;
    }

    public Task SendAsync(string phoneNumber, string message)
    {
        if (_logMessageBody)
        {
            _logger.LogWarning("=== SMS SIMULATION === To: {PhoneNumber} | Message: {Message}", phoneNumber, message);
        }
        else
        {
            _logger.LogWarning("SMS to {PhoneNumber} was NOT sent: no SMS provider is configured.", phoneNumber);
        }

        return Task.CompletedTask;
    }
}
