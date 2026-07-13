export function importFile(input) {
  return {
    normalizedModel: 'PXA.ImportModel.Document',
    fileName: input.fileName,
    detectedFormat: input.fileName.split('.').pop(),
    requestedOutput: input.requestedOutput,
  };
}
