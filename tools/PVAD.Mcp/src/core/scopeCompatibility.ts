const readScopes = ['docs.read', 'templates.read', 'documents.validate'] as const;

export function canonicalScope(scope: string): string {
  return readScopes.some((value) => scope === `pxa.${value}`) ? `pvad.${scope.slice(4)}` : scope;
}

export function canonicalScopes(scopes: string[]): string[] {
  return [...new Set(scopes.map(canonicalScope))];
}

export function hasCompatibleScope(scopes: string[], required: string): boolean {
  return scopes.some((scope) => canonicalScope(scope) === canonicalScope(required));
}

export function compatibleScopes(scopes: string[]): string[] {
  const canonical = canonicalScopes(scopes);
  return [...new Set(canonical.flatMap((scope) => readScopes.some((value) => scope === `pvad.${value}`)
    ? [scope, `pxa.${scope.slice(5)}`] : [scope]))];
}
