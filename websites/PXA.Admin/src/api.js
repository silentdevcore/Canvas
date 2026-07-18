const authBase = '/api/pxa/v1/auth';

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
