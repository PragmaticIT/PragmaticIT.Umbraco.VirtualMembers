using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PragmaticIT.Umbraco.VirtualMembers.Models;
using PragmaticIT.Umbraco.VirtualMembers.Providers;

namespace PragmaticIT.Umbraco.VirtualMembers.Tests.Providers;

public sealed class VirtualMemberProviderAggregatorTests
{
    private static VirtualMemberProviderAggregator BuildAggregator(params IVirtualMemberProvider[] providers) =>
        new(providers, NullLogger<VirtualMemberProviderAggregator>.Instance);

    private static IVirtualMemberProvider ProviderReturning(AuthenticationResult result)
    {
        var p = Substitute.For<IVirtualMemberProvider>();
        p.AuthenticateAsync(Arg.Any<AuthenticationContext>(), Arg.Any<CancellationToken>())
         .Returns(result);
        return p;
    }

    // ── Single provider ───────────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_SingleProviderSucceeds_ReturnsSucceeded()
    {
        var profile = new VirtualMemberProfile { Email = "user@example.com", Groups = ["editors"] };
        var aggregator = BuildAggregator(ProviderReturning(new AuthenticationResult.Succeeded(profile)));

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        var success = Assert.IsType<AuthenticationResult.Succeeded>(result);
        Assert.Equal("user@example.com", success.Profile.Email);
        Assert.Contains("editors", success.Profile.Groups);
    }

    [Fact]
    public async Task AuthenticateAsync_SingleProviderFails_ReturnsUserNotFound()
    {
        var aggregator = BuildAggregator(ProviderReturning(new AuthenticationResult.Failed("UserNotFound")));

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        var failed = Assert.IsType<AuthenticationResult.Failed>(result);
        Assert.Equal("UserNotFound", failed.Reason);
    }

    [Fact]
    public async Task AuthenticateAsync_ChallengeRequired_ReturnedImmediately()
    {
        var challenge = new AuthenticationResult.ChallengeRequired("otp-email", "tok-1");
        var first = ProviderReturning(challenge);
        var second = Substitute.For<IVirtualMemberProvider>();
        var aggregator = BuildAggregator(first, second);

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        Assert.IsType<AuthenticationResult.ChallengeRequired>(result);
        await second.DidNotReceive().AuthenticateAsync(Arg.Any<AuthenticationContext>(), Arg.Any<CancellationToken>());
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_ProviderThrows_ContinuesToNextProvider()
    {
        var failing = Substitute.For<IVirtualMemberProvider>();
        failing.AuthenticateAsync(Arg.Any<AuthenticationContext>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new InvalidOperationException("boom"));

        var profile = new VirtualMemberProfile { Email = "user@example.com", Groups = [] };
        var succeeding = ProviderReturning(new AuthenticationResult.Succeeded(profile));

        var aggregator = BuildAggregator(failing, succeeding);

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        Assert.IsType<AuthenticationResult.Succeeded>(result);
    }

    [Fact]
    public async Task AuthenticateAsync_AllProvidersThrow_ReturnsUserNotFound()
    {
        var failing = Substitute.For<IVirtualMemberProvider>();
        failing.AuthenticateAsync(Arg.Any<AuthenticationContext>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new InvalidOperationException("boom"));

        var aggregator = BuildAggregator(failing);

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        var failed = Assert.IsType<AuthenticationResult.Failed>(result);
        Assert.Equal("UserNotFound", failed.Reason);
    }

    // ── Profile merging ───────────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_TwoProviders_GroupsMerged()
    {
        var p1 = new VirtualMemberProfile { Email = "user@example.com", Groups = ["editors"] };
        var p2 = new VirtualMemberProfile { Email = "user@example.com", Groups = ["members"] };
        var aggregator = BuildAggregator(
            ProviderReturning(new AuthenticationResult.Succeeded(p1)),
            ProviderReturning(new AuthenticationResult.Succeeded(p2)));

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        var success = Assert.IsType<AuthenticationResult.Succeeded>(result);
        Assert.Contains("editors", success.Profile.Groups);
        Assert.Contains("members", success.Profile.Groups);
    }

    [Fact]
    public async Task AuthenticateAsync_TwoProviders_DuplicateGroupsDeduped()
    {
        var p1 = new VirtualMemberProfile { Email = "user@example.com", Groups = ["editors"] };
        var p2 = new VirtualMemberProfile { Email = "user@example.com", Groups = ["editors", "members"] };
        var aggregator = BuildAggregator(
            ProviderReturning(new AuthenticationResult.Succeeded(p1)),
            ProviderReturning(new AuthenticationResult.Succeeded(p2)));

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        var success = Assert.IsType<AuthenticationResult.Succeeded>(result);
        Assert.Equal(2, success.Profile.Groups.Count);
    }

    [Fact]
    public async Task AuthenticateAsync_TwoProviders_FirstNameWins()
    {
        var p1 = new VirtualMemberProfile { Email = "user@example.com", Name = "Alice", Groups = [] };
        var p2 = new VirtualMemberProfile { Email = "user@example.com", Name = "Bob", Groups = [] };
        var aggregator = BuildAggregator(
            ProviderReturning(new AuthenticationResult.Succeeded(p1)),
            ProviderReturning(new AuthenticationResult.Succeeded(p2)));

        var result = await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "user@example.com" });

        var success = Assert.IsType<AuthenticationResult.Succeeded>(result);
        Assert.Equal("Alice", success.Profile.Name);
    }

    // ── Email normalisation ───────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_EmailNormalizedBeforePassingToProviders()
    {
        AuthenticationContext? captured = null;
        var provider = Substitute.For<IVirtualMemberProvider>();
        provider.AuthenticateAsync(Arg.Do<AuthenticationContext>(c => captured = c), Arg.Any<CancellationToken>())
                .Returns(new AuthenticationResult.Failed("UserNotFound"));

        var aggregator = BuildAggregator(provider);
        await aggregator.AuthenticateAsync(new AuthenticationContext { Email = "  USER@EXAMPLE.COM  " });

        Assert.NotNull(captured);
        Assert.Equal("user@example.com", captured!.Email);
    }
}
