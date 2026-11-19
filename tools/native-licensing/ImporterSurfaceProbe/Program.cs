using System.Reflection;
using System.Text.Json;
using PVAD.FileImporter;
using PVAD.FileImporter.ImageAnalysis;
using PVAD.FileImporter.ImageOcr;
using PVAD.Importer;

var assemblies = new[] { typeof(PdfImporter).Assembly, typeof(IFileImporter).Assembly,
    typeof(ImageAnalysisFileImporter).Assembly, typeof(IOcrEngine).Assembly };
Console.WriteLine(JsonSerializer.Serialize(new
{
    schemaVersion = 1,
    assemblies = assemblies.Select(assembly => new
    {
        assembly = assembly.GetName().Name,
        types = PublicApiSurface.Capture(assembly).Select(surface =>
        {
            var type = assembly.GetType(surface.type)!;
            return new
            {
                surface.type, surface.kind, surface.members, baseType = type.BaseType?.ToString(), isInterface = type.IsInterface,
                interfaces = type.GetInterfaces().Where(contract => contract.IsVisible)
                    .Select(contract => contract.ToString()).Order(StringComparer.Ordinal).ToArray(),
                explicitBindings = type.IsInterface ? [] : type.GetInterfaces().Where(contract => contract.IsVisible)
                    .SelectMany(contract =>
                    {
                        var map = type.GetInterfaceMap(contract);
                        return map.InterfaceMethods.Select((method, index) => (method, target: map.TargetMethods[index]))
                            .Where(pair => !pair.target.IsPublic && pair.target.DeclaringType == type)
                            .Select(pair => $"{contract.FullName}:{pair.method}=>{pair.target.DeclaringType!.FullName}:{pair.target}");
                    }).Order(StringComparer.Ordinal).ToArray(),
            };
        }),
        methods = assembly.GetExportedTypes().SelectMany(type => type.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && !method.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .Select(method => new { type = type.FullName, name = method.Name, member = "Method:" + method }))
            .OrderBy(value => value.type, StringComparer.Ordinal).ThenBy(value => value.member, StringComparer.Ordinal),
    }),
}));
