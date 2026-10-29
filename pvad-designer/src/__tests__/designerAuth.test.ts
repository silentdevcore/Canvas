import {
  describeDesignerAuthError,
  DesignerAuthError,
  isKnownDesignerAuthCode,
  safeDesignerReturnPath,
  shouldRedirectToAccount,
  designerMutationHeaders,
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
    ['PXA_DESIGNER_ACCOUNT_LOCKED', 'Account locked'],
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

  test('redirects anonymous routes to Account without looping failed callbacks', () => {
    const unauthorized = error(undefined, 401);

    expect(shouldRedirectToAccount('/pdf/create', unauthorized)).toBe(true);
    expect(shouldRedirectToAccount('/auth/callback', unauthorized)).toBe(false);
    expect(shouldRedirectToAccount('/pdf/create', error(undefined, 403))).toBe(false);
  });

  test('keeps only bounded Designer parameters in Account return paths', () => {
    expect(safeDesignerReturnPath(
      '/pdf/create',
      '?mode=code&templateId=template-42&returnUrl=https://attacker.example/private',
      '#editor',
    )).toBe('/pdf/create?mode=code&templateId=template-42#editor');
    expect(safeDesignerReturnPath('/pdf/migrations/designer', '?provider=nutrient-viewer', ''))
      .toBe('/pdf/migrations/designer?provider=nutrient-viewer');
    expect(safeDesignerReturnPath('//attacker.example', '?mode=code', '')).toBe('/');
    expect(safeDesignerReturnPath('/pdf/create', '?mode=arbitrary', '#unsafe/path')).toBe('/pdf/create');
  });

  test('classifies diagnostic codes without exposing unknown values to telemetry', () => {
    expect(isKnownDesignerAuthCode('PXA_TRIAL_EXPIRED')).toBe(true);
    expect(isKnownDesignerAuthCode('customer-private-diagnostic')).toBe(false);
    expect(isKnownDesignerAuthCode()).toBe(true);
  });
});

describe('non-fetch Designer mutation authorization', () => {
  const originalFetch = globalThis.fetch;
  afterEach(() => { globalThis.fetch = originalFetch; });
  test('requests a fresh same-origin CSRF token and identifies the Designer', async () => {
    const fetchMock = jest.fn().mockResolvedValue({ ok: true, status: 200, json: async () => ({ token: 'synthetic-token' }) });
    globalThis.fetch = fetchMock;
    await expect(designerMutationHeaders()).resolves.toEqual({
      'X-PVAD-Application': 'designer', 'X-PVAD-CSRF': 'synthetic-token',
    });
    expect(fetchMock).toHaveBeenCalledWith('/api/pvad/v1/auth/csrf', expect.objectContaining({ credentials: 'include' }));
  });
  test.each([null, {}, { token: '' }, { token: '  ' }])('rejects malformed CSRF response %j', async body => {
    globalThis.fetch = jest.fn().mockResolvedValue({ ok: true, status: 200, json: async () => body });
    await expect(designerMutationHeaders()).rejects.toThrow('valid CSRF token');
  });
});
