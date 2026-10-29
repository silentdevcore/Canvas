# Pinned Independent Validator

CI builds `pvad-verapdf:1.31.166` locally instead of relying on the unavailable
historical registry tag. This does not update the validator version, rewrite
past evidence, or publish a new container.

```sh
sh tools/pdf-trust/build-verapdf.sh
PVAD_VERAPDF_IMAGE=pvad-verapdf:1.31.166 tools/pdfua/validate-pdfua.sh example.pdf
PVAD_VERAPDF_IMAGE=pvad-verapdf:1.31.166 tools/pdfa/validate-pdfa.sh 2b example.pdf
```

The build uses the [manufacturer's installer and automated-install procedure](https://github.com/veraPDF/veraPDF-apps/blob/integration/Dockerfile):

- Installer: `https://software.verapdf.org/develop/1.31/verapdf-greenfield-1.31.166-installer.zip`
- SHA-256: `25cc4d87daf1c6eb9efd40487141a03f23bb65a92ba59e49e0f33a01343038da`
- Java 11 JRE image: digest pinned in `Dockerfile`, Linux amd64 only.
- Only the CLI pack is installed. The resulting validator runs as a non-root user.
- The build checks the installer before execution and verifies `--version` after building.

A missing download, changed checksum, wrong version, or failed standards check
must fail CI. There is no fallback to `latest`, no profile substitution, and no
change to approved visual thresholds or external evidence. Future validator or
Java runtime updates require a separate review and a repeat of the conformance
matrix. Existing validator-image environment overrides remain supported.
