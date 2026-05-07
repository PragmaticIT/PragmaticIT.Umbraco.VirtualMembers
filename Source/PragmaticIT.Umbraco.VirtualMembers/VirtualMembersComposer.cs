using PragmaticIT.Umbraco.VirtualMembers.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace PragmaticIT.Umbraco.VirtualMembers;

public sealed class VirtualMembersComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddVirtualMembers();
}
