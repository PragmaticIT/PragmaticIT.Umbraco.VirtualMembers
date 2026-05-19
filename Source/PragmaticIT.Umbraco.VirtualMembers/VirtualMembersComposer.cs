using PragmaticIT.Umbraco.VirtualMembers.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace PragmaticIT.Umbraco.VirtualMembers;

/// <summary>
/// Umbraco composer that registers all VirtualMembers services into the DI container.
/// Automatically discovered by Umbraco – no manual registration required.
/// </summary>
public sealed class VirtualMembersComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
        => builder.AddVirtualMembers();
}
