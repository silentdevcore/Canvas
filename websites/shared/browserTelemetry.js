const TELEMETRY_PATH = '/api/pxa/v1/telemetry/browser';
const MAX_BATCH_SIZE = 20;
const FLUSH_DELAY_MS = 1000;

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

export function initializeBrowserTelemetry({ application, endpoint = TELEMETRY_PATH } = {}) {
  if (!applicationRoutes[application] || globalThis.__pxaBrowserTelemetry) return;

  const nativeFetch = globalThis.fetch.bind(globalThis);
  const queue = [];
  let flushTimer;
  let currentRoute = normalizeBrowserRoute(application, location.pathname);

  const event = (type, outcome, values = {}) => {
    queue.push({
      type,
      outcome,
      route: currentRoute,
      ...(values.name ? { name: values.name } : {}),
      ...(Number.isFinite(values.value) ? { value: values.value } : {}),
    });
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
        body: JSON.stringify({ application, events }),
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
    if (application === 'designer' && !headers.has('X-PXA-Application'))
      headers.set('X-PXA-Application', 'designer');

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
  const stopVitals = observeWebVitals((name, value, outcome) =>
    event('web_vital', outcome, { name, value }));

  globalThis.__pxaBrowserTelemetry = { flush, stopVitals };
}
