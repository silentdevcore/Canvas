/** @jest-environment jsdom */
import { clearDesignerTenantState } from '../auth/designerTenantState';

afterEach(() => jest.restoreAllMocks());

test('clears canonical and legacy unscoped drafts without clearing personal preferences', () => {
  localStorage.clear(); sessionStorage.clear();
  for (const key of ['pvad-code-editor-draft-v2', 'pvad-code-editor-draft-v2', 'pvad-spreadsheet', 'canvas-spreadsheet'])
    localStorage.setItem(key, 'private');
  localStorage.setItem('pvad-designer-element-favorites-v1:a', '["text"]');
  sessionStorage.setItem('pvad_migration_designer_handoff', 'private');
  clearDesignerTenantState();
  expect(localStorage.getItem('pvad-code-editor-draft-v2')).toBeNull();
  expect(localStorage.getItem('pvad-code-editor-draft-v2')).toBeNull();
  expect(localStorage.getItem('pvad-spreadsheet')).toBeNull();
  expect(localStorage.getItem('canvas-spreadsheet')).toBeNull();
  expect(sessionStorage.getItem('pvad_migration_designer_handoff')).toBeNull();
  expect(localStorage.getItem('pvad-designer-element-favorites-v1:a')).toBe('["text"]');
});

test('a failed removal does not interrupt remaining cleanup or sign-out', () => {
  const remove = jest.spyOn(Storage.prototype, 'removeItem').mockImplementationOnce(() => {
    throw new Error('blocked');
  });
  expect(() => clearDesignerTenantState()).not.toThrow();
  expect(remove).toHaveBeenCalledWith('pvad-code-editor-draft-v2');
  expect(remove).toHaveBeenCalledWith('pdf_viewer_handoff');
});
