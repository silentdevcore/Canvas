/** @jest-environment jsdom */
import { fireEvent, render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MemoryRouter } from 'react-router-dom';
import SpreadsheetImportPage from '@/pages/SpreadsheetImportPage';
import { SpreadsheetService } from '@/services/SpreadsheetService';

jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }), Trans: () => null }));
jest.mock('@/styles/migrations.css', () => ({}));
jest.mock('@/auth/DesignerAuthGate', () => ({ useOptionalDesignerAuth: () => ({ user: { accountCapabilities: ['import.csv'] } }) }));

test('the actual spreadsheet chooser narrows external formats to account grants', () => {
  const upload = jest.spyOn(SpreadsheetService, 'importXlsx');
  try {
    const { container } = render(<MemoryRouter><SpreadsheetImportPage /></MemoryRouter>);
    const input = container.querySelector<HTMLInputElement>('input[type=file]')!;
    expect(input.accept).toBe('.csv,.json');
    fireEvent.change(input, { target: { files: [new File(['synthetic'], 'not-granted.xlsx')] } });
    expect(screen.getByRole('alert')).toHaveTextContent('permissions.formatDenied');
    expect(upload).not.toHaveBeenCalled();
  } finally { upload.mockRestore(); }
});

test('native edit mode accepts only native Workbook JSON and explicitly refuses deferred XLS', () => {
  const upload = jest.spyOn(SpreadsheetService, 'importXlsx');
  try {
    const { container } = render(<MemoryRouter><SpreadsheetImportPage variant="edit" /></MemoryRouter>);
    const input = container.querySelector<HTMLInputElement>('input[type=file]')!;
    expect(input.accept).toBe('.json');
    fireEvent.change(input, { target: { files: [new File(['synthetic'], 'DEFERRED.XLS')] } });
    expect(screen.getByRole('alert')).toHaveTextContent('permissions.xlsDeferred');
    expect(upload).not.toHaveBeenCalled();
  } finally { upload.mockRestore(); }
});
