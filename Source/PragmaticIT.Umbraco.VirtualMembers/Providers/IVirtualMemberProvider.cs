using PragmaticIT.Umbraco.VirtualMembers.Models;

namespace PragmaticIT.Umbraco.VirtualMembers.Providers;

public interface IVirtualMemberProvider
{
    /// <summary>
    /// Wykonuje jeden krok uwierzytelnienia na podstawie dostarczonego kontekstu.
    /// Może zwrócić sukces, żądanie kolejnego kroku (challenge) lub porażkę.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationContext context,
        CancellationToken cancellationToken = default);
}
