# PXA PDF/UA Manual Accessibility Review

This review record complements the automated PDF/UA-1 preflight and the pinned
veraPDF report. It is an evidence template, not a conformance claim. A record
must remain `not-run` until a reviewer has tested the exact fixture with the
listed platform and assistive technology.

## Procedure

1. Generate a deterministic PDF/UA-1 fixture and record its SHA-256.
2. Run `tools/pdfua/validate-pdfua.sh` and retain the XML report beside the PDF.
3. Open the exact PDF in the supported desktop PDF viewer.
4. Complete every applicable check with keyboard-only navigation and a named
   screen reader. Record observed output, not a general statement that the file
   "looks accessible".
5. Save the completed record using `matterhorn-evidence.schema.json`.
6. Keep `not-run` or `blocked` for checks that were not actually exercised.

The controls below are PXA evidence controls mapped to the relevant Matterhorn
Protocol areas. They do not replace a formal Matterhorn Protocol assessment by
an accessibility specialist.

## Evidence Controls

| Control | Category | Required evidence |
| --- | --- | --- |
| `keyboard-navigation` | Interaction | Tab order reaches every interactive control without a focus trap. |
| `focus-order` | Interaction | Focus follows the logical reading and interaction order. |
| `screen-reader-reading-order` | Structure | A screen reader announces content in logical order. |
| `document-language-and-title` | Document | Title and language are announced correctly. |
| `headings-and-paragraphs` | Structure | Heading levels and paragraph semantics are meaningful. |
| `alternative-text` | Non-text content | Images/charts have useful alternatives; decorative content is skipped. |
| `table-navigation` | Tables | Headers and cells expose correct row/column relationships. |
| `link-names` | Annotations | Links expose meaningful names and understandable destinations. |
| `form-labels-and-values` | Forms | Fields expose labels, required state, values, and groups. |
| `annotation-state-and-feedback` | Annotations | Interactive annotations expose purpose and state. |
| `zoom-and-reflow-review` | Usability | Essential content remains readable at supported zoom levels. |

## Recording Rules

- Use `pass` only when the expected evidence was observed on the named setup.
- Use `fail` when the check was exercised and the result is incorrect.
- Use `not-applicable` only with a note explaining why the fixture has no such
  content.
- Use `blocked` when the environment or unsupported viewer prevents a reliable
  check; name the missing capability in `notes`.
- Never include customer data, real email addresses, credentials, tokens, or
  private certificates in evidence.
- Keep the automated veraPDF XML and manual record associated by fixture SHA-256.

The checked-in JSON template intentionally contains no completed manual result.
It can be copied to a release evidence directory after a real review.
