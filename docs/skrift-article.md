# Virtual Members: passwordless Umbraco auth without the identity-provider bill

*Or: how a spreadsheet, a cookie, and a decorator saved us a small fortune.*

---

## It started with a very reasonable request

"Hey, can we give our client's employees access to some documents on the portal?"

Sure! Easy! We've done this a hundred times. OIDC, a couple of redirect URIs, done in an afternoon.

Then the questions started.

*"Who manages the user accounts?"*
*"The client, I guess? They know their own staff."*
*"OK, so the client needs access to our identity provider's admin panel?"*
*"…Yes."*
*"And if they add three hundred users by mistake?"*
*"We pay for three hundred users."*

Long pause.

Our identity provider — like most of them — charges per seat. A client who can freely create accounts is a client who can freely inflate your monthly bill. The documents weren't secret, they were just *not quite public*. A full-blown federated identity setup felt like bringing a rocket launcher to a knife fight.

We needed something lighter. Something where the client just hands us a list and we handle the rest. Something that doesn't create a user record for every person who wants to read a PDF once a quarter.

That's how **Virtual Members** was born.

Our client shares dedicated portals with their own customers — large industrial companies, think steel mills and mining operations with thousands of employees. Each portal hosts restricted content: product brochures, price lists, documents meant only for that company's staff. The client's customers' HR teams know their own rosters best — but handing them access to a third-party IAM admin panel was never on the table. Instead, an HR officer sends over an updated CSV file once a month. It gets dropped in a folder. Access is updated. Nobody files a ticket, nobody gets an unexpected invoice.

---

## The idea in one sentence

> A Virtual Member is someone who can log in to the portal using their email address, but for whom we never create a database record anywhere.

The member list lives in a CSV file. The client maintains it at their own pace and hands it over whenever the roster changes. We drop it in a folder. Done.

Access control — which pages a given group can see — is configured the normal Umbraco way through Public Access rules. Nothing exotic. The only "magic" is a thin decorator on Umbraco's `IPublicAccessChecker` that knows how to evaluate a virtual session instead of demanding a real member record.

If you need to give a client's team read access to a section of your Umbraco site — and you'd rather spend the afternoon on something more interesting than provisioning accounts — drop in a CSV file, set your preferred mode, and call it done. More on modes below.

---

## Let's build the simplest case

The simplest case is **email-only** mode: the user types their address, the system checks whether it's on the list, and — if yes — issues a session cookie. As simple as that — no passwords, no codes.

Here's what you need.

### 1. Add the NuGet package

```
dotnet add package PragmaticIT.Umbraco.VirtualMembers
```

No changes to `Program.cs`. The package registers itself via an Umbraco `IComposer` that is picked up automatically at startup.

### 2. Drop a CSV file in the right folder

Create `umbraco/Data/VirtualMembers/` and add a file. **The file name becomes the group name.**

```
umbraco/
└── Data/
    └── VirtualMembers/
        └── Acme-Corp.csv
```

```
Name;Email;Mobile
Alice Smith;alice@acme.com;
Bob Jones;bob@acme.com;
```

That's it. No database migrations. No back-office clicks. The group is created automatically on first load if it doesn't already exist. The in-memory cache picks it up within minutes — or immediately if you leave the file-system watcher enabled (it is, by default).

Notice that neither row has a phone number. The `Mobile` column is optional — the CSV is valid with or without it. Phone numbers only become relevant when you switch to MFA mode.

### 3. Configure appsettings.json

```json
"VirtualMembers": {
  "Auth": {
    "LoginViewPath": "/login",
    "Mode": "None"
  },
  "Redirect": {
    "PostLoginRedirectUrl": "/"
  },
  "Csv": {
    "Directory": "|DataDirectory|/VirtualMembers"
  }
}
```

`Mode: "None"` means email-only — show up with a recognised address and you're in.

### 4. Create a login view in Umbraco

Add a content node at `/login` (any document type) and create the matching Razor view. The package provides `IVirtualMembersLoginViewHelper` which reads the current request and hands you everything the form needs:

```razor
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using PragmaticIT.Umbraco.VirtualMembers.Helpers
@inject IVirtualMembersLoginViewHelper LoginViewHelper
@{
    Layout = null;
    var lc = LoginViewHelper.GetLoginContext();
}

@if (lc.ErrorCode == "unauthorized")
{
    <p>That email address wasn't recognised. Please try again.</p>
}

<form method="post" action="@lc.Auth.LoginPath">
    <input type="hidden" name="ReturnUrl" value="@lc.ReturnUrl" />
    <label>Email address</label>
    <input type="email" name="Email" required />
    <button type="submit">Sign in</button>
</form>
```

`lc.Auth.LoginPath` resolves to `/api/login` — the Minimal API endpoint that the package registers automatically. `ReturnUrl` carries the original destination so users land on the right page after signing in.

### 5. Protect a content node

Back-office → open the node you want to protect → **Info** → **Public Access**:

- **Login page** → your `/login` node
- **Error page** → an "Access Denied" node (any page, just create it)
- **Allowed groups** → `Acme-Corp` (the CSV file name, without the extension)

That's the entire configuration. No code, no migrations, no identity-provider admin panel.

### 6. Watch it work

1. Navigate to the protected page.
2. Umbraco redirects you to `/login`.
3. Type `alice@acme.com` and submit.
4. The library looks Alice up in the CSV, issues a cookie, and redirects back to the original page.
5. Alice reads her documents.
6. Billing department sleeps soundly.

---

## What about something more secure?

Three modes are available, and you switch between them with a single config value:

**`None`** — email-only. The user types their address; if it's on the list, they're in. No codes, no extra steps. A reasonable choice any time membership is implied rather than sensitive — newsletter subscribers getting access to an archive, conference attendees browsing a resource library. You already have their email; the CSV is just the list you already maintain.

**`Otp`** — after submitting their email the user receives a 6-digit code. They enter it on the same login page. One extra step, meaningfully stronger. The code is delivered via Umbraco's built-in `IEmailSender`, so a working SMTP configuration in Umbraco is all you need.

**`Mfa`** — both an email code and an SMS code are issued simultaneously. The user must provide both to sign in. This requires a `Mobile` number in the CSV and a real SMS provider wired up via `IVirtualMemberSmsService` (Twilio, Azure Communication Services, or whatever you prefer — the package ships with a no-op placeholder that writes to Debug console, so you can start without one).

The login view handles all three modes from the same template. `LoginViewHelper.GetLoginContext()` tells the view whether it is in the initial email step or the challenge step, and what kind of code fields to render. You can add OTP support to the minimal template above with about ten more lines of Razor.

---

## The part that makes it actually work with Umbraco

Here's the bit that took the most thought: Umbraco's Public Access system normally evaluates access by looking up a `ClaimsPrincipal` in the **member database**. Virtual Members have no database record, so the standard check would always return `AccessDenied`.

The solution is a decorator on `IPublicAccessChecker`. At startup the package captures Umbraco's existing registration, wraps it, and puts the wrapper back. When a request arrives carrying a valid Virtual Members cookie, the decorator evaluates access using the group roles embedded in the cookie's claims — exactly the same group names you configured in the Public Access rule — and never touches the member database. For all other requests (a real Umbraco member, an admin, an anonymous visitor) the call is forwarded to the original checker unchanged.

The result: Virtual Members and regular Umbraco members can coexist on the same site, protecting different content, without either system knowing about the other. If you'd like to dig into the implementation details — cookie structure, claim names, the registration order trick — the [IMPLEMENTATION.md](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/blob/main/docs/IMPLEMENTATION.md) document covers all of that.

---

## Closing thoughts

Virtual Members is not trying to replace a proper identity provider. If you need SSO, fine-grained permissions, or a self-service account portal, go with the real thing.

There are also no passwords — by design, not by oversight. There is nothing to hash, nothing to store, nothing to breach, and no expiry policy to enforce. A user who logs in once a quarter will never arrive to find their password has expired and needs resetting. The authentication factor is always the current state of the CSV, which the client already maintains.

On the topic of audit trails: every login attempt, challenge step, and logout is written to the application log — email address, IP address, user-agent, groups, and outcome. That's usually enough to answer "did Alice access this page on Tuesday?". What you *don't* get is oversight of the CSV update process itself. When the client emails you a new file and you drop it in the folder, there's no record of who changed what or when. If that matters for your use case, keeping the CSV files in a Git repository is the simplest answer — you get a full history of every change, who made it, and when, for free.

The package is open source and available on NuGet. Bug reports and feature requests are welcome via [GitHub Issues](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers/issues); pull requests are equally appreciated.

---

*Source code and full documentation: [github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers](https://github.com/PragmaticIT/PragmaticIT.Umbraco.VirtualMembers)*

---

## Author's BIO

Hubert is a consultant, software architect, developer and entrepreneur who has been building software since the Y2K era. A long time C# / .NET enthusiast, he focuses on maintaining and evolving business critical .NET applications, with a recent emphasis on Umbraco based portals and DMS platforms. He enjoys translating between business stakeholders and development teams, and outside of work he’s a husband, father of two boys, and an occasional djembe player.