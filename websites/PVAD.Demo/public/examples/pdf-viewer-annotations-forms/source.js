export function createViewerSession(input) {
  return {
    viewerState: 'PVADE PDF Viewer session',
    document: input.document,
    formFields: input.formFields,
    annotations: input.annotations,
    enabledTools: ['select', 'highlight', 'ink', 'text-note', 'form-fill'],
  };
}
