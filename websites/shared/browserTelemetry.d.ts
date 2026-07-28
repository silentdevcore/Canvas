export type PxaBrowserApplication =
  | 'company'
  | 'documentation'
  | 'demo'
  | 'account'
  | 'admin'
  | 'designer';

export function normalizeBrowserRoute(application: PxaBrowserApplication, pathname?: string): string;
export function classifyBrowserApiOutcome(status: number): string;
export function initializeBrowserTelemetry(options: {
  application: PxaBrowserApplication;
  endpoint?: string;
}): void;
