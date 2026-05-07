namespace PragmaticIT.Umbraco.VirtualMembers.Options;

public enum AuthMode
{
	/// <summary>Tylko e-mail, bez dodatkowej weryfikacji. Tryb domyślny / demo.</summary>
	None,

	/// <summary>Jednorazowy kod (OTP) wysyłany na adres e-mail.</summary>
	Otp,

	/// <summary>Kod na e-mail oraz osobny kod na SMS – oba wymagane.</summary>
	Mfa
}