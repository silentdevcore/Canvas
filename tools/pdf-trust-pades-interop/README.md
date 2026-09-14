# PXA PAdES Interoperability Validation

This tool generates synthetic PXA PAdES B-T, B-LT, and B-LTA documents and
validates them with the standards-backed EU Digital Signature Services 6.4
validator in an isolated container.

```bash
tools/pdf-trust-pades-interop/generate-and-validate.sh
```

The synthetic hierarchy contains one root CA and separate signer and timestamp
authority certificates. A current CRL provides offline revocation evidence. The
container trusts only the generated root, has no network access, uses a read-only
filesystem, and writes XML evidence to `artifacts/pdf-trust-pades-interop`.

The checked-in corpus under
`websites/PXA.Documentation/public/downloads/pdf-trust/pades` is intentionally
immutable. Generation uses cryptographically secure random keys, so a reviewed
replacement has different document and report hashes.
