export function createViewerSession(input) {
  return {
    viewerState: 'PXA.PdfViewer.Session',
    document: input.document,
    formFields: input.formFields,
    annotations: input.annotations,
    enabledTools: ['select', 'highlight', 'ink', 'text-note', 'form-fill'],
  };
}
