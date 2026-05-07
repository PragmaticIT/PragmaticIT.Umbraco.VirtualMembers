using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PragmaticIT.Umbraco.VirtualMembers.Services;

/// <summary>
/// Implementacja no-op używana gdy żaden rzeczywisty dostawca SMS nie jest zarejestrowany.
/// Loguje ostrzeżenie zamiast wysyłać wiadomość – bezpieczna w środowisku demo/dev.
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
