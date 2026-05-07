namespace PragmaticIT.Umbraco.VirtualMembers.Services;

/// <summary>
/// Abstrakcja do wysyłki SMS niezależna od dostawcy.
/// Domyślna implementacja to <see cref="NullSmsService"/> (loguje ostrzeżenie, nie wysyła).
/// Aby podpiąć rzeczywistego dostawcę (Twilio, Azure Communication Services itp.),
/// zarejestruj własną implementację w DI:
/// <code>
/// services.AddSingleton&lt;IVirtualMemberSmsService, TwilioSmsService&gt;();
/// </code>
/// </summary>
public interface IVirtualMemberSmsService
{
    /// <summary>
    /// Wysyła wiadomość SMS pod wskazany numer telefonu.
    /// </summary>
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
