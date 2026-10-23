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
