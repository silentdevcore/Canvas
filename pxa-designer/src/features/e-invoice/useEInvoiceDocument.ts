/**
 * Owns the Designer's electronic-invoice state.
 *
 * Every mutation goes through `commitEInvoice`, which writes the invoice and the page elements
 * it projects onto in one store transaction. That gives three things at once: one undo step per
 * edit, one changed snapshot for the existing autosave pipeline, and a canvas that can never
 * show a figure the canonical invoice does not hold.
 */

import { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useEditorStore } from '@/store';
import type { Page } from '@/types';
import {
  createEInvoiceDesignerDocument,
  projectInvoicePages,
  readEInvoiceDocument,
  withStandardSelected,
  writeEInvoiceDocument,
  type EInvoiceDesignerDocument,
} from './designerDocument';
import {
  createEmptyLine,
  type EInvoiceDocument,
  type EInvoiceDocumentLine,
  type EInvoiceStandard,
} from './invoiceModel';
import { calculateInvoice, type CalculatedInvoice } from './invoiceTotals';
import { invoiceTemplate, type InvoiceTemplateDefinition } from './invoiceTemplates';
import {
  conformanceStateFor,
  validateInvoice,
  type DesignerConformanceState,
  type InvoiceFinding,
} from './invoiceValidation';

export interface EInvoiceDocumentController {
  /** Null when the open document is not an invoice yet. */
  document: EInvoiceDesignerDocument | null;
  /** Exact totals and tax groups, recomputed from the canonical invoice. */
  totals: CalculatedInvoice | null;
  findings: InvoiceFinding[];
  /** What the Designer is allowed to claim before the backend has answered. */
  conformance: DesignerConformanceState;
  /** True once a template has been inserted, so the canvas projects the invoice. */
  hasTemplate: boolean;
  start: () => void;
  updateInvoice: (change: (invoice: EInvoiceDocument) => EInvoiceDocument) => void;
  selectStandard: (standard: EInvoiceStandard) => void;
  selectProfile: (profile: string) => void;
  addLine: () => void;
  removeLine: (index: number) => void;
  moveLine: (index: number, direction: -1 | 1) => void;
  updateLine: (index: number, change: Partial<EInvoiceDocumentLine>) => void;
  insertTemplate: (templateId: string) => void;
}

export function useEInvoiceDocument(
  backendConformance?: string | null,
): EInvoiceDocumentController {
  const { t } = useTranslation('eInvoice');
  const currentTemplate = useEditorStore(state => state.currentTemplate);
  const commitEInvoice = useEditorStore(state => state.commitEInvoice);
  const storedState = currentTemplate?.eInvoice;

  const document = useMemo(() => readEInvoiceDocument(storedState), [storedState]);
  const totals = useMemo(
    () => (document ? calculateInvoice(document.invoice) : null),
    [document],
  );
  const findings = useMemo(
    () => (document ? validateInvoice(document.invoice, document.settings) : []),
    [document],
  );
  const conformance = conformanceStateFor(findings, backendConformance);

  const translate = useCallback((key: string) => t(key), [t]);

  /**
   * Writes a new invoice document and, when a template is active, the pages it projects onto.
   * Both land in one transaction so the canvas never lags a keystroke behind the editor.
   */
  const commit = useCallback(
    (next: EInvoiceDesignerDocument, options: { resetView?: boolean } = {}) => {
      const definition = next.templateId ? invoiceTemplate(next.templateId) : undefined;
      const pages: Page[] | undefined = definition
        ? projectInvoicePages(definition, next.invoice, translate)
        : undefined;
      commitEInvoice({
        eInvoice: writeEInvoiceDocument(next),
        pages,
        pageSettings: definition
          ? { width: definition.pageWidth, height: definition.pageHeight }
          : undefined,
        resetView: options.resetView,
      });
    },
    [commitEInvoice, translate],
  );

  const start = useCallback(() => {
    if (document) return;
    commit(createEInvoiceDesignerDocument());
  }, [commit, document]);

  const updateInvoice = useCallback(
    (change: (invoice: EInvoiceDocument) => EInvoiceDocument) => {
      if (!document) return;
      commit({ ...document, invoice: change(document.invoice) });
    },
    [commit, document],
  );

  const selectStandard = useCallback(
    (standard: EInvoiceStandard) => {
      if (!document) return;
      commit(withStandardSelected(document, standard));
    },
    [commit, document],
  );

  const selectProfile = useCallback(
    (profile: string) => {
      if (!document) return;
      commit({ ...document, settings: { ...document.settings, profile } });
    },
    [commit, document],
  );

  const addLine = useCallback(() => {
    updateInvoice(invoice => ({
      ...invoice,
      lines: [...invoice.lines, createEmptyLine(nextLinePosition(invoice.lines))],
    }));
  }, [updateInvoice]);

  const removeLine = useCallback(
    (index: number) => {
      updateInvoice(invoice => ({
        ...invoice,
        lines: invoice.lines.filter((_line, position) => position !== index),
      }));
    },
    [updateInvoice],
  );

  const moveLine = useCallback(
    (index: number, direction: -1 | 1) => {
      updateInvoice(invoice => {
        const target = index + direction;
        if (target < 0 || target >= invoice.lines.length) return invoice;
        const lines = [...invoice.lines];
        [lines[index], lines[target]] = [lines[target], lines[index]];
        return { ...invoice, lines };
      });
    },
    [updateInvoice],
  );

  const updateLine = useCallback(
    (index: number, change: Partial<EInvoiceDocumentLine>) => {
      updateInvoice(invoice => ({
        ...invoice,
        lines: invoice.lines.map((line, position) =>
          position === index ? { ...line, ...change } : line),
      }));
    },
    [updateInvoice],
  );

  const insertTemplate = useCallback(
    (templateId: string) => {
      const definition: InvoiceTemplateDefinition | undefined = invoiceTemplate(templateId);
      if (!definition) return;
      const base = document ?? createEInvoiceDesignerDocument();
      commit({ ...base, templateId: definition.id }, { resetView: true });
    },
    [commit, document],
  );

  return {
    document,
    totals,
    findings,
    conformance,
    hasTemplate: Boolean(document?.templateId),
    start,
    updateInvoice,
    selectStandard,
    selectProfile,
    addLine,
    removeLine,
    moveLine,
    updateLine,
    insertTemplate,
  };
}

/** The next free numeric position, so a removed line does not cause a duplicate identifier. */
function nextLinePosition(lines: readonly EInvoiceDocumentLine[]): number {
  const used = lines
    .map(line => Number.parseInt(line.identifier, 10))
    .filter(value => Number.isInteger(value));
  return used.length === 0 ? 1 : Math.max(...used) + 1;
}
