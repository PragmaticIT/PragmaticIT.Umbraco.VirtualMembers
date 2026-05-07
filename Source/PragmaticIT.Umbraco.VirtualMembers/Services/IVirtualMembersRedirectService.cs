using System.Security.Claims;

namespace PragmaticIT.Umbraco.VirtualMembers.Services;

public interface IVirtualMembersRedirectService
{
    /// <summary>
    /// Na podstawie zalogowanego użytkownika próbuje odnaleźć
    /// top-level node z dostępem dla którejkolwiek jego grup.
    /// Zwraca URL lub null.
    /// </summary>
    Task<string?> GetPreferredContentUrlForUserAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
