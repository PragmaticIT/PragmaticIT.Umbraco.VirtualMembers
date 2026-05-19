using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PragmaticIT.Umbraco.VirtualMembers.Helpers;
using PragmaticIT.Umbraco.VirtualMembers.Models;

namespace PragmaticIT.Umbraco.VirtualMembers.Providers;

/// <summary>
/// Composite <see cref="IVirtualMemberProvider"/> that fans out to all registered leaf providers,
/// merges their profiles, and forwards the first challenge it encounters.
/// </summary>
public sealed class VirtualMemberProviderAggregator : IVirtualMemberProvider
{
    private readonly IReadOnlyList<IVirtualMemberProvider> _providers;
    private readonly ILogger<VirtualMemberProviderAggregator> _logger;

    /// <summary>Initialises the aggregator with all keyed leaf providers.</summary>
    public VirtualMemberProviderAggregator(
        [FromKeyedServices(VirtualMembersProviderKeys.Leaf)] IEnumerable<IVirtualMemberProvider> providers,
        ILogger<VirtualMemberProviderAggregator> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        var normalizedContext = context with { Email = context.Email.NormalizeEmail() };
        var succeededProfiles = new List<VirtualMemberProfile>();

        foreach (var provider in _providers)
        {
            AuthenticationResult result;
            try
            {
                result = await provider.AuthenticateAsync(normalizedContext, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in IVirtualMemberProvider {ProviderType} for email {Email}",
                    provider.GetType().FullName, normalizedContext.Email);
                continue;
            }

            // Challenge ma priorytet – przerywamy i zwracamy od razu
            if (result is AuthenticationResult.ChallengeRequired)
                return result;

            if (result is AuthenticationResult.Succeeded succeeded)
                succeededProfiles.Add(succeeded.Profile);
        }

        if (succeededProfiles.Count == 0)
            return new AuthenticationResult.Failed("UserNotFound");

        // In the OTP challenge step context.Email is empty – the real e-mail
        // is recovered from the token by the leaf provider and stored in the profile.
        var resolvedEmail = string.IsNullOrWhiteSpace(normalizedContext.Email)
            ? succeededProfiles[0].Email
            : normalizedContext.Email;

        return new AuthenticationResult.Succeeded(
            MergeProfiles(resolvedEmail, succeededProfiles));
    }

    private static VirtualMemberProfile MergeProfiles(
        string email,
        IReadOnlyCollection<VirtualMemberProfile> profiles)
    {
        string? name = null;
        string? mobile = null;
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in profiles)
        {
            name ??= string.IsNullOrWhiteSpace(p.Name) ? null : p.Name;
            mobile ??= string.IsNullOrWhiteSpace(p.Mobile) ? null : p.Mobile;

            foreach (var g in p.Groups)
                if (!string.IsNullOrWhiteSpace(g))
                    groups.Add(g);
        }

        return new VirtualMemberProfile
        {
            Email = email,
            Name = name,
            Mobile = mobile,
            Groups = groups
        };
    }
}
