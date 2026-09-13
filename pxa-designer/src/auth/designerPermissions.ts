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
  return user.permissions.includes(permission);
}

export function migrationProviderPermission(
  domain: 'pdf' | 'spreadsheet' | 'report',
  provider: string,
): string {
  return `designer.migration.${domain}.provider.${provider}.execute`;
}
