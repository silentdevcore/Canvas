import type { DesignerUser } from './designerAuth';

export const designerPermissions = {
  access: 'designer.access',
  templatesRead: 'designer.templates.read',
  templatesCreate: 'designer.templates.create',
  templatesEdit: 'designer.templates.edit',
  templatesArchive: 'designer.templates.archive',
  templatesVersion: 'designer.templates.version',
  templatesPublish: 'designer.templates.publish',
  assetsRead: 'designer.assets.read',
  assetsManage: 'designer.assets.manage',
  codeRead: 'designer.code.read',
  codeEdit: 'designer.code.edit',
  codeExecute: 'designer.code.execute',
  codeApply: 'designer.code.apply',
  preferencesManage: 'designer.preferences.manage',
  notificationsRead: 'designer.notifications.read',
  jobsRead: 'designer.jobs.read',
  jobsCancel: 'designer.jobs.cancel',
  symbolsRender: 'designer.symbols.render',
  experimentalFeatures: 'designer.features.experimental',
  featuresManage: 'designer.features.manage',
  pdfAccess: 'designer.pdf.access',
  pdfCreate: 'designer.pdf.create',
  pdfEdit: 'designer.pdf.edit',
  pdfTemplatesRead: 'designer.pdf.templates.read',
  pdfImport: 'designer.pdf.import',
  pdfConvert: 'designer.pdf.convert',
  pdfExport: 'designer.pdf.export',
  pdfPrint: 'designer.pdf.print',
  pdfTrustInspect: 'designer.pdf.trust.inspect',
  pdfComplianceValidate: 'designer.pdf.compliance.validate',
  pdfComplianceConvert: 'designer.pdf.compliance.convert',
  pdfSignaturesValidate: 'designer.pdf.signatures.validate',
  pdfSignaturesSign: 'designer.pdf.signatures.sign',
  pdfSignatureTrustPoliciesManage: 'designer.pdf.signatures.trust-policies.manage',
  pdfSignatureKeyProvidersUse: 'designer.pdf.signatures.key-providers.use',
  pdfSignatureKeyProvidersManage: 'designer.pdf.signatures.key-providers.manage',
  pdfRedactionsInspect: 'designer.pdf.redactions.inspect',
  pdfRedactionsApply: 'designer.pdf.redactions.apply',
  pdfEInvoicesRead: 'designer.pdf.e-invoices.read',
  pdfEInvoicesValidate: 'designer.pdf.e-invoices.validate',
  pdfEInvoicesGenerate: 'designer.pdf.e-invoices.generate',
  pdfEInvoicesCreate: 'designer.pdf.e-invoices.create',
  pdfEInvoicesAttach: 'designer.pdf.e-invoices.attach',
  documentEdit: 'designer.document.edit',
  import: 'designer.import',
  export: 'designer.export',
  migrationRead: 'designer.migration.read',
  migrationExecute: 'designer.migration.execute',
  migrationPdfRead: 'designer.migration.pdf.read',
  migrationPdfExecute: 'designer.migration.pdf.execute',
  migrationSpreadsheetRead: 'designer.migration.spreadsheet.read',
  migrationSpreadsheetExecute: 'designer.migration.spreadsheet.execute',
  migrationReportRead: 'designer.migration.report.read',
  migrationReportExecute: 'designer.migration.report.execute',
  spreadsheetAccess: 'designer.spreadsheet.access',
  spreadsheetCreate: 'designer.spreadsheet.create',
  spreadsheetRead: 'designer.spreadsheet.read',
  spreadsheetEdit: 'designer.spreadsheet.edit',
  spreadsheetImport: 'designer.spreadsheet.import',
  spreadsheetExport: 'designer.spreadsheet.export',
  pdfViewerRead: 'designer.pdf-viewer.read',
  pdfViewerAnnotate: 'designer.pdf-viewer.annotate',
  pdfViewerForms: 'designer.pdf-viewer.forms',
  pdfViewerRedact: 'designer.pdf-viewer.redact',
} as const;

export type DesignerPermission = typeof designerPermissions[keyof typeof designerPermissions];

export function hasDesignerPermission(user: DesignerUser, permission: DesignerPermission): boolean {
  return user.permissions.includes(permission) && accountAllowsPermission(user, permission);
}

export function hasAnyDesignerPermission(user: DesignerUser, permissions: readonly string[]): boolean {
  return permissions.some(permission => user.permissions.includes(permission) && accountAllowsPermission(user, permission));
}

export function hasAccountCapability(user: DesignerUser | undefined, capability: string): boolean {
  // Older servers omit this field. New servers always send an explicit list,
  // including an empty list; the API remains the enforcement authority.
  return user?.accountCapabilities === undefined || user.accountCapabilities.includes(capability);
}

export function formatCapability(direction: 'import' | 'export', format: string): string {
  let normalized = format.trim().replace(/^\.+/, '').toLowerCase();
  normalized = ({ markdown: 'md', jpg: 'jpeg', tif: 'tiff', htm: 'html', word: 'docx', excel: 'xlsx' } as Record<string, string>)[normalized] ?? normalized;
  if (direction === 'import' && ['png', 'jpeg', 'gif', 'webp', 'bmp', 'tiff'].includes(normalized)) normalized = 'image';
  return `${direction}.${normalized}`;
}

function accountAllowsPermission(user: DesignerUser, permission: string): boolean {
  const capability = ({
    'designer.pdf.signatures.sign': 'pdf.signature.sign',
    'designer.pdf.signatures.key-providers.use': 'pdf.signature.sign',
    'designer.pdf.signatures.key-providers.manage': 'pdf.signature.sign',
    'designer.pdf.signatures.trust-policies.manage': 'pdf.trust.inspect',
    'designer.pdf.signatures.validate': 'pdf.signature.validate',
    'designer.pdf.redactions.apply': 'pdf.redaction.apply',
    'designer.pdf-viewer.redact': 'pdf.redaction.apply',
    'designer.pdf.redactions.inspect': 'pdf.redaction.inspect',
    'designer.pdf.compliance.convert': 'pdf.compliance.convert',
    'designer.pdf.compliance.validate': 'pdf.compliance.validate',
    'designer.pdf.trust.inspect': 'pdf.trust.inspect',
    'designer.pdf.e-invoices.create': 'pdf.e-invoice.create',
    'designer.pdf.e-invoices.generate': 'pdf.e-invoice.create',
    'designer.pdf.e-invoices.validate': 'pdf.e-invoice.validate',
    'designer.pdf.e-invoices.read': 'pdf.e-invoice.read',
    'designer.pdf.e-invoices.attach': 'pdf.e-invoice.attach',
  } as Record<string, string>)[permission];
  if (capability) return hasAccountCapability(user, capability);
  if (permission.startsWith('designer.migration.')) {
    const match = /^designer\.migration\.(pdf|spreadsheet|report)\.(code|viewer|designer)\./.exec(permission);
    const domain = /^designer\.migration\.(pdf|spreadsheet|report)\./.exec(permission);
    return match
      ? hasAccountCapability(user, `migration.${match[1]}.${match[2]}`)
      : user.accountCapabilities === undefined || user.accountCapabilities.some(value =>
          value.startsWith(domain ? `migration.${domain[1]}.` : 'migration.'));
  }
  return true;
}

export function migrationKindPermission(
  domain: 'pdf' | 'spreadsheet' | 'report',
  kind: 'code' | 'viewer' | 'designer' | 'datasource',
  action: 'read' | 'execute',
): string {
  return `designer.migration.${domain}.${kind}.${action}`;
}

export function migrationProviderPermission(
  domain: 'pdf' | 'spreadsheet' | 'report',
  provider: string,
): string {
  return `designer.migration.${domain}.provider.${provider}.execute`;
}

export function canonicalMigrationProviderPermission(
  domain: 'pdf' | 'spreadsheet' | 'report',
  kind: 'code' | 'viewer' | 'designer' | 'datasource',
  provider: string,
): string {
  return `designer.migration.${domain}.${kind}.provider.${provider}.execute`;
}
