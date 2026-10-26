namespace PXA.WebApi.Services.Licensing;

public static class PxaLicenseTimestamps
{
    // PostgreSQL persists microseconds; normalize before signing, never during verification.
    public static DateTimeOffset Normalize(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
}
