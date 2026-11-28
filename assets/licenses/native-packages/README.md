# Native Package Notice Sources

This directory retains original upstream license and notice bytes for exact
NuGet package versions whose restored contents do not provide their main
license text. `manifest.v1.json` records package/archive and metadata hashes,
declared licensing metadata, repository identity, a fixed source commit and
every original file's URL, role and SHA-256 hash. Collection is not legal
approval or permission to redistribute.

Git text conversion is disabled for this directory so checkout on Windows,
Linux and macOS preserves the hash-pinned original bytes and line endings.
Whitespace-style exceptions apply only to the versioned vendor-file folders;
their hashes, not reformatting, enforce integrity. Application code, this guide
and the manifest retain normal whitespace checks.

Twenty-six package versions expose their source commit in their NuGet
metadata. Four use a version-matched upstream release tag because no embedded
commit is available:

| Package | Version | Upstream Tag |
| --- | --- | --- |
| ExcelNumberFormat | 1.1.0 | v1.1.0 |
| MathNet.Numerics.Signed | 5.0.0 | v5.0.0 |
| Tesseract | 5.2.0 | 5.2.0 |
| ZXing.Net | 0.16.9 | v0.16.9.0 |

The resolved tag commit is pinned, not fetched during package creation. This
does not prove that an upstream binary was reproducibly built from that tag.
The distinct `sourceBinding` value and `releaseTag` remain visible in package
notice inventories. No source commit is invented or silently substituted.

The package builder validates the exact installed archive, NuGet metadata,
declared license and repository binding before copying a source. Texts are
copied byte-for-byte under each dependency's `upstream/` directory, without
overwriting notices already supplied in the NuGet package. A changed package,
metadata record or source file fails the build. An unlisted version receives
no automatic fallback, and missing texts stay explicitly unresolved.

To maintain this registry:

1. Inspect the actual restored package, its metadata and original notices.
2. Resolve its declared source commit. If absent, review the exact upstream
   release tag and retain the weaker tag-based provenance explicitly.
3. Retrieve original license and notice files from that fixed commit, never a
   mutable default branch or a generic replacement license template.
4. Record the archive, metadata and source-text hashes. Preserve copyright,
   attribution, whitespace and encoding; do not rewrite original vendor text.
5. Review the manifest diff and rerun the notice tests and protected local-feed
   rehearsal before using the new record.

```bash
node --test tools/native-licensing/package-documents.test.mjs
node tools/native-licensing/joint-package-rehearsal.mjs --migrations
```

The NodaTime records agree with the separate existing version/commit/hash
provenance file. Neither registry changes that prior evidence. Native OCR
libraries, language data, fonts and OS/container attribution need their own
review; this is not a complete native distribution bill of materials. The
existing NPOI decision, vendor terms requiring acceptance and manufacturer
publication gates remain unchanged. No acceptance is granted by acquisition,
package generation or these tests.
