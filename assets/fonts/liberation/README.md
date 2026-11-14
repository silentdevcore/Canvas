# Packaged Document Fonts

Serif and Mono are the eight unmodified faces from the official
[Liberation 2.1.5 release](https://github.com/liberationfonts/liberation-fonts/releases/tag/2.1.5).
Source archive and per-face SHA-256 values are recorded in
`serif-mono-provenance.json`. The original SIL Open Font License 1.1 is also
shipped in the Designer under `licenses/Liberation-2.1.5.LICENSE.txt`, with only
trailing whitespace and the redundant final blank line removed as recorded in
the provenance manifest.
The adjacent `LICENSE` covers the API font distribution; its license text is
the same apart from whitespace.

The existing Sans files are intentionally unchanged. They predate this import
and are part of approved visual evidence; the new provenance record does not
claim that those bytes came from the newly downloaded archive.

Browser aliases `PVADE Times` and `PVADE Courier` use these local files, while
the PDF font loader uses the same bytes for Times and Courier family requests.
No platform font installation or network font fetch is needed at runtime.
Native standard-font fallback remains available when an SDK caller supplies
no font loader. That fallback is not certified as cross-platform pixel parity.
