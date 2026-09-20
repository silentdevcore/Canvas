# PDF Security Interoperability

`aes256-interop.mjs` reopens the pinned AES-256 Revision 6 fixture with qpdf
and Poppler. It verifies Unicode user and owner passwords, rejects an incorrect
password, exercises metadata parsing, and extracts page content. The JSON report
contains only fixture hashes, tool versions, outcomes, exit codes, and durations.

Run from the repository root after installing qpdf and Poppler:

```bash
node tools/pdf-security/aes256-interop.mjs
```

When qpdf is unavailable locally, the runner uses the pinned
`pxa-pdf-trust-external-tools:1` container image. Set
`PXA_AES256_QPDF_IMAGE` to an equivalent reviewed image if required.

PXA's native suites separately cover every supported permission-bit combination,
encrypted metadata on and off, malformed encryption dictionaries, tampering, and
replacement encryption. Proprietary Acrobat and Foxit evidence remains a manual
release gate and is never inferred from these open-source checks.
