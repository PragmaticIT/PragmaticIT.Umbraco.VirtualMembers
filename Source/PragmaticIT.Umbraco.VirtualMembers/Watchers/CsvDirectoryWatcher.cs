using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Helpers;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using PragmaticIT.Umbraco.VirtualMembers.Providers.Csv;
using Umbraco.Cms.Core.Services;
using UmbracoModels = Umbraco.Cms.Core.Models;

namespace PragmaticIT.Umbraco.VirtualMembers.Watchers;

/// <summary>
/// Hosted background service that watches the configured CSV directory for file changes
/// and invalidates the <see cref="ICsvVirtualMemberStore"/> cache when a change is detected.
/// </summary>
public sealed class CsvDirectoryWatcher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VirtualMembersOptions _options;
    private readonly string _contentRootPath;
    private readonly ILogger<CsvDirectoryWatcher> _logger;
    private FileSystemWatcher? _watcher;

    /// <summary>Initialises a new instance with all required dependencies.</summary>
    public CsvDirectoryWatcher(
        IServiceProvider serviceProvider,
        IOptions<VirtualMembersOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<CsvDirectoryWatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _contentRootPath = hostEnvironment.ContentRootPath;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Csv.Watch)
        {
            _logger.LogInformation("CSV directory watching is disabled.");
            return Task.CompletedTask;
        }

        var dir = VirtualMembersPathHelper.ResolvePath(_options.Csv.Directory, _contentRootPath);
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning(
                "CSV directory {Directory} does not exist. Watcher will not start.", dir);
            return Task.CompletedTask;
        }

        _watcher = new FileSystemWatcher(dir, _options.Csv.WatchFilter)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Deleted += OnDeleted;

        _logger.LogInformation("CSV directory watcher started for {Directory}", dir);

        return Task.CompletedTask;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => HandleFileEvent(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e)
        => HandleFileEvent(e.FullPath);

    private void OnDeleted(object sender, FileSystemEventArgs e)
        => InvalidateStore();

    private void HandleFileEvent(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            InvalidateStore();
            return;
        }

        var groupName = Path.GetFileNameWithoutExtension(fullPath);
        _ = EnsureGroupExistsAsync(groupName);
        InvalidateStore();
    }

    private void InvalidateStore()
    {
        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICsvVirtualMemberStore>();
        store.Invalidate();
    }

    private async Task EnsureGroupExistsAsync(string groupName)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var memberGroupService = scope.ServiceProvider.GetRequiredService<IMemberGroupService>();

            var existing = await memberGroupService.GetByNameAsync(groupName);
            if (existing is null)
            {
                var group = new UmbracoModels.MemberGroup { Name = groupName };
                await memberGroupService.CreateAsync(group);
                _logger.LogInformation(
                    "Created Umbraco member group '{GroupName}' from CSV file.", groupName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to ensure Umbraco member group '{GroupName}' exists.", groupName);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_watcher is not null)
        {
            _watcher.Created -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Deleted -= OnDeleted;
            _watcher.Dispose();
        }
    }
}
