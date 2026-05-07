using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Models;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using PragmaticIT.Umbraco.VirtualMembers.Providers;
using System.Security.Claims;

namespace PragmaticIT.Umbraco.VirtualMembers.Services;

public sealed class VirtualMemberSignInService : IVirtualMemberSignInService
{
    private readonly IVirtualMemberProvider _provider;
    private readonly IAuthenticationService _authenticationService;
    private readonly VirtualMembersOptions _options;
    private readonly ILogger<VirtualMemberSignInService> _logger;

    public VirtualMemberSignInService(
        IVirtualMemberProvider provider,
        IAuthenticationService authenticationService,
        IOptions<VirtualMembersOptions> options,
        ILogger<VirtualMemberSignInService> logger)
    {
        _provider = provider;
        _authenticationService = authenticationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        HttpContext httpContext,
        AuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        AuthenticationResult result;
        try
        {
            result = await _provider.AuthenticateAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VirtualMembers authentication exception for {Email} from {IpAddress}",
                context.Email, ipAddress);

            LogAudit("LoginFailure", context.Email, null, ipAddress, userAgent, "Exception");
            return new AuthenticationResult.Failed("Exception");
        }

        switch (result)
        {
            case AuthenticationResult.Succeeded success:
                await IssueSessionCookieAsync(httpContext, success.Profile);
                _logger.LogInformation(
                    "VirtualMembers login success for {Email} from {IpAddress} with groups {Groups}",
                    context.Email, ipAddress, string.Join(",", success.Profile.Groups));
                LogAudit("LoginSuccess", context.Email, success.Profile.Groups, ipAddress, userAgent, null);
                break;

            case AuthenticationResult.ChallengeRequired challenge:
                _logger.LogInformation(
                    "VirtualMembers challenge required ({ChallengeType}) for {Email} from {IpAddress}",
                    challenge.ChallengeType, context.Email, ipAddress);
                LogAudit("ChallengeRequired", context.Email, null, ipAddress, userAgent, challenge.ChallengeType);
                break;

            case AuthenticationResult.Failed failed:
                _logger.LogInformation(
                    "VirtualMembers login failure ({Reason}) for {Email} from {IpAddress}",
                    failed.Reason, context.Email, ipAddress);
                LogAudit("LoginFailure", context.Email, null, ipAddress, userAgent, failed.Reason);
                break;
        }

        return result;
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        var email = httpContext.User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        await _authenticationService.SignOutAsync(httpContext, _options.Auth.Scheme, null);

        _logger.LogInformation(
            "VirtualMembers logout for {Email} from {IpAddress}", email, ipAddress);

        LogAudit("Logout", email, null, ipAddress, userAgent, null);
    }

    private async Task IssueSessionCookieAsync(HttpContext httpContext, VirtualMemberProfile profile)
    {
        var claims = BuildClaims(profile);
        var identity = new ClaimsIdentity(claims, _options.Auth.Scheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(_options.Auth.CookieLifetimeMinutes)
        };

        await _authenticationService.SignInAsync(
            httpContext, _options.Auth.Scheme, principal, authProperties);
    }

    private static IEnumerable<Claim> BuildClaims(VirtualMemberProfile profile)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, profile.Email);
        yield return new Claim(ClaimTypes.Email, profile.Email);

        if (!string.IsNullOrWhiteSpace(profile.Name))
            yield return new Claim(ClaimTypes.Name, profile.Name);

        if (!string.IsNullOrWhiteSpace(profile.Mobile))
            yield return new Claim("virtualmembers:mobile", profile.Mobile);

        foreach (var group in profile.Groups)
            if (!string.IsNullOrWhiteSpace(group))
                yield return new Claim(ClaimTypes.Role, group);
    }

    private void LogAudit(
        string eventType,
        string email,
        IEnumerable<string>? groups,
        string ipAddress,
        string userAgent,
        string? reason)
    {
        _logger.LogInformation(
            "VirtualMembers audit event {EventType} Email={Email} Groups={Groups} Ip={Ip} UserAgent={UserAgent} Reason={Reason}",
            eventType,
            email,
            groups is null ? string.Empty : string.Join(",", groups),
            ipAddress,
            userAgent,
            reason ?? string.Empty);
    }
}
