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
| Ukrainian OCR model | Byte-exact truncated prefix of the pinned official model; supplemental evidence below | Incomplete model, no intact-file source/license match or usability approval |
| Fifteen local macOS dylibs | Matching local Homebrew UUIDs and named sections; optional ignored files stay outside Git | Correlation is not whole-file identity, source-build reproduction or component/distribution approval |

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

## Supplemental Source Evidence

The [evidence index](source-evidence.v1.json) pins the unchanged original
registry and the observations in `evidence/`: the two original observations and,
since 2026-11-22, three supplemental records derived from them or from the local
NuGet cache. These records supplement,
but do not rewrite, the original unresolved classifications or shipped notice
bundles. All legal and publication approval flags remain false.

### OCR Model

The 2,891,776-byte Ukrainian file is the exact prefix of the 3,825,102-byte
[official fast model at a fixed commit](https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/87416418657359cb625c412a48b6e1d6d41c29bd/ukr.traineddata).
It is missing **933,326 bytes**. Four of its declared component offsets exceed
the local file length. The upstream file passes the bounded directory check;
that check alone is not an OCR quality or complete model-validation test.
SHA256, Git blob ID and lengths are independently checked against downloaded
reference bytes. The bundled file is not repaired, replaced or relabeled with
another asset's license. Usability/integrity remediation is a deferred task.

### Local Native Libraries

All fifteen existing x64 dylibs correlate with installed Homebrew candidates
through matching build UUIDs and every named file-backed section, including
zero-fill section metadata. None has an identical whole-file SHA256. These
records do not establish exactly which load-command, signature or other edits
produced the local binaries. Only Apple metadata inspection and byte comparison
are used; none of the libraries is executed.

**Supplement (2026-11-22):** `evidence/macos-build-differences.v1.json` locates
every differing byte. For all fifteen libraries, the files have equal lengths
and every difference lies in the Mach-O header and load commands. That is the
local install-name rewrite to `@loader_path`, recorded per library. No code
signature exists on either side, and no byte outside that metadata differs.
This explains the hash difference; it does not authenticate the bottle.

Installed candidate versions include Tesseract 5.5.2 and Leptonica 1.87.0,
despite the local filenames `libtesseract50` and `libleptonica-1.82.0`.
Receipt hashes are retained without private receipt paths. A local Homebrew
receipt is not publisher authentication, a reproducible source build or a full
inventory of statically linked components. Component notices and distribution
review remain open.

### Liberation Sans Source Archives

The Debian source descriptor and original/Debian archives for
`fonts-liberation` **1:1.07.4-11** were downloaded from the
[official package pool](https://deb.debian.org/debian/pool/main/f/fonts-liberation/)
and pinned by length and SHA256. Archive sizes/hashes agree with the descriptor.
The original archive includes FontForge sources and build scripts. This narrows
source discovery, but no rebuild or trusted-key Debian signature verification
has been performed. The existing GPL v2, exceptions and additional terms remain
unchanged; corresponding-source fulfillment and redistribution are not approved
by these technical records.

**Supplement (2026-11-22):** `verify-downloads` now recomputes every SHA-256,
SHA-1 and MD5 checksum and size that the descriptor declares, and requires the
supplied archives to equal its list. The clearsigned descriptor names signer
fingerprint `B8BF54137B09D35CF026FE9D091AB856069AAA1C`. That value is read from
the signature packet only. The signature remains **unverified**, with status
`blocked-no-authenticated-keyring`: no authenticated Debian keyring is available,
and a downloaded key is not trusted. A reproducible build is also unavailable
locally, because the sources are FontForge SFD files and FontForge is not
installed. The older Sans files keep their own terms; they are not reclassified
under the newer Serif/Mono agreement.

### Existing Local Container

The retained observation covers one **local Linux/ARM64** image by immutable
image ID, not a current-release image, a registry publication or an amd64 build.
It records **162 installed packages** and **176 original notice entries**.
Four packages lack a directly mapped copyright entry; the OpenSSL entry is an
unresolved link/empty archive entry. These **five** mappings remain open, as do
all component/source obligations and managed/static native runtime attribution.
The original notice tar is retained only in the local artifact directory, not
presented as a distributable notice bundle.

**Supplement (2026-11-22):** `evidence/container-notice-links.v1.json` resolves
all **five** mappings from the retained archive's own tar metadata. The archive
hash equals the historical record.
- `libgcc-s1`, `libgomp1` and `libstdc++6` are directory links to `gcc-14-base`.
- `libncursesw6` is a directory link to `libtinfo6`.
- OpenSSL's `copyright` is a file link to `../libssl3t64/copyright`.
- Each target is the original text of an installed package with the same source
  and source version, and its hash equals the historical notice record.
- The three linked `common-licenses` entries resolve to `GPL-3`, `LGPL-3` and
  `GFDL-1.3`.

The resolver never extracts entries or follows host links. It rejects traversal,
absolute or escaping links, cycles, duplicates, oversized members and unsupported
entry types. The historical observation is unchanged. A resolved link is an
attribution source, not an agreement or approval.

Recorded chains are also replayed to check that every hop reaches the declared
member with the declared link kind/status. This offline consistency check does
not reobserve original archive metadata; `verify-container-links` independently
recomputes the result from the hash-pinned archive. The archive reader requires
two zero end blocks and rejects nonzero trailing or concatenated content.

### Native NuGet Components

`evidence/native-components.v1.json` scans the 45 verified native payloads of the
existing restore review for fixed identification strings. It compares them with
the component sections of the retained package and repository notices:
- SkiaSharp names every observed component (libpng, libjpeg-turbo, zlib, expat).
- **Tesseract 5.2.0** carries markers for Leptonica 1.82.0, Tesseract, LibTIFF
  4.4.0, libjpeg-turbo 2.1.4, zlib 1.2.13 and liblzma. The package ships no
  notice file, and the repository copy holds only the wrapper license, so these
  six components remain **unresolved**.

A marker is a hint, not proof, and a missing marker does not prove absence.
Summary versions and asset counts must agree with the exact union of retained
payload markers. This does not replace source authentication or attribution.

The snapshot records the then-current Dockerfile hash but explicitly denies a
verified image-to-Dockerfile binding. No application or port is started. Three
ephemeral read-only, non-root, network-disabled commands query package metadata,
OS identity and original notices. Only uniquely labeled observer-owned
containers may be cleaned up; their removal is verified.

### Verification Commands

```bash
node tools/native-licensing/runtime-source-evidence.mjs
node --test tools/native-licensing/runtime-source-evidence.test.mjs
node tools/native-licensing/runtime-source-evidence.mjs verify-downloads artifacts/native-runtime-provenance/source-downloads
node tools/native-licensing/runtime-source-evidence.mjs observe-macos
node tools/native-licensing/runtime-source-evidence.mjs observe-container sha256:fcc0bd274ce6849fa0c6dd65962b7f1524576b783c02fdc07b6ed548859cfc63
node tools/native-licensing/runtime-source-evidence.mjs explain-macos
node tools/native-licensing/runtime-source-evidence.mjs resolve-container-links artifacts/native-runtime-provenance/1795374811152-19192c2c/os-notices.tar
node tools/native-licensing/runtime-source-evidence.mjs verify-container-links artifacts/native-runtime-provenance/1795374811152-19192c2c/os-notices.tar
node tools/native-licensing/runtime-source-evidence.mjs observe-native-components artifacts/native-dependency-inspection/local-restore-review.json ~/.nuget/packages
```

The default check is offline and checks local bindings and pinned historical
observations. It does not assert that references were downloaded again.
`verify-downloads` requires the separately acquired pinned references and
recomputes their hashes and actual model comparison; it does not fetch data.
The observation commands require actual local macOS/Homebrew or the existing
Docker image and write fresh reports under ignored `artifacts/`. They never
silently update the committed evidence. Clean CI uses the offline tests and
does not require the local image, reference downloads or optional macOS files.

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
