namespace PragmaticIT.Umbraco.VirtualMembers.Helpers;

internal static class VirtualMembersPathHelper
{
    /// <summary>
    /// Resolves a configured path against the application's content root.
    /// <list type="bullet">
    ///   <item><c>|DataDirectory|/VirtualMembers</c> → <c>{AppDomain DataDirectory}/VirtualMembers</c> (Umbraco sets this to <c>umbraco/Data</c>)</item>
    ///   <item><c>~/App_Data/MemberLists</c>         → <c>{contentRoot}/App_Data/MemberLists</c></item>
    ///   <item><c>Member Lists</c>                   → <c>{contentRoot}/Member Lists</c></item>
    ///   <item><c>C:\absolute\path</c>               → unchanged</item>
    /// </list>
    /// </summary>
    internal static string ResolvePath(string path, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return contentRootPath;

        // |DataDirectory| substitution — same convention as ADO.NET connection strings.
        // Umbraco sets AppDomain DataDirectory to {contentRoot}/umbraco/Data.
        const string dataDirectoryToken = "|DataDirectory|";
        if (path.StartsWith(dataDirectoryToken, StringComparison.OrdinalIgnoreCase))
        {
            var dataDir = AppDomain.CurrentDomain.GetData("DataDirectory") as string
                          ?? Path.Combine(contentRootPath, "umbraco", "Data");
            var rest = path[dataDirectoryToken.Length..].TrimStart('/', '\\');
            return Path.GetFullPath(Path.Combine(dataDir, rest));
        }

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
