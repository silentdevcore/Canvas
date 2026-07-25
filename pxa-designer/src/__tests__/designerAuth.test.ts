import {
  describeDesignerAuthError,
  DesignerAuthError,
} from '@/auth/designerAuth';

function error(code?: string, status = 403, message = 'Denied'): DesignerAuthError {
  const value = new DesignerAuthError(message);
  value.code = code;
  value.status = status;
  return value;
}

describe('Designer authentication states', () => {
  test.each([
    ['PXA_DESIGNER_VERIFICATION_REQUIRED', 'Email verification required'],
    ['PXA_DESIGNER_ACCOUNT_DISABLED', 'Account disabled'],
    ['PXA_ORGANIZATION_INACTIVE', 'Organization unavailable'],
    ['PXA_TRIAL_EXPIRED', 'Designer subscription expired'],
    ['PXA_ENTITLEMENT_DENIED', 'Designer access not included'],
    ['PXA_API_VERSION_INCOMPATIBLE', 'Designer update required'],
    ['PXA_DESIGNER_SESSION_EXPIRED', 'Session expired'],
  ])('maps %s to an explicit access state', (code, title) => {
    expect(describeDesignerAuthError(error(code)).title).toBe(title);
  });

  test('offers retry without Account navigation while the API is offline', () => {
    const offline = error(undefined, 0);
    offline.offline = true;

    expect(describeDesignerAuthError(offline)).toEqual(expect.objectContaining({
      title: 'Designer offline',
      retry: true,
      openAccount: false,
    }));
  });
});
