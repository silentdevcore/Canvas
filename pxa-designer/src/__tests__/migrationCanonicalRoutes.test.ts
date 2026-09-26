import { readFileSync } from 'node:fs';
import { join } from 'node:path';

describe('canonical migration routes', () => {
  it('integrates Nutrient Viewer into the editable PDF Designer migration', () => {
    const source = readFileSync(join(process.cwd(), 'src/pages/MigrationsPage.tsx'), 'utf8');
    const app = readFileSync(join(process.cwd(), 'src/App.tsx'), 'utf8');
    const tabs = readFileSync(join(process.cwd(), 'src/components/Migrations/MigrationTabs.tsx'), 'utf8');

    expect(source).toContain("`${API_BASE}/pdf/designer/nutrient-viewer/convert`");
    expect(source).toContain("form.append('pdf', nutrientPdf)");
    expect(source).toContain('setReportDesign(design)');
    expect(source).toContain('await renderDesignerPreview(design)');
    expect(app).toContain('/pdf/migrations/designer?provider=nutrient-viewer');
    expect(tabs).not.toContain("to: '/pdf/migrations/viewer'");
  });

  it('keeps PDF handoff parameters on the canonical Viewer route', () => {
    const migrations = readFileSync(join(process.cwd(), 'src/pages/MigrationsPage.tsx'), 'utf8');
    const preview = readFileSync(join(process.cwd(), 'src/components/Preview/LivePreview.tsx'), 'utf8');
    const app = readFileSync(join(process.cwd(), 'src/App.tsx'), 'utf8');

    expect(migrations).toContain("navigate('/pdf/viewer?handoff=session')");
    expect(preview).toContain("navigate('/pdf/viewer?handoff=session')");
    expect(migrations).not.toContain("navigate('/pdf-viewer?handoff=session')");
    expect(preview).not.toContain("navigate('/pdf-viewer?handoff=session')");
    expect(app).toContain("search: location.search, hash: location.hash");
  });
});
