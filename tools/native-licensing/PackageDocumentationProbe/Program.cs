using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length != 3) throw new ArgumentException("Expected assembly, compiler XML and destination XML.");
var assemblyPath = Path.GetFullPath(args[0]);
var sourcePath = Path.GetFullPath(args[1]);
var destination = Path.GetFullPath(args[2]);
if (File.Exists(destination)) throw new IOException("Documentation destination already exists.");
using var reader = XmlReader.Create(sourcePath, new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10 * 1024 * 1024,
});
var document = XDocument.Load(reader);
var name = document.Root?.Element("assembly")?.Element("name")?.Value;
if (name != Path.GetFileNameWithoutExtension(assemblyPath)) throw new InvalidDataException("Documentation assembly mismatch.");
var references = Directory.GetFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll")
    .Concat(((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator))
    .Where(File.Exists).DistinctBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
    .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
var compilation = CSharpCompilation.Create("PackageDocumentationInspection", references: references,
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
bool ExternallyVisible(ISymbol symbol)
{
    for (var current = symbol; current is not null && current is not INamespaceSymbol && current is not IAssemblySymbol; current = current.ContainingSymbol)
        if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            return false;
    return true;
}
bool Visible(ISymbol symbol) => symbol.ContainingAssembly?.Name == name && ExternallyVisible(symbol);
var members = document.Root?.Element("members") ?? throw new InvalidDataException("Missing documentation members.");
var ids = new HashSet<string>(StringComparer.Ordinal);
var removed = 0;
foreach (var member in members.Elements("member").ToArray())
{
    var id = (string?)member.Attribute("name") ?? throw new InvalidDataException("Missing documentation identity.");
    if (!ids.Add(id)) throw new InvalidDataException("Duplicate documentation identity.");
    if (!DocumentationCommentId.GetSymbolsForDeclarationId(id, compilation).Any(Visible))
    {
        member.Remove();
        removed++;
        continue;
    }
    foreach (var reference in member.Descendants().Where(element => element.Attribute("cref") is not null).ToArray())
    {
        var symbols = DocumentationCommentId.GetSymbolsForDeclarationId(reference.Attribute("cref")!.Value, compilation);
        if (symbols.Any(symbol => symbol.ContainingAssembly?.Name.StartsWith("PVAD.", StringComparison.Ordinal) == true)
            && !symbols.Any(ExternallyVisible)) reference.Remove();
    }
}
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
document.Save(destination);
Console.WriteLine(JsonSerializer.Serialize(new
{
    assembly = name, sourceMembers = ids.Count, publicMembers = ids.Count - removed, removedMembers = removed,
    sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourcePath))),
    xmlSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(destination))),
    assemblySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
}));
