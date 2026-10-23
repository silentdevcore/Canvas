import { normalizeSecurityHeaders } from '../auth/securityHeaders';

test.each(['Application', 'CSRF', 'API-Key'])('normalizes %s without changing its value', suffix => {
  const headers = new Headers({ [`X-PXA-${suffix}`]: 'synthetic-value' });
  normalizeSecurityHeaders(headers);
  expect(headers.get(`X-PVAD-${suffix}`)).toBe('synthetic-value');
  expect(headers.has(`X-PXA-${suffix}`)).toBe(false);
  normalizeSecurityHeaders(headers);
  expect(headers.get(`X-PVAD-${suffix}`)).toBe('synthetic-value');
});

test('conflicts fail without exposing secret values', () => {
  const headers = new Headers({ 'X-PVAD-CSRF': 'private-new', 'X-PXA-CSRF': 'private-old' });
  expect(() => normalizeSecurityHeaders(headers)).toThrow('Conflicting security headers: X-PVAD-CSRF and X-PXA-CSRF.');
});

test('matching aliases collapse and Application matching is case-insensitive', () => {
  const headers = new Headers({ 'X-PVAD-CSRF': 'same', 'X-PXA-CSRF': 'same',
    'X-PVAD-Application': 'designer', 'X-PXA-Application': 'DESIGNER' });
  normalizeSecurityHeaders(headers);
  expect(headers.get('X-PVAD-CSRF')).toBe('same');
  expect(headers.get('X-PVAD-Application')).toBe('designer');
  expect(headers.has('X-PXA-CSRF')).toBe(false);
});
