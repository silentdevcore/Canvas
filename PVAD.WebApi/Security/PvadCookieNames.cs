namespace PVAD.WebApi.Security;

public static class PvadCookieNames
{
    public static string Session(bool secure) => secure ? "__Host-PVAD.Session" : "PVAD.Session.Development";
    public static string Designer(bool secure) => secure ? "__Host-PVAD.Designer.Session" : "PVAD.Designer.Session.Development";
    public static string Antiforgery(bool secure) => secure ? "__Host-PVAD.Antiforgery" : "PVAD.Antiforgery.Development";
}
