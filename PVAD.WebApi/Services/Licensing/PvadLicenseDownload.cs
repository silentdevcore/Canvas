using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PVAD.Domain.Entities;

namespace PVAD.WebApi.Services.Licensing;

public static class PvadLicenseDownload
{
    public static FileContentResult Create(OfflineLicense license)
    {
        using var envelope = JsonDocument.Parse(license.EnvelopeJson);
        var payload = new Dictionary<string, object?>
        {
            ["envelope"] = envelope.RootElement,
            ["signature"] = license.Signature,
            ["keyId"] = license.KeyId,
            ["algorithm"] = license.Algorithm,
        };
        // Signature verification must use the original UTF-8 text, not reserialized JSON.
        payload["envelopeJson"] = license.EnvelopeJson;
        var artifact = JsonSerializer.Serialize(payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return new FileContentResult(Encoding.UTF8.GetBytes(artifact), "application/vnd.pvad.license+json")
        {
            FileDownloadName = $"{license.LicenseNumber}.pvad-license.json",
        };
    }
}
