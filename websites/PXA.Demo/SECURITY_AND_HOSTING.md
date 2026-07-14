# PXA.Demo Security And Hosting Notes

## Static Example Hosting
- MVP example files are hosted as static Vite public assets under `public/examples/<demo-id>/`.
- Each demo example should use the same file shape where possible:
  - `input.json`
  - `output.json`
  - `source.js`
- Example files must be non-sensitive, synthetic, and small enough to review in Git.
- Public example URLs are stable enough for `PXA.Company` and `PXA.Documentation` links.

## Upload And Live Migration Boundaries
- Upload and live migration demos are not enabled in the static MVP.
- Future upload demos must define:
  - allowed MIME types
  - maximum file size
  - maximum page or worksheet count where applicable
  - timeout limits
  - memory limits
  - clear error states
- Uploaded files must not be persisted by default.
- Uploaded files must not be sent to third-party services without an explicit product decision.
- Live migration demos should run in an isolated backend path with provider-specific diagnostics and no arbitrary code execution.

## MVP Policy
- Static examples are allowed.
- Browser-only form demos are allowed when they use synthetic data.
- Download links may point to generated JSON preview artifacts.
- Real PDF generation, file uploads, live provider migration, and persistent sessions remain post-MVP features.
