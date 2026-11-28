# Tessdata

Place bundled Tesseract `.traineddata` files here, for example:

- `deu.traineddata`
- `eng.traineddata`

The converter loads OCR language data from the application deployment output, not
from a manually installed system Tesseract path.

Nineteen bundled models match a fixed official `tessdata_fast` tree; the
Ukrainian model does not match the checked upstream datasets and still requires
an exact source/license receipt before redistribution. The
[runtime asset registry](../../../../assets/licenses/runtime-assets/README.md)
pins each model independently and supplies the applicable original notice only
to the verified group. Do not attach that notice to the unresolved model or
silently replace model bytes. New models require reviewed source and hash
records before the inventory check passes. Container-provided distribution
models have a separate provenance boundary.
