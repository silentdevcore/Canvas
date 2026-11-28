using System.Reflection;

internal sealed record PublicTypeSurface(string type, string kind, string[] members);

internal static class PublicApiSurface
{
    internal static PublicTypeSurface[] Capture(Assembly assembly) => assembly.GetExportedTypes().Select(type => new PublicTypeSurface(
        type.FullName!, type.Attributes.ToString(),
        type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(member => $"{member.MemberType}:{member}").Order(StringComparer.Ordinal).ToArray()))
        .OrderBy(value => value.type, StringComparer.Ordinal).ToArray();
}
