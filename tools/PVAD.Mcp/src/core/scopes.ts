export function uniqueScopes(scopes: string[]): string[] {
  return [...new Set(scopes)];
}

export function hasScope(scopes: string[], required: string): boolean {
  return scopes.includes(required);
}
