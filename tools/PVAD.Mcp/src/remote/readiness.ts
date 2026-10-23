import type { RemoteMcpConfig } from './config.js';

export interface DependencyReadiness {
  ready: boolean;
  api: 'ready' | 'unavailable';
  identity: 'ready' | 'unavailable';
}

export async function checkDependencyReadiness(
  config: RemoteMcpConfig,
  fetchImplementation: typeof fetch = fetch,
): Promise<DependencyReadiness> {
  const [api, identity] = await Promise.all([
    probe(new URL('/health/ready', config.apiUrl), config.readinessTimeoutMs, fetchImplementation),
    probe(config.jwksUri, config.readinessTimeoutMs, fetchImplementation),
  ]);
  return {
    ready: api && identity,
    api: api ? 'ready' : 'unavailable',
    identity: identity ? 'ready' : 'unavailable',
  };
}

async function probe(url: URL, timeoutMs: number, fetchImplementation: typeof fetch): Promise<boolean> {
  try {
    const response = await fetchImplementation(url, {
      method: 'GET',
      headers: { accept: 'application/json' },
      signal: AbortSignal.timeout(timeoutMs),
    });
    return response.ok;
  } catch {
    return false;
  }
}
