using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PragmaticIT.Umbraco.VirtualMembers.Services;

/// <summary>
/// No-op implementation used when no real SMS provider is registered.
/// Logs a warning instead of sending a message – safe for demo and development environments.
/// </summary>
internal sealed class NullSmsService : IVirtualMemberSmsService
{
    private readonly ILogger<NullSmsService> _logger;

    public NullSmsService(ILogger<NullSmsService> logger) => _logger = logger;

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "VirtualMembers: SMS to {PhoneNumber} was not sent – no IVirtualMemberSmsService implementation registered. " +
            "Register a real provider to enable SMS delivery.",
            phoneNumber);

        Debug.WriteLine($"[NullSmsService] SMS to {phoneNumber}: {message}");

        return Task.CompletedTask;
    }
}
