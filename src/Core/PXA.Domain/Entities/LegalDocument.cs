namespace PXA.Domain.Entities;

public sealed class LegalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LegalDocumentType Type { get; set; }
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
public enum LegalDocumentType
{
    TermsAndConditions,
    PrivacyNotice,
    CookieAndStoragePolicy,
    Imprint,
    ConsumerWithdrawal,
    DataProcessingAgreement,
    LicenseAgreement,
    SubprocessorList,
    ServiceLevelAgreement,
}
