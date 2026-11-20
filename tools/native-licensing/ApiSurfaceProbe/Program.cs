using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

if (args.Length != 1)
    throw new ArgumentException("Expected one library path.");
var path = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var dependency = Path.Combine(Path.GetDirectoryName(path)!, name.Name + ".dll");
    return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
};
var assembly = Assembly.LoadFrom(path);
var surface = assembly.GetExportedTypes().Select(type => new
{
    type = type.FullName,
    kind = type.Attributes.ToString(),
    members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Select(member => new
        {
            signature = $"{member.MemberType}:{member}",
            constant = member is FieldInfo { IsLiteral: true } field ? Convert.ToString(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture) : null,
            parameters = member is MethodBase method ? method.GetParameters().Select(parameter => new
            {
                parameter.Name, parameter.IsOptional,
                defaultValue = parameter.HasDefaultValue ? Convert.ToString(parameter.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) : null,
            }).ToArray() : null,
            attributes = member.GetCustomAttributesData().Where(attribute => attribute.AttributeType.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true)
                .Select(attribute => attribute.ToString()).Order(StringComparer.Ordinal).ToArray(),
        }).OrderBy(member => member.signature, StringComparer.Ordinal).ToArray(),
}).OrderBy(value => value.type, StringComparer.Ordinal).ToArray();
Console.WriteLine(JsonSerializer.Serialize(surface));
