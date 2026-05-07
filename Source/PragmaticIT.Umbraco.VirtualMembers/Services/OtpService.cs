using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace PragmaticIT.Umbraco.VirtualMembers.Services;

internal sealed class OtpService : IOtpService
{
    private const string CacheKeyPrefix    = "vm:otp:";
    private const string SmsCacheKeyPrefix = "vm:otp-sms:";
    private static readonly TimeSpan CodeExpiry = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly IEmailSender _emailSender;
    private readonly IVirtualMemberSmsService _smsService;
    private readonly string _fromAddress;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IMemoryCache cache,
        IEmailSender emailSender,
        IVirtualMemberSmsService smsService,
        IOptions<GlobalSettings> globalSettings,
        ILogger<OtpService> logger)
    {
        _cache = cache;
        _emailSender = emailSender;
        _smsService = smsService;
        _fromAddress = globalSettings.Value.Smtp?.From ?? string.Empty;
        _logger = logger;
    }

    public async Task<string> IssueAsync(string email, CancellationToken cancellationToken = default)
    {
        var code = Random.Shared.Next(0, 1_000_000).ToString("D6");
        var token = Guid.NewGuid().ToString("N");

        _cache.Set(CacheKeyPrefix + token, (email, code), CodeExpiry);

        try
        {
            var message = new EmailMessage(
                _fromAddress,
                email,
                "Your sign-in code",
                $"Your one-time sign-in code is: <strong>{code}</strong><br>This code expires in 10 minutes.",
                isBodyHtml: true);

            await _emailSender.SendAsync(message, "VirtualMembers");
            _logger.LogInformation("OTP sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP e-mail to {Email}", email);
        }

        return token;
    }

    public Task<string?> ValidateAndConsumeAsync(string token, string code, CancellationToken cancellationToken = default)
    {
        var key = CacheKeyPrefix + token;

        if (!_cache.TryGetValue(key, out (string email, string storedCode) entry))
            return Task.FromResult<string?>(null);

        if (!entry.storedCode.Equals(code.Trim(), StringComparison.Ordinal))
            return Task.FromResult<string?>(null);

        _cache.Remove(key);
        return Task.FromResult<string?>(entry.email);
    }

    public async Task IssueSmsAsync(string phoneNumber, string parentToken, CancellationToken cancellationToken = default)
    {
        var code = Random.Shared.Next(0, 1_000_000).ToString("D6");
        _cache.Set(SmsCacheKeyPrefix + parentToken, code, CodeExpiry);

        try
        {
            await _smsService.SendAsync(phoneNumber, $"Your sign-in code: {code}", cancellationToken);
            _logger.LogInformation("SMS OTP sent to {PhoneNumber}", phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS OTP to {PhoneNumber}", phoneNumber);
        }
    }

    public Task<bool> ValidateAndConsumeSmsAsync(string parentToken, string code, CancellationToken cancellationToken = default)
    {
        var key = SmsCacheKeyPrefix + parentToken;

        if (!_cache.TryGetValue(key, out string? storedCode))
            return Task.FromResult(false);

        if (!storedCode!.Equals(code.Trim(), StringComparison.Ordinal))
            return Task.FromResult(false);

        _cache.Remove(key);
        return Task.FromResult(true);
    }
}
