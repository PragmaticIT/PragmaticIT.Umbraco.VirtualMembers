using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace PragmaticIT.Umbraco.VirtualMembers.Security;

/// <summary>
/// Replaces Umbraco's default IPublicAccessChecker.
/// If the request carries a VirtualMembers cookie, access is evaluated against
/// ClaimsPrincipal roles using IPublicAccessService – no member DB record required.
/// All other requests are delegated to the original Umbraco implementation.
/// </summary>
public sealed class VirtualMembersPublicAccessChecker : IPublicAccessChecker
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPublicAccessService _publicAccessService;
    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IPublicAccessChecker _inner;
    private readonly ILogger<VirtualMembersPublicAccessChecker> _logger;

    public VirtualMembersPublicAccessChecker(
        IHttpContextAccessor httpContextAccessor,
        IPublicAccessService publicAccessService,
        IPublishedContentCache publishedContentCache,
        IPublicAccessChecker inner,
        ILogger<VirtualMembersPublicAccessChecker> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _publicAccessService = publicAccessService;
        _publishedContentCache = publishedContentCache;
        _inner = inner;
        _logger = logger;
    }

    public async Task<PublicAccessStatus> HasMemberAccessToContentAsync(int publishedContentId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogInformation("VirtualMembers checker: no HttpContext for content {Id} → delegating to inner", publishedContentId);
            return await _inner.HasMemberAccessToContentAsync(publishedContentId);
        }

        // Only intercept when the request carries a valid VirtualMembers cookie.
        var result = await httpContext.AuthenticateAsync(VirtualMembersDefaults.Scheme);

        _logger.LogInformation("VirtualMembers checker: content {Id}, cookie auth succeeded={Succeeded}",
            publishedContentId, result.Succeeded);

        if (!result.Succeeded)
            return await _inner.HasMemberAccessToContentAsync(publishedContentId);

        return await EvaluateAccessAsync(publishedContentId, result.Principal!);
    }

    public async Task<PublicAccessStatus> HasMemberAccessToContentAsync(
        int publishedContentId,
        ClaimsPrincipal claimsPrincipal)
    {
        // HttpContext.User already contains the merged VirtualMembers identity
        // (set by VirtualMembersAuthenticationMiddleware). Use it directly.
        // If there is no email claim the request is not a VirtualMembers session
        // – delegate to the original Umbraco checker.
        var email = claimsPrincipal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return await _inner.HasMemberAccessToContentAsync(publishedContentId);

        return await EvaluateAccessAsync(publishedContentId, claimsPrincipal);
    }

    private async Task<PublicAccessStatus> EvaluateAccessAsync(
        int publishedContentId,
        ClaimsPrincipal claimsPrincipal)
    {
        // IPublicAccessService.HasAccessAsync expects the Umbraco content Path
        // property (e.g. "-1,1050,1060"), NOT the request URL.
        var content = await _publishedContentCache.GetByIdAsync(publishedContentId);
        if (content is null)
        {
            _logger.LogInformation("VirtualMembers access check: content {Id} not found → AccessAccepted", publishedContentId);
            return PublicAccessStatus.AccessAccepted;
        }

        var email = claimsPrincipal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("VirtualMembers access check: no email claim on principal (authType={AuthType}) → AccessDenied",
                claimsPrincipal.Identity?.AuthenticationType);
            return PublicAccessStatus.AccessDenied;
        }

        var userRoles = claimsPrincipal
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "VirtualMembers access check: contentId={Id} path={Path} email={Email} roles=[{Roles}]",
            publishedContentId, content.Path, email, string.Join(", ", userRoles));

        var hasAccess = await _publicAccessService.HasAccessAsync(
            content.Path,
            email,
            () => Task.FromResult<IEnumerable<string>>(userRoles));

        _logger.LogInformation(
            "VirtualMembers access check: HasAccessAsync returned {Result} for email={Email} path={Path}",
            hasAccess, email, content.Path);

        return hasAccess
            ? PublicAccessStatus.AccessAccepted
            : PublicAccessStatus.AccessDenied;
    }
}
