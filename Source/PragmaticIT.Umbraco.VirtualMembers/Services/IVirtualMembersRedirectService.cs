using System.Security.Claims;

namespace PragmaticIT.Umbraco.VirtualMembers.Services;

public interface IVirtualMembersRedirectService
{
    /// <summary>
    /// Attempts to find a top-level Umbraco content node that is accessible
    /// by at least one of the authenticated member's groups.
    /// Returns the URL of the first matching node, or <c>null</c> when none is found.
    /// </summary>
    Task<string?> GetPreferredContentUrlForUserAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
