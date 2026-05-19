namespace PragmaticIT.Umbraco.VirtualMembers.Options;

public enum AuthMode
{
	/// <summary>E-mail only, no additional verification. Default / demo mode.</summary>
	None,

	/// <summary>A one-time code (OTP) is sent to the member's e-mail address.</summary>
	Otp,

	/// <summary>An OTP sent to e-mail and a separate OTP sent via SMS – both are required.</summary>
	Mfa
}