# Implementation guide

This document describes the internal architecture of `PragmaticIT.Umbraco.VirtualMembers` — how each component is composed, what it does, and how they interact at runtime.

---

## Table of contents

1. [Big picture](#1-big-picture)
2. [Project structure](#2-project-structure)
3. [Bootstrap — composer and DI registration](#3-bootstrap--composer-and-di-registration)
4. [Options](#4-options)
5. [Provider model](#5-provider-model)
6. [CSV provider](#6-csv-provider)
7. [OTP service](#7-otp-service)
8. [SMS abstraction](#8-sms-abstraction)
9. [Authentication middleware](#9-authentication-middleware)
10. [Public Access integration](#10-public-access-integration)
11. [Login endpoints](#11-login-endpoints)
12. [Login view helper](#12-login-view-helper)
13. [CSV directory watcher](#13-csv-directory-watcher)
14. [Authentication flow — step by step](#14-authentication-flow--step-by-step)
15. [Extending the library](#15-extending-the-library)
16. [Seed infrastructure (not part of the package)](#16-seed-infrastructure-not-part-of-the-package)

---

## 1. Big picture

```
Browser
  │
  │  POST /api/login (form)
  ▼
VirtualMembersAuthenticationMiddleware   ← reads existing cookie, merges identity into HttpContext.User
  │
  ▼
Login endpoint (Minimal API)
  │  AuthenticationContext
  ▼
VirtualMemberSignInService
  │  IVirtualMemberProvider.AuthenticateAsync()
  ▼
VirtualMemberProviderAggregator          ← iterates registered leaf providers
  │
  ▼
CsvVirtualMemberProvider                 ← resolves e-mail → VirtualMemberProfile, issues OTP if needed
  │
  ├─ AuthenticationResult.Succeeded      → cookie issued, redirect to returnUrl / PostLoginRedirectUrl
  ├─ AuthenticationResult.ChallengeRequired → redirect to /login?challengeType=…&challengeToken=…
  └─ AuthenticationResult.Failed         → redirect to /login?error=unauthorized
```

Umbraco's `IPublicAccessChecker` is replaced by `VirtualMembersPublicAccessChecker`, which intercepts requests that carry a valid VirtualMembers cookie and evaluates access using `ClaimsPrincipal` roles instead of database member records.

---

## 2. Project structure

```
Source/PragmaticIT.Umbraco.VirtualMembers/
├── VirtualMembersComposer.cs          # Umbraco IComposer entry point
├── VirtualMembersDefaults.cs          # Scheme name and pipeline filter name constants
│
├── Extensions/
│   └── VirtualMembersUmbracoBuilderExtensions.cs   # All DI wiring
│
├── Options/
│   ├── VirtualMembersOptions.cs       # Strongly-typed configuration
│   └── AuthMode.cs                    # None | Otp | Mfa enum
│
├── Models/
│   ├── VirtualMemberProfile.cs        # Email, Name, Mobile, Groups
│   ├── AuthenticationContext.cs       # Input to the provider (email, challenge token, factors)
│   └── AuthenticationResult.cs       # Discriminated union: Succeeded | ChallengeRequired | Failed
│
├── Providers/
│   ├── IVirtualMemberProvider.cs      # Core provider interface
│   ├── VirtualMemberProviderAggregator.cs  # Fan-out over all leaf providers
│   ├── VirtualMembersProviderKeys.cs  # Keyed DI key constant
│   └── Csv/
│       ├── ICsvVirtualMemberStore.cs
│       ├── CsvVirtualMemberStore.cs   # Reads + caches CSV files
│       └── CsvVirtualMemberProvider.cs  # Implements IVirtualMemberProvider using the store
│
├── Services/
│   ├── IVirtualMemberSignInService.cs / VirtualMemberSignInService.cs
│   ├── IVirtualMembersRedirectService.cs / VirtualMembersRedirectService.cs
│   ├── IOtpService.cs / OtpService.cs
│   ├── IVirtualMemberSmsService.cs
│   └── NullSmsService.cs              # No-op fallback
│
├── Middleware/
│   └── VirtualMembersAuthenticationMiddleware.cs
│
├── Security/
│   └── VirtualMembersPublicAccessChecker.cs
│
├── Helpers/
│   ├── IVirtualMembersLoginViewHelper.cs
│   ├── VirtualMembersLoginViewHelper.cs
│   ├── VirtualMembersLoginViewContext.cs
│   ├── VirtualMemberEmailHelper.cs    # E-mail normalisation extension method
│   └── VirtualMembersPathHelper.cs    # Resolves relative / absolute paths
│
├── Endpoints/
│   ├── VirtualLoginViewModel.cs       # Form model for POST /api/login
│   └── VirtualMembersEndpointRouteBuilderExtensions.cs  # MapVirtualMembersEndpoints()
│
└── Watchers/
    └── CsvDirectoryWatcher.cs         # BackgroundService — watches CSV directory
```

---

## 3. Bootstrap — composer and DI registration

`VirtualMembersComposer` implements Umbraco's `IComposer` interface and is discovered automatically at startup:

```csharp
public sealed class VirtualMembersComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddVirtualMembers();
}
```

All wiring is in `VirtualMembersUmbracoBuilderExtensions.AddVirtualMembers()`. It registers:

| Service | Lifetime | Notes |
|---|---|---|
| `VirtualMembersOptions` | — | Bound from `appsettings.json` section `VirtualMembers` |
| `ICsvVirtualMemberStore` / `CsvVirtualMemberStore` | Singleton | In-memory cache with TTL |
| `IVirtualMemberProvider` (keyed `Leaf`) / `CsvVirtualMemberProvider` | Singleton | Leaf provider |
| `IVirtualMemberProvider` / `VirtualMemberProviderAggregator` | Singleton | Default provider |
| `IOtpService` / `OtpService` | Singleton | IMemoryCache-backed |
| `IVirtualMemberSignInService` / `VirtualMemberSignInService` | Scoped | |
| `IVirtualMembersRedirectService` / `VirtualMembersRedirectService` | Scoped | |
| `IVirtualMemberSmsService` / `NullSmsService` | Singleton | `TryAdd` — replaced by real impl |
| `CsvDirectoryWatcher` | Hosted service | `BackgroundService` |
| `IVirtualMembersLoginViewHelper` | Scoped | Used from Razor views |
| `IPublicAccessChecker` | Scoped (decorator) | Wraps Umbraco's built-in checker |
| Cookie auth scheme `VirtualMembers` | — | `AddCookie()` |

The middleware and Minimal API endpoints are inserted into the Umbraco pipeline filter named `VirtualMembers`:

```csharp
pipelineOptions.AddFilter(new UmbracoPipelineFilter("VirtualMembers")
{
    PrePipeline = app => app.UseMiddleware<VirtualMembersAuthenticationMiddleware>(),
    Endpoints   = app => app.UseEndpoints(ep => ep.MapVirtualMembersEndpoints())
});
```

---

## 4. Options

All configuration lives under the `VirtualMembers` key in `appsettings.json`.

```
VirtualMembersOptions
├── Auth
│   ├── Scheme              (default: "VirtualMembers")
│   ├── CookieName          (default: "VirtualMembers.Auth")
│   ├── LoginPath           (default: "/api/login")       ← POST endpoint
│   ├── LogoutPath          (default: "/api/logout")      ← POST endpoint
│   ├── LoginViewPath       (default: "/login")           ← GET Razor view
│   ├── CookieLifetimeMinutes (default: 60)
│   ├── Mode                (None | Otp | Mfa)
│   └── CaptchaEnabled      (default: false)
├── Redirect
│   ├── PostLoginRedirectUrl  (default: "/")
│   └── PostLogoutRedirectUrl (default: "/")
├── Csv
│   ├── Directory     (default: "App_Data/VirtualMembers")
│   ├── FilePattern   (default: "*.csv")
│   ├── CacheMinutes  (default: 5)
│   ├── Watch         (default: true)
│   ├── WatchFilter   (default: "*.csv")
│   └── Encoding      (default: "utf-8")
└── Sms
    ├── SenderName          (default: "VirtualMembers")
    └── OtpMessageTemplate  (default: "Your verification code is: {0}")
```

Options are validated with Data Annotations at startup (`ValidateOnStart`).

---

## 5. Provider model

### `IVirtualMemberProvider`

```csharp
public interface IVirtualMemberProvider
{
    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationContext context,
        CancellationToken cancellationToken = default);
}
```

`AuthenticationContext` carries:
- `Email` — supplied by the user in step 1 (empty in step 2)
- `ChallengeToken` — opaque token issued in the previous step
- `Factors` — dictionary of submitted codes, keyed by factor type (e.g. `"otp-email"`, `"otp-sms"`)

`AuthenticationResult` is an abstract discriminated union with three subtypes:
- `Succeeded(VirtualMemberProfile)` — authentication complete
- `ChallengeRequired(string challengeType, string challengeToken)` — another step needed
- `Failed(string reason)` — access denied

### `VirtualMemberProviderAggregator`

Resolves all services registered under the keyed key `VirtualMembersProviderKeys.Leaf` and fans the call out across them:

- If any provider returns `ChallengeRequired`, it is returned immediately (challenge takes priority).
- If multiple providers return `Succeeded`, their `Groups` are merged into a single profile.
- If none succeed, `Failed("UserNotFound")` is returned.

---

## 6. CSV provider

### `CsvVirtualMemberStore`

Reads all `*.csv` files from the configured directory. Each file name (without extension) becomes the **group name** assigned to every member listed in that file.

Expected CSV format (semicolon-separated, with header):

```
Name;Email;Mobile
Alice;alice@example.com;+48111222333
Bob;bob@example.com;
```

- `Name` and `Mobile` are optional.
- The store is a singleton with a TTL-based in-memory cache (`CacheMinutes`).
- `Invalidate()` forces a reload on the next access (called by `CsvDirectoryWatcher`).

### `CsvVirtualMemberProvider`

Two-step authentication logic:

**Step 1** (no `ChallengeToken`):
1. Normalise email (lowercase, trimmed).
2. Look up the profile in the store → `Failed("UserNotFound")` if absent.
3. If `AuthMode.None` → return `Succeeded` immediately.
4. If `AuthMode.Otp` or `AuthMode.Mfa` → call `IOtpService.IssueAsync()` to generate and email a code.
5. If `AuthMode.Mfa` and the profile has a mobile number → also call `IOtpService.IssueSmsAsync()`.
6. Return `ChallengeRequired("otp-email", token)`.

**Step 2** (`ChallengeToken` present):
1. Extract submitted `otp-email` code from `Factors`.
2. Call `IOtpService.ValidateAndConsumeAsync()` → `Failed("InvalidOtpCode")` if wrong.
3. If `AuthMode.Mfa` → also validate `otp-sms` code via `IOtpService.ValidateAndConsumeSmsAsync()`.
4. Return `Succeeded(profile)`.

---

## 7. OTP service

`OtpService` uses `IMemoryCache` internally:

- `IssueAsync(email)` — generates a 6-digit code, stores `token → {email, code}` with a configurable TTL, sends the code by email.
- `ValidateAndConsumeAsync(token, code)` — verifies the code and removes the entry on success; returns the bound email or `null`.
- `IssueSmsAsync(phone, parentToken)` — generates a second 6-digit code keyed as `sms:{parentToken}`, sends via `IVirtualMemberSmsService`.
- `ValidateAndConsumeSmsAsync(parentToken, code)` — verifies and removes the SMS entry.

---

## 8. SMS abstraction

`IVirtualMemberSmsService` has a single method:

```csharp
Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
```

The default registration is `NullSmsService`, which logs a warning and does nothing. Replace it with your own implementation **before or after** calling `AddVirtualMembers()`:

```csharp
builder.Services.AddSingleton<IVirtualMemberSmsService, TwilioSmsService>();
```

Because `TryAddSingleton` is used for the null implementation, any registration made before this call will be preserved.

---

## 9. Authentication middleware

`VirtualMembersAuthenticationMiddleware` runs in the `PrePipeline` phase (before Umbraco's own middleware):

1. Calls `HttpContext.AuthenticateAsync(VirtualMembersDefaults.Scheme)`.
2. If the cookie is valid, **prepends** the resulting `ClaimsIdentity` to `HttpContext.User.Identities`.
3. This makes `HttpContext.User.Identity.IsAuthenticated == true` for Virtual Members sessions, without overwriting any Umbraco admin identity.

Claims stored in the cookie:
- `ClaimTypes.Email`
- `ClaimTypes.Name`
- `ClaimTypes.Role` — one claim per group

---

## 10. Public Access integration

`VirtualMembersPublicAccessChecker` decorates Umbraco's built-in `IPublicAccessChecker`. At registration time the original service descriptor is captured, removed, and re-added wrapped inside the decorator.

Logic:

1. If `HttpContext` is null → delegate to inner checker.
2. Try to authenticate the `VirtualMembers` cookie.
3. If the cookie is not valid → delegate to inner checker (normal Umbraco member flow).
4. If the cookie is valid → evaluate access using `IPublicAccessService.HasAccessAsync()` with the content's **path** and the principal's role claims (group names). No member database record is needed.

The two overloads of `HasMemberAccessToContentAsync` cover both the direct call (content ID only) and the call that already has a `ClaimsPrincipal` (used in some Umbraco pipeline paths).

---

## 11. Login endpoints

`MapVirtualMembersEndpoints()` registers two Minimal API endpoints:

### `POST {Auth.LoginPath}` (default `/api/login`)

Accepts form fields:
- `Email` — user's email address (step 1 only)
- `ReturnUrl` — optional, where to redirect after login
- `ChallengeToken` — opaque token from step 1 (step 2 only)
- `Factors[otp-email]` — OTP email code (step 2, OTP/MFA)
- `Factors[otp-sms]` — OTP SMS code (step 2, MFA only)

Result routing:
- `Succeeded` → redirect to `returnUrl` or `PostLoginRedirectUrl`
- `ChallengeRequired` → redirect to `{LoginViewPath}?challengeType=…&challengeToken=…[&returnUrl=…]`
- `Failed` → redirect to `{LoginViewPath}?error=unauthorized[&returnUrl=…]`

Antiforgery is **disabled** on this endpoint (the token is instead protected by the opaque `ChallengeToken`).

### `POST {Auth.LogoutPath}` (default `/api/logout`)

Calls `IVirtualMemberSignInService.SignOutAsync()` (deletes the cookie) and redirects to `PostLogoutRedirectUrl`.

---

## 12. Login view helper

`IVirtualMembersLoginViewHelper.GetLoginContext()` returns a `VirtualMembersLoginViewContext` record that consolidates everything a login Razor view needs:

| Property | Description |
|---|---|
| `Auth` | `VirtualMembersOptions.AuthOptions` sub-object |
| `ReturnUrl` | Derived from the request path (URL rewrite) or `?returnUrl=` query param |
| `ErrorCode` | Raw error code from `?error=` (e.g. `"unauthorized"`) |
| `IsChallenge` | `true` when `?challengeToken=` is present |
| `ChallengeType` | e.g. `"otp-email"` or `"otp-sms"` |
| `ChallengeToken` | Opaque token to forward to the login endpoint |

Inject it in a Razor view:

```razor
@inject IVirtualMembersLoginViewHelper LoginViewHelper
@{
    var lc = LoginViewHelper.GetLoginContext();
}
```

---

## 13. CSV directory watcher

`CsvDirectoryWatcher` is a `BackgroundService` that sets up a `FileSystemWatcher` on the CSV directory. On any `Created`, `Changed`, `Renamed`, or `Deleted` event it:

1. Calls `ICsvVirtualMemberStore.Invalidate()` to expire the cache.
2. Optionally synchronises Umbraco member group membership via `IMemberGroupService` and `IMemberService` (so that protected pages using standard Umbraco public access rules also reflect the updated lists immediately).

Watching can be disabled by setting `Csv.Watch = false`.

---

## 14. Authentication flow — step by step

### Mode: `None` (email only)

```
1. User submits email → POST /api/login
2. CsvVirtualMemberProvider looks up email → Succeeded(profile)
3. VirtualMemberSignInService issues cookie
4. Redirect to returnUrl or PostLoginRedirectUrl
```

### Mode: `Otp` (email + one-time code)

```
1. User submits email → POST /api/login
2. Provider issues OTP, sends email → ChallengeRequired("otp-email", token)
3. Endpoint redirects to /login?challengeType=otp-email&challengeToken=<token>
4. User submits code → POST /api/login  { ChallengeToken, Factors["otp-email"] }
5. Provider validates code → Succeeded(profile)
6. Cookie issued → redirect
```

### Mode: `Mfa` (email code + SMS code)

```
1. User submits email → POST /api/login
2. Provider issues email OTP + SMS OTP → ChallengeRequired("otp-email", token)
3. Redirect to /login?challengeType=otp-email&challengeToken=<token>
4. User submits both codes → POST /api/login
   { ChallengeToken, Factors["otp-email"], Factors["otp-sms"] }
5. Provider validates both codes → Succeeded(profile)
6. Cookie issued → redirect
```

---

## 15. Extending the library

### Custom member provider

Implement `IVirtualMemberProvider` and register it as a **keyed singleton** under the `VirtualMembersProviderKeys.Leaf` key. The aggregator will include it automatically:

```csharp
builder.Services.AddKeyedSingleton<IVirtualMemberProvider, MyDatabaseProvider>(
    VirtualMembersProviderKeys.Leaf);
```

Your provider receives the same `AuthenticationContext` and must return one of the three `AuthenticationResult` subtypes.

### Custom SMS provider

```csharp
builder.Services.AddSingleton<IVirtualMemberSmsService, TwilioSmsService>();
```

Register **before** `AddVirtualMembers()` or use `Replace` afterwards. The built-in `NullSmsService` is registered with `TryAddSingleton`, so any earlier registration wins.

---

## 16. Seed infrastructure (not part of the package)

The `Common/Seed/` directory contains Umbraco package migrations used to **automatically bootstrap the development and example projects**:

| Class | Purpose |
|---|---|
| `ContentMigrationComposer` | Registers the migration plan with Umbraco |
| `ContentMigrationPlan` | Defines the migration steps (`content-seed-v1`, `content-seed-v4`) |
| `ImportDataMigration` | Imports content nodes from embedded XML resources (home, org-a, org-b, login, logout, access-denied) |
| `RestrictPublicAccessMigration` | Configures Umbraco Public Access rules — links org pages to the login/access-denied nodes and assigns member groups |

The seed code is compiled into the test/example hosts only. It is **not included in the NuGet package** and is not required for production use. Real-world deployments configure Public Access rules through the Umbraco back-office.
