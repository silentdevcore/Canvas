export function readPvadEnvironment(name, environment = process.env) {
  if (!/^(?:VITE_)?PVAD_[A-Z0-9_]+$/.test(name)) throw new Error(`Expected a PVAD environment key: ${name}`);
  return environment[name];
}

export function normalizePvadEnvironment(environment = process.env) {
  return { ...environment };
}

export function resolvePvadDevApiTarget(environment = process.env) {
  const value = readPvadEnvironment('PVAD_DEV_API_URL', environment) ?? 'http://localhost:5086';
  try {
    const url = new URL(value);
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password ||
        url.pathname !== '/' || url.search || url.hash || /[\\\u0000-\u0020\u007f]/.test(value))
      throw new Error('invalid');
    return url.origin;
  } catch { throw new Error('Invalid configuration key: PVAD_DEV_API_URL.'); }
}
