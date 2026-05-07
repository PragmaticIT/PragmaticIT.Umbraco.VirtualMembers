using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Options;

namespace PragmaticIT.Umbraco.VirtualMembers.Helpers;

internal sealed class VirtualMembersLoginViewHelper(
    IHttpContextAccessor httpContextAccessor,
    IOptions<VirtualMembersOptions> options) : IVirtualMembersLoginViewHelper
{
    public VirtualMembersLoginViewContext GetLoginContext()
    {
        var auth    = options.Value.Auth;
        var request = httpContextAccessor.HttpContext!.Request;

        // When Umbraco rewrites a protected page to the login view the URL stays on the
        // protected page (e.g. /org-a) – that path is the natural returnUrl.
        // When the user navigates to the login page directly the path matches LoginViewPath,
        // so we fall back to the explicit query-string parameter (or empty).
        var isRewrite = !request.Path.StartsWithSegments(
            new PathString(auth.LoginViewPath),
            StringComparison.OrdinalIgnoreCase);

        var returnUrl = isRewrite
            ? (request.Path + request.QueryString).ToString()
            : request.Query["returnUrl"].ToString();

        var errorCode      = request.Query["error"].ToString();
        var challengeType  = request.Query["challengeType"].ToString();
        var challengeToken = request.Query["challengeToken"].ToString();

        return new VirtualMembersLoginViewContext
        {
            Auth           = auth,
            ReturnUrl      = returnUrl,
            ErrorCode      = string.IsNullOrEmpty(errorCode) ? null : errorCode,
            IsChallenge    = !string.IsNullOrEmpty(challengeToken),
            ChallengeType  = challengeType,
            ChallengeToken = challengeToken,
        };
    }
}
