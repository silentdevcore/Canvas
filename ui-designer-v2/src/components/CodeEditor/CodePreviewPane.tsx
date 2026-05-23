import React, { useMemo } from 'react';
import LivePreview from '@/components/Preview/LivePreview';
import type { Page, SimpleElement, Template, PageSettings } from '@/types';

export interface ParsedDesign {
  id?: string;
  name?: string;
  pages: { id: string; elements: SimpleElement[] }[];
  sharedElements?: SimpleElement[];
  pageSettings?: Partial<PageSettings> & { width?: number; height?: number };
}

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

import type { EditorLanguage } from './LiveCodeEditor';

interface Props {
  raw: string;
  language: EditorLanguage;
  validation: ValidationResult;
  parsed: ParsedDesign | null;
  pdfBlobUrl: string | null;
  onExport: () => void;
  isExporting: boolean;
  isConverting: boolean;
  convertError: string | null;
}

const DEFAULT_PAGE_SETTINGS: PageSettings = {
  width: 595,
  height: 842,
  orientation: 'portrait',
  backgroundColor: '#ffffff',
  backgroundImage: '',
  backgroundImageFit: 'contain',
  margins: { top: 0, right: 0, bottom: 0, left: 0 },
  headerEnabled: false,
  headerHeight: 60,
  headerFirstPageDifferent: false,
  headerOddEvenDifferent: false,
  footerEnabled: false,
  footerHeight: 40,
  footerFirstPageDifferent: false,
  footerOddEvenDifferent: false,
  bleedSize: 0,
  gridVisible: false,
  snapToGrid: false,
  gridSize: 10,
  unit: 'pt',
  showMarginGuide: false,
  showSafeArea: false,
  cropMarks: false,
  pageNumbering: { enabled: false, format: 'current', startNumber: 1, prefix: '', suffix: '', placement: 'bottom-center', showOnFirstPage: true },
  globalWatermark: { enabled: false, mode: 'text', content: '', opacity: 0.3, rotation: 45, scale: 1, pageScope: 'all', pageRange: '', color: '#d1d5db', fontSize: 48 },
  metadata: { title: '', author: '', subject: '', keywords: '' },
  exportDefaults: { quality: 'screen', embedFonts: true, compressImages: true, accessibilityTagged: false },
  pagination: { autoBreaks: false, repeatTableHeader: false, keepWithNext: false, sectionStartBehavior: 'continue', orphanLines: 2, widowLines: 2 },
};

export default function CodePreviewPane({ raw, language, validation, parsed, pdfBlobUrl, onExport, isExporting, isConverting, convertError }: Props) {
  const pages: Page[] = useMemo(
    () =>
      (parsed?.pages ?? []).map(p => ({
        id: p.id,
        elements: (p.elements ?? []) as SimpleElement[],
      })),
    [parsed],
  );

  const sharedElements = useMemo(
    () => (parsed?.sharedElements ?? []) as SimpleElement[],
    [parsed],
  );

  const pageSettings: PageSettings = useMemo(() => {
    const ps = parsed?.pageSettings ?? {};
    return {
      ...DEFAULT_PAGE_SETTINGS,
      width:  ps.width  ?? DEFAULT_PAGE_SETTINGS.width,
      height: ps.height ?? DEFAULT_PAGE_SETTINGS.height,
      orientation: (ps.orientation ?? DEFAULT_PAGE_SETTINGS.orientation) as 'portrait' | 'landscape',
      backgroundColor: (ps as any).backgroundColor ?? DEFAULT_PAGE_SETTINGS.backgroundColor,
      margins: (ps as any).margins ?? DEFAULT_PAGE_SETTINGS.margins,
    };
  }, [parsed]);

  const template: Template = useMemo(
    () => ({
      id: parsed?.id ?? 'code-preview',
      name: parsed?.name ?? 'Preview',
      category: 'code',
      description: '',
    }),
    [parsed],
  );

  // Shared: converting spinner
  if (isConverting) {
    return (
      <div className="code-preview-pane code-preview-empty">
        <div className="code-preview-placeholder">
          <div className="code-preview-placeholder-icon">⏳</div>
          <p>{language === 'csharp-code' ? 'Running C# code…' : 'Converting…'}</p>
        </div>
      </div>
    );
  }

  // Shared: C# error
  if (language !== 'json' && convertError) {
    return (
      <div className="code-preview-pane code-preview-error">
        <div className="code-preview-error-header">
          <span className="code-preview-error-icon">⚠</span>
          {language === 'csharp-code' ? 'C# error — fix it and click ▶ Run' : 'C# DTO error — fix it and click ▶ Run'}
        </div>
        <ul className="code-preview-error-list">
          {convertError.split('\n').map((e, i) => <li key={i}>{e}</li>)}
        </ul>
      </div>
    );
  }

  // C# Code: show PDF iframe
  if (language === 'csharp-code') {
    if (!pdfBlobUrl) {
      return (
        <div className="code-preview-pane code-preview-empty">
          <div className="code-preview-placeholder">
            <div className="code-preview-placeholder-icon">{'{ }'}</div>
            <p>Click <strong>▶ Run</strong> or press <strong>⌘↵</strong> to render the PDF.</p>
            <p className="code-preview-placeholder-hint">The script must return a <code>PdfDocument</code> instance as the last expression.</p>
          </div>
        </div>
      );
    }
    return (
      <div className="code-preview-pane code-preview-pdf-iframe">
        <iframe src={pdfBlobUrl} title="PDF Preview" className="code-preview-iframe" />
      </div>
    );
  }

  // C# DTO: waiting for first run
  if (language === 'csharp-dto' && !parsed) {
    return (
      <div className="code-preview-pane code-preview-empty">
        <div className="code-preview-placeholder">
          <div className="code-preview-placeholder-icon">{'{ }'}</div>
          <p>Click <strong>▶ Run</strong> or press <strong>⌘↵</strong> to see the preview.</p>
          <p className="code-preview-placeholder-hint">The expression must return a <code>DesignExportDto</code> instance.</p>
        </div>
      </div>
    );
  }

  // JSON: empty editor
  if (language === 'json' && raw.trim() === '') {
    return (
      <div className="code-preview-pane code-preview-empty">
        <div className="code-preview-placeholder">
          <div className="code-preview-placeholder-icon">{'{ }'}</div>
          <p>Start typing JSON on the left to see a live preview here.</p>
          <p className="code-preview-placeholder-hint">Pick a starter template from the toolbar to get started quickly.</p>
        </div>
      </div>
    );
  }

  // JSON: validation errors
  if (language === 'json' && !validation.valid) {
    return (
      <div className="code-preview-pane code-preview-error">
        <div className="code-preview-error-header">
          <span className="code-preview-error-icon">⚠</span>
          JSON errors — fix them to see the preview
        </div>
        <ul className="code-preview-error-list">
          {validation.errors.map((e, i) => <li key={i}>{e}</li>)}
        </ul>
      </div>
    );
  }

  // JSON / C# DTO: live preview
  return (
    <div className="code-preview-pane">
      <LivePreview
        template={template}
        pages={pages}
        sharedElements={sharedElements}
        pageSettings={pageSettings}
        onBack={() => {}}
        onExport={onExport}
        hideBackButton
        exportLabel={isExporting ? 'Generating…' : 'Export PDF'}
      />
    </div>
  );
}
