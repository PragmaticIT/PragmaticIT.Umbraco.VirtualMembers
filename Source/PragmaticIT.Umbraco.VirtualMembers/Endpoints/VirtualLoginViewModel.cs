using System.ComponentModel.DataAnnotations;

namespace PragmaticIT.Umbraco.VirtualMembers.Endpoints;

public sealed class VirtualLoginViewModel
{
    // Not [Required] – in the challenge step (Step 2) email is absent from the form;
    // it is carried inside the opaque ChallengeToken and recovered by the provider.
    [EmailAddress]
    public string? Email { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>Token wydany przez provider w poprzednim kroku (OTP, 2FA).</summary>
    public string? ChallengeToken { get; set; }

    /// <summary>Odpowiedzi na challenge indeksowane typem (np. "otp-email", "otp-sms").</summary>
    public Dictionary<string, string> Factors { get; set; } = [];

    public bool HasValidReturnUrl =>
        !string.IsNullOrWhiteSpace(ReturnUrl) &&
        Uri.IsWellFormedUriString(ReturnUrl, UriKind.Relative) &&
        ReturnUrl.StartsWith('/');
}
