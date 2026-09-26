using PXA.Pdf;
using PXA.Core.Contracts;
using PXA.Generator;
using System.Runtime.InteropServices;
using System.Text;

namespace PXA.Generator.Tests;

public sealed class PdfFacadeTests
{
    private const string FacturXXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rsm:CrossIndustryInvoice
            xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
            xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
          <rsm:ExchangedDocumentContext>
            <ram:GuidelineSpecifiedDocumentContextParameter>
              <ram:ID>urn:factur-x.eu:1p0:en16931</ram:ID>
            </ram:GuidelineSpecifiedDocumentContextParameter>
          </rsm:ExchangedDocumentContext>
        </rsm:CrossIndustryInvoice>
        """;

    private const string XRechnungXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:CustomizationID>urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0</cbc:CustomizationID>
        </Invoice>
        """;

    private const string SemanticCiiXml = """
        <rsm:CrossIndustryInvoice
            xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
            xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
          <rsm:ExchangedDocumentContext><ram:GuidelineSpecifiedDocumentContextParameter><ram:ID>urn:factur-x.eu:1p0:en16931</ram:ID></ram:GuidelineSpecifiedDocumentContextParameter></rsm:ExchangedDocumentContext>
          <rsm:ExchangedDocument><ram:ID>INV-2026-42</ram:ID><ram:IssueDateTime><ram:DateTimeString format="102">20260926</ram:DateTimeString></ram:IssueDateTime></rsm:ExchangedDocument>
          <rsm:SupplyChainTradeTransaction>
            <ram:IncludedSupplyChainTradeLineItem>
              <ram:AssociatedDocumentLineDocument><ram:LineID>1</ram:LineID></ram:AssociatedDocumentLineDocument>
              <ram:SpecifiedTradeProduct><ram:Name>Consulting</ram:Name></ram:SpecifiedTradeProduct>
              <ram:SpecifiedLineTradeDelivery><ram:BilledQuantity unitCode="HUR">2</ram:BilledQuantity></ram:SpecifiedLineTradeDelivery>
              <ram:SpecifiedLineTradeSettlement><ram:SpecifiedTradeSettlementLineMonetarySummation><ram:LineTotalAmount>100.00</ram:LineTotalAmount></ram:SpecifiedTradeSettlementLineMonetarySummation></ram:SpecifiedLineTradeSettlement>
            </ram:IncludedSupplyChainTradeLineItem>
            <ram:ApplicableHeaderTradeAgreement>
              <ram:SellerTradeParty><ram:ID>SELLER-1</ram:ID><ram:Name>Seller GmbH</ram:Name></ram:SellerTradeParty>
              <ram:BuyerTradeParty><ram:ID>BUYER-1</ram:ID><ram:Name>Buyer AG</ram:Name></ram:BuyerTradeParty>
            </ram:ApplicableHeaderTradeAgreement>
            <ram:ApplicableHeaderTradeSettlement>
              <ram:InvoiceCurrencyCode>EUR</ram:InvoiceCurrencyCode><ram:TaxTotalAmount currencyID="EUR">19.00</ram:TaxTotalAmount>
              <ram:SpecifiedTradeSettlementHeaderMonetarySummation><ram:LineTotalAmount>100.00</ram:LineTotalAmount><ram:TaxBasisTotalAmount>100.00</ram:TaxBasisTotalAmount><ram:GrandTotalAmount>119.00</ram:GrandTotalAmount><ram:DuePayableAmount>119.00</ram:DuePayableAmount></ram:SpecifiedTradeSettlementHeaderMonetarySummation>
            </ram:ApplicableHeaderTradeSettlement>
          </rsm:SupplyChainTradeTransaction>
        </rsm:CrossIndustryInvoice>
        """;

    [Fact]
    public void CreateDocument_ReturnsPxaPdfDocument()
    {
        var document = Pdf.CreateDocument();

        Assert.IsType<PdfDocument>(document);
        var page = document.AddPage(300, 180);
        page.DrawTextFromTop("PXA generator facade", 24, 24, 12);

        var bytes = document.ToBytes();

        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void CreateDocument_PreservesDefaultFontOption()
    {
        var document = Pdf.CreateDocument(PdfStandardFont.Courier);

        Assert.Equal(PdfStandardFont.Courier, document.DefaultFont);
    }

    [Fact]
    public void EInvoice_Validate_RecognizesFacturXCiiWithoutClaimingConformance()
    {
        var report = EInvoice.Validate(
            Encoding.UTF8.GetBytes(FacturXXml),
            FacturXOptions());

        Assert.True(report.IsStructurallyValid);
        Assert.False(report.IsConformanceValidated);
        Assert.Equal("CII", report.Syntax);
        Assert.Equal("urn:factur-x.eu:1p0:en16931", report.GuidelineIdentifier);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV000");
    }

    [Fact]
    public void EInvoice_Recognize_ExtractsCiiSemanticData()
    {
        var report = EInvoice.Recognize(Encoding.UTF8.GetBytes(SemanticCiiXml));

        Assert.True(report.IsRecognized);
        var data = Assert.IsType<PxaEInvoiceSemanticData>(report.SemanticData);
        Assert.Equal("INV-2026-42", data.InvoiceNumber);
        Assert.Equal("20260926", data.IssueDate);
        Assert.Equal("EUR", data.Currency);
        Assert.Equal("Seller GmbH", data.Seller.Name);
        Assert.Equal("Buyer AG", data.Buyer.Name);
        Assert.Equal(19.00m, data.TaxTotal);
        Assert.Equal(119.00m, data.PayableAmount);
        var line = Assert.Single(data.Lines);
        Assert.Equal("Consulting", line.Description);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal("HUR", line.UnitCode);
    }

    [Fact]
    public void EInvoice_Recognize_ExtractsUblSemanticData()
    {
        var xml = Encoding.UTF8.GetBytes("""
            <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
              <cbc:CustomizationID>urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0</cbc:CustomizationID><cbc:ID>XR-7</cbc:ID><cbc:IssueDate>2026-09-26</cbc:IssueDate><cbc:DocumentCurrencyCode>CHF</cbc:DocumentCurrencyCode>
              <cac:AccountingSupplierParty><cac:Party><cac:PartyName><cbc:Name>Swiss Seller AG</cbc:Name></cac:PartyName></cac:Party></cac:AccountingSupplierParty>
              <cac:AccountingCustomerParty><cac:Party><cac:PartyLegalEntity><cbc:RegistrationName>Customer SA</cbc:RegistrationName></cac:PartyLegalEntity></cac:Party></cac:AccountingCustomerParty>
              <cac:TaxTotal><cbc:TaxAmount>8.10</cbc:TaxAmount></cac:TaxTotal>
              <cac:LegalMonetaryTotal><cbc:LineExtensionAmount>100.00</cbc:LineExtensionAmount><cbc:TaxExclusiveAmount>100.00</cbc:TaxExclusiveAmount><cbc:TaxInclusiveAmount>108.10</cbc:TaxInclusiveAmount><cbc:PayableAmount>108.10</cbc:PayableAmount></cac:LegalMonetaryTotal>
              <cac:InvoiceLine><cbc:ID>1</cbc:ID><cbc:InvoicedQuantity unitCode="C62">1</cbc:InvoicedQuantity><cbc:LineExtensionAmount>100.00</cbc:LineExtensionAmount><cac:Item><cbc:Name>License</cbc:Name></cac:Item></cac:InvoiceLine>
            </Invoice>
            """);

        var data = Assert.IsType<PxaEInvoiceSemanticData>(EInvoice.Recognize(xml).SemanticData);
        Assert.Equal("XR-7", data.InvoiceNumber);
        Assert.Equal("Swiss Seller AG", data.Seller.Name);
        Assert.Equal("Customer SA", data.Buyer.Name);
        Assert.Equal("CHF", data.Currency);
        Assert.Equal(108.10m, data.TaxInclusiveTotal);
        Assert.Equal("License", Assert.Single(data.Lines).Description);
    }

    [Fact]
    public void EInvoice_GenerateXml_CreatesDeterministicCiiWithCalculatedTotals()
    {
        var invoice = TypedInvoice();

        var first = EInvoice.GenerateXml(invoice, FacturXOptions());
        var second = EInvoice.GenerateXml(invoice, FacturXOptions());

        Assert.True(first.IsGenerated);
        Assert.Equal(first.Xml.ToArray(), second.Xml.ToArray());
        Assert.Equal("CII", EInvoice.Recognize(first.Xml).Syntax);
        Assert.Equal("INV-2026-100", first.SemanticData?.InvoiceNumber);
        Assert.Equal(200.00m, first.SemanticData?.LineNetTotal);
        Assert.Equal(38.00m, first.SemanticData?.TaxTotal);
        Assert.Equal(238.00m, first.SemanticData?.PayableAmount);
        Assert.Equal(2, first.SemanticData?.Lines.Count);
    }

    [Fact]
    public void EInvoice_GenerateXml_CreatesUblXRechnung()
    {
        var result = EInvoice.GenerateXml(TypedInvoice(), new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.XRechnung,
            Version = EInvoice.CurrentXRechnungVersion,
            Profile = "XRechnung"
        });

        Assert.True(result.IsGenerated);
        var recognition = EInvoice.Recognize(result.Xml);
        Assert.Equal("UBL-Invoice", recognition.Syntax);
        Assert.Equal("XRECHNUNG", recognition.Profile);
        Assert.Equal("CHF", recognition.SemanticData?.Currency);
        Assert.Equal("PXA Customer AG", recognition.SemanticData?.Buyer.Name);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    [Fact]
    public void EInvoice_GenerateXml_RejectsInvalidBusinessData()
    {
        var invoice = TypedInvoice() with
        {
            DueDate = new DateOnly(2026, 9, 1),
            Lines =
            [
                new("1", "Invalid", 0, "C62", 10m, 120m),
                new("1", "Duplicate", 1, "C62", 10m, 19m)
            ]
        };

        var result = EInvoice.GenerateXml(invoice, FacturXOptions());

        Assert.False(result.IsGenerated);
        Assert.True(result.Xml.IsEmpty);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV112");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV113");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV115");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV116");
    }

    [Fact]
    public void EInvoice_CreatePdf_CreatesPdfA3HybridInvoice()
    {
        var fonts = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));

        var result = EInvoice.CreatePdf(TypedInvoice(), FacturXOptions(), fonts);

        Assert.True(result.IsGenerated, string.Join(" | ", result.Diagnostics.Select(static item => item.Message)));
        Assert.Equal("%PDF"u8.ToArray(), result.Pdf.Span[..4].ToArray());
        Assert.Contains("/AF [", Encoding.ASCII.GetString(result.Pdf.Span));
        Assert.Equal("INV-2026-100", EInvoice.Recognize(result.Xml).SemanticData?.InvoiceNumber);
    }

    [Fact]
    public void EInvoice_Attach_WritesPdfA3AssociatedFileStructureAndPayload()
    {
        var document = Pdf.CreateDocument();
        document.AddPage();
        var xml = Encoding.UTF8.GetBytes(FacturXXml);

        var associated = EInvoice.Attach(document, xml, FacturXOptions());
        var bytes = document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA3B
        });
        var pdfText = Encoding.ASCII.GetString(bytes);

        Assert.Equal("factur-x.xml", associated.FileName);
        Assert.Equal(PdfAssociatedFileRelationship.Alternative, associated.Relationship);
        Assert.Contains("/Type /EmbeddedFile", pdfText);
        Assert.Contains("/Subtype /application#2Fxml", pdfText);
        Assert.Contains("/AFRelationship /Alternative", pdfText);
        Assert.Contains("/Names << /EmbeddedFiles", pdfText);
        Assert.Contains("/AF [", pdfText);
        Assert.Contains("urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#", pdfText);
        Assert.Contains("<fx:DocumentType>INVOICE</fx:DocumentType>", pdfText);
        Assert.Contains("<fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>", pdfText);
        Assert.Contains("<fx:Version>1.0</fx:Version>", pdfText);
        Assert.Contains("<fx:ConformanceLevel>EN 16931</fx:ConformanceLevel>", pdfText);
        Assert.Contains("<pdfaProperty:name>DocumentFileName</pdfaProperty:name>", pdfText);
        Assert.True(bytes.AsSpan().IndexOf(xml) >= 0);
    }

    [Fact]
    public void EInvoice_Attach_WritesZugferdXmpValues()
    {
        var document = Pdf.CreateDocument();
        document.AddPage();
        EInvoice.Attach(document, Encoding.UTF8.GetBytes(FacturXXml), new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.Zugferd,
            Version = EInvoice.CurrentZugferdVersion,
            Profile = "EN16931"
        });

        var pdf = Encoding.ASCII.GetString(document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA3B
        }));

        Assert.Contains("urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#", pdf);
        Assert.Contains("<fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>", pdf);
        Assert.Contains("<fx:Version>1.0</fx:Version>", pdf);
        Assert.Contains("<fx:ConformanceLevel>EN 16931</fx:ConformanceLevel>", pdf);
    }

    [Fact]
    public void EInvoice_Attach_WritesXRechnungXmpValues()
    {
        var document = Pdf.CreateDocument();
        document.AddPage();
        EInvoice.Attach(document, Encoding.UTF8.GetBytes(XRechnungXml), new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.XRechnung,
            Version = EInvoice.CurrentXRechnungVersion,
            Profile = "XRechnung"
        });

        var pdf = Encoding.ASCII.GetString(document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA3B
        }));

        Assert.Contains("urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#", pdf);
        Assert.Contains("<fx:DocumentFileName>xrechnung.xml</fx:DocumentFileName>", pdf);
        Assert.Contains("<fx:Version>1.0</fx:Version>", pdf);
        Assert.Contains("<fx:ConformanceLevel>XRECHNUNG</fx:ConformanceLevel>", pdf);
    }

    [Theory]
    [InlineData("1.08", "EN 16931", null, "PXAEINV012")]
    [InlineData("1.09.2", "unknown", null, "PXAEINV013")]
    [InlineData("1.09.2", "EN 16931", "invoice.xml", "PXAEINV014")]
    [InlineData("1.09.2", "BASIC", null, "PXAEINV015")]
    public void EInvoice_Validate_RejectsInconsistentOptions(
        string version,
        string profile,
        string? fileName,
        string expectedCode)
    {
        var report = EInvoice.Validate(
            Encoding.UTF8.GetBytes(FacturXXml),
            new PxaEInvoiceOptions
            {
                Standard = PxaEInvoiceStandard.FacturX,
                Version = version,
                Profile = profile,
                FileName = fileName
            });

        Assert.False(report.IsStructurallyValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public void EInvoice_Attach_IsRejectedForPdfA2()
    {
        var document = Pdf.CreateDocument();
        document.AddPage();
        EInvoice.Attach(document, Encoding.UTF8.GetBytes(FacturXXml), FacturXOptions());

        var exception = Assert.Throws<PdfComplianceException>(() =>
            document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA2B }));

        Assert.Contains(exception.Report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFA007");
    }

    [Fact]
    public void EInvoice_Validate_RejectsDtdAndExternalEntityInput()
    {
        var xml = Encoding.UTF8.GetBytes(
            "<!DOCTYPE invoice [<!ENTITY external SYSTEM \"file:///etc/passwd\">]>" +
            "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\">" +
            "<CustomizationID>&external;</CustomizationID></Invoice>");

        var report = EInvoice.Validate(xml, new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.XRechnung,
            Version = EInvoice.CurrentXRechnungVersion,
            Profile = "XRechnung"
        });

        Assert.False(report.IsStructurallyValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV003");
    }

    [Fact]
    public void EInvoice_Validate_RejectsStandardMismatch()
    {
        var report = EInvoice.Validate(
            Encoding.UTF8.GetBytes(FacturXXml),
            new PxaEInvoiceOptions
            {
                Standard = PxaEInvoiceStandard.XRechnung,
                Version = EInvoice.CurrentXRechnungVersion,
                Profile = "XRechnung"
            });

        Assert.False(report.IsStructurallyValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV011");
    }

    [Fact]
    public void AddAssociatedFile_RejectsDuplicateNamesAndCopiesInput()
    {
        var document = Pdf.CreateDocument();
        var content = new byte[] { 1, 2, 3 };

        var associated = document.AddAssociatedFile(content, "source.xml", "application/xml");
        content[0] = 9;
        Assert.True(MemoryMarshal.TryGetArray(associated.Content, out var returned));
        returned.Array![returned.Offset] = 8;

        Assert.Equal(1, associated.Content.Span[0]);
        Assert.False(document.AssociatedFiles is List<PdfAssociatedFile>);
        Assert.Throws<ArgumentException>(() =>
            document.AddAssociatedFile(new byte[] { 4 }, "SOURCE.XML", "application/xml"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            document.AddAssociatedFile(new byte[] { 4 }, "other.xml", "application/xml", (PdfAssociatedFileRelationship)999));
    }

    [Fact]
    public void AddAssociatedFile_WritesNameTreeInFilenameOrder()
    {
        var document = Pdf.CreateDocument();
        document.AddPage();
        document.AddAssociatedFile(new byte[] { 2 }, "z.xml", "application/xml");
        document.AddAssociatedFile(new byte[] { 1 }, "a.xml", "application/xml");

        var pdf = Encoding.ASCII.GetString(document.ToBytes());

        Assert.True(
            pdf.IndexOf("0061002E0078006D006C", StringComparison.Ordinal) <
            pdf.IndexOf("007A002E0078006D006C", StringComparison.Ordinal));
    }

    [Fact]
    public void DirectPxaPdfConstructor_StillGeneratesPdf()
    {
#pragma warning disable PXA0001 // This test intentionally verifies the low-level PXA.Pdf constructor remains usable.
        var document = new PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage(300, 180);
        page.DrawTextFromTop("Direct PXA.Pdf constructor", 24, 24, 12);

        var bytes = document.ToBytes();

        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void CreateWorkbook_ReturnsSpreadsheetWorkbook()
    {
        var workbook = Spreadsheet.CreateWorkbook("Sales");
        var sheet = workbook.AddSheet("Q1");
        sheet.Cell("A1").Value("Revenue");
        sheet.Cell("B1").Value(42);

        var bytes = workbook.ToXlsx();

        Assert.Equal("Sales", workbook.Name);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void ExportWord_ReturnsDocxBytes()
    {
        var design = new DesignExportDto
        {
            Name = "PXA Word",
            Pages =
            [
                new PageDto
                {
                    Elements =
                    [
                        new ElementDto
                        {
                            Type = "text",
                            X = 24,
                            Y = 24,
                            Width = 180,
                            Height = 32,
                            Content = "PXA generator facade",
                        }
                    ]
                }
            ]
        };

        var bytes = Word.Export(design);

        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    private static PxaEInvoiceOptions FacturXOptions() =>
        new()
        {
            Standard = PxaEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "EN 16931"
        };

    private static PxaEInvoiceDocument TypedInvoice() => new()
    {
        InvoiceNumber = "INV-2026-100",
        IssueDate = new DateOnly(2026, 9, 26),
        DueDate = new DateOnly(2026, 10, 26),
        Currency = "CHF",
        BuyerReference = "ORDER-42",
        PaymentReference = "INV-2026-100",
        Seller = new(
            "Power Dox Automation AG",
            "CHE-SELLER",
            "CHE-123.456.789 MWST",
            "seller@example.test",
            new("Example Street 1", "Zurich", "8000", "CH")),
        Buyer = new(
            "PXA Customer AG",
            "CHE-BUYER",
            null,
            "buyer@example.test",
            new("Customer Street 2", "Bern", "3000", "CH")),
        Lines =
        [
            new("1", "PXA Premium license", 1, "C62", 120m, 19m),
            new("2", "Implementation service", 2, "HUR", 40m, 19m)
        ]
    };
}
