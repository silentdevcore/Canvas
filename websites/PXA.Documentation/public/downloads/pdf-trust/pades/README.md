# PXA PAdES Interoperability Corpus

This directory contains synthetic PXA-generated PAdES B-T, B-LT, and B-LTA
documents and the machine-readable reports produced by EU Digital Signature
Services (DSS) 6.4.

The reports confirm the exact requested profile, valid PDF signature structure,
and a `TOTAL_PASSED` validation indication. B-T is validated with the supplied
offline CRL. B-LT and B-LTA carry the required validation material in the PDF.

The certificate hierarchy is synthetic and is not trusted outside this corpus.
Only the public root certificate and CRL are retained. No private key, password,
customer document, production certificate, or operational credential is included.

The [negative trust corpus](negative/) independently distinguishes expired and
revoked certificates, untrusted chains, unavailable offline revocation evidence,
and tampered LT or LTA validation material.

Regenerate and independently validate a new candidate corpus from the repository
root with:

```bash
tools/pdf-trust-pades-interop/generate-and-validate.sh
```

Review the generated reports and hashes before replacing these immutable files.
