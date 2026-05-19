using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Helpers;
using PragmaticIT.Umbraco.VirtualMembers.Models;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using PragmaticIT.Umbraco.VirtualMembers.Services;

namespace PragmaticIT.Umbraco.VirtualMembers.Providers.Csv;

/// <summary>
/// <see cref="IVirtualMemberProvider"/> implementation that authenticates members
/// against profiles loaded from CSV files via <see cref="ICsvVirtualMemberStore"/>.
/// Supports <see cref="AuthMode.None"/>, <see cref="AuthMode.Otp"/>, and <see cref="AuthMode.Mfa"/>.
/// </summary>
public sealed class CsvVirtualMemberProvider : IVirtualMemberProvider
{
    private const string OtpEmailFactor = "otp-email";
    private const string OtpSmsFactor   = "otp-sms";

    private readonly ICsvVirtualMemberStore _store;
    private readonly IOtpService _otpService;
    private readonly VirtualMembersOptions _options;
    private readonly ILogger<CsvVirtualMemberProvider> _logger;

    /// <summary>Initialises a new instance with all required dependencies.</summary>
    public CsvVirtualMemberProvider(
        ICsvVirtualMemberStore store,
        IOtpService otpService,
        IOptions<VirtualMembersOptions> options,
        ILogger<CsvVirtualMemberProvider> logger)
    {
        _store = store;
        _otpService = otpService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        // Step 2: challenge token present – validate OTP
        if (!string.IsNullOrEmpty(context.ChallengeToken))
            return await ValidateChallengeAsync(context, cancellationToken);

        // Step 1: first factor – look up user by e-mail
        var normalizedEmail = context.Email.NormalizeEmail();
        var allProfiles = await _store.GetAllProfilesAsync(cancellationToken);

        if (!allProfiles.TryGetValue(normalizedEmail, out var profile))
            return new AuthenticationResult.Failed("UserNotFound");

        if (_options.Auth.Mode == AuthMode.Otp || _options.Auth.Mode == AuthMode.Mfa)
        {
            var token = await _otpService.IssueAsync(normalizedEmail, cancellationToken);

            if (_options.Auth.Mode == AuthMode.Mfa && !string.IsNullOrWhiteSpace(profile.Mobile))
                await _otpService.IssueSmsAsync(profile.Mobile, token, cancellationToken);

            return new AuthenticationResult.ChallengeRequired(OtpEmailFactor, token);
        }

        return new AuthenticationResult.Succeeded(profile);
    }

    private async Task<AuthenticationResult> ValidateChallengeAsync(
        AuthenticationContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Factors.TryGetValue(OtpEmailFactor, out var submittedCode))
            return new AuthenticationResult.Failed("MissingOtpCode");

        var email = await _otpService.ValidateAndConsumeAsync(
            context.ChallengeToken!, submittedCode, cancellationToken);

        if (email is null)
            return new AuthenticationResult.Failed("InvalidOtpCode");

        if (_options.Auth.Mode == AuthMode.Mfa)
        {
            if (!context.Factors.TryGetValue(OtpSmsFactor, out var submittedSmsCode))
                return new AuthenticationResult.Failed("MissingSmsCode");

            var smsValid = await _otpService.ValidateAndConsumeSmsAsync(
                context.ChallengeToken!, submittedSmsCode, cancellationToken);

            if (!smsValid)
                return new AuthenticationResult.Failed("InvalidSmsCode");
        }

        var allProfiles = await _store.GetAllProfilesAsync(cancellationToken);

        if (!allProfiles.TryGetValue(email, out var profile))
            return new AuthenticationResult.Failed("UserNotFound");

        return new AuthenticationResult.Succeeded(profile);
    }
}
