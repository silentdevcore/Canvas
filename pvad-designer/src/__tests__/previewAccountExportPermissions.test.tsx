/** @jest-environment jsdom */
import { fireEvent, render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MemoryRouter } from 'react-router-dom';
import LivePreview from '@/components/Preview/LivePreview';
import ExportService from '@/services/ExportService';
import type { DesignerUser } from '@/auth/designerAuth';

let mockUser: DesignerUser;
jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
jest.mock('@/auth/DesignerAuthGate', () => ({ useDesignerAuth: () => ({ user: mockUser }) }));
jest.mock('@/services/ExportService', () => ({ __esModule: true, default: { exportToJSON: jest.fn() },
  DesignerAccessibilityPreflightError: class extends Error {} }));
jest.mock('@/components/Editor/ExportModal', () => ({ __esModule: true, default: () => null }));

const open = () => {
  render(<MemoryRouter><LivePreview template={{ id: 'synthetic', name: 'Synthetic', category: '', description: '' }}
    pages={[{ id: 'page', elements: [] }]} sharedElements={[]} onBack={() => {}} onExport={() => {}} /></MemoryRouter>);
  fireEvent.click(screen.getByRole('button', { name: 'export' }));
};
beforeEach(() => {
  mockUser = { permissions: ['designer.pdf.export', 'designer.pdf.print'], accountCapabilities: ['export.pdf'] } as DesignerUser;
  jest.clearAllMocks();
});

test('private quick menu retains PDF/print and omits JSON/image without format rights', () => {
  open();
  expect(screen.getByText('exportMenu.pdf.label')).toBeInTheDocument();
  expect(screen.getByText('exportMenu.print.label')).toBeInTheDocument();
  expect(screen.queryByText('exportMenu.json.label')).toBeNull();
  expect(screen.queryByText('exportMenu.image.label')).toBeNull();
});

test('company quick JSON export still invokes the download when explicitly granted', () => {
  mockUser.accountCapabilities = ['export.pdf', 'export.json', 'export.png'];
  open();
  expect(screen.getByText('exportMenu.image.label')).toBeInTheDocument();
  fireEvent.click(screen.getByText('exportMenu.json.label'));
  expect(ExportService.exportToJSON).toHaveBeenCalledTimes(1);
});

test('the handler denies a visible JSON button after its account grant is removed', () => {
  mockUser.accountCapabilities = ['export.json'];
  open();
  const button = screen.getByText('exportMenu.json.label');
  mockUser.accountCapabilities = [];
  fireEvent.click(button);
  expect(ExportService.exportToJSON).not.toHaveBeenCalled();
});

test('PDF and print cannot bypass an explicit empty format list', () => {
  mockUser.accountCapabilities = [];
  open();
  for (const format of ['pdf', 'print', 'json', 'image']) expect(screen.queryByText(`exportMenu.${format}.label`)).toBeNull();
});

test('printing additionally requires the role print permission', () => {
  mockUser.permissions = ['designer.pdf.export'];
  open();
  expect(screen.getByText('exportMenu.pdf.label')).toBeInTheDocument();
  expect(screen.queryByText('exportMenu.print.label')).toBeNull();
});
