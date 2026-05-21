# Seed — data initialisation mechanism for the example project

This document describes how the automatic data initialisation works in the example project (`Examples/VirtualMembersTest/`). The mechanism lives in the `Common/Seed/` directory and **is not shipped as part of the NuGet package** — it exists solely to bring the example up and running without any manual configuration in the Umbraco back-office.

---

## Table of contents

1. [Purpose](#1-purpose)
2. [Files in the seed directory](#2-files-in-the-seed-directory)
3. [Entry point — ContentMigrationComposer](#3-entry-point--contentmigrationcomposer)
4. [Migration plan — ContentMigrationPlan](#4-migration-plan--contentmigrationplan)
5. [Migration steps](#5-migration-steps)
   - [Step 1 — ImportDataMigration](#51-step-1--importdatamigration)
   - [Step 2 — FixTemplateAssignment](#52-step-2--fixtemplateassignment)
   - [Step 3 — RestrictPublicAccessMigration](#53-step-3--restrictpublicaccessmigration)
   - [Step 4 — InsertCreatedPackagesMigration](#54-step-4--insertcreatedpackagesmigration)
6. [XML files — package contents](#6-xml-files--package-contents)
7. [Idempotency and restarts](#7-idempotency-and-restarts)
8. [Extending the seed for your own needs](#8-extending-the-seed-for-your-own-needs)

---

## 1. Purpose

Umbraco does not have a built-in database seeding mechanism comparable to Entity Framework Core. The standard workflow requires manually creating document types, templates, content nodes, and public access rules in the back-office.

The seed mechanism in the example project eliminates that need: on the first run it imports a complete content tree, assigns templates, and configures Public Access rules — all without any user interaction.

This approach also has an important advantage when distributing publicly shared projects (e.g. as a demo or starter kit): the repository does not need to contain any credentials, passwords, or database dumps. Anyone who clones the repository and runs the project gets a fully configured environment with their own fresh database — with no risk of leaking sensitive data and no need to manually import dumps or reset admin passwords.

---

## 2. Files in the seed directory

```
Common/Seed/
├── ContentMigrationComposer.cs   # Registers the migration plan with Umbraco DI
├── ContentMigrationPlan.cs       # Defines the ordered migration steps
├── ContentInstaller.cs           # Implements all migration steps
├── home.xml                      # Package: Home + Common Content + document types + templates
├── org-a.xml                     # Package: Org-A and its child pages
├── org-b.xml                     # Package: Org-B and its child pages
├── login.xml                     # Package: Login content node
├── logout.xml                    # Package: Logout content node
└── access-denied.xml             # Package: Access Denied content node
```

The XML files are embedded in the assembly as **embedded resources** (`EmbeddedResource`). The code loads them at runtime via `Assembly.GetManifestResourceStream`.

---

## 3. Entry point — `ContentMigrationComposer`

```csharp
public class ContentMigrationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.PackageMigrationPlans().Add<ContentMigrationPlan>();
    }
}
```

`IComposer` is discovered automatically by Umbraco on application startup via assembly scanning. It registers `ContentMigrationPlan` with Umbraco's package migration system.

Umbraco runs registered `PackageMigrationPlan` instances on startup if the plan has not yet been executed. Progress is persisted in the database in the `umbracoKeyValue` table.

---

## 4. Migration plan — `ContentMigrationPlan`

```csharp
public class ContentMigrationPlan : PackageMigrationPlan
{
    public ContentMigrationPlan() : base("ContentSeed") { }

    protected override void DefinePlan()
    {
        To<ImportDataMigration>("content-seed-step1");
        To<FixTemplateAssignment>("content-seed-step2");
        To<RestrictPublicAccessMigration>("content-seed-step3");
        To<InsertCreatedPackagesMigration>("content-seed-step4");
    }
}
```

The class inherits from `PackageMigrationPlan`. The constructor takes the plan name (`"ContentSeed"`), which is used as the key in the database for tracking progress.

`DefinePlan` defines the ordered steps via `To<TMigration>(stateKey)` calls. Each `stateKey` (e.g. `"content-seed-step1"`) must be unique — Umbraco records the last completed key and resumes from that point if the application is interrupted.

---

## 5. Migration steps

### 5.1 Step 1 — `ImportDataMigration`

**Responsibility:** Import all content nodes, document types, and templates from the embedded XML files.

**How it works:**

1. Retrieves the list of resource names from the current assembly (`GetManifestResourceNames()`).
2. For each name in the `resourceNames` array (`home`, `org-a`, `org-b`, `login`, `logout`, `access-denied`) it finds the matching resource ending in `.{name}.xml`.
3. Loads the XML file as an `XDocument`.
4. Calls `IPackagingService.InstallCompiledPackageData(packageDocument)` — the standard Umbraco API for importing packages.
5. For each imported content node calls `IContentService.Publish(node, ["*"])` to publish it in all language variants.

```csharp
var summary = _packagingService.InstallCompiledPackageData(packageDocument);
foreach (var node in summary.ContentInstalled)
{
    _contentService.Publish(node, ["*"]);
}
```

**Note:** Numeric template IDs (`template="1073"`) inside the XML files are specific to the source environment and are not portable. Step 2 is therefore required after the import.

---

### 5.2 Step 2 — `FixTemplateAssignment`

**Responsibility:** Correctly assign templates to the imported content nodes.

**Problem:** The XML files contain numeric template IDs from the environment in which they were exported. Those IDs do not correspond to the correct templates in a fresh database.

**Solution:** The `ContentKeyToTemplateAlias` dictionary maps the stable content node GUID (invariant across environments) to the template alias (also invariant):

```csharp
private static readonly Dictionary<Guid, string> ContentKeyToTemplateAlias = new()
{
    [Guid.Parse("46c54847-aae8-49ae-bf43-d0b5b826d431")] = "home",
    [Guid.Parse("3626f723-782b-4275-9a52-be63f5857216")] = "content",
    [Guid.Parse("b0321c35-e481-4aed-8e09-ba64ccc12480")] = "orgHome",
    // ...
    [Guid.Parse("b7717056-02d1-4eab-972b-784de7df23f4")] = "login",
    [Guid.Parse("10219e40-1e67-490c-a394-7bc24bad45d1")] = "logout",
    [Guid.Parse("d36afb30-271a-46b6-9353-482e3148b9e0")] = "accessDenied",
};
```

For each entry in the dictionary:
1. Fetches the content node by GUID (`IContentService.GetById`).
2. Fetches the template by alias (`IFileService.GetTemplate`) — with a local cache to avoid repeated database calls.
3. If the assigned template is already correct — skips the node.
4. Otherwise sets `content.TemplateId`, saves, and publishes the node.

---

### 5.3 Step 3 — `RestrictPublicAccessMigration`

**Responsibility:** Configure **Public Access** rules (content protection) for the Org-A and Org-B nodes.

**How it works:**

1. Locates the Login node and the Access Denied node among the root content nodes (`GetRootContent()`), matched by name.
2. Iterates over root nodes named `org-a` and `org-b`.
3. For each organisation:
   - Checks whether a member group with that name already exists (`IMemberGroupService.GetByNameAsync`).
   - If not — creates it.
   - Creates a `PublicAccessEntrySlim` with:
     - `ContentId` — GUID of the protected node,
     - `LoginPageId` — GUID of the login page,
     - `ErrorPageId` — GUID of the Access Denied page,
     - `MemberGroupNames` — array containing the group name (e.g. `["Org-A"]`).
   - Calls `IPublicAccessService.CreateAsync`.

After this step each organisation node is protected: unauthenticated users are redirected to the login page, and authenticated users without the required group are redirected to the Access Denied page.

---

### 5.4 Step 4 — `InsertCreatedPackagesMigration`

**Responsibility:** Register the imported packages in the `umbracoCreatedPackageSchema` table so that they are visible in the back-office and can be re-exported at any time.

**Why is this needed?** Entries in `umbracoCreatedPackageSchema` are what Umbraco displays under **Packages → Created** in the back-office. Having these entries means that an administrator can export the package contents at any point — for example after making changes to templates, document type structure, or the content tree — and replace the corresponding XML file in `Common/Seed/`. Without this entry, exporting an updated package directly from the back-office UI would not be possible.

**How it works:** For each of the six packages (`Home`, `Org-A`, `Org-B`, `Login`, `Logout`, `Access Denied`) it inserts a row into `umbracoCreatedPackageSchema` with:
- `name` — human-readable package name,
- `packageId` — stable package GUID,
- `value` — XML describing the package contents (generated by `BuildPackageXml`),
- `updateDate` — current UTC date.

```csharp
Database.Execute(
    "INSERT INTO umbracoCreatedPackageSchema (name, value, updateDate, packageId) VALUES (@0, @1, @2, @3)",
    name, value, DateTime.UtcNow.ToString("O"), packageId.ToUpperInvariant());
```

---

## 6. XML files — package contents

Each XML file uses the standard Umbraco export format (`<umbPackage>`):

| File | Content nodes | Document types | Templates |
|---|---|---|---|
| `home.xml` | Home, Common Content | `home`, `content`, `authUtilityPage` and others | `home`, `content`, `login`, `logout`, `accessDenied`, `orgHome` |
| `org-a.xml` | Org-A, child pages | — | — |
| `org-b.xml` | Org-B, child pages | — | — |
| `login.xml` | Login | — | — |
| `logout.xml` | Logout | — | — |
| `access-denied.xml` | Access Denied | — | — |

Document types and templates are defined only in `home.xml`, which is imported first. The remaining files contain only content nodes and reference types that already exist.

**Important:** Content node GUIDs (`key`) are stable and hardcoded in `ContentInstaller.cs`. This allows steps 2 and 3 to reliably look up nodes regardless of the numeric IDs assigned by the database.

---

## 7. Idempotency and restarts

Umbraco's `PackageMigrationPlan` system is designed to be idempotent:

- Each step has a unique `stateKey` that is written to `umbracoKeyValue` after successful execution (`_context.Complete()`).
- On the next application start the plan compares the saved state against its definition and skips already-completed steps.
- If the application is interrupted mid-step (e.g. a crash), **only that step** will be retried on the next start.

> **Note on the first run:** Umbraco needs to warm up its content cache after import. Freshly imported nodes may return 404 on the first request. If this happens, restart the application once or twice until the content cache is fully populated.

---

## 8. Extending the seed for your own needs

To add your own initial data to a new project based on this mechanism:

1. **Add a new XML file** — export a node from the Umbraco back-office as a package and place it in `Common/Seed/`. Set the file's build action to `EmbeddedResource`.

2. **Register the resource in `ImportDataMigration`** — add the file name (without `.xml`) to the `resourceNames` array:
   ```csharp
   private string[] resourceNames = { "home", "org-a", ..., "my-new-node" };
   ```

3. **Add a template mapping** — if the node uses a template, add an entry to `ContentKeyToTemplateAlias` in `FixTemplateAssignment`:
   ```csharp
   [Guid.Parse("your-node-guid")] = "templateAlias",
   ```

4. **Add Public Access rules** (optional) — extend `RestrictPublicAccessMigration` to handle the new node.

5. **Register the package** — add an entry to the `Packages` array in `InsertCreatedPackagesMigration`, assigning a new stable package GUID.

6. **Bump the plan state** — Umbraco tracks state by `stateKey`. Existing steps will not be re-run, so if you want a new step to execute on already-seeded databases, add it to `ContentMigrationPlan`:
   ```csharp
   To<MyNewMigration>("content-seed-step5");
   ```
