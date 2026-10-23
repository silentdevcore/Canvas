using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PVAD.Migration.Roslyn;

/// <summary>
/// Provides the best semantic information available when a vendor assembly is not referenced.
/// Local, field, property, and parameter symbols still resolve and aliases are inspected from
/// syntax, so migrations do not have to identify SDK receivers by variable text alone.
/// </summary>
public sealed class CSharpMigrationSemanticContext
{
    private readonly IReadOnlyDictionary<string, string> _aliases;

    private CSharpMigrationSemanticContext(
        SyntaxTree tree,
        CompilationUnitSyntax root,
        SemanticModel semanticModel)
    {
        Tree = tree;
        Root = root;
        SemanticModel = semanticModel;
        _aliases = root.Usings
            .Where(static directive => directive.Alias is not null && directive.Name is not null)
            .ToDictionary(
                static directive => directive.Alias!.Name.Identifier.ValueText,
                static directive => directive.Name!.ToString(),
                StringComparer.Ordinal);
    }

    public SyntaxTree Tree { get; }
    public CompilationUnitSyntax Root { get; }
    public SemanticModel SemanticModel { get; }

    public static CSharpMigrationSemanticContext Create(string sourceCode, string assemblyName)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return new CSharpMigrationSemanticContext(tree, root, compilation.GetSemanticModel(tree));
    }

    public IReadOnlySet<ISymbol> FindDeclaredSymbolsByType(params string[] typeNames)
    {
        ArgumentNullException.ThrowIfNull(typeNames);
        var expected = typeNames.ToHashSet(StringComparer.Ordinal);
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var declaration in Root.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            foreach (var variable in declaration.Variables)
            {
                var matchesDeclaredType = TypeMatches(declaration.Type, expected);
                var matchesCreatedType = variable.Initializer?.Value is ObjectCreationExpressionSyntax creation &&
                    TypeMatches(creation.Type, expected);
                if ((matchesDeclaredType || matchesCreatedType) && SemanticModel.GetDeclaredSymbol(variable) is { } symbol)
                    symbols.Add(symbol);
            }
        }

        foreach (var parameter in Root.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (parameter.Type is not null && TypeMatches(parameter.Type, expected) &&
                SemanticModel.GetDeclaredSymbol(parameter) is { } symbol)
                symbols.Add(symbol);
        }

        foreach (var property in Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (TypeMatches(property.Type, expected) && SemanticModel.GetDeclaredSymbol(property) is { } symbol)
                symbols.Add(symbol);
        }

        return symbols;
    }

    public bool ReferencesAny(ExpressionSyntax expression, IReadOnlySet<ISymbol> symbols)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(symbols);
        var symbol = SemanticModel.GetSymbolInfo(expression).Symbol;
        return symbol is not null && symbols.Contains(symbol);
    }

    public ISymbol? GetReferencedSymbol(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return SemanticModel.GetSymbolInfo(expression).Symbol;
    }

    public bool TypeMatches(TypeSyntax type, params string[] typeNames) =>
        TypeMatches(type, typeNames.ToHashSet(StringComparer.Ordinal));

    private bool TypeMatches(TypeSyntax type, IReadOnlySet<string> typeNames)
    {
        var simpleName = SimpleName(type);
        if (typeNames.Contains(simpleName))
            return true;
        if (_aliases.TryGetValue(simpleName, out var aliasedType) &&
            typeNames.Contains(aliasedType.Split('.').Last()))
            return true;
        return SemanticModel.GetTypeInfo(type).Type is { } symbol && typeNames.Contains(symbol.Name);
    }

    private static string SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => type.ToString().Split('.').Last()
    };
}
