# PVAD PAdES Interoperability Validation

This tool generates synthetic PVAD PAdES B-T, B-LT, and B-LTA documents and
validates them with the standards-backed EU Digital Signature Services 6.4
validator in an isolated container.

The internal Java package is `com.pvad.tools`, the Maven artifact is
`pvad-dss-validator`, and new local builds use `pvad-dss-validator:6.4`.
These are repository tool identities, not claims of Maven registry ownership.
The package, shaded main class, JAR copy path and local image tag were renamed
together. This source-only follow-up has not been built or tested.
Existing corpus metadata may retain the old tool image as provenance; do not
rewrite signed documents or archived reports to change that historical name.

```bash
tools/pdf-trust-pades-interop/generate-and-validate.sh
```

The synthetic hierarchy contains one root CA and separate signer and timestamp
authority certificates. A current CRL provides offline revocation evidence. The
container trusts only the generated root, has no network access, uses a read-only
filesystem, and writes XML evidence to `artifacts/pdf-trust-pades-interop`.

The checked-in corpus under
`websites/PVAD.Documentation/public/downloads/pdf-trust/pades` is intentionally
immutable. Generation uses cryptographically secure random keys, so a reviewed
replacement has different document and report hashes.

## Current Corpus Output

New generation uses `pvad-pades-*` filenames, canonical synthetic certificate
subjects and document metadata, `pvad-pades-corpus.json`, and the
`pvadClassifiedProfile` field. These names do not change profile classification,
signature algorithms, trust requirements or validation thresholds.

The read-only `corpus-contract.mjs` reader accepts both generated manifest
generations. It normalizes IDs and the classified-profile field in memory while
preserving file references, hashes and the source manifest. Conflicting profile
aliases and unknown or duplicate cases fail validation.

The negative-manifest builder accepts either complete evidence generation per
case. It writes canonical case IDs but retains the selected files and their
actual hashes. Ambiguous or partial generations are rejected; current PDFs are
never combined with old validation reports. Use a fresh output directory when
regenerating, rather than mixing archived and current runs.

The checked-in signed corpus and independent XML evidence are not regenerated
by a naming change. Tests against copied evidence establish reader compatibility,
not a new independent DSS validation. A fresh container validation is required
before any newly generated corpus can replace approved evidence.
