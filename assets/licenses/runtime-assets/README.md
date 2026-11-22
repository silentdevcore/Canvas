# Application-Owned Runtime Asset Notices

This registry records the actual fonts, OCR language models and optional local
native libraries used by PVAD. It supplements the separate NuGet dependency
notice registry. It does not grant legal approval, accept third-party terms or
certify a deployment's complete native-component inventory.

## Verified Sources

| Asset Group | Source Binding | Remaining Review |
| --- | --- | --- |
| Liberation Serif and Mono, eight faces | Existing 2.1.5 archive and per-face SHA256 pins | Preserve OFL notice and review distribution |
| Liberation Sans, four faces | Exact byte matches to `fonts-liberation_1.07.4-11_all.deb`, pinned archive and member hashes | Different license: vendor GPL v2 with font exceptions and additional terms; review corresponding-source and redistribution obligations |
| Noto Sans Arabic | Exact font bytes at a fixed upstream Git commit | Preserve both upstream and existing adjacent notices |
| Nineteen OCR models | Exact Git blob IDs at a fixed `tessdata_fast` commit, plus local SHA256 checks | Review distribution; does not cover the Ukrainian model |
| Ukrainian OCR model | Local SHA256 only; checked upstream datasets do not match | Exact source and applicable license unresolved |
| Fifteen local macOS dylibs | Local SHA256 only; optional, ignored files are not added to Git | Build provenance, exact versions and component notices unresolved |

The older Sans font name table reports **1.07.4**. The existing OFL text beside
the Serif/Mono fonts must not be used to relabel these older Sans files. The
Debian copyright file is preserved byte for byte, including its exceptions.
The additional original GPL v2 text comes from the separately identified
Liberation repository; that text does not prove the corresponding source of
the Debian binary build. The manifest records the separate source URLs.

The Ukrainian model remains in place, but has no fabricated license mapping.
Likewise ABI names such as `libtesseract50` do not prove an upstream version or
license. Known local dylibs may be absent on a clean clone. If present, changed
bytes or an unclassified native file fail the inventory guard.

## Reproducible Bundles

```bash
node tools/native-licensing/runtime-assets.mjs
node --test tools/native-licensing/runtime-assets.test.mjs
```

After reviewing an intentional source/notice update, regenerate committed
bundles with `node tools/native-licensing/runtime-assets.mjs --write`.
This command does not update source hashes, replace fonts/models or resolve
unknown provenance. The normal check rejects stale, additional or missing
generated notice files. Original line endings and whitespace are retained.

- `bundles/fonts`: copied to API build/publish output; also reproduced under
  Designer public `licenses/runtime-assets` for its production build.
- `bundles/ocr`: copied with the OCR library and worker, and included as
  copy-to-output NuGet content. Protected package documentation also includes
  this profile for packages whose resolved project graph contains OCR.
- Font files are not added as a dependency of native PDF-only libraries.

NuGet notices target the same framework as the package's other content, not an
`any` framework fallback that can be shadowed by framework-specific files.
The pack path is directory-only: NuGet appends the recursive source path once.
The protected consumer checks archive paths, copy declarations and actual output
bytes; the runner also checks original/protected worker bundles. Successful
archive creation alone is not evidence that a consumer receives the notices.

Inventories describe the catalog, not the selected RID files of a particular
installation. NuGet-native Skia/HarfBuzz/Tesseract components and operating-system
packages in Docker need separate attribution/source review. The API container's
distribution-maintained Tesseract models are not certified by this application
model inventory. Do not infer publication readiness from successful packaging.

See the [native licensing checklist](../../../checklists/PVAD.Native-Library-Licensing-And-Protection.md)
for overall progress, unresolved obligations and actual test evidence.
