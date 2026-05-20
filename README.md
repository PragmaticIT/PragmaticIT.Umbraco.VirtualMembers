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

---

## Running the development projects

Both `Source/PragmaticIT.Umbraco.VirtualMembers.Web` and `Examples/VirtualMembersTest` are **self-initialising**.  
After a fresh checkout there is no database to restore and no manual backoffice setup required.

### Steps

1. Clone or check out the repository.
2. Run the project (`F5` or `dotnet run`).
3. Umbraco's unattended installer creates a fresh SQLite database automatically.
4. On first boot the seed migration runs and installs:
   - document types, content tree (Home, Org-A, Org-B, Login, Logout, Access Denied)
   - template records in the database (Razor views are already present on disk from the repo)
   - public access rules (Org-A and Org-B nodes protected by member groups)
   - `Created Packages` definitions (Home, Org-A, Org-B, Login, Logout, Access Denied) so packages can be re-exported from the backoffice
5. **Restart the application after the first boot.** The seed migration runs during startup before all Umbraco services are fully warm; a second start is required for the published content cache and routing to work correctly.

> The database file (`umbraco/Data/Umbraco.sqlite.db`) is excluded from the repository via `.gitignore`.  
> Credentials for the default admin account are configured in `appsettings.json` under `Umbraco:CMS:Unattended`.

---

## Updating seed packages (templates, document types, content)

The XML files in `Common/Seed/` are the source of truth for the content structure.  
They are embedded resources compiled into the assembly and applied once, on a fresh database, via `PackageMigrationPlan`.

When you need to change templates, document types, or content nodes and want those changes to be reproducible on a fresh checkout, follow this process:

### Updating templates (`.cshtml` files)

1. Edit the `.cshtml` file on disk — it is the primary source.
2. Open the backoffice, go to **Packages → Created**, open the relevant package (e.g. `Home`), and click **Update** then **Download**.
3. Replace the corresponding XML file in `Common/Seed/` with the downloaded `package.xml`, renaming it to match the existing convention (`home.xml`, `org-a.xml`, etc.).
4. Commit both the updated `.cshtml` and the updated XML.

> The seed migration only runs on a **fresh** database (migration state `content-seed-step1` not yet recorded).  
> Existing databases are not affected — the `.cshtml` file on disk is always used at runtime regardless.

### Updating document types or content nodes

1. Make changes in the backoffice.
2. Re-export the affected package via **Packages → Created → Download**.
3. Replace the XML file in `Common/Seed/` and commit.

---

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
