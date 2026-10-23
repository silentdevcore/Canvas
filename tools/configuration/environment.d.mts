export function readPvadEnvironment(
  name: string,
  environment?: Record<string, string | undefined>,
): string | undefined;
export function normalizePvadEnvironment(
  environment?: Record<string, string | undefined>,
): Record<string, string | undefined>;
export function resolvePvadDevApiTarget(environment?: Record<string, string | undefined>): string;
