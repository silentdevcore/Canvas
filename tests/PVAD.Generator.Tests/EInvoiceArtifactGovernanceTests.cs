using System.Security.Cryptography;
using System.Text;
using PVAD.Generator;

namespace PVAD.Generator.Tests;

/// <summary>
/// The fail-closed gate in front of a mounted official artifact set.
/// </summary>
/// <remarks>
/// <para>
/// PXA ships no FeRD/FNFE or KoSIT artifacts. When an operator mounts a set, the only thing standing
/// between "some files in a directory" and a conformance verdict a customer will rely on is this
/// gate. Every refusal below produces <c>unavailable</c> and a diagnostic explaining why - never a
/// verdict, and never a silent fall back to structural recognition, which would look like a pass.
/// </para>
/// <para>
/// The artifacts here are synthetic and PXA-authored. Nothing in this file is, or stands in for, an
/// official distribution.
/// </para>
/// </remarks>
public sealed class EInvoiceArtifactGovernanceTests
{
    [Fact]
    public void An_approved_current_set_is_accepted()
    {
        using var fixture = Fixture.Create();

        var report = fixture.Validate();

        Assert.True(report.IsConformanceValidated);
        Assert.Equal("valid", report.ConformanceStatus);
        Assert.Equal(fixture.Manifest.ArtifactSetId, report.ValidatorArtifactSetId);
        Assert.NotNull(report.ValidatorArtifactSetSha256);
    }

    [Theory]
    [InlineData(PvadEInvoiceLicenseReviewState.NotReviewed)]
    [InlineData(PvadEInvoiceLicenseReviewState.InReview)]
    [InlineData(PvadEInvoiceLicenseReviewState.Rejected)]
    [InlineData(PvadEInvoiceLicenseReviewState.Unspecified)]
    public void A_set_without_an_approved_licence_review_is_refused(PvadEInvoiceLicenseReviewState state)
    {
        using var fixture = Fixture.Create(governance => governance with { LicenseReview = state });

        AssertUnavailable(fixture.Validate(), "PXAEINV029");
    }

    [Fact]
    public void An_approved_review_must_say_who_concluded_it_and_when()
    {
        using var fixture = Fixture.Create(governance => governance with
        {
            LicenseReviewedBy = null,
            LicenseReviewedOn = null
        });

        AssertUnavailable(fixture.Validate(), "PXAEINV029");
    }

    [Fact]
    public void An_expired_set_is_refused_rather_than_used()
    {
        using var fixture = Fixture.Create(governance => governance with
        {
            ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)
        });

        AssertUnavailable(fixture.Validate(), "PXAEINV030");
    }

    [Fact]
    public void A_set_expiring_today_is_still_current()
    {
        using var fixture = Fixture.Create(governance => governance with
        {
            ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        Assert.Equal("valid", fixture.Validate().ConformanceStatus);
    }

    [Theory]
    [InlineData("family")]
    [InlineData("upstreamVersion")]
    [InlineData("retrievedOn")]
    [InlineData("updatePolicy")]
    [InlineData("supportedProfiles")]
    public void A_set_that_cannot_state_its_provenance_is_refused(string missing)
    {
        using var fixture = Fixture.Create(governance => missing switch
        {
            "family" => governance with { Family = PvadEInvoiceArtifactFamily.Unspecified },
            "upstreamVersion" => governance with { UpstreamVersion = "  " },
            "retrievedOn" => governance with { RetrievedOn = null },
            "updatePolicy" => governance with { UpdatePolicy = null },
            _ => governance with { SupportedProfiles = [] }
        });

        AssertUnavailable(fixture.Validate(), "PXAEINV028");
    }

    [Fact]
    public void A_version_two_manifest_without_a_governance_record_is_refused()
    {
        using var fixture = Fixture.Create(governance: null);

        AssertUnavailable(fixture.Validate(), "PXAEINV028");
    }

    [Fact]
    public void An_artifact_family_that_contradicts_the_standard_is_refused()
    {
        // A KoSIT XRechnung distribution cannot validate a Factur-X invoice, however well-formed.
        using var fixture = Fixture.Create(governance => governance with
        {
            Family = PvadEInvoiceArtifactFamily.KositXRechnung
        });

        AssertUnavailable(fixture.Validate(), "PXAEINV026");
    }

    [Fact]
    public void A_profile_the_set_does_not_support_is_refused()
    {
        using var fixture = Fixture.Create(governance => governance with
        {
            SupportedProfiles = ["MINIMUM", "BASIC"]
        });

        AssertUnavailable(fixture.Validate(), "PXAEINV026");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(99)]
    public void A_manifest_schema_version_this_build_does_not_know_is_refused(int schemaVersion)
    {
        using var fixture = Fixture.Create(schemaVersion: schemaVersion);

        AssertUnavailable(fixture.Validate(), "PXAEINV027");
    }

    [Fact]
    public void A_modified_artifact_is_refused()
    {
        using var fixture = Fixture.Create();
        File.AppendAllText(Path.Combine(fixture.Root, "rules.xsl"), "<!-- edited -->");

        AssertUnavailable(fixture.Validate(), "PXAEINV022");
    }

    [Fact]
    public void A_missing_artifact_is_refused()
    {
        using var fixture = Fixture.Create();
        File.Delete(Path.Combine(fixture.Root, "invoice.xsd"));

        AssertUnavailable(fixture.Validate(), "PXAEINV020");
    }

    [Fact]
    public void A_refusal_never_reports_structural_recognition_as_a_conformance_result()
    {
        using var fixture = Fixture.Create(governance => governance with
        {
            LicenseReview = PvadEInvoiceLicenseReviewState.NotReviewed
        });

        var report = fixture.Validate();

        // The document really is structurally fine. That must not become the answer to a question
        // about official conformance.
        Assert.True(report.IsStructurallyValid);
        Assert.False(report.IsConformanceValidated);
        Assert.Equal("unavailable", report.ConformanceStatus);
        Assert.NotEqual("valid", report.ConformanceStatus);
    }

    [Fact]
    public void A_failing_rule_keeps_the_validator_s_own_identifier_severity_location_and_wording()
    {
        using var fixture = Fixture.Create(schematron: FailingSchematron);

        var report = fixture.Validate();

        Assert.Equal("invalid", report.ConformanceStatus);
        var diagnostic = Assert.Single(report.Diagnostics, item => item.Code == "PXAEINV024");
        // Mapped to a stable PXA code, without discarding what the artifact set actually said.
        var original = diagnostic.OriginalFinding;
        Assert.NotNull(original);
        Assert.Equal("schematron", original!.Source);
        Assert.Equal("BR-DE-15", original.RuleId);
        Assert.Equal("fatal", original.Severity);
        Assert.Equal("/rsm:CrossIndustryInvoice", original.Location);
        Assert.Equal("The buyer reference is required.", original.Message);
        Assert.Equal("false()", original.Test);
    }

    [Fact]
    public void An_advisory_rule_is_reported_as_a_warning_and_does_not_invalidate_the_document()
    {
        using var fixture = Fixture.Create(schematron: AdvisorySchematron);

        var report = fixture.Validate();

        Assert.Equal("valid", report.ConformanceStatus);
        var diagnostic = Assert.Single(report.Diagnostics, item => item.Code == "PXAEINV024");
        Assert.Equal("warning", diagnostic.Severity);
        Assert.Equal("warning", diagnostic.OriginalFinding!.Severity);
        Assert.Equal("BR-DE-ADVISORY", diagnostic.OriginalFinding.RuleId);
    }

    [Fact]
    public void An_unflagged_successful_report_is_informational_and_does_not_invalidate_the_document()
    {
        using var fixture = Fixture.Create(schematron: InformationalSchematron);

        var report = fixture.Validate();

        Assert.Equal("valid", report.ConformanceStatus);
        var diagnostic = Assert.Single(report.Diagnostics, item => item.Code == "PXAEINV024");
        Assert.Equal("information", diagnostic.Severity);
        Assert.Equal("information", diagnostic.OriginalFinding!.Severity);
    }

    [Fact]
    public void Governance_changes_are_covered_by_the_artifact_set_hash()
    {
        using var first = Fixture.Create();
        using var second = Fixture.Create(governance => governance with
        {
            LicenseReviewedBy = "A different synthetic reviewer"
        });

        Assert.NotEqual(
            first.Validate().ValidatorArtifactSetSha256,
            second.Validate().ValidatorArtifactSetSha256);
    }

    private static void AssertUnavailable(PvadEInvoiceValidationReport report, string code)
    {
        Assert.Equal("unavailable", report.ConformanceStatus);
        Assert.False(report.IsConformanceValidated);
        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Code == code && diagnostic.Severity == "error");
    }

    // ── Synthetic artifact set ───────────────────────────────────────────────

    private const string Schema = """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                   xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
                   targetNamespace="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
                   elementFormDefault="qualified">
          <xs:element name="CrossIndustryInvoice">
            <xs:complexType>
              <xs:sequence>
                <xs:any processContents="lax" minOccurs="0" maxOccurs="unbounded"/>
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;

    private const string PassingSchematron = """
        <?xml version="1.0" encoding="utf-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                        xmlns:svrl="http://purl.oclc.org/dsdl/svrl">
          <xsl:template match="/">
            <svrl:schematron-output/>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private const string FailingSchematron = """
        <?xml version="1.0" encoding="utf-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                        xmlns:svrl="http://purl.oclc.org/dsdl/svrl">
          <xsl:template match="/">
            <svrl:schematron-output>
              <svrl:failed-assert id="BR-DE-15" flag="fatal" test="false()"
                                  location="/rsm:CrossIndustryInvoice">
                <svrl:text>The buyer reference is required.</svrl:text>
              </svrl:failed-assert>
            </svrl:schematron-output>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private const string AdvisorySchematron = """
        <?xml version="1.0" encoding="utf-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                        xmlns:svrl="http://purl.oclc.org/dsdl/svrl">
          <xsl:template match="/">
            <svrl:schematron-output>
              <svrl:successful-report id="BR-DE-ADVISORY" flag="warning" test="true()"
                                      location="/rsm:CrossIndustryInvoice">
                <svrl:text>A payment reference is recommended.</svrl:text>
              </svrl:successful-report>
            </svrl:schematron-output>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private const string InformationalSchematron = """
        <?xml version="1.0" encoding="utf-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                        xmlns:svrl="http://purl.oclc.org/dsdl/svrl">
          <xsl:template match="/">
            <svrl:schematron-output>
              <svrl:successful-report id="BR-DE-INFO" test="true()"
                                      location="/rsm:CrossIndustryInvoice">
                <svrl:text>An informational rule was observed.</svrl:text>
              </svrl:successful-report>
            </svrl:schematron-output>
          </xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>The minimal Factur-X EN 16931 document the structural validator recognises.</summary>
    private static readonly byte[] Invoice = Encoding.UTF8.GetBytes(
        """
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
        """);

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root, PvadEInvoiceValidationArtifactManifest manifest)
        {
            Root = root;
            Manifest = manifest;
        }

        public string Root { get; }
        public PvadEInvoiceValidationArtifactManifest Manifest { get; }

        public static Fixture Create(
            Func<PvadEInvoiceArtifactGovernance, PvadEInvoiceArtifactGovernance>? adjust = null,
            int schemaVersion = 2,
            string? schematron = null) =>
            Build(adjust is null ? Approved() : adjust(Approved()), schemaVersion, schematron);

        public static Fixture Create(
            PvadEInvoiceArtifactGovernance? governance,
            int schemaVersion = 2,
            string? schematron = null) =>
            Build(governance, schemaVersion, schematron);

        private static Fixture Build(
            PvadEInvoiceArtifactGovernance? governance,
            int schemaVersion,
            string? schematron)
        {
            var root = Path.Combine(Path.GetTempPath(), "pxa-einvoice-gov-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var schemaPath = Path.Combine(root, "invoice.xsd");
            var schematronPath = Path.Combine(root, "rules.xsl");
            File.WriteAllText(schemaPath, Schema, new UTF8Encoding(false));
            File.WriteAllText(schematronPath, schematron ?? PassingSchematron, new UTF8Encoding(false));

            var manifest = new PvadEInvoiceValidationArtifactManifest
            {
                SchemaVersion = schemaVersion,
                ArtifactSetId = "factur-x-1.09.2-en16931-synthetic",
                Standard = PvadEInvoiceStandard.FacturX,
                StandardVersion = EInvoice.CurrentFacturXVersion,
                Profile = "EN 16931",
                Syntax = "CII",
                SourceUri = new Uri("https://example.invalid/synthetic-artifact-set"),
                License = "Synthetic PXA-authored stand-in; not an official distribution",
                XmlSchemas = [new PvadEInvoiceValidationArtifact("invoice.xsd", Hash(schemaPath))],
                SchematronXslt = new PvadEInvoiceValidationArtifact("rules.xsl", Hash(schematronPath)),
                Governance = governance
            };
            return new Fixture(root, manifest);
        }

        private static PvadEInvoiceArtifactGovernance Approved() => new()
        {
            Family = PvadEInvoiceArtifactFamily.FerdFnfeFacturX,
            UpstreamVersion = "1.09.2",
            RetrievedOn = new DateOnly(2026, 9, 1),
            LicenseReview = PvadEInvoiceLicenseReviewState.Approved,
            LicenseReviewedBy = "PXA synthetic test reviewer",
            LicenseReviewedOn = new DateOnly(2026, 9, 2),
            SupportedProfiles = ["MINIMUM", "BASIC WL", "BASIC", "EN 16931", "EXTENDED"],
            UpdatePolicy = "Re-fetched and re-reviewed whenever the upstream publishes a release.",
            ExpiresOn = new DateOnly(2099, 1, 1)
        };

        public PvadEInvoiceValidationReport Validate() =>
            new PvadEInvoiceConformanceValidator(Root, Manifest).Validate(Invoice, new PvadEInvoiceOptions
            {
                Standard = PvadEInvoiceStandard.FacturX,
                Version = EInvoice.CurrentFacturXVersion,
                Profile = "EN 16931"
            });

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
