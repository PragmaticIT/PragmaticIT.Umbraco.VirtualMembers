using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Helpers;
using PragmaticIT.Umbraco.VirtualMembers.Models;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using System.Text;

namespace PragmaticIT.Umbraco.VirtualMembers.Providers.Csv;

public sealed class CsvVirtualMemberStore : ICsvVirtualMemberStore
{
    private readonly VirtualMembersOptions _options;
    private readonly string _contentRootPath;
    private readonly ILogger<CsvVirtualMemberStore> _logger;
    private readonly object _syncRoot = new();
    private DateTimeOffset _lastLoadUtc = DateTimeOffset.MinValue;
    private IReadOnlyDictionary<string, VirtualMemberProfile> _cache =
        new Dictionary<string, VirtualMemberProfile>(StringComparer.OrdinalIgnoreCase);

    public CsvVirtualMemberStore(
        IOptions<VirtualMembersOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<CsvVirtualMemberStore> logger)
    {
        _options = options.Value;
        _contentRootPath = hostEnvironment.ContentRootPath;
        _logger = logger;
    }

    public Task<IReadOnlyDictionary<string, VirtualMemberProfile>> GetAllProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            if (_cache.Count == 0 || IsExpired(now))
            {
                Reload(now);
            }

            return Task.FromResult(_cache);
        }
    }

    public void Invalidate()
    {
        lock (_syncRoot)
        {
            _lastLoadUtc = DateTimeOffset.MinValue;
        }
    }

    private bool IsExpired(DateTimeOffset now)
    {
        var cacheMinutes = Math.Max(1, _options.Csv.CacheMinutes);
        return now - _lastLoadUtc > TimeSpan.FromMinutes(cacheMinutes);
    }

    private void Reload(DateTimeOffset now)
    {
        _logger.LogInformation("Reloading CSV virtual members from {Directory}", _options.Csv.Directory);

        var dir = VirtualMembersPathHelper.ResolvePath(_options.Csv.Directory, _contentRootPath);
        var dict = new Dictionary<string, VirtualMemberProfile>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("CSV directory {Directory} does not exist. No members loaded.", dir);
            _cache = dict;
            _lastLoadUtc = now;
            return;
        }

        var encoding = GetEncoding(_options.Csv.Encoding);

        foreach (var file in Directory.EnumerateFiles(dir, _options.Csv.FilePattern))
        {
            var groupName = Path.GetFileNameWithoutExtension(file);

            try
            {
                ParseCsvFile(file, groupName, encoding, dict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing CSV file {File}", file);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} virtual member profiles from CSV directory {Directory}",
            dict.Count, dir);

        _cache = dict;
        _lastLoadUtc = now;
    }

    private static void ParseCsvFile(
        string filePath,
        string groupName,
        Encoding encoding,
        Dictionary<string, VirtualMemberProfile> dict)
    {
        var lines = File.ReadAllLines(filePath, encoding);
        if (lines.Length == 0) return;

        // Parse header to find column indices
        var header = lines[0].Split(';');
        int nameIdx = FindColumnIndex(header, "Name");
        int emailIdx = FindColumnIndex(header, "Email");
        int mobileIdx = FindColumnIndex(header, "Mobile");

        if (emailIdx < 0) return; // No Email column – skip file

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var columns = line.Split(';');
            if (columns.Length <= emailIdx) continue;

            var rawEmail = columns[emailIdx].Trim();
            if (string.IsNullOrWhiteSpace(rawEmail)) continue;

            var email = rawEmail.ToLowerInvariant();
            var name = nameIdx >= 0 && columns.Length > nameIdx ? columns[nameIdx].Trim() : null;
            var mobile = mobileIdx >= 0 && columns.Length > mobileIdx ? columns[mobileIdx].Trim() : null;

            if (dict.TryGetValue(email, out var existing))
            {
                // Merge: add new group, keep first non-empty name/mobile
                var mergedGroups = existing.Groups
                    .Append(groupName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                dict[email] = new VirtualMemberProfile
                {
                    Email = email,
                    Name = string.IsNullOrWhiteSpace(existing.Name) ? name : existing.Name,
                    Mobile = string.IsNullOrWhiteSpace(existing.Mobile) ? mobile : existing.Mobile,
                    Groups = mergedGroups
                };
            }
            else
            {
                dict[email] = new VirtualMemberProfile
                {
                    Email = email,
                    Name = string.IsNullOrWhiteSpace(name) ? null : name,
                    Mobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile,
                    Groups = [groupName]
                };
            }
        }
    }

    private static int FindColumnIndex(string[] header, string columnName)
    {
        for (int i = 0; i < header.Length; i++)
        {
            if (string.Equals(header[i].Trim(), columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static Encoding GetEncoding(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
