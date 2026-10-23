export interface MigrationInputContract {
  id: string;
  mediaType: string;
  fileExtensions: string[];
  supportsText: boolean;
  supportsBinary: boolean;
}

const normalizedExtensions = (
  inputs: MigrationInputContract[],
  inputType?: string,
): string[] => {
  const matching = inputType
    ? inputs.filter(input => input.id.toLowerCase() === inputType.toLowerCase())
    : inputs;
  const selected = matching.length > 0 ? matching : inputs;
  return Array.from(new Set(selected
    .flatMap(input => input.fileExtensions)
    .map(extension => extension.trim().toLowerCase())
    .filter(extension => extension.startsWith('.'))));
};

export const migrationFileAccept = (
  inputs: MigrationInputContract[],
  inputType?: string,
): string => normalizedExtensions(inputs, inputType).join(',');

export const acceptsMigrationFile = (
  fileName: string,
  inputs: MigrationInputContract[],
  inputType?: string,
): boolean => {
  const normalizedName = fileName.trim().toLowerCase();
  return normalizedExtensions(inputs, inputType)
    .some(extension => normalizedName.endsWith(extension));
};

export const migrationFileExtensionsLabel = (
  inputs: MigrationInputContract[],
  inputType?: string,
): string => normalizedExtensions(inputs, inputType).join(', ');

export const inferMigrationInput = (
  fileName: string,
  inputs: MigrationInputContract[],
): MigrationInputContract | undefined => {
  const normalizedName = fileName.trim().toLowerCase();
  return inputs.find(input => input.fileExtensions.some(extension =>
    normalizedName.endsWith(extension.toLowerCase())));
};

export const startsWithZipSignature = (bytes: Uint8Array): boolean =>
  bytes.length >= 2 && bytes[0] === 0x50 && bytes[1] === 0x4b;

export const bytesToBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  const chunkSize = 0x8000;
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(offset, offset + chunkSize));
  }
  return btoa(binary);
};
