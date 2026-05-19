namespace PragmaticIT.Umbraco.VirtualMembers.Models;

/// <summary>Immutable profile of a virtual member loaded from a provider (e.g. CSV).</summary>
public sealed class VirtualMemberProfile
{
    /// <summary>Normalised (lower-case, trimmed) e-mail address that uniquely identifies the member.</summary>
    public string Email { get; init; } = default!;

    /// <summary>Display name of the member, or <c>null</c> when not available.</summary>
    public string? Name { get; init; }

    /// <summary>Mobile phone number used for SMS OTP delivery, or <c>null</c> when not available.</summary>
    public string? Mobile { get; init; }

    /// <summary>
    /// Nazwy grup, do których użytkownik należy.
    /// </summary>
    public IReadOnlyCollection<string> Groups { get; init; } = [];
}
