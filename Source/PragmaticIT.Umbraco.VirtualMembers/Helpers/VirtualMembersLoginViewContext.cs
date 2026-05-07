using PragmaticIT.Umbraco.VirtualMembers.Options;

namespace PragmaticIT.Umbraco.VirtualMembers.Helpers;

/// <summary>
/// Pre-computed login-page state derived from the current HTTP request and configuration.
/// Produced by <see cref="IVirtualMembersLoginViewHelper"/> and consumed by the login view.
/// </summary>
public sealed record VirtualMembersLoginViewContext
{
    /// <summary>Auth sub-options (LoginPath, LoginViewPath, Mode, CaptchaEnabled, …).</summary>
    public required VirtualMembersOptions.AuthOptions Auth { get; init; }

    /// <summary>
    /// The URL to redirect back to after a successful login.
    /// Empty when there is no meaningful return destination.
    /// </summary>
    public required string ReturnUrl { get; init; }

    /// <summary>Raw error code from the query string (e.g. <c>"unauthorized"</c>). Null when absent.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>True when the request carries a <c>challengeToken</c> query parameter.</summary>
    public required bool IsChallenge { get; init; }

    /// <summary>e.g. <c>"otp-email"</c> or <c>"otp-sms"</c>.</summary>
    public required string ChallengeType { get; init; }

    /// <summary>Opaque token forwarded to the login endpoint on the challenge step.</summary>
    public required string ChallengeToken { get; init; }
}
