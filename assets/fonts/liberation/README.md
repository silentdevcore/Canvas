# Packaged Document Fonts

Serif and Mono are the eight unmodified faces from the official
[Liberation 2.1.5 release](https://github.com/liberationfonts/liberation-fonts/releases/tag/2.1.5).
Source archive and per-face SHA-256 values are recorded in
`serif-mono-provenance.json`. The original SIL Open Font License 1.1 is also
shipped in the Designer under `licenses/Liberation-2.1.5.LICENSE.txt`, with only
trailing whitespace and the redundant final blank line removed as recorded in
the provenance manifest.
The adjacent `LICENSE` covers these Serif and Mono faces, not the older Sans
faces; its OFL text is the same apart from whitespace.

The existing Sans files are intentionally unchanged. They predate this import
and are part of approved visual evidence; the new provenance record does not
claim that those bytes came from the newly downloaded archive.

The four Sans faces report version 1.07.4 and match the independently pinned
Debian `fonts-liberation_1.07.4-11_all.deb` archive byte for byte. Their vendor
license is GPL v2 with Liberation font exceptions and additional terms, not
the newer OFL. Exact original notices, binary/source evidence and unresolved
corresponding-source/distribution review are recorded in the
[runtime asset registry](../../licenses/runtime-assets/README.md).
API and Designer builds receive the distinct font notice bundle. No font bytes
or approved visual references were changed for this attribution checkpoint.

Browser aliases `PVADE Times` and `PVADE Courier` use these local files, while
the PDF font loader uses the same bytes for Times and Courier family requests.
No platform font installation or network font fetch is needed at runtime.
Native standard-font fallback remains available when an SDK caller supplies
no font loader. That fallback is not certified as cross-platform pixel parity.
