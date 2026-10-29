import { migrationCodeArtifact } from '@/features/migrations/artifacts';

describe('migration code artifacts', () => {
  it.each(['pdf', 'spreadsheet'] as const)('reads current and legacy %s outputs without changing source', domain => {
    const suffix = domain === 'pdf' ? 'csharp' : 'spreadsheet-csharp';
    for (const prefix of ['pvad', 'pxa']) {
      const content = '// Keep comments and identifiers\nusing PVAD.Pdf;';
      const artifact = { id: `${prefix}-${suffix}`, mediaType: 'text/x-csharp', fileExtension: '.cs', content };
      expect(migrationCodeArtifact([{ id: 'design', content: {} }, artifact], domain)).toEqual({
        ...artifact, id: `pvad-${suffix}`,
      });
      expect(artifact.id).toBe(`${prefix}-${suffix}`);
    }
  });

  it.each([
    { id: 'pvad-csharp', mediaType: 'application/json', fileExtension: '.cs', content: 'code' },
    { id: 'pvad-csharp', mediaType: 'text/x-csharp', fileExtension: '.exe', content: 'code' },
    { id: 'pvad-spreadsheet-csharp', mediaType: 'text/x-csharp', fileExtension: '.cs', content: 'code' },
    { id: 'unknown', canonicalId: 'pvad-csharp', mediaType: 'text/x-csharp', fileExtension: '.cs', content: 'code' },
    { id: 'pvad-csharp', mediaType: 'text/x-csharp', fileExtension: '.cs', content: {} },
    null,
  ])('rejects invalid, cross-domain or spoofed artifact %j', artifact => {
    expect(migrationCodeArtifact([artifact], 'pdf')).toBeUndefined();
  });

  it.each([null, {}, 'code', undefined])('rejects a malformed artifact list %j', artifacts => {
    expect(migrationCodeArtifact(artifacts, 'pdf')).toBeUndefined();
  });
});
