/** Diagnostic codes are shown exactly as issued. */
export function displayDiagnosticCode(code: string | null | undefined): string {
  return code ?? '';
}

export function displaySystemMessage(message: string): string {
  return message;
}

// Published release history keeps its original product names; present them with the current brand.
function displayReleaseText(text: string): string {
  return text
    .replace(/\bPXA(?=[A-Z0-9_-])/g, 'PVAD')
    .replace(/\b(?:Power Dox Automation|PXA)\b/g, 'PVADE');
}

export function presentRelease<T extends {
  title: string;
  summary: string;
  changes: Record<string, string[]>;
}>(release: T): T {
  return {
    ...release,
    title: displayReleaseText(release.title),
    summary: displayReleaseText(release.summary),
    changes: Object.fromEntries(Object.entries(release.changes)
      .map(([category, entries]) => [category, entries.map(displayReleaseText)])),
  };
}
