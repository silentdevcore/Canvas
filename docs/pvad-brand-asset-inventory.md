# PVAD Brand Asset Inventory

## Identity And Current Fallback

The technical identity is PVAD. The platform and Designer display name is PVADE;
the PDF and Spreadsheet product names are PVADP and PVADS. The shared source is
`product-metadata/brand.json`.

All six application entrypoints currently use text branding. No approved symbol,
favicon, app-icon family or social-preview artwork is shipped in their sources.
Do not treat the absence of approved artwork as permission to publish a new logo.
The broken Designer `/vite.svg` favicon reference has been removed. Browser
default icons remain until approved files are available.

## Application Inventory

| Surface | Entrypoint | Current identity | Asset decision |
| --- | --- | --- | --- |
| Designer | `pvad-designer/index.html` | PVADE Designer | Text fallback; no Vite favicon |
| Company | `websites/PVAD.Company/index.html` | PVADE Company | Text fallback |
| Documentation | `websites/PVAD.Documentation/index.html` | PVADE Documentation | Text fallback |
| Demo | `websites/PVAD.Demo/index.html` | PVADE Demo | Text fallback |
| Account | `websites/PVAD.Account/index.html` | PVADE Account | Text fallback; noindex |
| Admin | `websites/PVAD.Admin/index.html` | PVADE Admin | Text fallback; noindex |

No application entrypoint references a web-app manifest or social-preview image.
Document-fidelity, Legal and native-project manifests elsewhere in the repository
are data contracts, not PWA manifests; they must not be renamed as branding files.
Designer is also noindex because it is an authenticated application rather than
a public marketing or documentation page.

## Screenshots And Non-Brand Media

- `websites/PVAD.Documentation/public/images/designer/`: three workflow captures
  (element favorites, content library and mixed page layout).
- `websites/PVAD.Documentation/public/images/pdf-trust/`: eight desktop/mobile
  Viewer workflow captures.
- `PVAD.WebApi/AdminDocumentation/images/`: eleven protected Admin captures.
  Keep these behind the authorized Admin documentation API; never copy them to
  Company or public Documentation as a branding migration.
- ImageAnalysis/OCR fixtures, corpus files, document attachments and user assets
  are test/customer material, not brand artwork. Leave their bytes unchanged.

This is a source/reference inventory, not artwork approval. Current-UI recapture
and privacy-review results are recorded below; the initial audit is historical.
Do not paint new names onto old captures or claim that captured UI has been
validated simply by renaming a file. Recheck provenance before replacing images.

## Initial Rename Audit - 2026-10-21

`node tools/configuration/rename-screenshot-audit.mjs` scans all 22 captures with
local Tesseract and writes only byte hashes and branding flags, never extracted
identity text. Ten protected Admin screenshots contain old PXA/Power Dox branding.
The dashboard was also visually inspected and confirms the finding. The mixed-page
Designer image was visually inspected as a geometry example without visible brand
text. Absence of an OCR match is not proof that any other screenshot is current.

At the initial audit the captures were unchanged. Do not replace captures with
edited labels or mock workflows. Admin recapture results follow below; public
images and protected Admin assets must remain in their separate locations.

### Admin Recapture Completed

The eleven Admin captures have now been replaced using the actual Admin and API
with an isolated, migrated PostgreSQL database and a synthetic development
administrator. No authentication or workspace API was mocked. After the expected
anonymous session-bootstrap 401, login succeeds and all captured workspace API
requests must succeed. Screenshots are staged until the complete capture passes;
email text is masked with a neutral color. No password, API secret or license
material is captured. Desktop users and the mobile dashboard were visually
inspected; all Admin files remain exclusively in the protected asset directory.

Run after building the API and generating a matching migration SQL script:

```bash
PVAD_RUN_ADMIN_CAPTURE=1 node tools/configuration/capture-admin-rename.mjs
```

The default SQL input is `artifacts/rename/readiness-schema.sql`; override it with
`PVAD_ADMIN_CAPTURE_SCHEMA`. Development bootstrap rejects pending migrations.
Ports 16086/16177 must be free; the tool never reuses or stops existing hosts.
Only its own PostgreSQL container and application processes are cleaned up.
Evidence and synthetic key material stay in ignored `artifacts/rename/admin-capture`.

### Public Screenshot Follow-up

Earlier manual inspection found old logo pixels missed by OCR. The follow-up
`node tools/configuration/capture-viewer-rename.mjs` regenerates eight Viewer
desktop/mobile images and two Editor images from the current UI. All three
browser cases must pass before the tool copies any image into public assets.
Capture logs, hashes and provenance stay in ignored
`artifacts/rename/viewer-captures/`. These captures use synthetic API fixtures;
their public captions explicitly distinguish interface demonstrations from real
signature validation, conformance or redaction verification. They do not replace
the separate authenticated backend tests. The two Editor captures and Viewer
trust capture were visually inspected. The unbranded mixed-layout geometry
illustration remains unchanged and is not evidence of application chrome.

## Release Gates

1. Obtain approval for PVADE artwork and its PVADP/PVADS product variants.
2. Generate and verify SVG/PNG/ICO, small-size contrast, light/dark variants and
   any required app-manifest icons from the approved masters.
3. Add social artwork only to public surfaces; do not expose private Admin images.
4. Recapture and review documentation screenshots against the renamed UI.
5. Verify published asset URLs, content types and caching through the real proxy.

See the [rename tracker](../checklists/Rename-PXA-To-PVAD.md) and the historical
[logo-system roadmap](../checklists/PVAD.Brand-And-Logo-System.md). That roadmap's
old PXA names are superseded by the PVAD/PVADE identity above; its approval tasks
remain separate from the completed inventory.
