using PragmaticIT.Umbraco.VirtualMembers.Models;

namespace PragmaticIT.Umbraco.VirtualMembers.Providers.Csv;

public interface ICsvVirtualMemberStore
{
    /// <summary>
    /// Zwraca mapę: email -> profil (wraz z grupami) zcache'owaną na podstawie plików CSV.
    /// </summary>
    Task<IReadOnlyDictionary<string, VirtualMemberProfile>> GetAllProfilesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invaliduje cache – powinno być wywołane przez watcher.
    /// </summary>
    void Invalidate();
}
