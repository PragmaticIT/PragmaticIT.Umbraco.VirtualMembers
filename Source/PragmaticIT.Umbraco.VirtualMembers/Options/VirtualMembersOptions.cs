namespace PragmaticIT.Umbraco.VirtualMembers.Options;

public sealed class VirtualMembersOptions
{
    public const string SectionName = "VirtualMembers";

    public AuthOptions Auth { get; set; } = new();
    public RedirectOptions Redirect { get; set; } = new();
    public CsvOptions Csv { get; set; } = new();
    public SmsOptions Sms { get; set; } = new();

    public sealed class AuthOptions
    {
        public string Scheme { get; set; } = VirtualMembersDefaults.Scheme;
        public string CookieName { get; set; } = "VirtualMembers.Auth";
        public string LoginPath { get; set; } = "/api/login";
        public string LogoutPath { get; set; } = "/api/logout";
        public string LoginViewPath { get; set; } = "/login";
        public int CookieLifetimeMinutes { get; set; } = 60;

        /// <summary>
        /// Tryb uwierzytelnienia po podaniu e-maila.
        /// <list type="bullet">
        ///   <item><see cref="AuthMode.None"/> – tylko e-mail, bez dodatkowej weryfikacji (domyślny / demo).</item>
        ///   <item><see cref="AuthMode.Otp"/> – jednorazowy kod wysyłany na e-mail.</item>
        ///   <item><see cref="AuthMode.Mfa"/> – kod na e-mail i osobny kod na SMS.</item>
        /// </list>
        /// </summary>
        public AuthMode Mode { get; set; } = AuthMode.None;

        /// <summary>
        /// Gdy true, formularz logowania wyświetla proste zabezpieczenie przed automatyzacją (np. CAPTCHA).
        /// </summary>
        public bool CaptchaEnabled { get; set; } = false;
    }

    public sealed class RedirectOptions
    {
        public string PostLoginRedirectUrl { get; set; } = "/";
        public string PostLogoutRedirectUrl { get; set; } = "/";

        public string EffectivePostLoginUrl =>
            string.IsNullOrWhiteSpace(PostLoginRedirectUrl) ? "/" : PostLoginRedirectUrl;

        public string EffectivePostLogoutUrl =>
            string.IsNullOrWhiteSpace(PostLogoutRedirectUrl) ? "/" : PostLogoutRedirectUrl;
    }

    public sealed class CsvOptions
    {
        public string Directory { get; set; } = "App_Data/VirtualMembers";
        public string FilePattern { get; set; } = "*.csv";
        public int CacheMinutes { get; set; } = 5;
        public bool Watch { get; set; } = true;
        public string WatchFilter { get; set; } = "*.csv";
        public string Encoding { get; set; } = "utf-8";
    }

    public sealed class SmsOptions
    {
        /// <summary>
        /// Nazwa nadawcy wyświetlana jako nadawca SMS (jeśli dostawca to wspiera).
        /// </summary>
        public string SenderName { get; set; } = "VirtualMembers";

        /// <summary>
        /// Szablon treści wiadomości. Użyj <c>{0}</c> jako placeholder dla kodu OTP.
        /// </summary>
        public string OtpMessageTemplate { get; set; } = "Your verification code is: {0}";
    }
}
