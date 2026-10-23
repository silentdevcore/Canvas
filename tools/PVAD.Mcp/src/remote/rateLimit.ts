import type { RequestHandler } from 'express';

interface WindowState {
  count: number;
  resetAt: number;
}

export function createRateLimit(requestsPerMinute: number): RequestHandler {
  const windows = new Map<string, WindowState>();
  return (request, response, next) => {
    const now = Date.now();
    const key = request.ip || request.socket.remoteAddress || 'unknown';
    const current = windows.get(key);
    const state = !current || current.resetAt <= now
      ? { count: 0, resetAt: now + 60_000 }
      : current;
    state.count += 1;
    windows.set(key, state);
    response.setHeader('X-RateLimit-Limit', requestsPerMinute);
    response.setHeader('X-RateLimit-Remaining', Math.max(0, requestsPerMinute - state.count));
    response.setHeader('X-RateLimit-Reset', Math.ceil(state.resetAt / 1000));
    if (state.count > requestsPerMinute) {
      response.status(429).json({
        jsonrpc: '2.0',
        error: { code: -32029, message: 'MCP request rate limit exceeded.' },
        id: null,
      });
      return;
    }
    if (windows.size > 10_000) {
      for (const [entry, value] of windows) if (value.resetAt <= now) windows.delete(entry);
    }
    next();
  };
}
