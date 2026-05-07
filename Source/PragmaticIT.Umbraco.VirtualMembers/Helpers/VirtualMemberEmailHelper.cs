namespace PragmaticIT.Umbraco.VirtualMembers.Helpers;

internal static class VirtualMemberEmailHelper
{
    internal static string NormalizeEmail(this string email)
        => email.Trim().ToLowerInvariant();
}
