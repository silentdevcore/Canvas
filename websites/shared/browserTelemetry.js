import { pvadVersion } from './buildInfo.js';
import { readMigratedStorage } from './storageMigration.js';

const TELEMETRY_PATH = '/api/pvad/v1/telemetry/browser';
const MAX_BATCH_SIZE = 20;
const FLUSH_DELAY_MS = 1000;
const STATUS_LOOP_STORAGE_KEY = 'pvad-status-loop-v1';
const STATUS_RECOVERY_STORAGE_KEY = 'pvad-status-recovery-v1';
const currentTelemetry = () => globalThis.__pvadBrowserTelemetry ?? globalThis.__pxaBrowserTelemetry;
const statusKeys = new Set([
  'badRequest', 'unauthenticated', 'sessionExpired', 'forbidden',
  'entitlementDenied', 'suspended', 'trialExpired', 'notFound', 'conflict',
  'requestTimeout', 'expiredLink', 'fileTooLarge', 'rateLimited', 'unexpected',
  'upstreamUnavailable', 'unavailable', 'maintenance', 'timeout', 'offline',
  'incompatibleVersion', 'unsupportedBrowser',
]);
const recoveryActions = new Set([
  'back', 'home', 'reload', 'retry', 'signIn', 'subscription', 'support', 'account',
]);

export function createStatusLoopDetector({ threshold = 3, windowMs = 60_000, storage } = {}) {
  const targetStorage = storage ?? globalThis.sessionStorage;
  return (statusKey, route, now = Date.now()) => {
    if (!statusKeys.has(statusKey) || typeof route !== 'string' || !targetStorage) return false;
    try {
      const previous = JSON.parse(readMigratedStorage(targetStorage, STATUS_LOOP_STORAGE_KEY, 'pxa-status-loop-v1') || '[]');
      const recent = Array.isArray(previous)
        ? previous.filter(item => Number.isFinite(item?.at) && now - item.at <= windowMs)
        : [];
      const earlierMatches = recent.filter(item => item.statusKey === statusKey && item.route === route).length;
      recent.push({ statusKey, route, at: now });
      targetStorage.setItem(STATUS_LOOP_STORAGE_KEY, JSON.stringify(recent.slice(-32)));
      return earlierMatches + 1 === threshold;
    } catch {
      return false;
    }
  };
}

const applicationRoutes = {
  company: new Set(['home', 'products', 'pricing', 'about', 'support', 'contact', 'terms', 'privacy', 'license']),
  documentation: new Set(['home', 'editor', 'code', 'migration', 'api', 'cookbook']),
  demo: new Set(['home', 'pdf', 'designer', 'report', 'migration', 'spreadsheet', 'import-export']),
  account: new Set(['home', 'login', 'register', 'verify-email', 'dashboard', 'profile', 'organization', 'subscription', 'usage', 'licenses', 'developer-access', 'security', 'support', 'closure']),
  admin: new Set(['home', 'login', 'dashboard', 'users', 'organizations', 'roles', 'subscriptions', 'licenses', 'service-accounts', 'mail', 'audit', 'settings', 'documentation']),
  designer: new Set(['home', 'designer', 'templates', 'migrations', 'importer', 'converter', 'spreadsheet', 'docs', 'pdf-viewer']),
};

export function normalizeBrowserRoute(application, pathname = '/') {
  const routes = applicationRoutes[application];
  if (!routes) return 'other';

  const cleanPath = String(pathname).split(/[?#]/, 1)[0];
  const firstSegment = cleanPath.split('/').filter(Boolean)[0]?.toLowerCase() || 'home';
  const aliases = {
    products: 'products',
    product: 'products',
    create: 'designer',
    templates: 'templates',
    migrations: 'migrations',
    migration: 'migration',
    'spreadsheet-editor': 'spreadsheet',
    'spreadsheet-import': 'spreadsheet',
    'convert-to-pdf': 'converter',
    docs: application === 'documentation' ? 'home' : 'docs',
  };
  const route = aliases[firstSegment] || firstSegment.replace(/\.html$/, '');
  return routes.has(route) ? route : 'other';
}

export function classifyBrowserApiOutcome(status) {
  if (status === 401) return 'unauthorized';
  if (status === 403) return 'forbidden';
  if (status === 429) return 'rate_limited';
  if (status >= 500) return 'server_error';
  if (status >= 400) return 'client_error';
  return 'completed';
}

export function createBrowserEventDeduplicator(windowMs = 2000) {
  const recent = new Map();
  const deduplicatedTypes = new Set([
    'error', 'unhandled_rejection', 'api_failure', 'status_page', 'status_recovery',
  ]);

  return (event, now = Date.now()) => {
    if (!deduplicatedTypes.has(event.type)) return true;
    const key = `${event.type}|${event.outcome}|${event.route}|${event.name || ''}`;
    const previous = recent.get(key);
    if (previous !== undefined && now - previous < windowMs) return false;
    recent.set(key, now);
    if (recent.size > 64) {
      for (const [candidate, recordedAt] of recent) {
        if (now - recordedAt > windowMs) recent.delete(candidate);
      }
    }
    return true;
  };
}

function boundedCorrelationId(value) {
  return typeof value === 'string' && /^[A-Za-z0-9._:-]{1,64}$/.test(value) ? value : undefined;
}

export function recordBrowserStatusPage(statusKey, correlationId) {
  if (!statusKeys.has(statusKey)) return;
  currentTelemetry()?.record('status_page', 'shown', {
    name: statusKey,
    correlationId: boundedCorrelationId(correlationId),
  });
}

export function recordBrowserRecoveryAction(action, correlationId) {
  if (!recoveryActions.has(action)) return;
  rememberStatusRecovery(action, correlationId);
  currentTelemetry()?.record('status_recovery', 'selected', {
    name: action,
    correlationId: boundedCorrelationId(correlationId),
  });
}

export function rememberStatusRecovery(action, correlationId, storage = globalThis.sessionStorage, now = Date.now()) {
  if (!recoveryActions.has(action) || !storage) return false;
  try {
    storage.setItem(STATUS_RECOVERY_STORAGE_KEY, JSON.stringify({
      action,
      at: now,
      ...(boundedCorrelationId(correlationId) ? { correlationId } : {}),
    }));
    return true;
  } catch {
    return false;
  }
}

export function consumeStatusRecovery(storage = globalThis.sessionStorage, now = Date.now(), maxAgeMs = 120_000) {
  if (!storage) return undefined;
  try {
    const value = JSON.parse(readMigratedStorage(storage, STATUS_RECOVERY_STORAGE_KEY, 'pxa-status-recovery-v1') || 'null');
    storage.removeItem(STATUS_RECOVERY_STORAGE_KEY);
    storage.removeItem('pxa-status-recovery-v1');
    if (!value || !recoveryActions.has(value.action) || !Number.isFinite(value.at) || now - value.at > maxAgeMs)
      return undefined;
    return {
      action: value.action,
      correlationId: boundedCorrelationId(value.correlationId),
    };
  } catch {
    try { storage.removeItem(STATUS_RECOVERY_STORAGE_KEY); } catch { /* unavailable storage */ }
    return undefined;
  }
}

export function recordBrowserUnknownDiagnosticMapping() {
  currentTelemetry()?.record('diagnostic_mapping', 'unknown');
}

function randomHex(byteCount) {
  const bytes = new Uint8Array(byteCount);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, value => value.toString(16).padStart(2, '0')).join('');
}

function createTraceParent() {
  return `00-${randomHex(16)}-${randomHex(8)}-01`;
}

function webVitalOutcome(name, value) {
  const thresholds = {
    lcp: [2500, 4000],
    inp: [200, 500],
    cls: [0.1, 0.25],
  };
  const [good, poor] = thresholds[name];
  if (value <= good) return 'good';
  if (value <= poor) return 'needs_improvement';
  return 'poor';
}

function observeWebVitals(report) {
  if (!('PerformanceObserver' in window)) return () => {};

  const observers = [];
  let lcp = 0;
  let cls = 0;
  let inp = 0;

  const observe = (type, callback) => {
    try {
      const observer = new PerformanceObserver(list => callback(list.getEntries()));
      observer.observe({ type, buffered: true });
      observers.push(observer);
    } catch {
      // Unsupported entry types are expected on older browsers.
    }
  };

  observe('largest-contentful-paint', entries => {
    lcp = entries.at(-1)?.startTime || lcp;
  });
  observe('layout-shift', entries => {
    for (const entry of entries) {
      if (!entry.hadRecentInput) cls += entry.value;
    }
  });
  observe('event', entries => {
    for (const entry of entries) inp = Math.max(inp, entry.duration || 0);
  });

  let reported = false;
  const flush = () => {
    if (reported) return;
    reported = true;
    if (lcp > 0) report('lcp', lcp, webVitalOutcome('lcp', lcp));
    if (cls >= 0) report('cls', cls, webVitalOutcome('cls', cls));
    if (inp > 0) report('inp', inp, webVitalOutcome('inp', inp));
  };
  const onVisibilityChange = () => {
    if (document.visibilityState === 'hidden') flush();
  };
  document.addEventListener('visibilitychange', onVisibilityChange);
  window.addEventListener('pagehide', flush, { once: true });

  return () => {
    flush();
    observers.forEach(observer => observer.disconnect());
    document.removeEventListener('visibilitychange', onVisibilityChange);
  };
}

export function initializeBrowserTelemetry({ application, endpoint = TELEMETRY_PATH, version = pvadVersion } = {}) {
  if (!applicationRoutes[application]) return;
  const existing = currentTelemetry();
  if (existing) {
    globalThis.__pvadBrowserTelemetry = existing;
    return;
  }

  const nativeFetch = globalThis.fetch.bind(globalThis);
  const queue = [];
  const shouldRecord = createBrowserEventDeduplicator();
  const isStatusLoop = createStatusLoopDetector();
  let flushTimer;
  let currentRoute = normalizeBrowserRoute(application, location.pathname);

  const event = (type, outcome, values = {}) => {
    if (type === 'status_recovery' && outcome === 'selected' && values.name)
      rememberStatusRecovery(values.name, values.correlationId);
    const browserEvent = {
      type,
      outcome,
      route: currentRoute,
      ...(values.name ? { name: values.name } : {}),
      ...(Number.isFinite(values.value) ? { value: values.value } : {}),
      ...(boundedCorrelationId(values.correlationId) ? { correlationId: values.correlationId } : {}),
    };
    if (!shouldRecord(browserEvent)) return;
    queue.push(browserEvent);
    if (type === 'status_page' && values.name && isStatusLoop(values.name, currentRoute)) {
      queue.push({
        type: 'status_loop',
        outcome: 'detected',
        route: currentRoute,
        name: values.name,
        ...(boundedCorrelationId(values.correlationId) ? { correlationId: values.correlationId } : {}),
      });
    }
    if (queue.length >= MAX_BATCH_SIZE) void flush();
    else if (!flushTimer) flushTimer = setTimeout(flush, FLUSH_DELAY_MS);
  };

  const flush = async () => {
    clearTimeout(flushTimer);
    flushTimer = undefined;
    if (!queue.length) return;
    const events = queue.splice(0, MAX_BATCH_SIZE);
    try {
      await nativeFetch(endpoint, {
        method: 'POST',
        credentials: 'omit',
        keepalive: true,
        headers: {
          'Content-Type': 'application/json',
          traceparent: createTraceParent(),
        },
        body: JSON.stringify({ application, version, events }),
      });
    } catch {
      // Telemetry must never affect the product experience or retry indefinitely.
    }
    if (queue.length && !flushTimer) flushTimer = setTimeout(flush, FLUSH_DELAY_MS);
  };

  const isApiRequest = input => {
    try {
      const url = new URL(typeof input === 'string' || input instanceof URL ? input : input.url, location.href);
      return url.origin === location.origin && url.pathname.startsWith('/api/');
    } catch {
      return false;
    }
  };

  globalThis.fetch = async (input, init = {}) => {
    if (!isApiRequest(input) || new URL(typeof input === 'string' || input instanceof URL ? input : input.url, location.href).pathname === TELEMETRY_PATH)
      return nativeFetch(input, init);

    const headers = new Headers(input instanceof Request ? input.headers : undefined);
    new Headers(init.headers).forEach((value, key) => headers.set(key, value));
    if (!headers.has('traceparent')) headers.set('traceparent', createTraceParent());
    if (application === 'designer' && !headers.has('X-PVAD-Application'))
      headers.set('X-PVAD-Application', 'designer');

    try {
      const response = await nativeFetch(input, { ...init, headers });
      if (!response.ok)
        event('api_failure', classifyBrowserApiOutcome(response.status));
      return response;
    } catch (error) {
      event('api_failure', 'network_error');
      throw error;
    }
  };

  const recordNavigation = () => {
    const nextRoute = normalizeBrowserRoute(application, location.pathname);
    if (nextRoute === currentRoute) return;
    currentRoute = nextRoute;
    event('navigation', 'completed');
  };
  for (const method of ['pushState', 'replaceState']) {
    const original = history[method].bind(history);
    history[method] = (...args) => {
      const result = original(...args);
      recordNavigation();
      return result;
    };
  }
  window.addEventListener('popstate', recordNavigation);
  window.addEventListener('hashchange', () => event('navigation', 'completed'));
  window.addEventListener('error', () => event('error', 'failed'));
  window.addEventListener('unhandledrejection', () => event('unhandled_rejection', 'failed'));

  event('navigation', 'completed');
  setTimeout(() => {
    const pendingRecovery = consumeStatusRecovery();
    if (!pendingRecovery) return;
    const failed = Boolean(document.querySelector('[data-status-key]'));
    event('status_recovery', failed ? 'failed' : 'completed', {
      name: pendingRecovery.action,
      correlationId: pendingRecovery.correlationId,
    });
  }, 0);
  const stopVitals = observeWebVitals((name, value, outcome) =>
    event('web_vital', outcome, { name, value }));

  const bridge = { flush, stopVitals, record: event };
  globalThis.__pvadBrowserTelemetry = bridge;
  // One compatibility alias, not a second collector or a second fetch wrapper.
  globalThis.__pxaBrowserTelemetry = bridge;
}
