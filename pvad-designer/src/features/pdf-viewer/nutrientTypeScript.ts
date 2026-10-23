export interface NutrientTypeScriptCompilation {
  outputText: string;
  diagnostics: string[];
}

export const transpileNutrientViewerTypeScript = async (
  source: string,
): Promise<NutrientTypeScriptCompilation> => {
  const ts = await import('typescript');
  const result = ts.transpileModule(source, {
    compilerOptions: {
      target: ts.ScriptTarget.ES2022,
      module: ts.ModuleKind.ESNext,
      importsNotUsedAsValues: ts.ImportsNotUsedAsValues.Remove,
    },
    fileName: 'nutrient-viewer.ts',
    reportDiagnostics: true,
  });
  const diagnostics = result.diagnostics?.map(diagnostic =>
    ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n')) ?? [];
  const compilerError = result.diagnostics?.find(diagnostic => diagnostic.category === ts.DiagnosticCategory.Error);
  if (compilerError) {
    throw new Error(ts.flattenDiagnosticMessageText(compilerError.messageText, '\n'));
  }

  return { outputText: result.outputText, diagnostics };
};
