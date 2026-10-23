# PVADE PDF Trust External Producer Corpus

The page and guide use current platform branding. Historical report fields,
diagnostic identifiers, PDF bytes, hashes and signing evidence remain unchanged.

This corpus contains synthetic documents created locally with independent PDF
producers. It contains no customer files or copied third-party document content.

The checked-in PDFs are immutable interoperability evidence:

- Chromium 152 with Skia/PDF m152
- LibreOffice 26.2.3.2
- qpdf 12.2.0 with AES-256/R6 and Unicode passwords
- pyHanko 0.37.0 with a PAdES Baseline B-B signature
- LibreOffice PDF/A-2b validated by veraPDF CLI 1.31.166
- LibreOffice PDF/UA-1 validated by veraPDF CLI 1.31.166

Each PDF has a stable recorded trust report. `external-corpus.json` records the
producer evidence, SHA-256 hash, and byte length. The producer marker is also verified
directly against the PDF metadata during tests. Independent validation evidence is
published beside the applicable document and pinned by SHA-256.

Refresh only the copies and expected reports from repository-owned source fixtures with:

```bash
dotnet run --project tools/pdf-trust-external-corpus/PVAD.PdfTrustExternalCorpus.csproj
```

Regenerate all external profiles and their validator evidence with:

```bash
tools/pdf-trust-external-corpus/generate-external-profiles.sh
```

The generation script uses ephemeral signing keys and stores no private key. The
encrypted fixture uses the synthetic reader password `PXA-reader-ä漢字`.

This positive baseline does not cover PAdES B-T/B-LT/B-LTA, revoked or expired
certificates, incorrect passwords, damaged encryption dictionaries, or non-conforming
PDF/A and PDF/UA failure cases.
