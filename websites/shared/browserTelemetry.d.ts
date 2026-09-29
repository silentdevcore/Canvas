export type PxaBrowserApplication =
  | 'company'
  | 'documentation'
  | 'demo'
  | 'account'
  | 'admin'
  | 'designer';

export function normalizeBrowserRoute(application: PxaBrowserApplication, pathname?: string): string;
export function classifyBrowserApiOutcome(status: number): string;
export function createBrowserEventDeduplicator(windowMs?: number): (
  event: { type: string; outcome: string; route: string; name?: string },
  now?: number,
) => boolean;
export function createStatusLoopDetector(options?: {
  threshold?: number;
  windowMs?: number;
  storage?: Pick<Storage, 'getItem' | 'setItem'>;
}): (statusKey: string, route: string, now?: number) => boolean;
export function recordBrowserStatusPage(statusKey: string, correlationId?: string): void;
export function recordBrowserRecoveryAction(action: string, correlationId?: string): void;
export function rememberStatusRecovery(
  action: string,
  correlationId?: string,
  storage?: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>,
  now?: number,
): boolean;
export function consumeStatusRecovery(
  storage?: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>,
  now?: number,
  maxAgeMs?: number,
): { action: string; correlationId?: string } | undefined;
export function recordBrowserUnknownDiagnosticMapping(): void;
export function initializeBrowserTelemetry(options: {
  application: PxaBrowserApplication;
  endpoint?: string;
  version?: string;
}): void;
