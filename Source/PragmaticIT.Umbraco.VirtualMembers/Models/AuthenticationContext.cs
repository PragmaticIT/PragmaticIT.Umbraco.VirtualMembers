namespace PragmaticIT.Umbraco.VirtualMembers.Models;

/// <summary>
/// Niesie wszystko, co provider potrzebuje do podjęcia decyzji w danym kroku.
/// Przy pierwszym wywołaniu wypełniony jest tylko Email.
/// Przy kolejnych krokach (OTP, 2FA) obecne są ChallengeToken i Factors.
/// </summary>
public sealed record AuthenticationContext
{
    public required string Email { get; init; }

    /// <summary>Nieprzezroczysty token wydany przez provider w poprzednim kroku.</summary>
    public string? ChallengeToken { get; init; }

    /// <summary>
    /// Odpowiedzi użytkownika na challenge (np. "otp-email" → "123456").
    /// Kluczami są typy zadeklarowane przez <see cref="AuthenticationResult.ChallengeRequired"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Factors { get; init; }
        = new Dictionary<string, string>();
}
