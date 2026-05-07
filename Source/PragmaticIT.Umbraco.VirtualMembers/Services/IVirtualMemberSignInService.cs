using Microsoft.AspNetCore.Http;
using PragmaticIT.Umbraco.VirtualMembers.Models;

namespace PragmaticIT.Umbraco.VirtualMembers.Services;

public interface IVirtualMemberSignInService
{
    /// <summary>
    /// Wykonuje krok uwierzytelnienia. Jeśli zakończy się sukcesem, wystawia cookie.
    /// Zwraca <see cref="AuthenticationResult"/> pozwalający endpointowi podjąć dalszą decyzję.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(
        HttpContext httpContext,
        AuthenticationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Wylogowuje użytkownika (usuwa cookie).</summary>
    Task SignOutAsync(HttpContext httpContext);
}
