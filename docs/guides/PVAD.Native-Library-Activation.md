# Native Library Activation And Renewal

This guide is for applications embedding PVAD .NET libraries directly. It is
not an Account registration flow or a REST SDK login guide. It covers the
existing native licensing APIs; it does not publish packages, activate billing,
select production keys, or grant distribution permission.

## Prerequisites

- Use .NET 10 and an approved, internally consistent set of PVAD assemblies.
- Obtain a manufacturer-signed native license with the products and canonical
  migration providers your application uses.
- Use binaries containing the approved public verification key for that license.
  The repository's source trust register is intentionally empty; normal source
  builds cannot activate a customer license until that release gate is resolved.
- Store the license outside the public web root and source repository, with
  read access limited to the application identity. Keep its approved backup.
- Never supply a manufacturer private key or a replacement trust register to a
  customer application. Do not edit, reformat, or regenerate signed payloads.

No public NuGet installation or publisher ownership is implied here. Package
publication, third-party redistribution terms and final EULA approval remain
separate gates. An approved local feed or supplied package set must include the
matching native dependencies; copying isolated DLLs between builds is unsafe.

Prepared packages include an offline README with a local-only `NuGet.Config`,
pinned package references, an isolated package cache and lock-file restore
steps. Obtain the complete approved dependency delivery before disconnecting;
a license file does not supply missing runtime libraries or package binaries.
Protected package compatibility is verified on Windows 2025/x64, Ubuntu
24.04/x64 and macOS 15/Intel. That evidence is not publication permission, complete functional
validation of every format or a claim that all architectures are supported.

## Complete C# Example

The following class is compiled directly from this guide during documentation
verification. The application obtains file paths from its own configuration;
there is no built-in default license path or automatic evaluation activation.
Only content-free status/diagnostic values should be logged, not license bytes,
file paths, customer references, keys, or document content.

```csharp
using System.IO;
using PVAD.Licensing;

public static class NativeActivationExample
{
    public static PvadLicenseStatus Activate(
        string licensePath, string? revocationListPath = null)
    {
        if (!string.IsNullOrWhiteSpace(revocationListPath))
            PvadLicense.ImportRevocationsFromFile(revocationListPath);
        return PvadLicense.LoadFromFile(licensePath);
    }

    public static PvadLicenseStatus ActivateFromStream(Stream stream) =>
        PvadLicense.LoadFromStream(stream);

    public static byte[] CreatePdf()
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.Info.Title = "Licensed application example";
        document.AddPage(595, 842)
            .DrawText("Native PVAD licensing example", 40, 790, 18);
        return document.ToBytes();
    }

    public static bool TryLoadReplacement(
        Stream replacement, out string? diagnosticCode)
    {
        try
        {
            PvadLicense.LoadFromStream(replacement);
            diagnosticCode = null;
            return true;
        }
        catch (PvadLicenseException error)
        {
            diagnosticCode = error.Code;
            return false;
        }
    }

    public static PvadLicenseStatus Inspect() => PvadLicense.GetStatus();
}
```

Call `Activate` at application startup before protected processing, then call
`CreatePdf`. Store or return the bytes only after successful generation. A native
PDF grant is sufficient for this PDF example; a Converter grant is not required.
`CreateDocument` and `ToBytes` each perform their own current license checks.

`ActivateFromStream` reads from the caller's current position and leaves the
stream open. The caller still owns disposal. Licenses have a 64 KiB envelope
limit and a 32 KiB decoded payload limit; whitespace does not bypass those limits.
An unreadable file/stream produces `PVADLIC013`; null or invalid application
arguments remain programming errors and should not be treated as activation
success.

`GetStatus` does not activate a license. `CanStartOperation` describes temporal
license state, not permission for every product. Product/provider rights are
checked at the processing entry. Manual `Require` is a preflight admission value,
not a transferable token, a reservation for future calls, or a way to bypass
the library's own checks.

## Product Rights And Dependencies

New version-two licenses additionally carry a signed `accountScope` with the
account type and explicit capability IDs. A product grant is necessary but no
longer sufficient for scoped operations. For example, `import.docx` does not
authorize `import.pdf`, and `export.odt` does not authorize image or CSV export.
`PVADLIC016` means the signed license lacks the requested capability. Loading
the file or calling `Require(product)` is not a transferable capability grant.

Version-one product licenses retain their existing product/provider semantics;
they are not silently converted into individual-account licenses. Older
libraries reject version-two artifacts, so upgrade the complete approved
assembly set before deploying a scoped license. Envelope and payload versions
must match. Renewal preserves the signed scope; new rights require a separately
approved issuance. Both versions continue to verify locally without networking.

Hosted product APIs separately require an authenticated, eligible account.
The host derives account restrictions from its database, irrespective of roles
or UI headers, and intersects them with its native license. Migration execution
requires explicit non-default, unexpired administrator entitlement approval.
The XLS account-policy decision remains deferred: scoped licenses and hosted
account processing do not authorize XLS import/export yet. Existing version-one
offline product licenses retain their native XLS behavior; the dependency and
its distribution decision are unchanged.
Background jobs recheck account policy at execution; CodeWorker dispatch carries
the trusted host restriction snapshot into the child process. A website role
change does not rewrite a downloaded offline artifact.

Standalone E-Invoice generation/validation/recognition/attachment now requires
an active native license and, for version two, the relevant signed capability.
Metadata helpers and DTO construction remain separate from processing. This
requirement and authenticated product APIs are intentional workflow changes;
previously anonymous API clients must authenticate and have account context.

Detached contracts, DTOs and options can be constructed without activation.
SDK type metadata, renderer capabilities and format/provider catalogs are also
license-free. This does not include reading metadata from a customer document:
parsing, Trust inspection, calculation, rendering, import, export and Migration
processing retain their product checks. Constructing a worker request or
inspecting an interface does not authorize executing it. HTTP authentication,
tenant isolation and application permissions still apply independently.

| Work | Native grant |
| --- | --- |
| PDF creation and public serialization | `pdf` |
| DOCX generation and native Word signing | `word` |
| Workbook authoring, calculation and IO | `spreadsheet` |
| Document import, PDF editing/Trust, image analysis and OCR | `importer` |
| HTML, Markdown, CSV, XML, SVG, ODT and image-format exports | `converter` |
| PDF Code migration | `migration.pdf.code` plus canonical provider |
| PDF Viewer migration | `migration.pdf.viewer` plus canonical provider |
| PDF Designer migration | `migration.pdf.designer` plus canonical provider |
| Spreadsheet Code migration | `migration.spreadsheet.code` plus canonical provider |
| Report Designer migration | `migration.report.designer` plus canonical provider |

Provider names in signed grants are canonical
registry IDs, not routing aliases or `auto`. Creating a design DTO or migrating
source does not authorize subsequent PDF generation. Built-in private
dependencies do not expose a general cross-product grant; independently invoked
generation, callbacks and worker jobs obtain fresh admission.

## Trial, Subscription And Perpetual Terms

| Model | Start and end | Effect on new native work |
| --- | --- | --- |
| Trial | Signed issue/start dates; explicitly issued for 30 days | No automatic first-use start, no grace and no redistribution |
| Subscription | Signed validity; exactly seven days of grace after its end | New work is allowed in grace and reports `GracePeriod` / `PVADLIC010`; rejected at grace end |
| Perpetual | No usage expiry for covered releases; signed update cutoff | Eligibility uses immutable library release metadata, not installation time or the computer's file timestamps |

At a Trial's expiry instant, or at a subscription's grace-end instant, new
processing is denied with `PVADLIC009`. A perpetual license can keep using an
eligible old release after its update cutoff; loading an ineligible newer
release fails with `PVADLIC011`. Reinstalling or changing file timestamps does
not extend update rights. Offline wall-clock rollback resistance is not
guaranteed; do not describe the local clock as trusted licensing hardware.

Built-in operations with implemented admitted continuations can finish under
their captured license after replacement/expiry. A later public operation,
retained-object save, external callback or newly dispatched worker job rechecks
its own rights. Implemented continuation coverage is explicit; it is not a
universal guarantee for every composite pipeline.

## Renewal And Replacement Procedure

1. Obtain an approved signed replacement for the intended native product set.
   Manufacturer renewal, Account download, server subscription renewal and local
   activation are different steps.
2. Deliver the file through the application's trusted deployment channel. Stage
   it separately from the currently configured file and retain a known-good copy.
3. Call `LoadFromFile` or the example's `TryLoadReplacement` explicitly. The
   library has no file watcher and does not fetch a renewal from the Internet.
4. On failure, inspect only the stable diagnostic. The previous active snapshot
   is retained, but may itself expire or be revoked; check its status instead of
   assuming it remains usable forever.
5. On success, check the intended products with their normal processing APIs and
   persist the selected file/configuration for the next application restart.
   Use an application-owned atomic deployment procedure where appropriate.

A successful replacement replaces the **entire** active license; grants are not
merged. A cryptographically valid Word-only replacement can load successfully
and still deny later PDF processing with `PVADLIC012`. The sample's `true`
return means the file was accepted, not that every product was granted. The
manufacturer renewal endpoint preserves rights within a renewal chain; the
generic load API is also capable of selecting a different valid license.

Already expired, future-dated, malformed, untrusted or signature-invalid
replacements do not discard a usable current snapshot. A newly constructed
exporter is not required after valid replacement; no process restart is needed
for the native `LoadFromFile`/`LoadFromStream` API. Retained native documents do
not retain permission for every future save.

## API, Worker And REST SDK Boundary

| Deployment | Activation owner | Additional checks |
| --- | --- | --- |
| Embedded .NET application | Application calls `PvadLicense` explicitly | Native product/provider grant on each protected entry |
| PVADE API / customer-managed server | Operator configures `NativeLicensing__LicensePath` | Existing user/session, tenant, role/permission, entitlement and feature policy checks remain required |
| CodeWorker and OCR worker | Trusted host supplies the minimum signed local configuration | Child verifies against its own shipped public-key register; a new job is fresh work |
| Java, Python, TypeScript/JavaScript or C# REST client | Server operator activates the processing server | Client authenticates through the server contract; it does not load a native engine license or contain manufacturer signing keys |

`NativeLicensing__RevocationListPath` configures an explicit local list on the
API host. Invalid configured native artifacts fail startup. An absent path can
allow Account/Admin-only startup but never enables protected native work.
Native failures return HTTP 503 Problem Details with a `PVADLIC...` diagnostic;
HTTP 401/403 session, tenant, permission and entitlement denials are separate.

Updating a mounted file, issuing a replacement in Admin, or downloading it in
Account does not automatically replace an already running host's native
snapshot. An operator must use an explicitly implemented reload mechanism or
restart with the approved configuration. CodeWorker's configured license file
must also be updated consistently. Do not promise a public host-reload endpoint
or universal cross-process hot reload that does not exist.

An Account Trial, a Premium/Enterprise edition, an administrator role and an
active developer seat do not sign or activate a native license. An explicitly
issued native Trial is not the automatic Account registration Trial. REST SDK
packages and embedded .NET engine packages are different distribution models.

## Offline Revocation And Recovery

Import the latest approved manufacturer-signed cumulative revocation list
before activation and on every restart. `Activate` does that when a list path
is provided. Lists use separate signed artifacts; never edit their sequence,
IDs, signature or content. A revoked license fails with `PVADLIC014`.

A stale list still blocks its known revoked IDs and reports freshness; it does
not silently contact a server. The library does not guarantee immediate remote
revocation, global seat counting, a persistent anti-rollback counter or a trusted
clock. Back up the selected native license, the latest approved list and operator
configuration separately from customer documents. Reimport the approved list on
restart; loading another license does not clear known revocations.

Keep signing keys out of customer deployments and backups intended for
customers. If an approved key ID is unknown to the shipped library, obtain a
reviewed library update; do not inject a new runtime trust anchor. Corrupted
release metadata or trust configuration is an operator/release problem, not a
reason to disable verification.

## Troubleshooting

| Diagnostic | Action |
| --- | --- |
| `PVADLIC000` | Active temporal state; normal product/provider checks still apply |
| `PVADLIC001` | Explicitly activate the intended native license before processing |
| `PVADLIC002` | Check approved build/trust/release metadata; do not patch it at runtime |
| `PVADLIC003` / `PVADLIC004` / `PVADLIC005` | Obtain the complete supported signed artifact; check truncation, schema and size |
| `PVADLIC006` / `PVADLIC007` | Check artifact provenance and approved shipped signing key; never substitute a customer key |
| `PVADLIC008` | Check signed start dates and the deployment clock |
| `PVADLIC009` | Obtain a valid replacement or use a perpetually covered release where applicable |
| `PVADLIC010` | Warn that the subscription is in grace and arrange renewal before its end |
| `PVADLIC011` | Use a covered release or obtain an extended signed update cutoff |
| `PVADLIC012` | Check exact product, domain/kind and canonical provider grants |
| `PVADLIC013` | Check application identity, configured path/stream and read permissions without logging the path |
| `PVADLIC014` / `PVADLIC015` | Follow approved revocation/replacement procedures; do not reset or remove history |

Missing rights do not add a watermark or intentionally corrupt the output.
Handle the failure before returning a download, billing a completed operation,
or retrying a job indefinitely. Retrying an unchanged invalid license is not a
renewal strategy. Preserve only the diagnostic, library version and safe
operation context for support; exclude license contents and document data.

## Verification Scope

The guide's complete C# class is extracted through a Markdown parser, compiled
against real source-project dependencies in an isolated synthetic-trust build,
and executed against valid, wrong-product, expired-replacement and revoked
license cases. Generated packages/keys are not published or installed into
normal product output directories. Core tests separately cover exact Trial,
subscription/grace and perpetual-update boundaries. This is not production key
provisioning, package-publication approval or new Windows/Linux acceptance.
