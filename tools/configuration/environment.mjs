export function readPvadEnvironment(name, environment = process.env) {
  const legacy = name.replace('PVAD_', 'PXA_');
  if (legacy === name) throw new Error(`Expected a PVAD environment key: ${name}`);
  const current = environment[name];
  const previous = environment[legacy];
  if (current !== undefined && previous !== undefined && current !== previous)
    throw new Error(`Conflicting configuration keys: ${name} and ${legacy}.`);
  return current ?? previous;
}

export function normalizePvadEnvironment(environment = process.env) {
  const result = { ...environment };
  for (const key of Object.keys(environment)) {
    if (!/^(?:VITE_)?(?:PXA|PVAD)_/.test(key)) continue;
    const canonical = key.replace('PXA_', 'PVAD_');
    result[canonical] = readPvadEnvironment(canonical, environment);
  }
  return result;
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
