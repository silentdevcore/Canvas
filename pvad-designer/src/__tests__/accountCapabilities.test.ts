import { designerPermissionDenial, designerPermissions, formatCapability, hasAccountCapability, hasDesignerPermission } from '@/auth/designerPermissions';
import type { DesignerUser } from '@/auth/designerAuth';

describe('account capability restrictions', () => {
  const user = {
    permissions: Object.values(designerPermissions),
    accountCapabilities: ['import.docx', 'import.csv', 'export.pdf', 'export.odt', 'migration.report.designer'],
  } as DesignerUser;

  it('intersects broad role permissions with account rights', () => {
    expect(hasDesignerPermission(user, designerPermissions.pdfSignaturesSign)).toBe(false);
    expect(hasDesignerPermission(user, designerPermissions.pdfEInvoicesGenerate)).toBe(false);
    expect(hasDesignerPermission(user, designerPermissions.pdfComplianceConvert)).toBe(false);
    expect(hasDesignerPermission(user, designerPermissions.documentEdit)).toBe(true);
  });

  it('does not conflate a permitted import with export', () => {
    expect(hasAccountCapability(user, formatCapability('import', '.CSV'))).toBe(true);
    expect(hasAccountCapability(user, formatCapability('export', '.CSV'))).toBe(false);
    expect(hasAccountCapability(user, formatCapability('export', 'odt'))).toBe(true);
    expect(hasAccountCapability(user, formatCapability('export', '.JPG'))).toBe(false);
    expect(formatCapability('import', '.TIFF')).toBe('import.image');
  });

  it('preserves migration kind approval boundaries', () => {
    const migrationUser = { ...user, permissions: [
      'designer.migration.report.designer.execute', 'designer.migration.pdf.code.execute',
    ] };
    expect(hasDesignerPermission(migrationUser, 'designer.migration.report.designer.execute' as never)).toBe(true);
    expect(hasDesignerPermission(migrationUser, 'designer.migration.pdf.code.execute' as never)).toBe(false);
  });

  it('treats an explicit empty capability list as no grants', () => {
    expect(hasAccountCapability({ ...user, accountCapabilities: [] }, 'export.pdf')).toBe(false);
  });

  it('keeps older server responses compatible without fabricating a new policy', () => {
    const legacy = { permissions: [designerPermissions.pdfSignaturesSign] } as DesignerUser;
    expect(hasDesignerPermission(legacy, designerPermissions.pdfSignaturesSign)).toBe(true);
  });

  it('distinguishes role permission, account restriction and separate migration approval', () => {
    expect(designerPermissionDenial({ ...user, permissions: [] }, [designerPermissions.pdfImport])).toBe('role');
    expect(designerPermissionDenial(user, [designerPermissions.pdfImport])).toBe('account');
    expect(hasDesignerPermission(user, designerPermissions.pdfImport)).toBe(false);
    expect(designerPermissionDenial(user, [designerPermissions.migrationPdfExecute])).toBe('approval');
    expect(designerPermissionDenial(user, [designerPermissions.documentEdit])).toBeNull();
    expect(designerPermissionDenial(user, [designerPermissions.pdfImport, designerPermissions.documentEdit])).toBeNull();
  });
});
