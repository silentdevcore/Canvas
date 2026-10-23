import { readFileSync, readdirSync } from 'node:fs';

export const publicGuidePaths = [
  'docs/architecture/designer-assets.md', 'docs/architecture/existing-pdf-boundaries.md',
  'docs/csharp-cookbook.md', 'docs/native-designer-project-formats.md',
  'docs/charts.md', 'docs/code-designer-workspace.md',
  'docs/guides/PVAD.EInvoicing.md', 'docs/guides/PVAD.Pdf-Trust-Generator-Examples.md',
  'docs/guides/PVAD.Pdf-Trust-Migration-Examples.md', 'docs/guides/PVAD.Unified-Migration-Provider-Coverage.md',
];

export function publicGuidesPlugin() {
  return {
    name: 'pvad-public-guide-downloads',
    generateBundle() {
      const paths = [...publicGuidePaths];
      for (const directory of ['docs/schema', 'docs/fixtures/native-projects']) {
        const root = new URL(`../../${directory}/`, import.meta.url);
        for (const entry of readdirSync(root, { withFileTypes: true }))
          if (entry.isFile()) paths.push(`${directory}/${entry.name}`);
      }
      for (const path of paths) this.emitFile({
        type: 'asset', fileName: path, source: readFileSync(new URL(`../../${path}`, import.meta.url)),
      });
    },
  };
}
