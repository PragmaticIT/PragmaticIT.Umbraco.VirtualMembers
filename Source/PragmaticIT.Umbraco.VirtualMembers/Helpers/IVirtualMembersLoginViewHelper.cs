namespace PragmaticIT.Umbraco.VirtualMembers.Helpers;

/// <summary>
/// Builds <see cref="VirtualMembersLoginViewContext"/> from the current HTTP request
/// and configuration, keeping login views free of options infrastructure.
/// </summary>
public interface IVirtualMembersLoginViewHelper
{
    VirtualMembersLoginViewContext GetLoginContext();
}
