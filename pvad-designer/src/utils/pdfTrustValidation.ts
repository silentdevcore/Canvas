import type { PageSettings } from '@/types';

export type PdfTrustValidationIssue = {
  code: string;
  messageKey: string;
};

export function validatePdfTrustSettings(settings: PageSettings): PdfTrustValidationIssue[] {
  const issues: PdfTrustValidationIssue[] = [];
  const encryption = settings.encryption;

  if (encryption?.enabled) {
    if (settings.complianceProfile === 'PdfA2B' || settings.complianceProfile === 'PdfA3B') {
      issues.push({ code: 'PVADPDFTRUST203', messageKey: 'pageSettings.trustValidation.pdfAEncryption' });
    }
    if (settings.accessibilityProfile === 'PdfUa1') {
      issues.push({ code: 'PVADPDFTRUST204', messageKey: 'pageSettings.trustValidation.pdfUaEncryption' });
    }

    const restricted = Object.values(encryption.permissions).some(allowed => !allowed);
    if (encryption.algorithm !== 'Rc4_128' && restricted &&
      (!encryption.ownerPassword || encryption.ownerPassword === encryption.userPassword)) {
      issues.push({ code: 'PVADPDFTRUST206', messageKey: 'pageSettings.trustValidation.ownerPassword' });
    }
  }

  return issues;
}
