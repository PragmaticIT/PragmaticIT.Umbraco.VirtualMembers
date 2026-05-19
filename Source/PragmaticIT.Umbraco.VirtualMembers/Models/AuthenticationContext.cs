namespace PragmaticIT.Umbraco.VirtualMembers.Models;

/// <summary>
/// Carries all data a provider needs to make a decision at a given authentication step.
/// On the first call only <see cref="Email"/> is populated.
/// On subsequent steps (OTP, MFA) <see cref="ChallengeToken"/> and <see cref="Factors"/> are also present.
/// </summary>
public sealed record AuthenticationContext
{
    /// <summary>E-mail address submitted by the member. May be empty on challenge steps when it is encoded inside the token.</summary>
    public required string Email { get; init; }

    /// <summary>Opaque token issued by the provider in the previous step.</summary>
    public string? ChallengeToken { get; init; }

    /// <summary>
    /// Member's responses to the challenge, keyed by factor type (e.g. <c>"otp-email"</c> → <c>"123456"</c>).
    /// Keys are the challenge types declared by <see cref="AuthenticationResult.ChallengeRequired"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Factors { get; init; }
        = new Dictionary<string, string>();
}
