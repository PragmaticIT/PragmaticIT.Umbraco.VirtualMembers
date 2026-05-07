namespace PragmaticIT.Umbraco.VirtualMembers.Helpers;

internal static class VirtualMembersPathHelper
{
    /// <summary>
    /// Resolves a configured path against the application's content root.
    /// <list type="bullet">
    ///   <item><c>~/App_Data/MemberLists</c> → <c>{contentRoot}/App_Data/MemberLists</c></item>
    ///   <item><c>Member Lists</c>           → <c>{contentRoot}/Member Lists</c></item>
    ///   <item><c>C:\absolute\path</c>       → unchanged</item>
    /// </list>
    /// </summary>
    internal static string ResolvePath(string path, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return contentRootPath;

        if (path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith(@"~\", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, contentRootPath);
    }
}
