export function isDocumentRtlLanguage(language: string | null | undefined): boolean {
  return (language ?? '').split('-')[0].toLowerCase() === 'ar';
}
