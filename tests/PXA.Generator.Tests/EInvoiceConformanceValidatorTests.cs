using System.Security.Cryptography;
using System.Text;
using PXA.Generator;

namespace PXA.Generator.Tests;

public sealed class EInvoiceConformanceValidatorTests
{
    private const string InvoiceXml = """
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

    private const string Schema = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                   targetNamespace="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
                   xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
                   elementFormDefault="qualified">
          <xs:element name="CrossIndustryInvoice">
            <xs:complexType>
              <xs:sequence>
                <xs:any minOccurs="0" maxOccurs="unbounded" processContents="lax" />
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;

    private const string PassingSchematronXslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0"
            xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:svrl="http://purl.oclc.org/dsdl/svrl">
          <xsl:output method="xml" encoding="UTF-8" />
          <xsl:template match="/"><svrl:schematron-output /></xsl:template>
        </xsl:stylesheet>
        """;

    private const string FailingSchematronXslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0"
            xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:svrl="http://purl.oclc.org/dsdl/svrl">
          <xsl:output method="xml" encoding="UTF-8" />
          <xsl:template match="/">
            <svrl:schematron-output>
              <svrl:failed-assert location="/invoice"><svrl:text>Business rule failed.</svrl:text></svrl:failed-assert>
            </svrl:schematron-output>
          </xsl:template>
        </xsl:stylesheet>
        """;

    [Fact]
    public void Validate_ClaimsConformanceOnlyAfterPinnedXsdAndSchematronPass()
    {
        using var artifacts = ArtifactFixture.Create(PassingSchematronXslt);
        var validator = artifacts.CreateValidator();

        var report = validator.Validate(Encoding.UTF8.GetBytes(InvoiceXml), Options());

        Assert.True(report.IsStructurallyValid);
        Assert.True(report.IsConformanceValidated);
        Assert.Equal("valid", report.ConformanceStatus);
        Assert.Equal("factur-x-1.09.2-en16931-test", report.ValidatorArtifactSetId);
        Assert.Matches("^[0-9a-f]{64}$", report.ValidatorArtifactSetSha256);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV019");
    }

    [Fact]
    public void Validate_ReportsSchematronBusinessRuleFailureAsInvalid()
    {
        using var artifacts = ArtifactFixture.Create(FailingSchematronXslt);

        var report = artifacts.CreateValidator().Validate(
            Encoding.UTF8.GetBytes(InvoiceXml),
            Options());

        Assert.False(report.IsConformanceValidated);
        Assert.Equal("invalid", report.ConformanceStatus);
        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Code == "PXAEINV024" && diagnostic.Message.Contains("Business rule failed."));
    }

    [Fact]
    public void Validate_FailsClosedWhenPinnedArtifactWasModified()
    {
        using var artifacts = ArtifactFixture.Create(PassingSchematronXslt);
        var validator = artifacts.CreateValidator();
        File.AppendAllText(artifacts.SchemaPath, "<!-- changed -->");

        var report = validator.Validate(Encoding.UTF8.GetBytes(InvoiceXml), Options());

        Assert.False(report.IsConformanceValidated);
        Assert.Equal("unavailable", report.ConformanceStatus);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV022");
    }

    [Fact]
    public void Validate_FailsClosedWhenArtifactSetDoesNotMatchRequest()
    {
        using var artifacts = ArtifactFixture.Create(PassingSchematronXslt);
        var options = Options() with { Profile = "BASIC" };

        var report = artifacts.CreateValidator().Validate(
            Encoding.UTF8.GetBytes(InvoiceXml.Replace("en16931", "basic")),
            options);

        Assert.False(report.IsConformanceValidated);
        Assert.Equal("unavailable", report.ConformanceStatus);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV026");
    }

    [Fact]
    public void Validate_RejectsArtifactPathTraversal()
    {
        using var artifacts = ArtifactFixture.Create(PassingSchematronXslt);
        var manifest = artifacts.Manifest with
        {
            XmlSchemas = [new PxaEInvoiceValidationArtifact("../invoice.xsd", new string('0', 64))]
        };

        var report = new PxaEInvoiceConformanceValidator(artifacts.Root, manifest).Validate(
            Encoding.UTF8.GetBytes(InvoiceXml),
            Options());

        Assert.False(report.IsConformanceValidated);
        Assert.Equal("unavailable", report.ConformanceStatus);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV021");
    }

    private static PxaEInvoiceOptions Options() => new()
    {
        Standard = PxaEInvoiceStandard.FacturX,
        Version = EInvoice.CurrentFacturXVersion,
        Profile = "EN 16931"
    };

    private sealed class ArtifactFixture : IDisposable
    {
        private ArtifactFixture(string root, string schemaPath, PxaEInvoiceValidationArtifactManifest manifest)
        {
            Root = root;
            SchemaPath = schemaPath;
            Manifest = manifest;
        }

        public string Root { get; }
        public string SchemaPath { get; }
        public PxaEInvoiceValidationArtifactManifest Manifest { get; }

        public static ArtifactFixture Create(string schematron)
        {
            var root = Path.Combine(Path.GetTempPath(), "pxa-einvoice-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var schemaPath = Path.Combine(root, "invoice.xsd");
            var schematronPath = Path.Combine(root, "rules.xsl");
            File.WriteAllText(schemaPath, Schema, new UTF8Encoding(false));
            File.WriteAllText(schematronPath, schematron, new UTF8Encoding(false));
            var manifest = new PxaEInvoiceValidationArtifactManifest
            {
                ArtifactSetId = "factur-x-1.09.2-en16931-test",
                Standard = PxaEInvoiceStandard.FacturX,
                StandardVersion = EInvoice.CurrentFacturXVersion,
                Profile = "EN 16931",
                Syntax = "CII",
                SourceUri = new Uri("https://example.invalid/official-test-artifacts"),
                License = "Test-only synthetic artifacts",
                XmlSchemas =
                [
                    new PxaEInvoiceValidationArtifact("invoice.xsd", Hash(schemaPath))
                ],
                SchematronXslt = new PxaEInvoiceValidationArtifact("rules.xsl", Hash(schematronPath))
            };
            return new ArtifactFixture(root, schemaPath, manifest);
        }

        public PxaEInvoiceConformanceValidator CreateValidator() => new(Root, Manifest);

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
