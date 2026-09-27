/**
 * The Validate, Generate XML, Create PDF, Download and Open-in-Viewer actions.
 *
 * Every action sends the canonical invoice to the backend and reports back exactly what the
 * backend said. Nothing here recomputes a total, and nothing upgrades structural recognition
 * into a conformance claim: the three validation states arrive separately and are surfaced
 * separately.
 */

import { useCallback, useRef, useState } from 'react';
import { blobToDataUrl, writePdfViewerHandoff } from '@/features/pdf-viewer/handoff';
// `inspect` already has a client in the Viewer feature; reuse it rather than adding a second.
import {
  inspectEInvoices,
  type EInvoiceAttachmentSummary,
} from '@/features/pdf-viewer/eInvoiceApi';
import {
  EInvoiceApiError,
  createHybridInvoicePdf,
  generateInvoiceXml,
  validateInvoiceXml,
  type EInvoiceValidationReport,
} from './eInvoiceDesignerApi';
import { attachmentFileName, type EInvoiceDocument, type EInvoiceSettings } from './invoiceModel';

export type EInvoiceActionKind = 'validate' | 'generateXml' | 'createPdf';

export interface EInvoiceGeneratedArtifacts {
  xml?: { blob: Blob; fileName: string };
  pdf?: { blob: Blob; fileName: string; dataUrl: string };
}

export interface EInvoiceActionState {
  busy: EInvoiceActionKind | null;
  /** The backend's validation report, or null before any action has run. */
  report: EInvoiceValidationReport | null;
  /** What `inspect` reported about the generated PDF, including PDF/XML consistency. */
  inspection: EInvoiceAttachmentSummary | null;
  artifacts: EInvoiceGeneratedArtifacts;
  /** A localization key plus its parameters, never a pre-built sentence. */
  error: { key: string; code?: string; detail?: string } | null;
  /** Announcement key for the live region, changed on every completed action. */
  announcement: { key: string; nonce: number } | null;
}

export interface EInvoiceActions extends EInvoiceActionState {
  validate: (invoice: EInvoiceDocument, settings: EInvoiceSettings) => Promise<void>;
  generateXml: (invoice: EInvoiceDocument, settings: EInvoiceSettings) => Promise<void>;
  createPdf: (invoice: EInvoiceDocument, settings: EInvoiceSettings) => Promise<void>;
  downloadXml: () => void;
  downloadPdf: () => void;
  openInViewer: () => void;
  reset: () => void;
}

const initialState: EInvoiceActionState = {
  busy: null,
  report: null,
  inspection: null,
  artifacts: {},
  error: null,
  announcement: null,
};

export function useEInvoiceActions(navigate: (path: string) => void): EInvoiceActions {
  const [state, setState] = useState<EInvoiceActionState>(initialState);
  const nonce = useRef(0);
  // Invalidates responses from an operation that belonged to an older invoice revision.
  // Editing the invoice calls reset(), so a slow request can never repopulate the UI with
  // validation evidence or downloads for values that are no longer on screen.
  const requestGeneration = useRef(0);

  const announce = useCallback((key: string) => {
    nonce.current += 1;
    return { key, nonce: nonce.current };
  }, []);

  const failure = useCallback((caught: unknown, fallbackKey: string) => {
    if (caught instanceof EInvoiceApiError) {
      return { key: fallbackKey, code: caught.code, detail: caught.message };
    }
    return {
      key: navigator.onLine === false ? 'errors.offline' : fallbackKey,
      detail: caught instanceof Error ? caught.message : undefined,
    };
  }, []);

  const validate = useCallback(
    async (invoice: EInvoiceDocument, settings: EInvoiceSettings) => {
      const generation = ++requestGeneration.current;
      setState(previous => ({ ...previous, busy: 'validate', error: null }));
      try {
        // Validation runs over the generated XML, so what is checked is exactly what would
        // be embedded - not a client-side approximation of it.
        const xml = await generateInvoiceXml(invoice, settings);
        if (generation !== requestGeneration.current) return;
        const report = await validateInvoiceXml(xml, settings);
        if (generation !== requestGeneration.current) return;
        setState(previous => ({
          ...previous,
          busy: null,
          report,
          announcement: announce(
            report.isStructurallyValid ? 'announce.validated' : 'announce.validationFailed',
          ),
        }));
      } catch (caught) {
        if (generation !== requestGeneration.current) return;
        setState(previous => ({
          ...previous,
          busy: null,
          error: failure(caught, 'errors.validateFailed'),
          announcement: announce('announce.validationFailed'),
        }));
      }
    },
    [announce, failure],
  );

  const generateXml = useCallback(
    async (invoice: EInvoiceDocument, settings: EInvoiceSettings) => {
      const generation = ++requestGeneration.current;
      setState(previous => ({ ...previous, busy: 'generateXml', error: null }));
      try {
        const blob = await generateInvoiceXml(invoice, settings);
        if (generation !== requestGeneration.current) return;
        setState(previous => ({
          ...previous,
          busy: null,
          artifacts: {
            ...previous.artifacts,
            xml: { blob, fileName: attachmentFileName(settings.standard) },
          },
          announcement: announce('announce.xmlReady'),
        }));
      } catch (caught) {
        if (generation !== requestGeneration.current) return;
        setState(previous => ({
          ...previous,
          busy: null,
          error: failure(caught, 'errors.generateXmlFailed'),
          announcement: announce('announce.generateFailed'),
        }));
      }
    },
    [announce, failure],
  );

  const createPdf = useCallback(
    async (invoice: EInvoiceDocument, settings: EInvoiceSettings) => {
      const generation = ++requestGeneration.current;
      setState(previous => ({ ...previous, busy: 'createPdf', error: null }));
      try {
        const blob = await createHybridInvoicePdf(invoice, settings);
        if (generation !== requestGeneration.current) return;
        const dataUrl = await blobToDataUrl(blob);
        if (generation !== requestGeneration.current) return;
        const fileName = `${safeName(invoice.invoiceNumber)}.pdf`;
        // Inspecting the document PXA just produced is what surfaces the semantic PDF/XML
        // comparison. A failure here must not discard the PDF the user asked for.
        let inspection: EInvoiceAttachmentSummary | null = null;
        try {
          const summaries = await inspectEInvoices(
            new File([blob], fileName, { type: 'application/pdf' }),
          );
          if (generation !== requestGeneration.current) return;
          inspection = summaries.find(summary => summary.isXml) ?? null;
        } catch {
          inspection = null;
        }
        if (generation !== requestGeneration.current) return;
        setState(previous => ({
          ...previous,
          busy: null,
          inspection,
          artifacts: { ...previous.artifacts, pdf: { blob, fileName, dataUrl } },
          announcement: announce('announce.pdfReady'),
        }));
      } catch (caught) {
        if (generation !== requestGeneration.current) return;
        setState(previous => ({
          ...previous,
          busy: null,
          error: failure(caught, 'errors.createPdfFailed'),
          announcement: announce('announce.generateFailed'),
        }));
      }
    },
    [announce, failure],
  );

  const downloadXml = useCallback(() => {
    if (state.artifacts.xml) download(state.artifacts.xml.blob, state.artifacts.xml.fileName);
  }, [state.artifacts.xml]);

  const downloadPdf = useCallback(() => {
    if (state.artifacts.pdf) download(state.artifacts.pdf.blob, state.artifacts.pdf.fileName);
  }, [state.artifacts.pdf]);

  const openInViewer = useCallback(() => {
    const pdf = state.artifacts.pdf;
    if (!pdf) return;
    writePdfViewerHandoff({ dataUrl: pdf.dataUrl, name: pdf.fileName });
    navigate('/pdf/viewer?handoff=session');
  }, [navigate, state.artifacts.pdf]);

  const reset = useCallback(() => {
    requestGeneration.current += 1;
    setState(initialState);
  }, []);

  return {
    ...state,
    validate,
    generateXml,
    createPdf,
    downloadXml,
    downloadPdf,
    openInViewer,
    reset,
  };
}

function download(blob: Blob, fileName: string): void {
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = fileName;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(link.href), 0);
}

/** Mirrors the backend's download-name sanitisation so both sides agree on the filename. */
function safeName(value: string): string {
  const sanitized = value
    .trim()
    .replace(/[<>:"/\\|?*\u0000-\u001F]/g, '-')
    .replace(/^[\s.-]+|[\s.-]+$/g, '')
    .slice(0, 120);
  return sanitized || 'invoice';
}
