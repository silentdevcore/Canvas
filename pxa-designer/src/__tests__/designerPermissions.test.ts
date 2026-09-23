import {
  canonicalMigrationProviderPermission,
  designerPermissions,
  hasAnyDesignerPermission,
  hasDesignerPermission,
  migrationKindPermission,
  migrationProviderPermission,
} from '@/auth/designerPermissions';
import type { DesignerUser } from '@/auth/designerAuth';

describe('Designer permissions', () => {
  it('uses unique stable designer permission names', () => {
    const permissions = Object.values(designerPermissions);

    expect(new Set(permissions).size).toBe(permissions.length);
    expect(permissions.every((permission) => permission.startsWith('designer.'))).toBe(true);
  });

  it('checks the effective permissions returned by the API', () => {
    const user = {
      permissions: [designerPermissions.templatesRead, designerPermissions.export],
    } as DesignerUser;

    expect(hasDesignerPermission(user, designerPermissions.templatesRead)).toBe(true);
    expect(hasDesignerPermission(user, designerPermissions.templatesEdit)).toBe(false);
  });

  it('builds domain-specific provider permissions without conflating frameworks', () => {
    expect(migrationProviderPermission('pdf', 'aspose-pdf'))
      .toBe('designer.migration.pdf.provider.aspose-pdf.execute');
    expect(migrationProviderPermission('spreadsheet', 'aspose-cells'))
      .toBe('designer.migration.spreadsheet.provider.aspose-cells.execute');
    expect(migrationProviderPermission('report', 'devexpress-report'))
      .toBe('designer.migration.report.provider.devexpress-report.execute');
  });

  it('builds canonical kind and provider-family permissions', () => {
    expect(migrationKindPermission('pdf', 'viewer', 'execute'))
      .toBe('designer.migration.pdf.viewer.execute');
    expect(canonicalMigrationProviderPermission('pdf', 'viewer', 'nutrient'))
      .toBe('designer.migration.pdf.viewer.provider.nutrient.execute');
  });

  it('accepts any matching compatibility permission', () => {
    const user = {
      permissions: [migrationKindPermission('report', 'designer', 'execute')],
    } as DesignerUser;

    expect(hasAnyDesignerPermission(user, [
      designerPermissions.migrationReportExecute,
      migrationKindPermission('report', 'designer', 'execute'),
    ])).toBe(true);
  });
});
