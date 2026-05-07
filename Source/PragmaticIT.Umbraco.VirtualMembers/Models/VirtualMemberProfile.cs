namespace PragmaticIT.Umbraco.VirtualMembers.Models;

public sealed class VirtualMemberProfile
{
    public string Email { get; init; } = default!;
    public string? Name { get; init; }
    public string? Mobile { get; init; }

    /// <summary>
    /// Nazwy grup, do których użytkownik należy.
    /// </summary>
    public IReadOnlyCollection<string> Groups { get; init; } = [];
}
