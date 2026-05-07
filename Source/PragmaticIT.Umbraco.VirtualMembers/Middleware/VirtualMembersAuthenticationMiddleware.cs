using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using System.Security.Claims;

namespace PragmaticIT.Umbraco.VirtualMembers.Middleware;

/// <summary>
/// Authenticates the VirtualMembers cookie scheme and merges the resulting
/// <see cref="ClaimsIdentity"/> into <see cref="HttpContext.User"/> so that
/// downstream code (views, controllers, other middleware) can read it without
/// knowing which scheme was used.
/// </summary>
internal sealed class VirtualMembersAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _scheme;

    public VirtualMembersAuthenticationMiddleware(
        RequestDelegate next,
        IOptions<VirtualMembersOptions> options)
    {
        _next = next;
        _scheme = options.Value.Auth.Scheme;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(_scheme);

        if (result.Succeeded && result.Principal is not null)
        {
            // Place the VirtualMembers identities first so that ClaimsPrincipal.Identity
            // (which returns Identities.FirstOrDefault()) resolves to the authenticated one,
            // making Context.User.Identity.IsAuthenticated == true.
            context.User = new ClaimsPrincipal(
                result.Principal.Identities.Concat(context.User.Identities));
        }

        await _next(context);
    }
}
