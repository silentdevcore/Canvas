const authBase = '/api/pxa/v1/auth';
const adminUsersBase = '/api/pxa/v1/admin/users';

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
