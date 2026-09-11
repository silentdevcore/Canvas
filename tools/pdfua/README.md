# PXA PDF/UA Validation

`validate-pdfua.sh` runs the pinned official veraPDF CLI container with the
PDF/UA-1 profile and retains one machine-readable XML report per input file.

```bash
PXA_PDFUA_REPORT_DIR=/tmp/pxa-pdfua-reports \
  tools/pdfua/validate-pdfua.sh document.pdf
```

Generate the maintained PXA fixtures and validate them with:

```bash
PXA_PDFUA_ARTIFACT_DIR=/tmp/pxa-pdfua-fixtures \
  dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj \
  --filter FullyQualifiedName~PdfUaGenerationTests

PXA_PDFUA_ARTIFACT_DIR=/tmp/pxa-pdfua-fixtures \
  dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj \
  --filter FullyQualifiedName~PdfComplianceExportTests.DesignerModel_ExportsAccessibleTableLinkAndFormAnnotations \
  -p:SkipOcrWorkerPackaging=true

PXA_PDFUA_REPORT_DIR=/tmp/pxa-pdfua-reports \
  tools/pdfua/validate-pdfua.sh /tmp/pxa-pdfua-fixtures/*.pdf
```

Override `PXA_VERAPDF_IMAGE` only for an intentional validator upgrade. The
default is `ghcr.io/verapdf/cli:v1.31.166`, matching the repository's PDF/A
validation pipeline.

veraPDF independently evaluates the machine-checkable PDF/UA-1 requirements.
Its successful result is necessary release evidence, but it is not a complete
accessibility claim. Human checks from the Matterhorn Protocol and manual tests
with assistive technology remain a separate release gate.

The maintained fixture set includes provider-specific structure tags mapped to
standard PDF 1.7 roles through `/RoleMap`. PXA retains the source namespace URI
in its design contract, but does not emit PDF 2.0 `/Namespaces` or `/NS` entries
in PDF/UA-1 files. Native structure namespaces are reserved for the evaluated
PDF/UA-2 milestone.

`test-validate-pdfua.sh` verifies the wrapper's pass, conformance-failure, and
missing-input behavior without starting Docker.
