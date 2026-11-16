using System.Reflection;
using System.Text.Json;

if (args.Length != 1)
    throw new ArgumentException("Expected one library path.");
var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
var surface = assembly.GetExportedTypes().Select(type => new
{
    type = type.FullName,
    kind = type.Attributes.ToString(),
    members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Select(member => $"{member.MemberType}:{member}").Order(StringComparer.Ordinal).ToArray(),
}).OrderBy(value => value.type, StringComparer.Ordinal).ToArray();
Console.WriteLine(JsonSerializer.Serialize(surface));
