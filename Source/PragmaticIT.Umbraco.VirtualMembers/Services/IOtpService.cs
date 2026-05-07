namespace PragmaticIT.Umbraco.VirtualMembers.Services;

public interface IOtpService
{
    /// <summary>
    /// Generates a 6-digit OTP, stores it in cache, sends it by e-mail, and returns
    /// an opaque token that identifies the pending challenge.
    /// </summary>
    Task<string> IssueAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates <paramref name="code"/> against the pending challenge identified by
    /// <paramref name="token"/>. Consumes (removes) the entry on success.
    /// </summary>
    /// <returns>The e-mail address bound to the token when valid; <c>null</c> otherwise.</returns>
    Task<string?> ValidateAndConsumeAsync(string token, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a 6-digit SMS OTP, stores it in cache under a key derived from
    /// <paramref name="parentToken"/>, and sends it via <see cref="IVirtualMemberSmsService"/>.
    /// </summary>
    Task IssueSmsAsync(string phoneNumber, string parentToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates <paramref name="code"/> against the pending SMS challenge linked to
    /// <paramref name="parentToken"/>. Consumes the entry on success.
    /// </summary>
    Task<bool> ValidateAndConsumeSmsAsync(string parentToken, string code, CancellationToken cancellationToken = default);
}
