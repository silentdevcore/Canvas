/** @jest-environment jsdom */
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import ExportModal from '@/components/Editor/ExportModal';
import { DesignerAuthContextProvider } from '@/auth/DesignerAuthGate';
import type { DesignerUser } from '@/auth/designerAuth';
import ExportService from '@/services/ExportService';

jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
jest.mock('@/components/Editor/SignDocxModal', () => ({ __esModule: true, default: () => null }));

describe('rendered account export choices', () => {
  afterEach(() => jest.restoreAllMocks());
  for (const company of [false, true]) {
    test(`${company ? 'company' : 'private'} choices intersect account rights even with broad format discovery`, async () => {
      jest.spyOn(ExportService, 'listSupportedFormats').mockResolvedValue(
        ['word', 'odt', 'excel', 'html', 'xml', 'csv', 'png', 'jpeg', 'tiff', 'svg', 'md'].map(key => ({ key })) as never);
      const user = { id: 'synthetic', activeOrganizationId: 'synthetic-org', permissions: [],
        accountCapabilities: company
          ? ['export.pdf', 'export.docx', 'export.xlsx', 'export.odt', 'export.html', 'export.xml', 'export.csv', 'export.png', 'export.jpeg', 'export.tiff', 'export.svg', 'export.md', 'export.json']
          : ['export.pdf', 'export.docx', 'export.xlsx', 'export.odt'],
      } as unknown as DesignerUser;
      const { container } = render(<DesignerAuthContextProvider user={user}>
        <ExportModal template={{ id: 'synthetic', name: 'Synthetic', category: '', description: '' }}
          pages={[{ id: 'page', elements: [] }]} sharedElements={[]} onClose={() => {}} />
      </DesignerAuthContextProvider>);
      for (const key of ['pdf', 'word', 'odt', 'excel']) expect(screen.getByText(`export.formats.${key}.label`)).toBeInTheDocument();
      for (const key of ['json', 'html', 'xml', 'csv', 'png', 'jpeg', 'tiff', 'svg', 'md']) {
        if (company) expect(screen.getByText(`export.formats.${key}.label`)).toBeInTheDocument();
        else expect(screen.queryByText(`export.formats.${key}.label`)).not.toBeInTheDocument();
      }
      await waitFor(() => expect(container.querySelectorAll('.export-card--disabled')).toHaveLength(0));
    });
  }
});
