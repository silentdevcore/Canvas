/**
 * Localized, client-side invoice validation.
 *
 * Every rule mirrors one `PXAEINV1xx` diagnostic from `EInvoice.ValidateDocument`, and each
 * finding carries that stable code. The point is a fast, translated hint while the user types -
 * the backend remains the authority, and passing this check is never presented as conformance.
 */

import {
  EINVOICE_PROFILES,
  EINVOICE_VERSIONS,
  type EInvoiceDocument,
  type EInvoiceSettings,
} from './invoiceModel';

export type InvoiceFindingSeverity = 'error' | 'warning';

export interface InvoiceFinding {
  /** The same diagnostic code the backend reports, so support can match them up. */
  code: string;
  severity: InvoiceFindingSeverity;
  /** Path into the invoice, for focusing the offending field. */
  field: string;
  /** i18n key under the `eInvoice` namespace. */
  messageKey: string;
  params?: Record<string, string | number>;
}

const MAXIMUM_LINES = 10_000;

export function validateInvoice(
  invoice: EInvoiceDocument,
  settings: EInvoiceSettings,
): InvoiceFinding[] {
  const findings: InvoiceFinding[] = [];

  required(invoice.invoiceNumber, 200, 'PXAEINV100', 'invoiceNumber', 'validation.invoiceNumberRequired', findings);
  required(invoice.currency, 3, 'PXAEINV101', 'currency', 'validation.currencyRequired', findings);
  const currency = invoice.currency?.trim() ?? '';
  if (currency !== '' && (currency.length !== 3 || !/^[A-Za-z]{3}$/.test(currency))) {
    findings.push(error('PXAEINV102', 'currency', 'validation.currencyFormat'));
  }
  if (
    !Number.isInteger(invoice.currencyDecimalPlaces) ||
    invoice.currencyDecimalPlaces < 0 ||
    invoice.currencyDecimalPlaces > 4
  ) {
    findings.push(error('PXAEINV103', 'currencyDecimalPlaces', 'validation.currencyDecimalPlaces'));
  }

  validateParty(invoice.seller, 'seller', 'PXAEINV104', findings);
  validateParty(invoice.buyer, 'buyer', 'PXAEINV105', findings);

  const lines = invoice.lines ?? [];
  if (lines.length === 0) {
    findings.push(error('PXAEINV106', 'lines', 'validation.linesRequired'));
  } else if (lines.length > MAXIMUM_LINES) {
    findings.push(
      error('PXAEINV107', 'lines', 'validation.tooManyLines', { maximum: MAXIMUM_LINES }),
    );
  } else {
    const identifiers = new Set<string>();
    lines.forEach((line, index) => {
      const position = index + 1;
      const field = `lines[${index}]`;
      required(line.identifier, 200, 'PXAEINV108', `${field}.identifier`, 'validation.lineIdentifierRequired', findings, { position });
      required(line.description, 4_096, 'PXAEINV109', `${field}.description`, 'validation.lineDescriptionRequired', findings, { position });
      required(line.unitCode, 16, 'PXAEINV110', `${field}.unitCode`, 'validation.lineUnitCodeRequired', findings, { position });
      required(line.taxCategoryCode, 16, 'PXAEINV111', `${field}.taxCategoryCode`, 'validation.lineTaxCategoryRequired', findings, { position });

      const identifier = line.identifier?.trim() ?? '';
      if (identifier !== '') {
        if (identifiers.has(identifier)) {
          findings.push(
            error('PXAEINV112', `${field}.identifier`, 'validation.lineIdentifierDuplicated', {
              identifier,
            }),
          );
        }
        identifiers.add(identifier);
      }
      if (!(line.quantity > 0)) {
        findings.push(error('PXAEINV113', `${field}.quantity`, 'validation.lineQuantityPositive', { position }));
      }
      if (!(line.unitPrice >= 0)) {
        findings.push(error('PXAEINV114', `${field}.unitPrice`, 'validation.lineUnitPriceNonNegative', { position }));
      }
      if (!(line.taxRate >= 0 && line.taxRate <= 100)) {
        findings.push(error('PXAEINV115', `${field}.taxRate`, 'validation.lineTaxRateRange', { position }));
      }
    });
  }

  if (invoice.dueDate && invoice.issueDate && invoice.dueDate < invoice.issueDate) {
    findings.push(error('PXAEINV116', 'dueDate', 'validation.dueDateBeforeIssueDate'));
  }
  if (settings.standard === 'XRechnung' && !invoice.buyerReference?.trim()) {
    findings.push({
      code: 'PXAEINV117',
      severity: 'warning',
      field: 'buyerReference',
      messageKey: 'validation.buyerReferenceRecommended',
    });
  }

  findings.push(...validateSettings(settings));
  return findings;
}

/**
 * Mirrors `EInvoice.ValidateOptions`. Keeping it here means the Designer refuses an unsupported
 * combination before a request is sent, rather than surfacing a backend rejection.
 */
export function validateSettings(settings: EInvoiceSettings): InvoiceFinding[] {
  const findings: InvoiceFinding[] = [];
  const expectedVersion = EINVOICE_VERSIONS[settings.standard];
  if (!expectedVersion) {
    findings.push(error('PXAEINV016', 'standard', 'validation.standardUnsupported'));
    return findings;
  }
  if (settings.version?.trim() !== expectedVersion) {
    findings.push(
      error('PXAEINV012', 'version', 'validation.versionUnsupported', {
        standard: settings.standard,
        expected: expectedVersion,
        actual: settings.version?.trim() ?? '',
      }),
    );
  }
  if (!EINVOICE_PROFILES[settings.standard].includes(settings.profile?.trim() ?? '')) {
    findings.push(
      error('PXAEINV013', 'profile', 'validation.profileUnsupported', {
        profile: settings.profile?.trim() ?? '',
        standard: settings.standard,
      }),
    );
  }
  return findings;
}

export function hasBlockingFindings(findings: readonly InvoiceFinding[]): boolean {
  return findings.some(finding => finding.severity === 'error');
}

/**
 * What the Designer is allowed to display about conformance before the backend has answered.
 * "draft" and "structurallyValid" are never shown as conformance.
 */
export type DesignerConformanceState =
  | 'draft'
  | 'structurallyValid'
  | 'notEvaluated'
  | 'unavailable'
  | 'validated'
  | 'invalid';

/**
 * Maps the backend's conformance vocabulary onto what the Designer may display.
 *
 * `validated` is reachable only when the backend reported `valid`, which it does only from a
 * pinned official artifact set. Anything else - including a clean structural result - stops at
 * `structurallyValid`, `notEvaluated` or `unavailable`, and a blocking local finding outranks
 * every backend verdict because the document in front of the user is no longer the one that
 * was validated.
 */
export function conformanceStateFor(
  findings: readonly InvoiceFinding[],
  backendConformanceStatus?: string | null,
): DesignerConformanceState {
  if (hasBlockingFindings(findings)) return 'draft';
  if (backendConformanceStatus === 'valid') return 'validated';
  if (backendConformanceStatus === 'invalid') return 'invalid';
  if (backendConformanceStatus === 'unavailable') return 'unavailable';
  if (backendConformanceStatus === 'notEvaluated') return 'notEvaluated';
  return 'structurallyValid';
}

function validateParty(
  party: EInvoiceDocument['seller'] | undefined,
  role: 'seller' | 'buyer',
  code: string,
  findings: InvoiceFinding[],
): void {
  const name = party?.name?.trim() ?? '';
  if (name === '' || name.length > 200) {
    findings.push(error(code, `${role}.name`, 'validation.partyNameRequired', { role }));
    return;
  }
  const address = party?.address;
  if (!address) return;

  required(address.street, 200, code, `${role}.address.street`, 'validation.partyStreetRequired', findings, { role });
  required(address.city, 200, code, `${role}.address.city`, 'validation.partyCityRequired', findings, { role });
  required(address.postalCode, 32, code, `${role}.address.postalCode`, 'validation.partyPostalCodeRequired', findings, { role });
  required(address.countryCode, 2, code, `${role}.address.countryCode`, 'validation.partyCountryCodeRequired', findings, { role });
  const country = address.countryCode?.trim() ?? '';
  if (country !== '' && !/^[A-Za-z]{2}$/.test(country)) {
    findings.push(error(code, `${role}.address.countryCode`, 'validation.partyCountryCodeFormat', { role }));
  }
}

function required(
  value: string | undefined,
  maximumLength: number,
  code: string,
  field: string,
  messageKey: string,
  findings: InvoiceFinding[],
  params?: Record<string, string | number>,
): void {
  const trimmed = value?.trim() ?? '';
  if (trimmed === '' || trimmed.length > maximumLength) {
    findings.push(error(code, field, messageKey, params));
  }
}

function error(
  code: string,
  field: string,
  messageKey: string,
  params?: Record<string, string | number>,
): InvoiceFinding {
  return { code, severity: 'error', field, messageKey, params };
}
