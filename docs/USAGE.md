# Usage guide

This guide walks you through everything needed to add Virtual Members authentication to an existing Umbraco 17 site.

---

## Table of contents

1. [Install the NuGet package](#1-install-the-nuget-package)
2. [Add configuration](#2-add-configuration)
3. [Add member list CSV files](#3-add-member-list-csv-files)
4. [Add view templates](#4-add-view-templates)
   - [Login view](#41-login-view)
   - [Logout view](#42-logout-view)
   - [Access Denied view](#43-access-denied-view)
5. [Protect content in the back-office](#5-protect-content-in-the-back-office)
6. [Authentication modes](#6-authentication-modes)
7. [Plugging in a real SMS provider](#7-plugging-in-a-real-sms-provider)
8. [Example project](#8-example-project)

---

## 1. Install the NuGet package

```
dotnet add package PragmaticIT.Umbraco.VirtualMembers
```

Or via the Visual Studio NuGet Package Manager — search for `PragmaticIT.Umbraco.VirtualMembers`.

No code changes are required in `Program.cs` or `Startup.cs`. The package registers itself through an Umbraco `IComposer` that is discovered automatically.

---

## 2. Add configuration

Add a `VirtualMembers` section to `appsettings.json`. All values below are the defaults — you only need to include keys you want to change.

```json
{
  "VirtualMembers": {
    "Auth": {
      "LoginPath": "/api/login",
      "LogoutPath": "/api/logout",
      "LoginViewPath": "/login",
      "CookieLifetimeMinutes": 60,
      "Mode": "None",
      "CaptchaEnabled": false
    },
    "Redirect": {
      "PostLoginRedirectUrl": "/",
      "PostLogoutRedirectUrl": "/"
    },
    "Csv": {
      "Directory": "App_Data/VirtualMembers",
      "FilePattern": "*.csv",
      "CacheMinutes": 5,
      "Watch": true
    }
  }
}
```

### Key settings explained

| Key | Description |
|---|---|
| `Auth.LoginPath` | URL of the **POST endpoint** that processes the login form. Must match the `action` attribute of your login form. |
| `Auth.LogoutPath` | URL of the **POST endpoint** that processes logout. Must match the `action` attribute of your logout form. |
| `Auth.LoginViewPath` | URL of the **Razor view** that renders the login page. Umbraco will rewrite protected page requests to this URL. |
| `Auth.Mode` | `None` (email only) · `Otp` (email + one-time code) · `Mfa` (email code + SMS code). See [section 6](#6-authentication-modes). |
| `Csv.Directory` | Path to the folder containing member list CSV files. Relative paths are resolved from the application content root. |
| `Redirect.PostLoginRedirectUrl` | Where to send the user after a successful login when no `returnUrl` is present. |

---

## 3. Add member list CSV files

Create the directory configured in `Csv.Directory` (default `App_Data/VirtualMembers`) and add one or more `.csv` files.

**The file name (without extension) becomes the member group name.**

```
App_Data/
└── VirtualMembers/
    ├── Org-A.csv
    └── Org-B.csv
```

### CSV format

The file must use semicolons as the column separator and include a header row. Column order does not matter, but the column names are fixed.

```
Name;Email;Mobile
Alice Smith;alice@acompany.com;+48111222333
Bob Jones;bob@acompany.com;
Charlie;charlie@acompany.com;+48999888777
```

| Column | Required | Notes |
|---|---|---|
| `Email` | **Yes** | Used as the unique identifier. Case-insensitive. |
| `Name` | No | Display name stored as a `ClaimTypes.Name` claim. |
| `Mobile` | No | Required for MFA mode (SMS code). Include country code. |

> **Tip:** Each user can appear in multiple files and will belong to multiple groups.

The in-memory cache is refreshed automatically when:
- The TTL expires (`Csv.CacheMinutes`).
- A CSV file is created, modified, renamed, or deleted (when `Csv.Watch = true`).

---

## 4. Add view templates

Virtual Members requires **three Razor view templates** in your Umbraco project. These are standard Umbraco content views — they must be wired to the corresponding Umbraco document types and content nodes (see [section 5](#5-protect-content-in-the-back-office)).

All three views inherit from `Umbraco.Cms.Web.Common.Views.UmbracoViewPage` and are rendered by the standard Umbraco routing pipeline (`RenderController`).

---

### 4.1 Login view

**What it does:** Renders the login form. Umbraco rewrites all requests to protected pages to this view when the user is not authenticated. It also handles the OTP/MFA challenge step.

**Umbraco document type:** Any type. The view is linked to a content node whose URL matches `Auth.LoginViewPath` (default `/login`).

**Required service injection:**

```razor
@inject IVirtualMembersLoginViewHelper LoginViewHelper
```

**`LoginViewHelper.GetLoginContext()`** returns a `VirtualMembersLoginViewContext` with everything the form needs:

| Property | Type | Description |
|---|---|---|
| `Auth` | `VirtualMembersOptions.AuthOptions` | Login path, logout path, mode, captcha flag, etc. |
| `ReturnUrl` | `string` | Where to return the user after login. Pass it as a hidden field. |
| `IsChallenge` | `bool` | `true` when showing the OTP/MFA code entry form (step 2). |
| `ChallengeType` | `string` | `"otp-email"` or `"otp-sms"` — determines which code field to show. |
| `ChallengeToken` | `string` | Opaque token. **Must be forwarded** to the login endpoint as a hidden field. |
| `ErrorCode` | `string?` | `"unauthorized"` when login failed. Display an error message to the user. |

**Minimal template example:**

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

    @* Always forward returnUrl *@
    <input type="hidden" name="ReturnUrl" value="@lc.ReturnUrl" />

    @if (!lc.IsChallenge)
    {
        @* ── Step 1: email ── *@
        <label>Email address</label>
        <input type="email" name="Email" required />
        <button type="submit">Continue</button>
    }
    else
    {
        @* ── Step 2: OTP / MFA code ── *@
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

**Key points:**
- `action` must point to `lc.Auth.LoginPath` (the POST endpoint, not the view URL).
- `ReturnUrl` must always be present as a hidden field so the redirect works after step 2.
- In the challenge step, `Email` is **not** included in the form — the email is recovered from the `ChallengeToken` server-side.
- Field names for the factor codes follow the dictionary binding pattern: `Factors[otp-email]`, `Factors[otp-sms]`.

---

### 4.2 Logout view

**What it does:** Displayed after the user signs out. The actual sign-out happens at the POST endpoint (`Auth.LogoutPath`); this view is just the confirmation page the user sees afterwards.

**Umbraco document type:** Any type. The content node URL should be used as `Redirect.PostLogoutRedirectUrl` in configuration (or you can redirect to any other page).

**No special injection required.** The view can optionally read `HttpContext.User` to confirm the session is cleared, but typically it simply shows a "you have been signed out" message.

**Minimal template example:**

```razor
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@{
    Layout = null;
}

<h1>Signed out</h1>
<p>Your session has ended.</p>
<a href="/login">Sign in again</a>
```

**Logout button in any other view:**

```html
<form method="post" action="/api/logout">
    <button type="submit">Sign out</button>
</form>
```

---

### 4.3 Access Denied view

**What it does:** Shown when a user is authenticated but does not have access to the requested content node (403 — wrong group). Umbraco routes here automatically when `IPublicAccessChecker` returns `AccessDenied`.

**Umbraco document type:** Any type. The content node must be configured as the "No access" page in the Umbraco public access settings for each protected node (see [section 5](#5-protect-content-in-the-back-office)).

**Optional injection** — display who the user is and which groups they belong to:

```razor
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Microsoft.Extensions.Options
@using System.Security.Claims
@using PragmaticIT.Umbraco.VirtualMembers.Options
@inject IOptions<VirtualMembersOptions> VmOptions
@{
    Layout = null;
    var identity = Context.User.Identity as ClaimsIdentity;
    var email    = identity?.FindFirst(ClaimTypes.Email)?.Value;
    var groups   = identity?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
}

<h1>Access denied</h1>
<p>You are signed in as <strong>@email</strong> but do not have access to this area.</p>
@if (groups.Any())
{
    <p>Your groups: @string.Join(", ", groups)</p>
}
<a href="@VmOptions.Value.Auth.LoginViewPath">Switch account</a>
```

---

## 5. Protect content in the back-office

Virtual Members relies on standard Umbraco **Public Access** rules, evaluated by the decorated `IPublicAccessChecker`. No special configuration is needed — the rules are set up the same way you would protect content for regular Umbraco members.

For each content node you want to protect:

1. Open the content node in the Umbraco back-office.
2. Go to **Info** → **Public Access**.
3. Enable public access and configure:
   - **Login page** — the content node at your `Auth.LoginViewPath` (e.g. `/login`).
   - **Error/No access page** — the content node for your Access Denied view.
   - **Allowed groups** — the member group names that match your CSV file names (e.g. `Org-A`, `Org-B`).

> **Important:** The allowed group names must match the CSV file names **exactly** (case-insensitive). For example, a file named `Org-A.csv` creates the group `Org-A`.

---

## 6. Authentication modes

Set `Auth.Mode` in `appsettings.json`:

### `None` (default)
Email-only. The user types their email address and — if it is found in a CSV file — they are signed in immediately. Suitable for demos and low-security internal tools.

### `Otp`
Email + one-time code. After the user submits their email, a 6-digit code is sent to that address. The user then enters the code on the same login page (step 2).

Requires an email delivery infrastructure. The `OtpService` sends the email via the standard .NET email sender configured in your application.

### `Mfa`
Email code + SMS code. Both codes are issued simultaneously on step 1. Both must be submitted together on step 2. The user's `Mobile` column in the CSV must contain a valid phone number.

Requires a real `IVirtualMemberSmsService` implementation (see [section 7](#7-plugging-in-a-real-sms-provider)).

---

## 7. Plugging in a real SMS provider

The package ships with a no-op `NullSmsService` that logs a warning instead of sending an SMS. To enable real SMS delivery, implement `IVirtualMemberSmsService` and register it in DI:

```csharp
// In Program.cs, before or after builder.CreateUmbracoBuilder()
builder.Services.AddSingleton<IVirtualMemberSmsService, TwilioSmsService>();
```

`IVirtualMemberSmsService` has a single method:

```csharp
Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
```

The `message` is already formatted using `Sms.OtpMessageTemplate` from options (placeholder `{0}` is replaced with the code before calling `SendAsync`).

---

## 8. Example project

The `Example/` directory in the repository contains a standalone Umbraco project that references the **published NuGet package** (rather than the local source). It serves as a reference implementation showing all features working end-to-end.

> ⚠️ The example project is still a work in progress. Once complete it will demonstrate a full two-organisation setup with OTP login, CSV hot-reload, and Public Access integration.

The example project uses the `Common/Seed/` infrastructure to automatically create content nodes and Public Access rules on first run, so no manual back-office configuration is needed. This seed code is **part of the example host only** and is not shipped as part of the NuGet package.
