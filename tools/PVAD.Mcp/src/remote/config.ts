import { normalizePvadEnvironment } from '../../../configuration/environment.mjs';

export const REMOTE_READ_SCOPES = ['pvad.docs.read'] as const;

export interface RemoteMcpConfig {
  host: string;
  port: number;
  resourceUri: URL;
  issuer: string;
  jwksUri: URL;
  allowedOrigins: Set<string>;
  allowedHosts: Set<string>;
  organizationClaim: string;
  requestLimit: string;
  requestsPerMinute: number;
  apiUrl: URL;
  apiKey?: string;
  delegationSecret?: string;
  readinessTimeoutMs: number;
  allowInsecureInternalDependencies: boolean;
}

export function readRemoteMcpConfig(input: NodeJS.ProcessEnv = process.env): RemoteMcpConfig {
  const environment = normalizePvadEnvironment(input);
  const resourceUri = requiredUrl(environment.PVAD_MCP_RESOURCE_URI, 'PVAD_MCP_RESOURCE_URI');
  const issuer = requiredUrl(environment.PVAD_MCP_OAUTH_ISSUER, 'PVAD_MCP_OAUTH_ISSUER').href.replace(/\/$/, '');
  const jwksUri = requiredUrl(environment.PVAD_MCP_JWKS_URI, 'PVAD_MCP_JWKS_URI');
  const apiUrl = optionalUrl(environment.PVAD_API_URL, 'http://localhost:5086', 'PVAD_API_URL');
  const host = environment.PVAD_MCP_HOST?.trim() || '127.0.0.1';
  const port = boundedInteger(environment.PVAD_MCP_PORT, 3000, 1, 65_535, 'PVAD_MCP_PORT');
  const allowedOrigins = csvSet(environment.PVAD_MCP_ALLOWED_ORIGINS, normalizeOrigin);
  const allowedHosts = csvSet(environment.PVAD_MCP_ALLOWED_HOSTS, normalizeHost);
  allowedHosts.add(resourceUri.host.toLowerCase());
  if (isLoopbackHost(host)) {
    allowedHosts.add('localhost');
    allowedHosts.add('127.0.0.1');
    allowedHosts.add('[::1]');
  }
  if (!isLoopbackUrl(resourceUri) && resourceUri.protocol !== 'https:') {
    throw new Error('PVAD_MCP_RESOURCE_URI must use HTTPS outside loopback development.');
  }
  if (!isLoopbackUrl(new URL(issuer)) && !issuer.startsWith('https://')) {
    throw new Error('PVAD_MCP_OAUTH_ISSUER must use HTTPS outside loopback development.');
  }
  const allowInsecureInternalDependencies = parseBoolean(
    environment.PVAD_MCP_ALLOW_INSECURE_INTERNAL_DEPENDENCIES,
    false,
    'PVAD_MCP_ALLOW_INSECURE_INTERNAL_DEPENDENCIES',
  );
  if (!allowInsecureInternalDependencies && !isLoopbackUrl(jwksUri) && jwksUri.protocol !== 'https:') {
    throw new Error('PVAD_MCP_JWKS_URI must use HTTPS outside loopback development.');
  }

  return {
    host,
    port,
    resourceUri,
    issuer,
    jwksUri,
    allowedOrigins,
    allowedHosts,
    organizationClaim: environment.PVAD_MCP_ORGANIZATION_CLAIM?.trim() || 'pvad_organization',
    requestLimit: environment.PVAD_MCP_REQUEST_LIMIT?.trim() || '11mb',
    requestsPerMinute: boundedInteger(
      environment.PVAD_MCP_REQUESTS_PER_MINUTE,
      120,
      1,
      10_000,
      'PVAD_MCP_REQUESTS_PER_MINUTE',
    ),
    apiUrl,
    apiKey: environment.PVAD_API_KEY?.trim() || undefined,
    delegationSecret: environment.PVAD_MCP_DELEGATION_SECRET?.trim() || undefined,
    readinessTimeoutMs: boundedInteger(
      environment.PVAD_MCP_READINESS_TIMEOUT_MS,
      3_000,
      250,
      30_000,
      'PVAD_MCP_READINESS_TIMEOUT_MS',
    ),
    allowInsecureInternalDependencies,
  };
}

function parseBoolean(value: string | undefined, fallback: boolean, name: string): boolean {
  if (!value) return fallback;
  if (value.toLowerCase() === 'true') return true;
  if (value.toLowerCase() === 'false') return false;
  throw new Error(`${name} must be true or false.`);
}

function optionalUrl(value: string | undefined, fallback: string, name: string): URL {
  return requiredUrl(value?.trim() || fallback, name);
}

function requiredUrl(value: string | undefined, name: string): URL {
  if (!value?.trim()) throw new Error(`${name} is required.`);
  try {
    const url = new URL(value);
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.hash) {
      throw new Error();
    }
    return url;
  } catch {
    throw new Error(`${name} must be an absolute HTTP(S) URL without credentials or a fragment.`);
  }
}

function boundedInteger(
  value: string | undefined,
  fallback: number,
  minimum: number,
  maximum: number,
  name: string,
): number {
  if (!value) return fallback;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < minimum || parsed > maximum) {
    throw new Error(`${name} must be an integer between ${minimum} and ${maximum}.`);
  }
  return parsed;
}

function csvSet(value: string | undefined, normalize: (entry: string) => string): Set<string> {
  return new Set((value ?? '').split(',').map((entry) => entry.trim()).filter(Boolean).map(normalize));
}

function normalizeOrigin(value: string): string {
  const url = requiredUrl(value, 'PVAD_MCP_ALLOWED_ORIGINS');
  return url.origin.toLowerCase();
}

function normalizeHost(value: string): string {
  return value.toLowerCase().replace(/\.$/, '');
}

function isLoopbackUrl(url: URL): boolean {
  return isLoopbackHost(url.hostname);
}

function isLoopbackHost(value: string): boolean {
  return ['127.0.0.1', 'localhost', '::1', '[::1]'].includes(value.toLowerCase());
}
