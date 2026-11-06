/** Presentation aliases only. Never use these values for authorization or persistence. */
export function displayDiagnosticCode(code: string | null | undefined): string {
  return (code ?? '').replace(/^PXA(?=[A-Z0-9_-]|$)/, 'PVAD');
}

export function displaySystemMessage(message: string): string {
  return message.replace(/\bPXA(?=[A-Z0-9_-])/g, 'PVAD');
}

function displayReleaseText(text: string): string {
  return displaySystemMessage(text).replace(/\b(?:Power Dox Automation|PXA)\b/g, 'PVADE');
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
