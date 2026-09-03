# PDF Redaction Recovery Harness

This opt-in security suite applies PXA redaction to synthetic text and masked-image
fixtures, then searches the rewritten PDF with tools outside the PXA parser.

Requirements:

- Poppler `pdftotext`
- Poppler `pdfimages`
- POSIX `strings`

Run from the repository root:

```bash
bash tools/pdf-redaction-recovery/run.sh
```

The fixtures contain unique synthetic canaries. The suite fails when text extraction,
binary string inspection, or extracted image/mask files contain a covered canary. This
suite supplements PXA's structural verifier; it does not claim universal recovery
resistance for unsupported codecs or arbitrary hostile PDFs.
