const authBase = '/api/pxa/v1/auth';

async function request(path, options = {}) {
  const response = await fetch(path, {
    credentials: 'include',
    ...options,
    headers: { Accept: 'application/json', ...options.headers },
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

async function csrf() {
  const response = await request(`${authBase}/csrf`);
  return response.token;
}

async function mutation(path, body) {
  return request(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-PXA-CSRF': await csrf() },
    body: JSON.stringify(body),
  });
}

export const currentUser = () => request(`${authBase}/me`);
export const login = (identifier, password, rememberMe) =>
  mutation(`${authBase}/login`, { identifier, password, rememberMe });
export const logout = () => mutation(`${authBase}/logout`, {});
export const register = (values) => mutation(`${authBase}/register`, values);
export const verifyEmail = (token) => mutation(`${authBase}/verify-email`, { token });
export const requestPasswordReset = (email) =>
  mutation(`${authBase}/password-reset/request`, { email });
export const confirmPasswordReset = (token, newPassword) =>
  mutation(`${authBase}/password-reset/confirm`, { token, newPassword });
