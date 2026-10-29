export function importFile(input) {
  return {
    normalizedModel: 'PVAD.Core.Contracts.DesignExportDto',
    fileName: input.fileName,
    detectedFormat: input.fileName.split('.').pop(),
    requestedOutput: input.requestedOutput,
  };
}
