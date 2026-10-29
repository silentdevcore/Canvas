# PVAD PDF/UA Validation

`validate-pdfua.sh` runs the pinned official veraPDF CLI container with the
PDF/UA-1 profile and retains one machine-readable XML report per input file.

```bash
PVAD_PDFUA_REPORT_DIR=/tmp/pvad-pdfua-reports \
  tools/pdfua/validate-pdfua.sh document.pdf
```

Generate the maintained PVAD fixtures and validate them with:

```bash
PVAD_PDFUA_ARTIFACT_DIR=/tmp/pvad-pdfua-fixtures \
  dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj \
  --filter FullyQualifiedName~PdfUaGenerationTests

PVAD_PDFUA_ARTIFACT_DIR=/tmp/pvad-pdfua-fixtures \
  dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj \
  --filter FullyQualifiedName~PdfComplianceExportTests.DesignerModel_ExportsAccessibleTableLinkAndFormAnnotations \
  -p:SkipOcrWorkerPackaging=true

PVAD_PDFUA_REPORT_DIR=/tmp/pvad-pdfua-reports \
  tools/pdfua/validate-pdfua.sh /tmp/pvad-pdfua-fixtures/*.pdf
```

Override `PVAD_VERAPDF_IMAGE` only for an intentional validator upgrade. The
default is `ghcr.io/verapdf/cli:v1.31.166`, matching the repository's PDF/A
validation pipeline.

veraPDF independently evaluates the machine-checkable PDF/UA-1 requirements.
Its successful result is necessary release evidence, but it is not a complete
accessibility claim. Human checks from the Matterhorn Protocol and manual tests
with assistive technology remain a separate release gate.

Use `MATTERHORN-REVIEW.md` for the manual procedure and
`matterhorn-evidence.schema.json` plus `matterhorn-evidence.template.json` for
the structured evidence record. The checked-in template is deliberately
`not-run`; it must not be copied into release evidence as a passing review.

The maintained fixture set includes provider-specific structure tags mapped to
standard PDF 1.7 roles through `/RoleMap`. PVAD retains the source namespace URI
in its design contract, but does not emit PDF 2.0 `/Namespaces` or `/NS` entries
in PDF/UA-1 files. Native structure namespaces are reserved for the evaluated
PDF/UA-2 milestone.

`test-validate-pdfua.sh` verifies the wrapper's pass, conformance-failure, and
missing-input behavior without starting Docker.
