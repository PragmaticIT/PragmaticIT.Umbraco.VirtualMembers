using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Models;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using PragmaticIT.Umbraco.VirtualMembers.Services;

namespace PragmaticIT.Umbraco.VirtualMembers.Endpoints;

public static class VirtualMembersEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapVirtualMembersEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<VirtualMembersOptions>>().Value;

        endpoints.MapPost(options.Auth.LoginPath, async (
            HttpContext httpContext,
            IVirtualMemberSignInService signInService,
            IVirtualMembersRedirectService redirectService,
            [FromForm] VirtualLoginViewModel model,
            CancellationToken cancellationToken) =>
        {
            // In the challenge step (Step 2) the email field is absent from the form –
            // it is recovered from the opaque ChallengeToken inside the provider.
            if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.ChallengeToken))
                return Results.BadRequest("Email is required.");

            var context = new AuthenticationContext
            {
                Email = model.Email ?? string.Empty,
                ChallengeToken = model.ChallengeToken,
                Factors = model.Factors
            };

            var result = await signInService.AuthenticateAsync(httpContext, context, cancellationToken);

            return result switch
            {
                AuthenticationResult.Succeeded =>
                    await ResolvePostLoginRedirectAsync(model, redirectService, httpContext, cancellationToken, options),

                AuthenticationResult.ChallengeRequired challenge =>
                    Results.Redirect(BuildChallengeRedirectUrl(options.Auth.LoginViewPath, context.Email, challenge, model.ReturnUrl)),

                AuthenticationResult.Failed =>
                    Results.Redirect(BuildFailedRedirectUrl(options.Auth.LoginViewPath, model.ReturnUrl)),

                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        })
        .WithDisplayName("VirtualMembers Login")
        .DisableAntiforgery()
        .AllowAnonymous();

        endpoints.MapPost(options.Auth.LogoutPath, async (
            HttpContext httpContext,
            IVirtualMemberSignInService signInService) =>
        {
            await signInService.SignOutAsync(httpContext);

            return Results.Redirect(options.Redirect.EffectivePostLogoutUrl);
        })
        .WithDisplayName("VirtualMembers Logout")
        .AllowAnonymous();

        return endpoints;
    }

    private static string BuildFailedRedirectUrl(string loginViewPath, string? returnUrl)
    {
        var pairs = new List<KeyValuePair<string, string?>>
        {
            KeyValuePair.Create<string, string?>("error", "unauthorized")
        };

        if (!string.IsNullOrWhiteSpace(returnUrl))
            pairs.Add(KeyValuePair.Create<string, string?>("returnUrl", returnUrl));

        return loginViewPath + QueryString.Create(pairs);
    }

    private static string BuildChallengeRedirectUrl(
        string loginViewPath,
        string email,
        AuthenticationResult.ChallengeRequired challenge,
        string? returnUrl)
    {
        // E-mail is intentionally omitted from the query string –
        // it is carried inside the opaque ChallengeToken issued by the provider.
        var query = QueryString.Create([
            KeyValuePair.Create("challengeType",  challenge.ChallengeType),
            KeyValuePair.Create("challengeToken", challenge.ChallengeToken),
            .. string.IsNullOrWhiteSpace(returnUrl)
                ? Array.Empty<KeyValuePair<string, string?>>()
                : [KeyValuePair.Create<string, string?>("returnUrl", returnUrl)]
        ]);

        return loginViewPath + query;
    }

    private static async Task<IResult> ResolvePostLoginRedirectAsync(
        VirtualLoginViewModel model,
        IVirtualMembersRedirectService redirectService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        VirtualMembersOptions options)
    {
        if (model.HasValidReturnUrl)
            return Results.Redirect(model.ReturnUrl!);

        var preferredUrl = await redirectService.GetPreferredContentUrlForUserAsync(
            httpContext.User, cancellationToken);

        if (!string.IsNullOrWhiteSpace(preferredUrl))
            return Results.Redirect(preferredUrl);

        return Results.Redirect(options.Redirect.EffectivePostLoginUrl);
    }
}
