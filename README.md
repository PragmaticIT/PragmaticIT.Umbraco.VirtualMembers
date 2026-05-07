# PragmaticIT.Umbraco.VirtualMembers

> **Passwordless, database-free member authentication for Umbraco CMS.**

Virtual Members lets you protect Umbraco content nodes with a lightweight authentication layer that requires **no member records in the Umbraco database**. Users are loaded from plain CSV files and authenticated via email-only, OTP, or full MFA — all without touching the Umbraco Members section.

[![NuGet](https://img.shields.io/nuget/v/PragmaticIT.Umbraco.VirtualMembers)](https://www.nuget.org/packages/PragmaticIT.Umbraco.VirtualMembers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![CI](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/actions/workflows/ci.yml/badge.svg)](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/actions/workflows/ci.yml)

---

## Features

- 🔑 **Passwordless login** — email-only, OTP (email code), or MFA (email + SMS code)
- 📄 **CSV member lists** — drop files into `App_Data/VirtualMembers`, no database needed
- 🔄 **Hot-reload** — file-system watcher invalidates the in-memory cache automatically
- 🛡️ **Umbraco Public Access integration** — works with standard Umbraco protected pages / member groups
- 🔌 **Pluggable providers** — implement `IVirtualMemberProvider` to source members from any backend
- 📱 **SMS abstraction** — plug in Twilio, Azure Communication Services, or any provider via `IVirtualMemberSmsService`
- ⚙️ **Zero-friction setup** — one NuGet package, one `appsettings.json` section, two Razor view templates

---

## Repository layout

```
/
├── Common/
│   ├── Member Lists/          # Sample CSV files shared by test projects (Org-A.csv, Org-B.csv)
│   └── Seed/                  # Umbraco migration that auto-seeds content + public access rules
│                              # (used by development projects only, NOT part of the NuGet package)
│
├── Source/
│   ├── PragmaticIT.Umbraco.VirtualMembers/          # ← The NuGet library
│   └── PragmaticIT.Umbraco.VirtualMembers.Web/      # Local test host (references the library directly)
│
└── Example/                   # Example project referencing the published NuGet package
    └── UmbracoProject*/       # (work in progress — see docs/USAGE.md)
```

---

## Quick start

See **[docs/USAGE.md](docs/USAGE.md)** for the full step-by-step guide:
install the package → configure `appsettings.json` → add CSV member lists → add view templates.

## Architecture & internals

See **[docs/IMPLEMENTATION.md](docs/IMPLEMENTATION.md)** for a deep-dive into the authentication pipeline, provider model, middleware, and extension points.

---

## Requirements

| Dependency | Version |
|---|---|
| .NET | 10.0 |
| Umbraco CMS | ≥ 15.0 |

> **Compatibility matrix**
>
> | Package | Umbraco |
> |---------|---------|
> | `1.x`   | 15.x · 16.x · 17.x |
>
> Tested on Umbraco 17.3.4 (latest stable). Compilation-verified on 15.0, 16.0, 17.0–17.3.
> Incompatible with versions below 15.0 — `IPublishedContentCache.GetByIdAsync` was introduced in Umbraco 15.

---

## License

[MIT](LICENSE)
