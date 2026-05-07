using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Endpoints;
using PragmaticIT.Umbraco.VirtualMembers.Middleware;
using PragmaticIT.Umbraco.VirtualMembers.Providers;
using PragmaticIT.Umbraco.VirtualMembers.Services;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using PragmaticIT.Umbraco.VirtualMembers.Helpers;
using PragmaticIT.Umbraco.VirtualMembers.Providers;
using PragmaticIT.Umbraco.VirtualMembers.Providers.Csv;
using PragmaticIT.Umbraco.VirtualMembers.Security;
using PragmaticIT.Umbraco.VirtualMembers.Services;
using PragmaticIT.Umbraco.VirtualMembers.Watchers;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace PragmaticIT.Umbraco.VirtualMembers.Extensions;

public static class VirtualMembersUmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddVirtualMembers(this IUmbracoBuilder builder)
    {
        var services = builder.Services;

        // Options
        services
            .AddOptions<VirtualMembersOptions>()
            .Bind(builder.Config.GetSection(VirtualMembersOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // CSV store + provider (leaf – keyed so the aggregator can resolve them without circular dependency)
        services.AddSingleton<ICsvVirtualMemberStore, CsvVirtualMemberStore>();
        services.AddKeyedSingleton<IVirtualMemberProvider, CsvVirtualMemberProvider>(VirtualMembersProviderKeys.Leaf);

        // Aggregator registered as the default IVirtualMemberProvider
        services.AddSingleton<IVirtualMemberProvider, VirtualMemberProviderAggregator>();

        // OTP service
        services.AddSingleton<IOtpService, OtpService>();

        // Sign-in + redirect services
        services.AddScoped<IVirtualMemberSignInService, VirtualMemberSignInService>();
        services.AddScoped<IVirtualMembersRedirectService, VirtualMembersRedirectService>();

        // SMS – no-op fallback; replace with a real provider before or after AddVirtualMembers():
        //   services.AddSingleton<IVirtualMemberSmsService, YourSmsService>();
        services.TryAddSingleton<IVirtualMemberSmsService, NullSmsService>();

        // Directory watcher
        services.AddHostedService<CsvDirectoryWatcher>();

        // Login view helper
        services.AddScoped<IVirtualMembersLoginViewHelper, VirtualMembersLoginViewHelper>();

        // Public access checker – manual decorator pattern.
        // Capture Umbraco's existing IPublicAccessChecker registration, remove it,
        // then re-add it wrapped inside our VirtualMembersPublicAccessChecker.
        var existingChecker = services.LastOrDefault(d => d.ServiceType == typeof(IPublicAccessChecker));
        if (existingChecker is not null)
            services.Remove(existingChecker);

        services.Add(new ServiceDescriptor(
            typeof(IPublicAccessChecker),
            sp =>
            {
                IPublicAccessChecker inner = existingChecker switch
                {
                    { ImplementationInstance: not null } =>
                        (IPublicAccessChecker)existingChecker.ImplementationInstance,
                    { ImplementationFactory: not null } =>
                        (IPublicAccessChecker)existingChecker.ImplementationFactory(sp),
                    { ImplementationType: not null } =>
                        (IPublicAccessChecker)ActivatorUtilities.CreateInstance(
                            sp, existingChecker.ImplementationType),
                    _ => throw new InvalidOperationException(
                        "Cannot resolve original IPublicAccessChecker implementation.")
                };

                return new VirtualMembersPublicAccessChecker(
                    sp.GetRequiredService<IHttpContextAccessor>(),
                    sp.GetRequiredService<IPublicAccessService>(),
                    sp.GetRequiredService<IPublishedContentCache>(),
                    inner,
                    sp.GetRequiredService<ILogger<VirtualMembersPublicAccessChecker>>());
            },
            existingChecker?.Lifetime ?? ServiceLifetime.Scoped));

        // Cookie authentication scheme
        services
            .AddAuthentication()
            .AddCookie(VirtualMembersDefaults.Scheme, _ => { });

        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>,
            VirtualMembersCookieOptionsConfigurator>();

        // Endpoint registration via Umbraco pipeline filter
        services.Configure<UmbracoPipelineOptions>(pipelineOptions =>
        {
            pipelineOptions.AddFilter(new UmbracoPipelineFilter(VirtualMembersDefaults.PipelineFilterName)
            {
                PrePipeline = appBuilder => appBuilder.UseMiddleware<VirtualMembersAuthenticationMiddleware>(),
                Endpoints = appBuilder => appBuilder.UseEndpoints(
                    endpoints => endpoints.MapVirtualMembersEndpoints())
            });
        });

        return builder;
    }
}

/// <summary>
/// Configures CookieAuthenticationOptions for the VirtualMembers scheme
/// using the resolved <see cref="VirtualMembersOptions"/> after DI is built.
/// </summary>
internal sealed class VirtualMembersCookieOptionsConfigurator : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly IOptions<VirtualMembersOptions> _vmOptions;

    public VirtualMembersCookieOptionsConfigurator(IOptions<VirtualMembersOptions> vmOptions)
        => _vmOptions = vmOptions;

    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name != VirtualMembersDefaults.Scheme) return;

        var vm = _vmOptions.Value;
        options.Cookie.Name = vm.Auth.CookieName;
        options.LoginPath = vm.Auth.LoginPath;
        options.LogoutPath = vm.Auth.LogoutPath;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(vm.Auth.CookieLifetimeMinutes);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    }
}
