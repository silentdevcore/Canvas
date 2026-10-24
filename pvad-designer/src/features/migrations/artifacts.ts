export interface MigrationCodeArtifact {
  id: 'pvad-csharp' | 'pvad-spreadsheet-csharp';
  mediaType: 'text/x-csharp';
  fileExtension: '.cs';
  content: string;
}

export const migrationCodeArtifact = (
  artifacts: unknown,
  domain: 'pdf' | 'spreadsheet',
): MigrationCodeArtifact | undefined => {
  if (!Array.isArray(artifacts)) return undefined;
  const canonicalId = domain === 'pdf' ? 'pvad-csharp' : 'pvad-spreadsheet-csharp';
  const legacyId = domain === 'pdf' ? 'pxa-csharp' : 'pxa-spreadsheet-csharp';
  const artifact = artifacts.find(item => item && typeof item === 'object'
    && (item.id === canonicalId || item.id === legacyId)
    && item.mediaType === 'text/x-csharp' && item.fileExtension === '.cs'
    && typeof item.content === 'string');
  return artifact ? {
    id: canonicalId,
    mediaType: 'text/x-csharp',
    fileExtension: '.cs',
    content: artifact.content,
  } : undefined;
};
