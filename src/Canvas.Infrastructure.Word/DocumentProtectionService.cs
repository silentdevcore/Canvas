using Canvas.Core.Contracts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Canvas.Infrastructure.Word;

/// <summary>
/// Applies document-level write protection and editing restrictions to a DOCX.
/// </summary>
internal static class DocumentProtectionService
{
    internal static void Apply(WordprocessingDocument doc, DocumentProtectionDto? protection)
    {
        if (protection is null || !protection.Enabled) return;

        var settings = doc.MainDocumentPart!.DocumentSettingsPart
            ?? doc.MainDocumentPart.AddNewPart<DocumentSettingsPart>();

        settings.Settings ??= new Settings();

        var editRestriction = protection.Mode switch
        {
            "comments"       => DocumentProtectionValues.Comments,
            "trackedChanges" => DocumentProtectionValues.TrackedChanges,
            "formFields"     => DocumentProtectionValues.Forms,
            _                => DocumentProtectionValues.ReadOnly,
        };

        var dp = new DocumentProtection
        {
            Edit = editRestriction,
            Enforcement = true,
        };

        if (!string.IsNullOrWhiteSpace(protection.PasswordHash))
        {
            // Store hash directly — real implementations would use OOXML password hashing.
            dp.Hash = protection.PasswordHash;
            dp.CryptographicAlgorithmSid = 4; // SHA-1 per OOXML spec
        }

        // Remove any previous protection element before inserting.
        settings.Settings.RemoveAllChildren<DocumentProtection>();
        settings.Settings.InsertAt(dp, 0);
        settings.Settings.Save();
    }
}
