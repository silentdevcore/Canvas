/**
 * Types mirroring the backend redaction contract.
 *
 * Preflight, apply and verify are three separate steps because they answer three different
 * questions: what would be removed, what was removed, and whether anything survived. The
 * Viewer presents them separately for the same reason.
 */

export type RedactionCapability = 'Removable' | 'ReviewRequired' | 'Unsupported';

export type RedactionCoverage = 'None' | 'Partial' | 'Contained';

export interface RedactionFinding {
  code: string;
  capability: RedactionCapability;
  message: string;
  pageNumber?: number | null;
  regionIndex?: number | null;
}

export interface RedactionCoveredContent {
  kind: string;
  coverage: RedactionCoverage;
  capability: RedactionCapability;
  elementCount: number;
  characterCount: number;
}

export interface RedactionRegionReport {
  regionIndex: number;
  pageNumber: number;
  capability: RedactionCapability;
  covered: RedactionCoveredContent[];
  findings: RedactionFinding[];
}

export interface RedactionPreflightReport {
  capability: RedactionCapability;
  sha256: string;
  fileSizeBytes: number;
  pageCount: number;
  revisionCount: number;
  regions: RedactionRegionReport[];
  documentFindings: RedactionFinding[];
}

export interface RedactionVerification {
  passed: boolean;
  residualCoveredElementCount: number;
  residualExtractableMatches: number;
  findings: RedactionFinding[];
}

/** Everything the apply step returns alongside the redacted bytes. */
export interface RedactionApplyOutcome {
  blob: Blob;
  resultSha256: string | null;
  auditRecordSha256: string | null;
  auditSigned: boolean;
}

/** Raised when preflight refuses the redaction, carrying the report that explains why. */
export class RedactionBlockedError extends Error {
  constructor(
    message: string,
    readonly code: string | null,
    readonly preflight: RedactionPreflightReport | null,
    readonly verification: RedactionVerification | null,
  ) {
    super(message);
    this.name = 'RedactionBlockedError';
  }
}

/** Collects the findings of a report into one list for display. */
export const allFindings = (report: RedactionPreflightReport): RedactionFinding[] => [
  ...report.documentFindings,
  ...report.regions.flatMap((region) => region.findings),
];

/** A redaction may only be applied when nothing needs review. */
export const isApplyAllowed = (report: RedactionPreflightReport | null): boolean =>
  report?.capability === 'Removable';
