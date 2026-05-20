namespace PragmaticIT.Umbraco.VirtualMembers.Options;

/// <summary>Root configuration options for the VirtualMembers library, bound from <c>appsettings.json</c> section <c>"VirtualMembers"</c>.</summary>
public sealed class VirtualMembersOptions
{
    /// <summary>Configuration section name used when binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "VirtualMembers";

    /// <summary>Authentication behaviour (scheme, paths, mode, cookie lifetime).</summary>
    public AuthOptions Auth { get; set; } = new();

    /// <summary>Post-login and post-logout redirect URLs.</summary>
    public RedirectOptions Redirect { get; set; } = new();

    /// <summary>CSV member store settings (directory, cache, file watcher).</summary>
    public CsvOptions Csv { get; set; } = new();

    /// <summary>SMS delivery settings (sender name, OTP message template).</summary>
    public SmsOptions Sms { get; set; } = new();

    /// <summary>Authentication sub-options: scheme name, cookie, paths, mode and CAPTCHA toggle.</summary>
    public sealed class AuthOptions
    {
        /// <summary>Name of the cookie authentication scheme. Defaults to <see cref="VirtualMembersDefaults.Scheme"/>.</summary>
        public string Scheme { get; set; } = VirtualMembersDefaults.Scheme;

        /// <summary>Name of the authentication cookie issued after a successful login.</summary>
        public string CookieName { get; set; } = "VirtualMembers.Auth";

        /// <summary>Route of the login POST endpoint (handled internally by VirtualMembers).</summary>
        public string LoginPath { get; set; } = "/api/login";

        /// <summary>Route of the logout POST endpoint (handled internally by VirtualMembers).</summary>
        public string LogoutPath { get; set; } = "/api/logout";

        /// <summary>Umbraco content path that renders the login form (Razor Page or custom view).</summary>
        public string LoginViewPath { get; set; } = "/login";

        /// <summary>Lifetime of the authentication cookie in minutes. Defaults to 60.</summary>
        public int CookieLifetimeMinutes { get; set; } = 60;

        /// <summary>
        /// Authentication mode applied after the member submits their e-mail.
        /// <list type="bullet">
        ///   <item><see cref="AuthMode.None"/> – e-mail only, no additional verification (default / demo).</item>
        ///   <item><see cref="AuthMode.Otp"/> – a one-time code is sent to the member's e-mail.</item>
        ///   <item><see cref="AuthMode.Mfa"/> – an OTP to e-mail and a separate OTP via SMS.</item>
        /// </list>
        /// </summary>
        public AuthMode Mode { get; set; } = AuthMode.None;

        /// <summary>
        /// When <c>true</c>, the login form displays a simple bot-prevention challenge (e.g. CAPTCHA).
        /// </summary>
        public bool CaptchaEnabled { get; set; } = false;
    }

    /// <summary>Redirect URLs applied after login and logout.</summary>
    public sealed class RedirectOptions
    {
        /// <summary>URL to redirect to after a successful login. Defaults to <c>"/"</c>.</summary>
        public string PostLoginRedirectUrl { get; set; } = "/";

        /// <summary>URL to redirect to after logout. Defaults to <c>"/"</c>.</summary>
        public string PostLogoutRedirectUrl { get; set; } = "/";

        /// <summary>Returns <see cref="PostLoginRedirectUrl"/> falling back to <c>"/"</c> when blank.</summary>
        public string EffectivePostLoginUrl =>
            string.IsNullOrWhiteSpace(PostLoginRedirectUrl) ? "/" : PostLoginRedirectUrl;

        /// <summary>Returns <see cref="PostLogoutRedirectUrl"/> falling back to <c>"/"</c> when blank.</summary>
        public string EffectivePostLogoutUrl =>
            string.IsNullOrWhiteSpace(PostLogoutRedirectUrl) ? "/" : PostLogoutRedirectUrl;
    }

    /// <summary>CSV member store settings.</summary>
    public sealed class CsvOptions
    {
        /// <summary>Directory containing the member CSV files. Supports <c>|DataDirectory|</c> substitution (Umbraco sets this to <c>umbraco/Data</c>), <c>~/</c> relative to content root, and absolute paths.</summary>
        public string Directory { get; set; } = "|DataDirectory|/VirtualMembers";

        /// <summary>Glob pattern used to enumerate CSV files inside <see cref="Directory"/>.</summary>
        public string FilePattern { get; set; } = "*.csv";

        /// <summary>How long (in minutes) parsed member data is kept in memory before being reloaded.</summary>
        public int CacheMinutes { get; set; } = 5;

        /// <summary>When <c>true</c> a <see cref="System.IO.FileSystemWatcher"/> monitors the directory and invalidates the cache on changes.</summary>
        public bool Watch { get; set; } = true;

        /// <summary>File filter passed to the <see cref="System.IO.FileSystemWatcher"/>.</summary>
        public string WatchFilter { get; set; } = "*.csv";

        /// <summary>Encoding used when reading CSV files (e.g. <c>"utf-8"</c>, <c>"windows-1250"</c>).</summary>
        public string Encoding { get; set; } = "utf-8";
    }

    /// <summary>SMS delivery settings.</summary>
    public sealed class SmsOptions
    {
        /// <summary>Sender name displayed on the SMS message (if supported by the SMS provider).</summary>
        public string SenderName { get; set; } = "VirtualMembers";

        /// <summary>Message template for OTP delivery. Use <c>{0}</c> as the placeholder for the code.</summary>
        public string OtpMessageTemplate { get; set; } = "Your verification code is: {0}";
    }
}
