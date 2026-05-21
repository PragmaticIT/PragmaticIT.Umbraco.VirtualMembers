# PragmaticIT.Umbraco.VirtualMembers

> **Protect Umbraco pages without Umbraco Members.**  
> Authenticate users from CSV files using passwordless login, OTP or MFA — zero member records in the database.

[![NuGet](https://img.shields.io/nuget/v/PragmaticIT.Umbraco.VirtualMembers)](https://www.nuget.org/packages/PragmaticIT.Umbraco.VirtualMembers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/blob/main/LICENSE)
[![CI](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/actions/workflows/ci.yml/badge.svg)](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/actions/workflows/ci.yml)

---

## Why Virtual Members?

Standard Umbraco authentication requires member records in the database, back-office setup, and password management. Virtual Members removes all of that — drop a CSV file, configure one `appsettings.json` section, and your protected pages are ready.

---

## Features

- 🔑 **Passwordless login** — email-only, OTP (email code), or MFA (email + SMS code)
- 📄 **CSV member lists** — drop files into `App_Data/VirtualMembers`, no database needed
- 🔄 **Hot-reload** — file-system watcher invalidates the in-memory cache automatically
- 🛡️ **Umbraco Public Access integration** — works with standard protected pages and member groups
- 🔌 **Pluggable providers** — implement `IVirtualMemberProvider` to source members from any backend
- 📱 **SMS abstraction** — plug in Twilio, Azure Communication Services, or any provider via `IVirtualMemberSmsService`
- ⚙️ **Zero-friction setup** — one NuGet package, one `appsettings.json` section, no `Program.cs` changes

---

## Requirements

| | Version |
|---|---|
| .NET | ≥ 9.0 |
| Umbraco CMS | ≥ 15.0 |

---

## Quick start

### 1. Install

```
dotnet add package PragmaticIT.Umbraco.VirtualMembers
```

No changes needed in `Program.cs` — the package registers itself via an Umbraco `IComposer`.

### 2. Configure `appsettings.json`

```json
{
  "VirtualMembers": {
    "Auth": {
      "Mode": "None",
      "LoginViewPath": "/login"
    },
    "Csv": {
      "Directory": "|DataDirectory|/VirtualMembers"
    }
  }
}
```

### 3. Add a CSV member list

Create `umbraco/Data/VirtualMembers/Org-A.csv` — the file name becomes the member group name:

```
Name;Email;Mobile
Alice Smith;alice@example.com;
Bob Jones;bob@example.com;+48111222333
```

### 4. Add view templates

Create three standard Umbraco content views wired to content nodes in the back-office.

**Login view** — handles both step 1 (email) and step 2 (OTP/MFA code):

```razor
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using PragmaticIT.Umbraco.VirtualMembers.Helpers
@using PragmaticIT.Umbraco.VirtualMembers.Options
@inject IVirtualMembersLoginViewHelper LoginViewHelper
@{
    Layout = null;
    var lc = LoginViewHelper.GetLoginContext();
}

@if (!string.IsNullOrEmpty(lc.ErrorCode))
{
    <p>Login failed. Please try again.</p>
}

<form method="post" action="@lc.Auth.LoginPath">
    <input type="hidden" name="ReturnUrl" value="@lc.ReturnUrl" />

    @if (!lc.IsChallenge)
    {
        <label>Email address</label>
        <input type="email" name="Email" required />
        <button type="submit">Continue</button>
    }
    else
    {
        <input type="hidden" name="ChallengeToken" value="@lc.ChallengeToken" />

        <label>Verification code (email)</label>
        <input type="text" name="Factors[otp-email]" required maxlength="6" />

        @if (lc.Auth.Mode == AuthMode.Mfa)
        {
            <label>Verification code (SMS)</label>
            <input type="text" name="Factors[otp-sms]" required maxlength="6" />
        }

        <button type="submit">Sign in</button>
    }
</form>
```

**Logout view** — confirmation page shown after sign-out (the actual sign-out happens at the POST endpoint):

```razor
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@{
    Layout = null;
}

<h1>Signed out</h1>
<p>Your session has ended.</p>
<a href="/login">Sign in again</a>
```

To add a logout button anywhere on the site:

```html
<form method="post" action="/api/logout">
    <button type="submit">Sign out</button>
</form>
```

**Access Denied view** — shown when an authenticated user lacks access to a protected node (403). Configure it as the "No access" page in Public Access settings. No special injection required — a simple message is enough.

### 5. Protect content in the back-office

In the Umbraco back-office go to **Info → Public Access** on any content node and set:
- **Login page** → your `/login` node
- **Allowed groups** → `Org-A` (matches the CSV file name)

That's it — users on the CSV list can now log in, everyone else is redirected to the login page.

---

## Full documentation

- 📖 [Usage guide](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/blob/main/docs/USAGE.md) — step-by-step setup, all configuration options, view templates, authentication modes
- 🏗️ [Architecture & internals](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/blob/main/docs/IMPLEMENTATION.md) — authentication pipeline, provider model, extension points

---

## License

[MIT](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/blob/main/LICENSE)
