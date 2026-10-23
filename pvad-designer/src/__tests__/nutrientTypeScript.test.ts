import { transpileNutrientViewerTypeScript } from '@/features/pdf-viewer/nutrientTypeScript';

describe('Nutrient Viewer TypeScript compiler boundary', () => {
  it('removes type-only syntax and preserves the Viewer configuration', async () => {
    const result = await transpileNutrientViewerTypeScript(`
      import NutrientViewer, { type Configuration } from '@nutrient-sdk/viewer';
      const config: Configuration = { document: '/documents/sample.pdf' };
      NutrientViewer.load(config);
    `);

    expect(result.outputText).toContain("document: '/documents/sample.pdf'");
    expect(result.outputText).toContain('NutrientViewer.load(config)');
    expect(result.outputText).not.toContain(': Configuration');
    expect(result.diagnostics).toEqual([]);
  });

  it('reports malformed TypeScript before it reaches the migration API', async () => {
    await expect(transpileNutrientViewerTypeScript('const config: = {};'))
      .rejects.toThrow();
  });
});
