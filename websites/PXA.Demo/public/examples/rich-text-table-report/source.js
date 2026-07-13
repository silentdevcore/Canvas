export function createRichTextTableReport(input) {
  return {
    documentType: 'RichTextTableReport',
    title: input.title,
    blocks: input.sections,
  };
}
