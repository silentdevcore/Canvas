import { createRemoteJWKSet, jwtVerify, type JWTVerifyGetKey } from 'jose';
import { InvalidTokenError } from '@modelcontextprotocol/sdk/server/auth/errors.js';
import type { OAuthTokenVerifier } from '@modelcontextprotocol/sdk/server/auth/provider.js';
import type { AuthInfo } from '@modelcontextprotocol/sdk/server/auth/types.js';

import type { RemoteMcpConfig } from './config.js';
import { canonicalScopes, compatibleScopes } from '../core/scopeCompatibility.js';

export class PvadJwtTokenVerifier implements OAuthTokenVerifier {
  private readonly keySet: JWTVerifyGetKey;

  constructor(
    private readonly config: RemoteMcpConfig,
    keySet?: JWTVerifyGetKey,
  ) {
    this.keySet = keySet ?? createRemoteJWKSet(config.jwksUri);
  }

  async verifyAccessToken(token: string): Promise<AuthInfo> {
    try {
      const { payload } = await jwtVerify(token, this.keySet, {
        issuer: this.config.issuer,
        audience: this.config.resourceUri.href,
        algorithms: ['RS256', 'ES256'],
        clockTolerance: 5,
      });
      const subject = requiredClaim(payload.sub, 'sub');
      const configured = this.config.organizationClaim;
      const canonical = 'pvad_organization';
      const legacy = 'pxa_organization';
      if ((configured === canonical || configured === legacy) &&
          Object.hasOwn(payload, canonical) && Object.hasOwn(payload, legacy) &&
          payload[canonical] !== payload[legacy]) throw new InvalidTokenError('The organization context is inconsistent.');
      const organizationId = requiredClaim(configured === canonical || configured === legacy
        ? Object.hasOwn(payload, canonical) ? payload[canonical] : payload[legacy]
        : payload[configured], configured);
      const clientId = claimString(payload.client_id) ?? claimString(payload.azp) ?? subject;
      const scopes = compatibleScopes(readScopes(payload.scope, payload.scp));
      const expiresAt = requiredNumericDate(payload.exp, 'exp');
      return {
        token,
        clientId,
        scopes,
        expiresAt,
        resource: this.config.resourceUri,
        extra: { subject, organizationId },
      };
    } catch (error) {
      if (error instanceof InvalidTokenError) throw error;
      throw new InvalidTokenError('The access token is invalid, expired, or not intended for this MCP resource.');
    }
  }
}

function requiredNumericDate(value: unknown, name: string): number {
  if (typeof value !== 'number' || !Number.isInteger(value) || value <= 0) {
    throw new InvalidTokenError(`The access token is missing ${name}.`);
  }
  return value;
}

function readScopes(scope: unknown, scp: unknown): string[] {
  const parse = (value: unknown, arrayAllowed: boolean): string[] | undefined => {
    if (value === undefined) return undefined;
    if (typeof value === 'string') return value.split(/\s+/).filter(Boolean);
    if (arrayAllowed && Array.isArray(value) && value.every((entry) => typeof entry === 'string'))
      return value.map((entry: string) => entry.trim()).filter(Boolean);
    throw new InvalidTokenError('The access token contains invalid scopes.');
  };
  const primary = parse(scope, false);
  const secondary = parse(scp, true);
  if (primary && secondary && JSON.stringify(canonicalScopes(primary).sort()) !== JSON.stringify(canonicalScopes(secondary).sort()))
    throw new InvalidTokenError('The access token scope context is inconsistent.');
  return [...new Set(primary ?? secondary ?? [])];
}

function requiredClaim(value: unknown, name: string): string {
  const claim = claimString(value);
  if (!claim || claim.length > 200) throw new InvalidTokenError(`The access token is missing ${name}.`);
  return claim;
}

function claimString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value.trim() : undefined;
}
