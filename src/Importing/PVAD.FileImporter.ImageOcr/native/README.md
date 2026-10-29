# Native OCR Libraries

This folder contains app-owned native libraries used by the embedded Tesseract
adapter.

Current macOS x64 bundle:

- `x64/libleptonica-1.82.0.dylib`
- `x64/libtesseract50.dylib`
- `x64/libarchive.13.dylib`
- `x64/libb2.1.dylib`
- `x64/libgif.dylib`
- `x64/libjpeg.8.dylib`
- `x64/liblz4.1.dylib`
- `x64/liblzma.5.dylib`
- `x64/libopenjp2.7.dylib`
- `x64/libpng16.16.dylib`
- `x64/libsharpyuv.0.dylib`
- `x64/libtiff.6.dylib`
- `x64/libwebp.7.dylib`
- `x64/libwebpmux.3.dylib`
- `x64/libzstd.1.dylib`

The dylibs are relinked to use `@loader_path` for non-system transitive
dependencies, so the macOS x64 test/runtime output does not depend on Homebrew
paths such as `/usr/local/opt/...`.

The API's Linux container uses distribution-maintained Tesseract and Leptonica
libraries instead of this macOS bundle. Its Dockerfile creates the wrapper's
expected `.so` names in the isolated worker and selects the library directory
for `amd64` or `arm64`. See the [container validation scope](../../../../docs/pvad-runtime-container-validation.md).

Additional standalone runtime bundles still need to be supplied for other
deployment RIDs, including macOS arm64, Linux outside that container, and
Windows x64. A Dockerfile architecture mapping alone is not a successful OCR
execution test for every RID.
