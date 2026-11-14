# Windows x64 Raster Review

The repository owner confirmed viewing and approving all twenty-two Windows/x64
200% comparisons on 2026-11-15 (Europe/Berlin). The exact confirmation is recorded
in `approval.json`.

The record covers eleven font-family, six date-layout and five catalog cases.
Backend, Canvas, Preview and generated C# crops are preserved for every case
(88 images), with file/pixel hashes, fixture hashes, thirteen font hashes and
the original Windows Server 2025 runner environment.

The successful source is 76ad7101 in workflow run 43650032284. The first Windows
attempt failed the 1973 Kathmandu timezone vector before visual comparison;
the pinned IANA provider corrected that behavior. This record approves only
the corrected, successfully tested source images.

Native raster/ClearType differences remain measured P2 findings. No thresholds
or other platform baselines are changed, and future changed pixels require
review. This is not certification of other Windows/browser versions, customer
authentication/persistence, native print dialogs or a Windows production sandbox.

Display each crop at twice its recorded dimensions to repeat the review.
The original local page is
`artifacts/platform-parity-runs/43650032284/windows-x64-review.html`.
