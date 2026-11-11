export function migrateProviderCode(input) {
  return {
    migrationResult: 'preview',
    provider: input.provider,
    targetNamespace: input.targetNamespace,
    diagnostics: [
      {
        id: 'PVADMIG001',
        severity: 'info',
        message: `Provider detected: ${input.provider} ${input.domain} ${input.kind}`,
      },
      {
        id: 'PVADMIG101',
        severity: 'warning',
        message: 'Manual review required for processor lifetime and file IO.',
      },
    ],
  };
}
