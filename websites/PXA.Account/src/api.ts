const authBase = '/api/pxa/v1/auth';
const accountProfileBase = '/api/pxa/v1/account/profile';
const accountOrganizationBase = '/api/pxa/v1/account/organization';
const accountSubscriptionBase = '/api/pxa/v1/account/subscription';
const accountLicensesBase = '/api/pxa/v1/account/licenses';
const accountServiceAccountsBase = '/api/pxa/v1/account/service-accounts';
const accountSecurityBase = '/api/pxa/v1/account/security';
const accountClosureBase = '/api/pxa/v1/account/closure';

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
  permissions: string[];
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

export interface DesignerHandoffValues {
  designerOrigin: string;
  returnPath: string;
  codeChallenge: string;
  state: string;
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
  subscribeToNewsletter: boolean;
  campaignContext: Record<string, string> | null;
  returnUrl: string | null;
  termsVersionId: string | null;
  privacyVersionId: string | null;
}

export interface RegistrationPolicyDocument {
  id: string | null;
  version: string;
  locale: string;
  contentHash: string | null;
  effectiveAt: string | null;
}

export interface RegistrationPolicyResponse {
  available: boolean;
  databaseBacked: boolean;
  terms: RegistrationPolicyDocument | null;
  privacy: RegistrationPolicyDocument | null;
}

export interface AccountProfileResponse {
  id: string;
  displayName: string;
  email: string;
  pendingEmail: string | null;
  locale: string;
  country: string | null;
  roles: string[];
  termsAcceptedVersion: string | null;
  currentTermsVersionId: string | null;
  currentTermsVersion: string;
  requiresTermsAcceptance: boolean;
  privacyAcknowledgedVersion: string | null;
  currentPrivacyVersionId: string | null;
  currentPrivacyVersion: string;
  requiresPrivacyAcknowledgement: boolean;
  legalPolicyAvailable: boolean;
  marketingConsent: boolean;
}

export interface AccountOrganizationResponse {
  id: string;
  name: string;
  slug: string;
  status: string;
}

export interface AccountOrganizationMemberResponse {
  userId: string;
  membershipId: string;
  displayName: string;
  email: string;
  isActive: boolean;
  membershipStatus: string;
  roles: string[];
  createdAt: string;
}

export interface AccountEntitlementResponse {
  capability: string;
  enabled: boolean;
  limit: number | null;
  unit: string | null;
  expiresAt: string | null;
}

export interface AccountSubscriptionResponse {
  id: string;
  organizationName: string;
  edition: string;
  accountType: string;
  status: string;
  billingPeriod: string;
  deploymentMode: string;
  seatLimit: number | null;
  assignedSeats: number;
  startsAt: string;
  currentPeriodStartsAt: string;
  trialEndsAt: string | null;
  currentPeriodEndsAt: string | null;
  cancellationEffectiveAt: string | null;
  gracePeriodEndsAt: string | null;
  entitlements: AccountEntitlementResponse[];
}

export interface AccountSubscriptionSeatResponse {
  membershipId: string;
  userId: string;
  displayName: string;
  email: string;
  membershipStatus: string;
  assigned: boolean;
}

export interface AccountSubscriptionUsageItem {
  capability: string;
  operation: string;
  quantity: number;
  eventCount: number;
  lastOccurredAt: string;
}

export interface AccountSubscriptionUsageResponse {
  periodStartsAt: string;
  periodEndsAt: string | null;
  totalQuantity: number;
  items: AccountSubscriptionUsageItem[];
}

export interface AccountLicenseResponse {
  id: string;
  licenseNumber: string;
  edition: string;
  deploymentMode: string;
  status: string;
  validFrom: string;
  validUntil: string;
  instanceLimit: number;
  issuedAt: string;
  revokedAt: string | null;
  revocationReason: string | null;
}

export interface AccountLicenseValidationResponse {
  valid: boolean;
  status: string;
  validFrom: string;
  validUntil: string;
  code: string;
}

export interface AccountApiKeyResponse {
  id: string;
  serviceAccountId: string;
  name: string;
  prefix: string;
  expiresAt: string | null;
  lastUsedAt: string | null;
  createdAt: string;
  revokedAt: string | null;
}

export interface AccountServiceAccountResponse {
  id: string;
  name: string;
  isActive: boolean;
  createdAt: string;
  revokedAt: string | null;
  keys: AccountApiKeyResponse[];
}

export interface CreateAccountApiKeyResponse extends AccountApiKeyResponse {
  secret: string;
}

export interface AccountSessionResponse {
  id: string;
  userAgent: string;
  createdAt: string;
  lastSeenAt: string;
  expiresAt: string;
  revokedAt: string | null;
  isCurrent: boolean;
  isActive: boolean;
}

export interface AccountRevokeSessionsResponse {
  revokedCount: number;
}

export interface AccountClosureResponse {
  id: string;
  targetType: string;
  status: string;
  reason: string | null;
  requestedAt: string;
  scheduledPurgeAt: string;
  resolvedAt: string | null;
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
export const switchOrganization = (organizationId: string) =>
  mutation<LoginResponse>(`${authBase}/switch-organization`, { organizationId });
export const register = (values: RegisterAccountValues) =>
  mutation<RegistrationAcceptedResponse>(`${authBase}/register`, values);
export const getRegistrationPolicy = (locale: string) =>
  request<RegistrationPolicyResponse>(
    `${authBase}/registration-policy?locale=${encodeURIComponent(locale)}`);
export const verifyEmail = (token: string) => mutation(`${authBase}/verify-email`, { token });
export const resendVerification = (email: string, returnUrl: string | null = null) =>
  mutation<RegistrationAcceptedResponse>(`${authBase}/resend-verification`, { email, returnUrl });
export const acceptInvitation = (token: string, password: string | null, displayName: string | null = null) =>
  mutation(`${authBase}/accept-invitation`, { token, password, displayName });
export const createDesignerHandoff = (values: DesignerHandoffValues) =>
  mutation<{ redirectUrl: string }>(`${authBase}/designer-handoff`, values);
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
export const updateAccountConsent = (
  acceptTerms: boolean | null,
  acceptPrivacy: boolean | null,
  marketingConsent: boolean,
  termsVersionId: string | null = null,
  privacyVersionId: string | null = null,
) => mutation<AccountProfileResponse>(
  `${accountProfileBase}/consent`,
  { acceptTerms, acceptPrivacy, marketingConsent, termsVersionId, privacyVersionId },
  'PATCH',
);

export const getAccountOrganization = () => request<AccountOrganizationResponse>(accountOrganizationBase);
export const updateAccountOrganizationName = (name: string) =>
  mutation<AccountOrganizationResponse>(accountOrganizationBase, { name }, 'PATCH');
export const getAccountOrganizationMembers = () =>
  request<AccountOrganizationMemberResponse[]>(`${accountOrganizationBase}/members`);
export const inviteAccountOrganizationMember = (email: string, displayName: string, roles: string[]) =>
  mutation<AccountOrganizationMemberResponse>(`${accountOrganizationBase}/members`, { email, displayName, roles });
export const updateAccountOrganizationMemberRoles = (userId: string, roles: string[]) =>
  mutation<AccountOrganizationMemberResponse>(
    `${accountOrganizationBase}/members/${encodeURIComponent(userId)}/roles`, { roles }, 'PUT');
export const removeAccountOrganizationMember = (userId: string) =>
  mutation(`${accountOrganizationBase}/members/${encodeURIComponent(userId)}`, {}, 'DELETE');

export const getAccountSubscription = () => request<AccountSubscriptionResponse>(accountSubscriptionBase);
export const getAccountSubscriptionSeats = () =>
  request<AccountSubscriptionSeatResponse[]>(`${accountSubscriptionBase}/seats`);
export const getAccountSubscriptionUsage = () =>
  request<AccountSubscriptionUsageResponse>(`${accountSubscriptionBase}/usage`);

export const getAccountLicenses = () => request<AccountLicenseResponse[]>(accountLicensesBase);
export const validateAccountLicense = (licenseId: string) =>
  request<AccountLicenseValidationResponse>(`${accountLicensesBase}/${encodeURIComponent(licenseId)}/validate`);
export const accountLicenseDownloadUrl = (licenseId: string) =>
  `${accountLicensesBase}/${encodeURIComponent(licenseId)}/download`;

export const getAccountServiceAccounts = () =>
  request<AccountServiceAccountResponse[]>(accountServiceAccountsBase);
export const createAccountServiceAccount = (name: string) =>
  mutation<AccountServiceAccountResponse>(accountServiceAccountsBase, { name });
export const createAccountApiKey = (serviceAccountId: string, name: string, expiresAt: string | null) =>
  mutation<CreateAccountApiKeyResponse>(
    `${accountServiceAccountsBase}/${encodeURIComponent(serviceAccountId)}/keys`, { name, expiresAt });
export const revokeAccountApiKey = (serviceAccountId: string, keyId: string) =>
  mutation(`${accountServiceAccountsBase}/${encodeURIComponent(serviceAccountId)}/keys/${encodeURIComponent(keyId)}/revoke`, {});
export const revokeAccountServiceAccount = (serviceAccountId: string) =>
  mutation(`${accountServiceAccountsBase}/${encodeURIComponent(serviceAccountId)}/revoke`, {});

export const getAccountSessions = () => request<AccountSessionResponse[]>(`${accountSecurityBase}/sessions`);
export const revokeAccountSession = (sessionId: string) =>
  mutation(`${accountSecurityBase}/sessions/${encodeURIComponent(sessionId)}/revoke`, {});
export const revokeAllAccountSessions = () =>
  mutation<AccountRevokeSessionsResponse>(`${accountSecurityBase}/sessions/revoke-all`, {});

export const getAccountClosureRequests = () => request<AccountClosureResponse[]>(accountClosureBase);
export const requestAccountClosure = (reason: string | null) =>
  mutation<AccountClosureResponse>(`${accountClosureBase}/account`, { reason });
export const requestOrganizationClosure = (reason: string | null) =>
  mutation<AccountClosureResponse>(`${accountClosureBase}/organization`, { reason });
export const cancelAccountClosure = (requestId: string) =>
  mutation<AccountClosureResponse>(`${accountClosureBase}/${encodeURIComponent(requestId)}/cancel`, {});
