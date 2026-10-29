import crypto from 'node:crypto';

import type { AuthInfo } from '@modelcontextprotocol/sdk/server/auth/types.js';
import type { Request, RequestHandler } from 'express';

interface RequestWithAuth extends Request {
  auth?: AuthInfo;
}

export interface McpLogEvent {
  level: 'info' | 'warn' | 'error';
  event: string;
  requestId?: string;
  route?: string;
  rpcMethod?: string;
  tool?: string;
  status?: number;
  durationMs?: number;
  clientRef?: string;
  subjectRef?: string;
  organizationRef?: string;
}

export type McpLogger = (event: McpLogEvent) => void;

export const stderrJsonLogger: McpLogger = (event) => {
  console.error(JSON.stringify({ timestamp: new Date().toISOString(), service: 'pvad-mcp', ...event }));
};

export class McpMetrics {
  private activeRequests = 0;
  private readonly requests = new Map<string, number>();
  private readonly requestDuration = new Map<string, { count: number; sum: number }>();
  private readonly tools = new Map<string, number>();
  private readonly dependencyReady = new Map<string, number>([['api', 0], ['identity', 0]]);

  startRequest(): void {
    this.activeRequests += 1;
  }

  finishRequest(route: string, method: string, status: number, durationSeconds: number, tool?: string): void {
    this.activeRequests = Math.max(0, this.activeRequests - 1);
    increment(this.requests, labels({ route, method, status: String(status) }));
    const durationKey = labels({ route, method });
    const duration = this.requestDuration.get(durationKey) ?? { count: 0, sum: 0 };
    duration.count += 1;
    duration.sum += durationSeconds;
    this.requestDuration.set(durationKey, duration);
    if (tool) increment(this.tools, labels({ tool, status: String(status) }));
  }

  setDependency(name: 'api' | 'identity', ready: boolean): void {
    this.dependencyReady.set(name, ready ? 1 : 0);
  }

  render(): string {
    const lines = [
      '# HELP pvad_mcp_active_requests Current HTTP requests handled by the MCP host.',
      '# TYPE pvad_mcp_active_requests gauge',
      `pvad_mcp_active_requests ${this.activeRequests}`,
      '# HELP pvad_mcp_http_requests_total Completed MCP host HTTP requests.',
      '# TYPE pvad_mcp_http_requests_total counter',
      ...mapMetrics('pvad_mcp_http_requests_total', this.requests),
      '# HELP pvad_mcp_request_duration_seconds Request duration by bounded route and method.',
      '# TYPE pvad_mcp_request_duration_seconds summary',
      ...[...this.requestDuration].flatMap(([key, value]) => [
        `pvad_mcp_request_duration_seconds_count${key} ${value.count}`,
        `pvad_mcp_request_duration_seconds_sum${key} ${value.sum.toFixed(6)}`,
      ]),
      '# HELP pvad_mcp_tool_calls_total MCP tool calls observed at the protocol endpoint.',
      '# TYPE pvad_mcp_tool_calls_total counter',
      ...mapMetrics('pvad_mcp_tool_calls_total', this.tools),
      '# HELP pvad_mcp_dependency_ready Whether a required MCP dependency is ready.',
      '# TYPE pvad_mcp_dependency_ready gauge',
      ...[...this.dependencyReady].map(([dependency, value]) =>
        `pvad_mcp_dependency_ready${labels({ dependency })} ${value}`),
      '',
    ];
    return lines.join('\n');
  }
}

export function observeRequests(metrics: McpMetrics, logger: McpLogger): RequestHandler {
  return (request: RequestWithAuth, response, next) => {
    const started = process.hrtime.bigint();
    metrics.startRequest();
    response.once('finish', () => {
      const durationMs = Number(process.hrtime.bigint() - started) / 1_000_000;
      const route = classifyRoute(request.path);
      const rpcMethod = readBoundedString(request.body?.method, 80);
      const tool = rpcMethod === 'tools/call' ? readBoundedString(request.body?.params?.name, 100) : undefined;
      metrics.finishRequest(route, request.method, response.statusCode, durationMs / 1_000, tool);
      if (route === '/metrics' || route.startsWith('/health/')) return;
      const auth = request.auth;
      logger({
        level: response.statusCode >= 500 ? 'error' : response.statusCode >= 400 ? 'warn' : 'info',
        event: 'mcp.http.completed',
        requestId: response.getHeader('X-Request-ID')?.toString(),
        route,
        rpcMethod,
        tool,
        status: response.statusCode,
        durationMs: Math.round(durationMs * 100) / 100,
        clientRef: identityReference(auth?.clientId),
        subjectRef: identityReference(auth?.extra?.subject),
        organizationRef: identityReference(auth?.extra?.organizationId),
      });
    });
    next();
  };
}

function classifyRoute(path: string): string {
  if (path === '/mcp') return '/mcp';
  if (path === '/metrics') return '/metrics';
  if (path.startsWith('/health/')) return '/health/:check';
  if (path.startsWith('/.well-known/oauth-protected-resource')) return '/.well-known/oauth-protected-resource';
  return '/other';
}

function identityReference(value: unknown): string | undefined {
  const identity = readBoundedString(value, 200);
  return identity ? crypto.createHash('sha256').update(identity).digest('hex').slice(0, 16) : undefined;
}

function readBoundedString(value: unknown, maximum: number): string | undefined {
  return typeof value === 'string' && value.length > 0 && value.length <= maximum ? value : undefined;
}

function increment(map: Map<string, number>, key: string): void {
  map.set(key, (map.get(key) ?? 0) + 1);
}

function mapMetrics(name: string, values: Map<string, number>): string[] {
  return [...values].map(([key, value]) => `${name}${key} ${value}`);
}

function labels(values: Record<string, string>): string {
  const entries = Object.entries(values).map(([key, value]) => `${key}="${escapeLabel(value)}"`);
  return `{${entries.join(',')}}`;
}

function escapeLabel(value: string): string {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"').replaceAll('\n', '\\n');
}
