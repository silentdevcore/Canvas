const localTenantKeys = [
  'editor-storage',
  'pxa-spreadsheet',
  'pxa-code-editor-draft-v2',
];

const sessionTenantKeys = [
  'pxa_migration_designer_handoff',
  'pdf_viewer_handoff',
];

export function clearDesignerTenantState(): void {
  localTenantKeys.forEach(key => localStorage.removeItem(key));
  sessionTenantKeys.forEach(key => sessionStorage.removeItem(key));
}
