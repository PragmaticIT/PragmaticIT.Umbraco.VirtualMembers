using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PragmaticIT.Umbraco.VirtualMembers.Options;
using PragmaticIT.Umbraco.VirtualMembers.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace PragmaticIT.Umbraco.VirtualMembers.Diagnostics;

internal sealed class VirtualMembersStartupNotificationHandler
    : INotificationHandler<UmbracoApplicationStartingNotification>
{
    private const string DocsUrl =
        "https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/blob/main/docs/USAGE.md#2-add-configuration";

    private readonly IConfiguration _configuration;
    private readonly IOptions<VirtualMembersOptions> _options;
    private readonly IVirtualMemberSmsService _smsService;
    private readonly ILogger<VirtualMembersStartupNotificationHandler> _logger;

    public VirtualMembersStartupNotificationHandler(
        IConfiguration configuration,
        IOptions<VirtualMembersOptions> options,
        IVirtualMemberSmsService smsService,
        ILogger<VirtualMembersStartupNotificationHandler> logger)
    {
        _configuration = configuration;
        _options = options;
        _smsService = smsService;
        _logger = logger;
    }

    public void Handle(UmbracoApplicationStartingNotification notification)
    {
        CheckConfiguration();
        CheckCsvDirectory();
        CheckSmtp();
        CheckSms();
    }

    private void CheckConfiguration()
    {
        if (_configuration.GetSection(VirtualMembersOptions.SectionName).Exists())
            return;

        _logger.LogWarning(
            "VirtualMembers: No '{Section}' section found in appsettings. " +
            "Running with default values. To customise the behaviour add the section to your appsettings.json. " +
            "See: {DocsUrl}",
            VirtualMembersOptions.SectionName,
            DocsUrl);
    }

    private void CheckCsvDirectory()
    {
        var csv = _options.Value.Csv;

        if (string.IsNullOrWhiteSpace(csv.Directory))
        {
            _logger.LogWarning(
                "VirtualMembers: 'VirtualMembers:Csv:Directory' is not configured. " +
                "No CSV members will be available. See: {DocsUrl}",
                DocsUrl);
            return;
        }

        var resolved = Path.IsPathRooted(csv.Directory)
            ? csv.Directory
            : Path.GetFullPath(csv.Directory);

        if (!Directory.Exists(resolved))
        {
            _logger.LogWarning(
                "VirtualMembers: CSV directory '{Directory}' does not exist. " +
                "No members will be available until the directory is created and populated. See: {DocsUrl}",
                resolved,
                DocsUrl);
            return;
        }

        if (!Directory.EnumerateFiles(resolved, csv.FilePattern, SearchOption.TopDirectoryOnly).Any())
        {
            _logger.LogWarning(
                "VirtualMembers: CSV directory '{Directory}' exists but contains no files matching '{Pattern}'. " +
                "No members will be available until matching files are added. See: {DocsUrl}",
                resolved,
                csv.FilePattern,
                DocsUrl);
        }
    }

    private void CheckSmtp()
    {
        var mode = _options.Value.Auth.Mode;
        if (mode == AuthMode.None)
            return;

        var smtpHost = _configuration["Umbraco:CMS:Global:Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
            return;

        _logger.LogWarning(
            "VirtualMembers: Auth:Mode is set to '{Mode}' which requires e-mail delivery, " +
            "but no SMTP host is configured under 'Umbraco:CMS:Global:Smtp:Host'. " +
            "OTP codes will not be sent until SMTP is configured. See: {DocsUrl}",
            mode,
            DocsUrl);
    }

    private void CheckSms()
    {
        if (_options.Value.Auth.Mode != AuthMode.Mfa)
            return;

        if (_smsService is not NullSmsService)
            return;

        _logger.LogWarning(
            "VirtualMembers: Auth:Mode is set to 'Mfa' but no real IVirtualMemberSmsService implementation " +
            "is registered. SMS codes will not be sent. Register a provider before or after AddVirtualMembers(). " +
            "See: {DocsUrl}",
            DocsUrl);
    }
}
