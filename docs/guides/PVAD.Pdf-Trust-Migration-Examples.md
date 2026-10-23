# PDF Trust Migration Examples

PVAD includes synthetic migration-analysis fixtures for eleven prioritized PDF SDKs. The
fixtures exercise signing, timestamping, signature validation, encrypted input, secure
redaction, PDF/A conversion, and PDF/UA validation without embedding credentials or
customer documents.

> [!IMPORTANT]
> These fixtures model representative API shapes for testing PVAD migration. They are not
> executable vendor SDK tutorials and do not replace the provider's current documentation.

## Covered providers

| Provider | Migration key |
| --- | --- |
| Nutrient SDK | `nutrient` |
| iText 7 | `itext7` |
| Apryse | `apryse` |
| Aspose.PDF | `aspose-pdf` |
| Syncfusion PDF | `syncfusion-pdf` |
| DevExpress PDF | `devexpress-pdf` |
| Foxit PDF SDK | `foxit-pdf` |
| DsPdf | `dspdf` |
| GemBox.Pdf | `gembox-pdf` |
| Spire.PDF | `spire-pdf` |
| Pdftools | `pdftools` |

## Run an example

```csharp
var example = PdfTrustMigrationExamples.Get(PdfMigrationProviderKeys.IText7);
var result = PdfMigrationProviders.Migrate(example.ProviderKey, example.SourceCode);

foreach (var evidence in result.PdfTrust!.Evidence)
{
    Console.WriteLine($"{evidence.Capability}: {evidence.Disposition}");
    Console.WriteLine(evidence.DocumentationPath);
}
```

Every detected operation is deliberately reported as `reviewRequired` or `unsupported`.
PVAD does not claim automatic security equivalence when certificate stores, passwords,
timestamp authorities, trust policies, redaction guarantees, or compliance settings cannot
be inferred safely.

## Credential handling

The examples use only `certificateReference`, `timestampAuthorityReference`, and
`passwordReference` placeholders. Production migrations must resolve credentials at runtime
through the configured PVAD trust services. Never place private keys, passwords, tokens, or
certificate bytes in source code, templates, migration reports, logs, or telemetry.

For the corresponding PVAD workflows, see [PDF Trust Generator Examples](PVAD.Pdf-Trust-Generator-Examples.md).
