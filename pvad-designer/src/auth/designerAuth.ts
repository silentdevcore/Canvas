import { readDesignerRuntimeConfig } from '@/config/runtimeConfig';
import { normalizeSecurityHeaders } from './securityHeaders';

const authBase = '/api/pvad/v1/auth';
const transactionKey = 'pvad.designer.auth-transaction';
const legacyTransactionKey = 'pxa.designer.auth-transaction';
const nativeFetch = globalThis.fetch;
const originalFetch: typeof fetch = nativeFetch
  ? nativeFetch.bind(globalThis)
  : (() => Promise.reject(new TypeError('Fetch is unavailable in this runtime.'))) as typeof fetch;
let csrfToken: string | null = null;

export interface DesignerUser {
  id: string;
  username: string;
  email: string;
  displayName: string;
  roles: string[];
  permissions: string[];
  organizations: DesignerOrganization[];
  activeOrganizationId: string | null;
  lastLoginAt: string | null;
  apiVersion?: string;
}

export interface DesignerOrganization {
  id: string;
  name: string;
  slug: string;
}

interface AuthTransaction {
  verifier: string;
  state: string;
  returnPath: string;
  createdAt: number;
}

export class DesignerAuthError extends Error {
  status?: number;
  code?: string;
  offline = false;
  cause?: unknown;
}

export interface DesignerAccessPresentation {
  title: string;
  message: string;
  retry: boolean;
  openAccount: boolean;
}

const knownDesignerAuthCodes = new Set([
  'PXA_DESIGNER_VERIFICATION_REQUIRED', 'PXAAPI010',
  'PXA_DESIGNER_ACCOUNT_DISABLED', 'PXAAPI015', 'PXA_DESIGNER_ACCOUNT_LOCKED',
  'PXA_DESIGNER_MEMBERSHIP_INACTIVE', 'PXA_ORGANIZATION_INACTIVE', 'PXAAPI016',
  'PXA_SUBSCRIPTION_CANCELLED', 'PXA_SUBSCRIPTION_INACTIVE', 'PXA_TRIAL_EXPIRED',
  'PXA_GRACE_PERIOD_EXPIRED', 'PXA_ENTITLEMENT_EXPIRED',
  'PXA_ENTITLEMENT_MISSING', 'PXA_ENTITLEMENT_DENIED',
  'PXA_DESIGNER_PERMISSION_REQUIRED', 'PXA_API_VERSION_INCOMPATIBLE',
  'PXA_DESIGNER_SESSION_EXPIRED',
]);

export function isKnownDesignerAuthCode(code?: string): boolean {
  return !code || knownDesignerAuthCodes.has(code);
}

export function describeDesignerAuthError(error: DesignerAuthError): DesignerAccessPresentation {
  if (error.offline) {
    return {
      title: 'Designer offline',
      message: 'PVADE Designer cannot reach the API. Check the connection and try again.',
      retry: true,
      openAccount: false,
    };
  }

  switch (error.code) {
    case 'PXA_DESIGNER_VERIFICATION_REQUIRED':
    case 'PXAAPI010':
      return {
        title: 'Email verification required',
        message: 'Verify your email address in PVADE Account before opening the Designer.',
        retry: true,
        openAccount: true,
      };
    case 'PXA_DESIGNER_ACCOUNT_DISABLED':
    case 'PXAAPI015':
      return {
        title: 'Account disabled',
        message: 'This account is disabled. Contact your organization administrator.',
        retry: false,
        openAccount: true,
      };
    case 'PXA_DESIGNER_ACCOUNT_LOCKED':
      return {
        title: 'Account locked',
        message: 'This account is temporarily locked. Wait before trying again or reset the password in PVADE Account.',
        retry: false,
        openAccount: true,
      };
    case 'PXA_DESIGNER_MEMBERSHIP_INACTIVE':
    case 'PXA_ORGANIZATION_INACTIVE':
    case 'PXAAPI016':
      return {
        title: 'Organization unavailable',
        message: 'Your organization or membership is suspended. Contact an organization administrator.',
        retry: false,
        openAccount: true,
      };
    case 'PXA_SUBSCRIPTION_CANCELLED':
    case 'PXA_SUBSCRIPTION_INACTIVE':
    case 'PXA_TRIAL_EXPIRED':
    case 'PXA_GRACE_PERIOD_EXPIRED':
    case 'PXA_ENTITLEMENT_EXPIRED':
      return {
        title: 'Designer subscription expired',
        message: error.message,
        retry: false,
        openAccount: true,
      };
    case 'PXA_ENTITLEMENT_MISSING':
    case 'PXA_ENTITLEMENT_DENIED':
      return {
        title: 'Designer access not included',
        message: error.message,
        retry: false,
        openAccount: true,
      };
    case 'PXA_DESIGNER_PERMISSION_REQUIRED':
      return {
        title: 'Designer permission required',
        message: 'Your organization role does not allow this Designer operation.',
        retry: false,
        openAccount: true,
      };
    case 'PXA_API_VERSION_INCOMPATIBLE':
      return {
        title: 'Designer update required',
        message: 'This Designer version is not compatible with the connected PVADE API.',
        retry: true,
        openAccount: false,
      };
    case 'PXA_DESIGNER_SESSION_EXPIRED':
      return {
        title: 'Session expired',
        message: 'Your Account session expired before Designer sign-in completed. Sign in again.',
        retry: false,
        openAccount: true,
      };
    default:
      return {
        title: error.status === 403 ? 'Designer access denied' : 'Designer unavailable',
        message: error.message,
        retry: error.status === 400,
        openAccount: true,
      };
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      credentials: 'include',
      ...init,
      headers: { Accept: 'application/json', ...init.headers },
    });
  } catch (cause) {
    const error = new DesignerAuthError('PVADE Designer cannot reach the API.');
    error.offline = true;
    error.cause = cause;
    throw error;
  }

  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const error = new DesignerAuthError(body?.detail || body?.title || 'Authentication failed.');
    error.status = response.status;
    error.code = body?.code;
    throw error;
  }
  return body as T;
}

async function csrf(): Promise<string> {
  const response = await request<{ token: string }>(`${authBase}/csrf`);
  if (typeof response?.token !== 'string' || !response.token.trim())
    throw new DesignerAuthError('The API did not return a valid CSRF token.');
  return response.token;
}

export async function designerMutationHeaders(): Promise<Record<string, string>> {
  return { 'X-PVAD-Application': 'designer', 'X-PVAD-CSRF': await csrf() };
}

export function installDesignerApiFetch(): void {
  globalThis.fetch = async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const requestUrl = input instanceof Request ? input.url : input.toString();
    const url = new URL(requestUrl, location.origin);
    if (url.origin !== location.origin || !url.pathname.startsWith('/api/'))
      return originalFetch(input, init);

    const method = (init.method || (input instanceof Request ? input.method : 'GET')).toUpperCase();
    const headers = new Headers(input instanceof Request ? input.headers : undefined);
    new Headers(init.headers).forEach((value, key) => headers.set(key, value));
    normalizeSecurityHeaders(headers);
    headers.set('X-PVAD-Application', 'designer');
    if (!['GET', 'HEAD', 'OPTIONS'].includes(method) && !headers.has('X-PVAD-CSRF')) {
      if (!csrfToken) {
        const tokenResponse = await originalFetch(`${authBase}/csrf`, {
          credentials: 'include',
          headers: { Accept: 'application/json', 'X-PVAD-Application': 'designer' },
        });
        const tokenBody = await tokenResponse.json();
        csrfToken = tokenBody.token;
      }
      headers.set('X-PVAD-CSRF', csrfToken!);
    }

    const response = await originalFetch(input, { ...init, credentials: 'include', headers });
    if (response.status === 400 && !['GET', 'HEAD', 'OPTIONS'].includes(method))
      csrfToken = null;
    return response;
  };
}

function encode(bytes: Uint8Array): string {
  let binary = '';
  bytes.forEach(value => { binary += String.fromCharCode(value); });
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
}

function randomValue(length: number): string {
  return encode(crypto.getRandomValues(new Uint8Array(length)));
}

async function challenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return encode(new Uint8Array(digest));
}

export function accountBaseUrl(): string {
  const configured = readDesignerRuntimeConfig().accountUrl;
  if (configured) return configured;
  const local = location.hostname === 'localhost' || location.hostname === '127.0.0.1';
  return local ? 'http://localhost:5178/' : 'https://account.pvade.com/';
}

export function accountPageUrl(path: string): string {
  return new URL(path.replace(/^\//, ''), accountBaseUrl()).toString();
}

export function shouldRedirectToAccount(pathname: string, error: DesignerAuthError): boolean {
  return error.status === 401 && pathname !== '/auth/callback';
}

export function safeDesignerReturnPath(
  pathname = location.pathname,
  search = location.search,
  hash = location.hash,
): string {
  if (!pathname.startsWith('/') || pathname.startsWith('//')) return '/';

  const source = new URLSearchParams(search);
  const filtered = new URLSearchParams();
  const mode = source.get('mode');
  if (mode === 'code' || mode === 'editor') filtered.set('mode', mode);
  for (const key of ['templateId', 'provider', 'category']) {
    const value = source.get(key);
    if (value && value.length <= 160 && /^[a-z0-9._-]+$/i.test(value)) filtered.set(key, value);
  }

  const query = filtered.toString();
  const safeHash = /^#[a-z0-9._-]{1,100}$/i.test(hash) ? hash : '';
  return `${pathname}${query ? `?${query}` : ''}${safeHash}`;
}

export async function redirectToAccount(): Promise<never> {
  const verifier = randomValue(64);
  const state = randomValue(32);
  const returnPath = safeDesignerReturnPath();
  const transaction: AuthTransaction = { verifier, state, returnPath, createdAt: Date.now() };
  sessionStorage.setItem(transactionKey, JSON.stringify(transaction));
  // A new transaction supersedes any unfinished pre-rename sign-in.
  sessionStorage.removeItem(legacyTransactionKey);

  const parameters = new URLSearchParams({
    designerOrigin: location.origin,
    returnPath,
    codeChallenge: await challenge(verifier),
    state,
  });
  location.replace(`${accountBaseUrl()}designer-authorize?${parameters}`);
  return new Promise<never>(() => undefined);
}

export async function exchangeCallback(): Promise<string> {
  const parameters = new URLSearchParams(location.search);
  const code = parameters.get('code') ?? '';
  const state = parameters.get('state') ?? '';
  history.replaceState({}, '', '/auth/callback');

  const rawTransaction = sessionStorage.getItem(transactionKey) ?? sessionStorage.getItem(legacyTransactionKey);
  sessionStorage.removeItem(transactionKey);
  sessionStorage.removeItem(legacyTransactionKey);
  if (!rawTransaction)
    throw new DesignerAuthError('The Designer sign-in transaction is missing. Start again.');

  let transaction: AuthTransaction;
  try { transaction = JSON.parse(rawTransaction); }
  catch { throw new DesignerAuthError('The Designer sign-in state is invalid or expired.'); }
  if (!transaction || typeof transaction.verifier !== 'string'
    || !/^[A-Za-z0-9_-]{43,128}$/.test(transaction.verifier)
    || typeof transaction.state !== 'string' || !/^[A-Za-z0-9_-]{16,256}$/.test(transaction.state)
    || !Number.isFinite(transaction.createdAt) || transaction.createdAt > Date.now()
    || Date.now() - transaction.createdAt >= 10 * 60 * 1000 || transaction.state !== state
    || !code || code.length > 2048)
    throw new DesignerAuthError('The Designer sign-in state is invalid or expired.');

  const response = await request<{ returnPath: string }>(`${authBase}/designer-handoff/exchange`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-PVAD-CSRF': await csrf() },
    body: JSON.stringify({
      code,
      state,
      codeVerifier: transaction.verifier,
      designerOrigin: location.origin,
    }),
  });
  if (typeof response.returnPath !== 'string' || !response.returnPath.startsWith('/')
    || response.returnPath.startsWith('//') || new URL(response.returnPath, location.origin).origin !== location.origin)
    throw new DesignerAuthError('The Designer return destination is invalid.');
  return response.returnPath;
}

export async function currentDesignerUser(): Promise<DesignerUser> {
  const user = await request<DesignerUser>(`${authBase}/me`);
  if (user.apiVersion && user.apiVersion !== '1') {
    const error = new DesignerAuthError(
      `PVADE Designer requires API v1, but the server reported v${user.apiVersion || 'unknown'}.`);
    error.status = 409;
    error.code = 'PXA_API_VERSION_INCOMPATIBLE';
    throw error;
  }
  return user;
}

export async function switchDesignerOrganization(organizationId: string): Promise<DesignerUser> {
  const response = await request<{ user: DesignerUser }>(`${authBase}/switch-organization`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-PVAD-CSRF': await csrf() },
    body: JSON.stringify({ organizationId }),
  });
  csrfToken = null;
  return response.user;
}

export async function signOutDesigner(): Promise<void> {
  await request(`${authBase}/logout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-PVAD-CSRF': await csrf() },
    body: '{}',
  });
  csrfToken = null;
}
