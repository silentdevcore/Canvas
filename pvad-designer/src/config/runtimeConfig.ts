export interface DesignerRuntimeConfig { accountUrl?: string; }
export interface RuntimeConfigGlobals { __PVAD_CONFIG__?: unknown; __PXA_CONFIG__?: unknown; }

export class RuntimeConfigurationError extends Error {
  constructor(key: string, conflict = false) {
    super(conflict ? `Conflicting runtime configuration keys: ${key}.` : `Invalid runtime configuration key: ${key}.`);
    this.name = 'RuntimeConfigurationError';
  }
}

function accountUrl(config: unknown, key: string): string | undefined {
  if (config === undefined) return undefined;
  if (!config || typeof config !== 'object' || Array.isArray(config)) throw new RuntimeConfigurationError(key);
  const value = (config as Record<string, unknown>).accountUrl;
  if (value === undefined) return undefined;
  if (typeof value !== 'string') throw new RuntimeConfigurationError(`${key}.accountUrl`);
  if (!value.trim()) return '';
  try {
    const url = new URL(value.trim());
    if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.search || url.hash
      || /[\\\u0000-\u0020\u007f]/.test(value.trim())) throw new Error('invalid');
    if (!url.pathname.endsWith('/')) url.pathname += '/';
    return url.toString();
  } catch {
    throw new RuntimeConfigurationError(`${key}.accountUrl`);
  }
}

/** Runtime scripts must run before the application bundle; no values are logged. */
export function readDesignerRuntimeConfig(globals: RuntimeConfigGlobals = globalThis as RuntimeConfigGlobals): Readonly<DesignerRuntimeConfig> {
  const current = accountUrl(globals.__PVAD_CONFIG__, '__PVAD_CONFIG__');
  const legacy = accountUrl(globals.__PXA_CONFIG__, '__PXA_CONFIG__');
  if (current !== undefined && legacy !== undefined && current !== legacy)
    throw new RuntimeConfigurationError('__PVAD_CONFIG__.accountUrl and __PXA_CONFIG__.accountUrl', true);
  return Object.freeze({ accountUrl: (current ?? legacy) || undefined });
}
