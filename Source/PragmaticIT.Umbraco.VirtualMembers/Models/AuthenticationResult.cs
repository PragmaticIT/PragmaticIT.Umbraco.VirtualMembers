namespace PragmaticIT.Umbraco.VirtualMembers.Models;

/// <summary>
/// Wynik pojedynczego kroku uwierzytelnienia.
/// Trzy możliwe stany: sukces, żądanie kolejnego kroku, porażka.
/// </summary>
public abstract class AuthenticationResult
{
    private AuthenticationResult() { }

    /// <summary>Uwierzytelnienie zakończone – profil gotowy do użycia.</summary>
    public sealed class Succeeded(VirtualMemberProfile profile) : AuthenticationResult
    {
        public VirtualMemberProfile Profile { get; } = profile;
    }

    /// <summary>
    /// Provider potrzebuje więcej danych (np. kodu OTP).
    /// <see cref="ChallengeType"/> informuje UI, jakiego pola oczekiwać.
    /// <see cref="ChallengeToken"/> należy odesłać w kolejnym żądaniu.
    /// </summary>
    public sealed class ChallengeRequired(string challengeType, string challengeToken) : AuthenticationResult
    {
        public string ChallengeType { get; } = challengeType;
        public string ChallengeToken { get; } = challengeToken;
    }

    /// <summary>Uwierzytelnienie odrzucone.</summary>
    public sealed class Failed(string reason) : AuthenticationResult
    {
        public string Reason { get; } = reason;
    }
}
