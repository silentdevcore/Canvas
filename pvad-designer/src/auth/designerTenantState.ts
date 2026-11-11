import { clearDocumentHandoffs } from './documentHandoff';
import { clearNativeProjects } from '@/native/nativeProjectLaunch';

const localTenantKeys = [
  'editor-storage',
  'pvad-spreadsheet',
  'canvas-spreadsheet',
  'pvad-code-editor-draft-v2',
];

const sessionTenantKeys = [
  'pvad_migration_designer_handoff',
  'pdf_viewer_handoff',
  'pvad_pdf_viewer_handoff',
];

export function clearDesignerTenantState(): void {
  clearDocumentHandoffs();
  clearNativeProjects();
  for (const key of localTenantKeys) {
    try { localStorage.removeItem(key); } catch { /* Continue clearing other keys. */ }
  }
  for (const key of sessionTenantKeys) {
    try { sessionStorage.removeItem(key); } catch { /* Continue clearing other keys. */ }
  }
}
