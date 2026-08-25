import { createRemoteJWKSet, jwtVerify, type JWTVerifyGetKey } from 'jose';
import { InvalidTokenError } from '@modelcontextprotocol/sdk/server/auth/errors.js';
import type { OAuthTokenVerifier } from '@modelcontextprotocol/sdk/server/auth/provider.js';
import type { AuthInfo } from '@modelcontextprotocol/sdk/server/auth/types.js';

import type { RemoteMcpConfig } from './config.js';

export class PxaJwtTokenVerifier implements OAuthTokenVerifier {
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
      const organizationId = requiredClaim(payload[this.config.organizationClaim], this.config.organizationClaim);
      const clientId = claimString(payload.client_id) ?? claimString(payload.azp) ?? subject;
      const scopes = readScopes(payload.scope, payload.scp);
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
  const values = typeof scope === 'string'
    ? scope.split(/\s+/)
    : Array.isArray(scp)
      ? scp.filter((value): value is string => typeof value === 'string')
      : typeof scp === 'string'
        ? scp.split(/\s+/)
        : [];
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

function requiredClaim(value: unknown, name: string): string {
  const claim = claimString(value);
  if (!claim || claim.length > 200) throw new InvalidTokenError(`The access token is missing ${name}.`);
  return claim;
}

function claimString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value.trim() : undefined;
}
