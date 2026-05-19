using PragmaticIT.Umbraco.VirtualMembers.Models;

namespace PragmaticIT.Umbraco.VirtualMembers.Providers.Csv;

public interface ICsvVirtualMemberStore
{
    /// <summary>
    /// Returns a map of normalised e-mail → member profile (including groups), cached from the CSV files.
    /// </summary>
    Task<IReadOnlyDictionary<string, VirtualMemberProfile>> GetAllProfilesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the in-memory cache. Should be called by the file watcher when a CSV file changes.
    /// </summary>
    void Invalidate();
}
