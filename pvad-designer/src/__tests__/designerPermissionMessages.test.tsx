/** @jest-environment jsdom */
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import RequireDesignerPermission from '@/auth/RequireDesignerPermission';
import { DesignerAuthContextProvider } from '@/auth/DesignerAuthGate';
import type { DesignerUser } from '@/auth/designerAuth';
import { designerPermissions } from '@/auth/designerPermissions';

jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));

test.each([
  [[], [], designerPermissions.pdfImport, 'required'],
  [[designerPermissions.pdfImport], [], designerPermissions.pdfImport, 'account'],
  [[designerPermissions.migrationPdfExecute], [], designerPermissions.migrationPdfExecute, 'approval'],
] as const)('denial text follows actual role/account/approval cause (%s)', (permissions, capabilities, permission, prefix) => {
  const user = { permissions: [...permissions], accountCapabilities: [...capabilities] } as unknown as DesignerUser;
  render(<DesignerAuthContextProvider user={user}><RequireDesignerPermission permission={permission}>
    <p>Protected operation</p>
  </RequireDesignerPermission></DesignerAuthContextProvider>);
  expect(screen.getByRole('heading')).toHaveTextContent(`permissions.${prefix}Title`);
  expect(screen.getByText(`permissions.${prefix}Message`)).toBeInTheDocument();
  expect(screen.queryByText('Protected operation')).toBeNull();
});
