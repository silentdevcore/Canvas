import { z } from 'zod';

export type DiagnosticSeverity = 'info' | 'warning' | 'error';

export interface PvadMcpDiagnostic {
  code: string;
  severity: DiagnosticSeverity;
  message: string;
  path?: string;
}

export const toolOutputSchema = {
  status: z.enum(['ok', 'error']),
  data: z.record(z.unknown()),
  diagnostics: z.array(z.object({
    code: z.string(),
    severity: z.enum(['info', 'warning', 'error']),
    message: z.string(),
    path: z.string().optional(),
  })),
};

export function toolResult(
  data: Record<string, unknown>,
  diagnostics: PvadMcpDiagnostic[] = [],
  isError = false,
) {
  const structuredContent = {
    status: isError ? 'error' as const : 'ok' as const,
    data,
    diagnostics,
  };

  return {
    content: [{ type: 'text' as const, text: JSON.stringify(structuredContent, null, 2) }],
    structuredContent,
    isError,
  };
}
