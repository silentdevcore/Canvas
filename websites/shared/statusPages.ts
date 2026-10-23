export type PvadStatusKey =
  | 'badRequest' | 'unauthenticated' | 'sessionExpired' | 'forbidden'
  | 'entitlementDenied' | 'suspended' | 'trialExpired' | 'notFound'
  | 'conflict' | 'expiredLink' | 'fileTooLarge' | 'rateLimited'
  | 'requestTimeout'
  | 'unexpected' | 'upstreamUnavailable' | 'unavailable' | 'maintenance'
  | 'timeout' | 'offline' | 'incompatibleVersion' | 'unsupportedBrowser';

export type PvadStatusAction = 'back' | 'home' | 'reload' | 'retry' | 'signIn' | 'subscription' | 'support';
export type PvadStatusRetryPolicy = 'none' | 'manual';

export interface PvadStatusDefinition {
  httpStatus: number | null;
  title: string;
  message: string;
  primaryAction: PvadStatusAction;
}

export interface PvadStatusPageOptions {
  product?: string;
  title?: string;
  message?: string;
  workState?: string;
  diagnosticCode?: string;
  correlationId?: string;
  primaryAction?: PvadStatusAction;
  primaryLabel?: string;
  secondaryAction?: PvadStatusAction;
  secondaryHref?: string;
  secondaryLabel?: string;
  backHref?: string;
  homeHref?: string;
  signInHref?: string;
  subscriptionHref?: string;
  supportHref?: string;
  allowedOrigins?: string;
}

export interface PvadStatusPageModel extends PvadStatusDefinition {
  statusKey: PvadStatusKey;
  retryPolicy: PvadStatusRetryPolicy;
  secondaryAction?: PvadStatusAction;
  diagnosticCode?: string;
  correlationId?: string;
  supportHref: string;
}

interface PvadBrowserTelemetryBridge {
  record(type: string, outcome: string, values?: { name?: string; correlationId?: string }): void;
}

function browserTelemetry(): PvadBrowserTelemetryBridge | undefined {
  const bridge = globalThis as typeof globalThis & {
    __pvadBrowserTelemetry?: PvadBrowserTelemetryBridge;
    __pxaBrowserTelemetry?: PvadBrowserTelemetryBridge;
  };
  return bridge.__pvadBrowserTelemetry ?? bridge.__pxaBrowserTelemetry;
}

export function recordStatusPageShown(statusKey: PvadStatusKey, correlationId?: string) {
  browserTelemetry()?.record('status_page', 'shown', { name: statusKey, correlationId });
}

export function recordStatusRecoverySelected(action: PvadStatusAction | 'account', correlationId?: string) {
  browserTelemetry()?.record('status_recovery', 'selected', { name: action, correlationId });
}

const STATUS_DEFINITIONS: Readonly<Record<PvadStatusKey, PvadStatusDefinition>> = Object.freeze({
  badRequest: {
    httpStatus: 400,
    title: 'Request could not be processed',
    message: 'The request is incomplete, invalid, or no longer supported.',
    primaryAction: 'back',
  },
  unauthenticated: {
    httpStatus: 401,
    title: 'Sign in required',
    message: 'Sign in to continue to this protected area.',
    primaryAction: 'signIn',
  },
  sessionExpired: {
    httpStatus: 401,
    title: 'Your session expired',
    message: 'Your work remains in your account. Sign in again to continue.',
    primaryAction: 'signIn',
  },
  forbidden: {
    httpStatus: 403,
    title: 'Access not permitted',
    message: 'Your account does not have permission to use this capability.',
    primaryAction: 'home',
  },
  entitlementDenied: {
    httpStatus: 403,
    title: 'Subscription access required',
    message: 'This capability is not included in the active subscription.',
    primaryAction: 'subscription',
  },
  suspended: {
    httpStatus: 403,
    title: 'Account access is suspended',
    message: 'Contact your organization administrator or PVADE Support before trying again.',
    primaryAction: 'support',
  },
  trialExpired: {
    httpStatus: 403,
    title: 'Your Designer trial has expired',
    message: 'Your documents remain in your account. An active subscription is required to continue.',
    primaryAction: 'subscription',
  },
  notFound: {
    httpStatus: 404,
    title: 'Page not found',
    message: 'The address may be incorrect, or the page may have moved.',
    primaryAction: 'home',
  },
  conflict: {
    httpStatus: 409,
    title: 'A newer change is available',
    message: 'Reload the latest version before applying your changes again.',
    primaryAction: 'reload',
  },
  requestTimeout: {
    httpStatus: 408,
    title: 'Request timed out',
    message: 'The request did not finish in time. Check the current state before trying again.',
    primaryAction: 'retry',
  },
  expiredLink: {
    httpStatus: 410,
    title: 'This link has expired',
    message: 'Request a new link and use only the most recent message.',
    primaryAction: 'home',
  },
  fileTooLarge: {
    httpStatus: 413,
    title: 'File is too large',
    message: 'Choose a smaller file or reduce its size before trying again.',
    primaryAction: 'back',
  },
  rateLimited: {
    httpStatus: 429,
    title: 'Too many requests',
    message: 'Wait a moment before trying again.',
    primaryAction: 'retry',
  },
  unexpected: {
    httpStatus: 500,
    title: 'Something went wrong',
    message: 'PVADE could not complete this request. Your saved work remains available.',
    primaryAction: 'retry',
  },
  upstreamUnavailable: {
    httpStatus: 502,
    title: 'A required service is unavailable',
    message: 'The application is available, but a required service did not respond correctly.',
    primaryAction: 'retry',
  },
  unavailable: {
    httpStatus: 503,
    title: 'Service temporarily unavailable',
    message: 'PVADE is temporarily unavailable. Try again shortly.',
    primaryAction: 'retry',
  },
  maintenance: {
    httpStatus: 503,
    title: 'Planned maintenance',
    message: 'PVADE is undergoing planned maintenance. Your stored documents are not affected.',
    primaryAction: 'retry',
  },
  timeout: {
    httpStatus: 504,
    title: 'The request took too long',
    message: 'The operation did not finish in time. Its status may still be available after reloading.',
    primaryAction: 'retry',
  },
  offline: {
    httpStatus: null,
    title: 'You are offline',
    message: 'Reconnect to continue. Changes that were already saved remain available.',
    primaryAction: 'retry',
  },
  incompatibleVersion: {
    httpStatus: null,
    title: 'Update required',
    message: 'This application version is not compatible with the PVADE API. Reload to get the latest version.',
    primaryAction: 'reload',
  },
  unsupportedBrowser: {
    httpStatus: null,
    title: 'Browser not supported',
    message: 'Open PVADE in a current version of a supported browser.',
    primaryAction: 'support',
  },
});

const ACTION_LABELS = Object.freeze({
  back: 'Go back',
  home: 'Go home',
  reload: 'Reload',
  retry: 'Try again',
  signIn: 'Sign in',
  subscription: 'View subscription',
  support: 'Contact support',
});

function escapeHtml(value = '') {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function safeHref(value: string, fallback = '/', allowedOrigins: string[] = []) {
  if (!value) return fallback;
  try {
    const url = new URL(value, window.location.origin);
    if (!['http:', 'https:'].includes(url.protocol)) return fallback;
    if (url.origin !== window.location.origin && !allowedOrigins.includes(url.origin)) return fallback;
    return url.href;
  } catch {
    return fallback;
  }
}

export function getStatusDefinition(statusKey: PvadStatusKey) {
  const definition = STATUS_DEFINITIONS[statusKey];
  if (!definition) throw new Error(`Unknown PVADE status key: ${statusKey}`);
  return definition;
}

export function getStatusCatalog() {
  return Object.entries(STATUS_DEFINITIONS).map(([statusKey, definition]) => ({
    statusKey,
    ...definition,
  }));
}

export function createStatusPageModel(
  statusKey: PvadStatusKey,
  options: PvadStatusPageOptions = {},
): PvadStatusPageModel {
  const definition = getStatusDefinition(statusKey);
  return {
    statusKey,
    ...definition,
    retryPolicy:
      definition.primaryAction === 'retry' || definition.primaryAction === 'reload'
        ? 'manual'
        : 'none',
    secondaryAction: options.secondaryAction,
    diagnosticCode: options.diagnosticCode,
    correlationId: options.correlationId,
    supportHref: options.supportHref || '/support',
  };
}

export function renderStatusPage(statusKey: PvadStatusKey, options: PvadStatusPageOptions = {}) {
  const definition = getStatusDefinition(statusKey);
  const product = options.product || 'PVADE';
  const candidateAction = options.primaryAction;
  const action: PvadStatusAction = candidateAction && candidateAction in ACTION_LABELS
    ? candidateAction
    : definition.primaryAction;
  const hrefs: Record<PvadStatusAction, string> = {
    back: options.backHref || '/',
    home: options.homeHref || '/',
    reload: window.location.href,
    retry: window.location.href,
    signIn: options.signInHref || '/login',
    subscription: options.subscriptionHref || '/subscription',
    support: options.supportHref || '/support',
  };
  const diagnosticParts = [options.diagnosticCode, options.correlationId].filter(Boolean);
  const allowedOrigins = (options.allowedOrigins || '').split(',').map(value => value.trim()).filter(Boolean);
  const statusLabel = definition.httpStatus ? String(definition.httpStatus) : 'PVADE';
  const title = options.title || definition.title;
  const message = options.message || definition.message;

  return `
    <main class="pvad-status-page" data-status-key="${escapeHtml(statusKey)}"${options.correlationId ? ` data-correlation-id="${escapeHtml(options.correlationId)}"` : ''} tabindex="-1">
      <div class="pvad-status-page__brand" aria-label="${escapeHtml(product)}">
        <span class="pvad-status-page__mark" aria-hidden="true">PVADE</span>
        <span>${escapeHtml(product)}</span>
      </div>
      <section class="pvad-status-page__content" aria-labelledby="pvad-status-title" aria-live="polite" aria-atomic="true">
        <p class="pvad-status-page__code">${escapeHtml(statusLabel)}</p>
        <h1 id="pvad-status-title" tabindex="-1">${escapeHtml(title)}</h1>
        <p class="pvad-status-page__message">${escapeHtml(message)}</p>
        ${options.workState ? `<p class="pvad-status-page__work-state">${escapeHtml(options.workState)}</p>` : ''}
        <div class="pvad-status-page__actions">
          <a class="pvad-button pvad-button--primary" href="${escapeHtml(safeHref(hrefs[action], '/', allowedOrigins))}" data-status-action="${escapeHtml(action)}">${escapeHtml(options.primaryLabel || ACTION_LABELS[action])}</a>
          ${options.secondaryHref && options.secondaryLabel ? `<a class="pvad-button pvad-button--secondary" href="${escapeHtml(safeHref(options.secondaryHref, '/', allowedOrigins))}">${escapeHtml(options.secondaryLabel)}</a>` : ''}
        </div>
        ${diagnosticParts.length ? `<div class="pvad-status-page__diagnostic"><span>Diagnostic details</span><code>${escapeHtml(diagnosticParts.join(' / '))}</code><button type="button" data-copy-diagnostic="${escapeHtml(diagnosticParts.join(' / '))}">Copy</button></div>` : ''}
      </section>
      <footer class="pvad-status-page__footer">Need help? <a href="${escapeHtml(safeHref(options.supportHref || '/support', '/', allowedOrigins))}">Contact PVADE Support</a>.</footer>
    </main>`;
}

export function bindStatusPage(container: Document | Element = document) {
  const heading = container.querySelector<HTMLElement>('#pvad-status-title');
  heading?.focus({ preventScroll: true });
  const page = container.querySelector<HTMLElement>('.pvad-status-page');
  if (page?.dataset.statusKey)
    recordStatusPageShown(page.dataset.statusKey as PvadStatusKey, page.dataset.correlationId);
  container.querySelector<HTMLElement>('[data-status-action]')?.addEventListener('click', (event) => {
    const action = (event.currentTarget as HTMLElement).dataset.statusAction;
    if (action) recordStatusRecoverySelected(action as PvadStatusAction, page?.dataset.correlationId);
  });
  container.querySelector('[data-copy-diagnostic]')?.addEventListener('click', async (event) => {
    const button = event.currentTarget as HTMLButtonElement;
    try {
      await navigator.clipboard.writeText(button.dataset.copyDiagnostic || '');
      button.textContent = 'Copied';
    } catch {
      button.textContent = 'Copy unavailable';
    }
  });
}
