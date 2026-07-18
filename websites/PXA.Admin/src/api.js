const authBase = '/api/pxa/v1/auth';
const adminUsersBase = '/api/pxa/v1/admin/users';
const adminOrganizationsBase = '/api/pxa/v1/admin/organizations';
const adminSubscriptionsBase = '/api/pxa/v1/admin/subscriptions';

async function request(path, options = {}) {
  const response = await fetch(path, {
    credentials: 'include',
    ...options,
    headers: {
      Accept: 'application/json',
      ...options.headers,
    },
  });

  if (response.status === 204) return null;

  const contentType = response.headers.get('content-type') || '';
  const body = contentType.includes('application/json') ? await response.json() : null;
  if (!response.ok) {
    const error = new Error(body?.detail || body?.title || `Request failed with status ${response.status}.`);
    error.status = response.status;
    error.body = body;
    throw error;
  }

  return body;
}

export async function currentUser() {
  return request(`${authBase}/me`);
}

export async function login(identifier, password, rememberMe) {
  const { token } = await request(`${authBase}/csrf`);
  return request(`${authBase}/login`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-PXA-CSRF': token,
    },
    body: JSON.stringify({ identifier, password, rememberMe }),
  });
}

export async function logout() {
  const { token } = await request(`${authBase}/csrf`);
  return request(`${authBase}/logout`, {
    method: 'POST',
    headers: {
      'X-PXA-CSRF': token,
    },
  });
}

export async function getAdminUsers({ search = '', status = '', page = 1, pageSize = 25 } = {}) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (search) query.set('search', search);
  if (status) query.set('status', status);
  return request(`${adminUsersBase}?${query}`);
}

export async function getAdminUser(userId) {
  return request(`${adminUsersBase}/${encodeURIComponent(userId)}`);
}

async function adminMutation(path, method, body) {
  const { token } = await request(`${authBase}/csrf`);
  return request(path, {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-PXA-CSRF': token,
    },
    body: JSON.stringify(body),
  });
}

export async function updateAdminUserStatus(userId, isActive) {
  return adminMutation(`${adminUsersBase}/${encodeURIComponent(userId)}/status`, 'PATCH', { isActive });
}

export async function updateAdminUserRoles(userId, roles) {
  return adminMutation(`${adminUsersBase}/${encodeURIComponent(userId)}/roles`, 'PUT', { roles });
}

export async function getAdminOrganizations({ search = '', status = '', page = 1, pageSize = 25 } = {}) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (search) query.set('search', search);
  if (status) query.set('status', status);
  return request(`${adminOrganizationsBase}?${query}`);
}

export async function getAdminOrganization(organizationId) {
  return request(`${adminOrganizationsBase}/${encodeURIComponent(organizationId)}`);
}

export async function getAdminOrganizationMembers(organizationId) {
  return request(`${adminOrganizationsBase}/${encodeURIComponent(organizationId)}/members`);
}

export async function createAdminOrganization(name, slug) {
  return adminMutation(adminOrganizationsBase, 'POST', { name, slug });
}

export async function updateAdminOrganization(organizationId, changes) {
  return adminMutation(`${adminOrganizationsBase}/${encodeURIComponent(organizationId)}`, 'PATCH', changes);
}

export async function addAdminOrganizationMember(organizationId, email, roles) {
  return adminMutation(
    `${adminOrganizationsBase}/${encodeURIComponent(organizationId)}/members`,
    'POST',
    { email, roles });
}

export async function removeAdminOrganizationMember(organizationId, userId) {
  const { token } = await request(`${authBase}/csrf`);
  return request(
    `${adminOrganizationsBase}/${encodeURIComponent(organizationId)}/members/${encodeURIComponent(userId)}`,
    { method: 'DELETE', headers: { 'X-PXA-CSRF': token } });
}

export async function switchOrganization(organizationId) {
  return adminMutation(`${authBase}/switch-organization`, 'POST', { organizationId });
}

export async function createAdminInvitation(email, displayName, roles) {
  return adminMutation('/api/pxa/v1/admin/invitations', 'POST', { email, displayName, roles });
}

export async function getAdminMail({ status = '', page = 1, pageSize = 25 } = {}) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (status) query.set('status', status);
  return request(`/api/pxa/v1/admin/mail?${query}`);
}

export async function getAdminSubscriptions({ status = '', edition = '', page = 1, pageSize = 25 } = {}) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (status) query.set('status', status);
  if (edition) query.set('edition', edition);
  return request(`${adminSubscriptionsBase}?${query}`);
}

export async function createAdminSubscription(subscription) {
  return adminMutation(adminSubscriptionsBase, 'POST', subscription);
}

export async function getAdminSubscription(subscriptionId) {
  return request(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}`);
}

export async function getAdminSubscriptionSeats(subscriptionId) {
  return request(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/seats`);
}

export async function getAdminSubscriptionHistory(subscriptionId) {
  return request(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/history`);
}

export async function updateAdminSubscription(subscriptionId, changes) {
  return adminMutation(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}`, 'PATCH', changes);
}

export async function assignAdminSubscriptionSeat(subscriptionId, membershipId) {
  return adminMutation(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/seats/${encodeURIComponent(membershipId)}`, 'POST');
}

export async function revokeAdminSubscriptionSeat(subscriptionId, membershipId) {
  const { token } = await request(`${authBase}/csrf`);
  return request(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/seats/${encodeURIComponent(membershipId)}`, {
    method: 'DELETE', headers: { 'X-PXA-CSRF': token },
  });
}

export async function extendAdminTrial(subscriptionId, days) {
  return adminMutation(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/trial/extend`, 'POST', { days });
}

export async function renewAdminSubscription(subscriptionId, periodEndsAt) {
  return adminMutation(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/renew`, 'POST', { periodEndsAt });
}

export async function startAdminGracePeriod(subscriptionId, endsAt) {
  return adminMutation(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/grace-period`, 'POST', { endsAt });
}

export async function cancelAdminSubscription(subscriptionId, effectiveAt) {
  return adminMutation(`${adminSubscriptionsBase}/${encodeURIComponent(subscriptionId)}/cancel`, 'POST', { effectiveAt });
}

export async function getAdminMailStatus() {
  return request('/api/pxa/v1/admin/mail/status');
}

export async function retryAdminMail(messageId) {
  return adminMutation(`/api/pxa/v1/admin/mail/${encodeURIComponent(messageId)}/retry`, 'POST');
}

export async function cancelAdminMail(messageId) {
  return adminMutation(`/api/pxa/v1/admin/mail/${encodeURIComponent(messageId)}/cancel`, 'POST');
}

export async function acceptInvitation(token, password, displayName) {
  return adminMutation(`${authBase}/accept-invitation`, 'POST', { token, password, displayName });
}

export async function requestPasswordReset(email) {
  return adminMutation(`${authBase}/password-reset/request`, 'POST', { email });
}

export async function confirmPasswordReset(token, newPassword) {
  return adminMutation(`${authBase}/password-reset/confirm`, 'POST', { token, newPassword });
}
