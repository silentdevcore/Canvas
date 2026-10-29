import crypto from 'node:crypto';

import express, { type ErrorRequestHandler, type RequestHandler } from 'express';
import { StreamableHTTPServerTransport } from '@modelcontextprotocol/sdk/server/streamableHttp.js';
import { getOAuthProtectedResourceMetadataUrl } from '@modelcontextprotocol/sdk/server/auth/router.js';
import { requireBearerAuth } from '@modelcontextprotocol/sdk/server/auth/middleware/bearerAuth.js';
import type { OAuthTokenVerifier } from '@modelcontextprotocol/sdk/server/auth/provider.js';

import { createPvadMcpServer } from '../core/server.js';
import { compatibleScopes } from '../core/scopeCompatibility.js';
import { PvadJwtTokenVerifier } from './auth.js';
import { REMOTE_READ_SCOPES, type RemoteMcpConfig } from './config.js';
import { createRateLimit } from './rateLimit.js';
import { checkDependencyReadiness, type DependencyReadiness } from './readiness.js';
import { McpMetrics, observeRequests, stderrJsonLogger, type McpLogger } from './observability.js';

export interface RemoteMcpAppOptions {
  verifier?: OAuthTokenVerifier;
  readinessProbe?: () => Promise<DependencyReadiness>;
  metrics?: McpMetrics;
  logger?: McpLogger;
}

export function createRemoteMcpApp(config: RemoteMcpConfig, options: RemoteMcpAppOptions = {}) {
  const app = express();
  const resourceMetadataUrl = getOAuthProtectedResourceMetadataUrl(config.resourceUri);
  const verifier = options.verifier ?? new PvadJwtTokenVerifier(config);
  const metrics = options.metrics ?? new McpMetrics();
  const logger = options.logger ?? stderrJsonLogger;
  const bearerAuth = requireBearerAuth({
    verifier: {
      async verifyAccessToken(token) {
        const auth = await verifier.verifyAccessToken(token);
        return { ...auth, scopes: compatibleScopes(auth.scopes) };
      },
    },
    requiredScopes: [...REMOTE_READ_SCOPES],
    resourceMetadataUrl,
  });

  app.disable('x-powered-by');
  app.set('trust proxy', 1);
  app.use(securityHeaders);
  app.use(requestIdentity);
  app.use(express.json({ limit: config.requestLimit, type: ['application/json', 'application/*+json'] }));
  app.use(observeRequests(metrics, logger));

  app.get('/health/live', (_request, response) => {
    response.json({ status: 'healthy' });
  });
  app.get('/health/ready', async (_request, response) => {
    const readiness = await (options.readinessProbe?.() ?? checkDependencyReadiness(config));
    metrics.setDependency('api', readiness.api === 'ready');
    metrics.setDependency('identity', readiness.identity === 'ready');
    response.status(readiness.ready ? 200 : 503).json({
      status: readiness.ready ? 'ready' : 'not_ready',
      dependencies: { api: readiness.api, identity: readiness.identity },
    });
  });
  app.get('/metrics', (_request, response) => {
    response.type('text/plain; version=0.0.4; charset=utf-8').send(metrics.render());
  });

  const metadata = {
    resource: config.resourceUri.href,
    authorization_servers: [config.issuer],
    scopes_supported: [...REMOTE_READ_SCOPES, 'pvad.documents.validate'],
    bearer_methods_supported: ['header'],
    resource_name: 'PVADE MCP',
  };
  app.get('/.well-known/oauth-protected-resource', (_request, response) => response.json(metadata));
  app.get('/.well-known/oauth-protected-resource/mcp', (_request, response) => response.json(metadata));

  app.use('/mcp', validateHost(config));
  app.use('/mcp', validateOrigin(config));
  app.use('/mcp', createRateLimit(config.requestsPerMinute));
  app.use('/mcp', bearerAuth);

  app.post('/mcp', async (request, response) => {
    const server = createPvadMcpServer({
      apiUrl: config.apiUrl.href,
      apiKey: config.apiKey,
      delegationSecret: config.delegationSecret,
      correlationId: response.getHeader('X-Request-ID')?.toString(),
    });
    const transport = new StreamableHTTPServerTransport({
      sessionIdGenerator: undefined,
      enableJsonResponse: true,
    });
    try {
      await server.connect(transport);
      await transport.handleRequest(request, response, request.body);
    } catch (error) {
      console.error('Remote MCP request failed', {
        requestId: response.getHeader('X-Request-ID'),
        error: error instanceof Error ? error.name : 'UnknownError',
      });
      if (!response.headersSent) {
        response.status(500).json({
          jsonrpc: '2.0',
          error: { code: -32603, message: 'Internal MCP server error.' },
          id: null,
        });
      }
    } finally {
      await transport.close().catch(() => undefined);
      await server.close().catch(() => undefined);
    }
  });

  const methodNotAllowed: RequestHandler = (_request, response) => {
    response.status(405).json({
      jsonrpc: '2.0',
      error: { code: -32000, message: 'This stateless MCP endpoint accepts POST requests only.' },
      id: null,
    });
  };
  app.get('/mcp', methodNotAllowed);
  app.delete('/mcp', methodNotAllowed);
  app.use(errorHandler);
  return app;
}

const securityHeaders: RequestHandler = (_request, response, next) => {
  response.setHeader('Cache-Control', 'no-store');
  response.setHeader('Content-Security-Policy', "default-src 'none'; frame-ancestors 'none'");
  response.setHeader('Referrer-Policy', 'no-referrer');
  response.setHeader('X-Content-Type-Options', 'nosniff');
  response.setHeader('X-Frame-Options', 'DENY');
  next();
};

const requestIdentity: RequestHandler = (request, response, next) => {
  const supplied = request.header('X-Request-ID');
  const requestId = supplied && /^[A-Za-z0-9._-]{1,100}$/.test(supplied)
    ? supplied
    : crypto.randomUUID();
  response.setHeader('X-Request-ID', requestId);
  next();
};

function validateHost(config: RemoteMcpConfig): RequestHandler {
  return (request, response, next) => {
    const host = request.hostname.toLowerCase().replace(/\.$/, '');
    if (!config.allowedHosts.has(host) && !config.allowedHosts.has(request.headers.host?.toLowerCase() ?? '')) {
      response.status(403).json({
        jsonrpc: '2.0',
        error: { code: -32003, message: 'Host is not allowed.' },
        id: null,
      });
      return;
    }
    next();
  };
}

function validateOrigin(config: RemoteMcpConfig): RequestHandler {
  return (request, response, next) => {
    const origin = request.header('Origin');
    if (!origin) {
      next();
      return;
    }
    let normalized: string;
    try {
      normalized = new URL(origin).origin.toLowerCase();
    } catch {
      response.status(403).end();
      return;
    }
    if (!config.allowedOrigins.has(normalized)) {
      response.status(403).json({
        jsonrpc: '2.0',
        error: { code: -32003, message: 'Origin is not allowed.' },
        id: null,
      });
      return;
    }
    next();
  };
}

const errorHandler: ErrorRequestHandler = (error, _request, response, _next) => {
  if (error?.type === 'entity.too.large') {
    response.status(413).json({
      jsonrpc: '2.0',
      error: { code: -32013, message: 'MCP request body exceeds the configured limit.' },
      id: null,
    });
    return;
  }
  response.status(400).json({
    jsonrpc: '2.0',
    error: { code: -32700, message: 'Invalid JSON request body.' },
    id: null,
  });
};
