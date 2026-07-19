const authBase = '/api/pxa/v1/auth';
const accountProfileBase = '/api/pxa/v1/account/profile';

export interface OrganizationInfo {
  id: string;
  name: string;
  slug: string;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  displayName: string;
  roles: string[];
  organizations: OrganizationInfo[];
  activeOrganizationId: string | null;
  lastLoginAt: string | null;
}

export interface LoginResponse {
  user: UserInfo;
}

export interface RegistrationAcceptedResponse {
  message: string;
}

export interface RegisterAccountValues {
  accountType: string;
  displayName: string;
  email: string;
  password: string;
  companyName: string | null;
  organizationSlug: string | null;
  country: string | null;
  locale: string | null;
  acceptTerms: boolean;
  acceptPrivacy: boolean;
}

export interface AccountProfileResponse {
  id: string;
  displayName: string;
  email: string;
  pendingEmail: string | null;
  locale: string;
  country: string | null;
  roles: string[];
}

export class ApiError extends Error {
  status?: number;
  body?: unknown;
  code?: string;
  traceId?: string;
  isOffline?: boolean;
  override cause?: unknown;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T | null> {
  let response: Response;
  try {
    response = await fetch(path, {
      credentials: 'include',
      ...options,
      headers: { Accept: 'application/json', ...options.headers },
    });
  } catch (cause) {
    const error = new ApiError('PXA Account cannot reach the API. Previously loaded data may be stale.');
    error.isOffline = true;
    error.cause = cause;
    window.dispatchEvent(new CustomEvent('pxa:api-offline', { detail: error }));
    throw error;
  }

  if (response.status === 204) {
    window.dispatchEvent(new Event('pxa:api-online'));
    return null;
  }

  const contentType = response.headers.get('content-type') || '';
  const body = contentType.includes('application/json') ? await response.json() : null;
  if (!response.ok) {
    const error = new ApiError(body?.detail || body?.title || `Request failed with status ${response.status}.`);
    error.status = response.status;
    error.body = body;
    error.code = body?.code;
    error.traceId = body?.traceId;
    if (response.status === 401)
      window.dispatchEvent(new CustomEvent('pxa:session-expired', { detail: error }));
    else if (response.status === 403 && error.code !== 'PXAAPI010')
      window.dispatchEvent(new CustomEvent('pxa:access-denied', { detail: error }));
    throw error;
  }

  window.dispatchEvent(new Event('pxa:api-online'));
  return body as T;
}

async function csrf(): Promise<string> {
  const response = await request<{ token: string }>(`${authBase}/csrf`);
  return response!.token;
}

async function mutation<T>(path: string, body: unknown, method = 'POST'): Promise<T | null> {
  return request<T>(path, {
    method,
    headers: { 'Content-Type': 'application/json', 'X-PXA-CSRF': await csrf() },
    body: JSON.stringify(body),
  });
}

export const currentUser = () => request<UserInfo>(`${authBase}/me`);
export const login = (identifier: string, password: string, rememberMe: boolean) =>
  mutation<LoginResponse>(`${authBase}/login`, { identifier, password, rememberMe });
export const logout = () => mutation(`${authBase}/logout`, {});
export const register = (values: RegisterAccountValues) =>
  mutation<RegistrationAcceptedResponse>(`${authBase}/register`, values);
export const verifyEmail = (token: string) => mutation(`${authBase}/verify-email`, { token });
export const resendVerification = (email: string) =>
  mutation<RegistrationAcceptedResponse>(`${authBase}/resend-verification`, { email });
export const requestPasswordReset = (email: string) =>
  mutation(`${authBase}/password-reset/request`, { email });
export const confirmPasswordReset = (token: string, newPassword: string) =>
  mutation(`${authBase}/password-reset/confirm`, { token, newPassword });
export const confirmEmailChange = (token: string) =>
  mutation(`${authBase}/email-change/confirm`, { token });

export const getAccountProfile = () => request<AccountProfileResponse>(accountProfileBase);
export const updateDisplayName = (displayName: string) =>
  mutation<AccountProfileResponse>(`${accountProfileBase}/display-name`, { displayName }, 'PATCH');
export const updateLocale = (locale: string) =>
  mutation<AccountProfileResponse>(`${accountProfileBase}/locale`, { locale }, 'PATCH');
export const requestEmailChange = (newEmail: string) =>
  mutation<RegistrationAcceptedResponse>(`${accountProfileBase}/email-change/request`, { newEmail });
export const changePassword = (currentPassword: string, newPassword: string) =>
  mutation(`${accountProfileBase}/password-change`, { currentPassword, newPassword });
