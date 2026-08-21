using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PXA.Core.Contracts;

namespace PXA.CodeWorker;

public static class SandboxPolicy
{
    private static readonly string[] ForbiddenFragments =
    [
        "System.IO", "System.Net", "System.Reflection", "System.Diagnostics", "System.Runtime.InteropServices",
        "Microsoft.Win32", "File.", "Directory.", "Path.", "Process.", "Environment.", "Assembly.",
        "Activator.", "AppDomain.", "HttpClient", "WebClient", "Socket", "TcpClient", "UdpClient",
        "DllImport", "Marshal.", "Thread", "Task.Run", "AssemblyLoadContext", "GCHandle",
        "typeof(", ".GetType(", ".Assembly", "Console.",
    ];

    public static List<PxaCodeDiagnosticDto> Analyze(string source)
    {
        var diagnostics = new List<PxaCodeDiagnosticDto>();
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(kind: SourceCodeKind.Script));
        foreach (var diagnostic in tree.GetDiagnostics().Where(value => value.Severity == DiagnosticSeverity.Error))
        {
            var point = diagnostic.Location.GetLineSpan().StartLinePosition;
            diagnostics.Add(New("PXACODE001", diagnostic.GetMessage(), point.Line + 1, point.Character + 1));
        }

        var root = tree.GetRoot();
        foreach (var directive in root.DescendantTrivia().Where(value => value.IsDirective))
            diagnostics.Add(At("PXACODE002", "Preprocessor directives are not allowed.", directive.GetLocation()));
        foreach (var node in root.DescendantNodes().Where(value =>
                     value is UnsafeStatementSyntax or PointerTypeSyntax or FunctionPointerTypeSyntax))
            diagnostics.Add(At("PXACODE003", "Unsafe and pointer code is not allowed.", node.GetLocation()));
        foreach (var node in root.DescendantNodes().OfType<IdentifierNameSyntax>()
                     .Where(value => value.Identifier.ValueText == "dynamic"))
            diagnostics.Add(At("PXACODE004", "Dynamic dispatch is not allowed.", node.GetLocation()));
        foreach (var node in root.DescendantNodes().OfType<AttributeSyntax>()
                     .Where(value => value.Name.ToString().Contains("DllImport", StringComparison.OrdinalIgnoreCase)))
            diagnostics.Add(At("PXACODE005", "Native interop is not allowed.", node.GetLocation()));
        foreach (var node in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var expression = node.Expression.ToString();
            if (ForbiddenFragments.Any(fragment => expression.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                diagnostics.Add(At("PXACODE006", $"The API '{expression}' is not available in the PXA sandbox.", node.GetLocation()));
            if (expression.EndsWith("DrawImage", StringComparison.Ordinal) &&
                node.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
                diagnostics.Add(At("PXACODE007", "Image file paths are not allowed; use a tenant asset ID.", node.GetLocation()));
        }
        foreach (var fragment in ForbiddenFragments.Where(fragment => source.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            if (!diagnostics.Any(value => value.Code == "PXACODE008" && value.Message.Contains(fragment, StringComparison.Ordinal)))
                diagnostics.Add(New("PXACODE008", $"Forbidden capability reference: {fragment}.", 1, 1));
        }
        if (System.Text.Encoding.UTF8.GetByteCount(source) > PxaCodeLimits.MaximumSourceBytes)
            diagnostics.Add(New("PXACODE009", "Source code exceeds the 32 MiB sandbox limit.", 1, 1));
        return diagnostics.DistinctBy(value => (value.Code, value.Line, value.Column, value.Message)).ToList();
    }

    private static PxaCodeDiagnosticDto At(string code, string message, Location location)
    {
        var point = location.GetLineSpan().StartLinePosition;
        return New(code, message, point.Line + 1, point.Character + 1);
    }

    private static PxaCodeDiagnosticDto New(string code, string message, int line, int column) => new()
    {
        Code = code, Severity = "error", Message = message, Line = line, Column = column,
    };
}
