import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import LivePreview from '@/components/Preview/LivePreview';
import type { Page, SimpleElement, Template, PageSettings } from '@/types';
import { normalizePageSettings } from '@/store';

export interface ParsedDesign {
  id?: string;
  name?: string;
  category?: string;
  description?: string;
  pages: Page[];
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

export default function CodePreviewPane({ raw, language, validation, parsed, pdfBlobUrl, onExport, isExporting, isConverting, convertError }: Props) {
  const { t } = useTranslation('codeEditor');
  const pages: Page[] = useMemo(
    () =>
      (parsed?.pages ?? []).map(p => ({
        ...p,
        elements: (p.elements ?? []) as SimpleElement[],
        layout: p.layout ? { ...p.layout, margins: p.layout.margins ? { ...p.layout.margins } : undefined } : undefined,
      })),
    [parsed],
  );

  const sharedElements = useMemo(
    () => (parsed?.sharedElements ?? []) as SimpleElement[],
    [parsed],
  );

  const pageSettings: PageSettings = useMemo(
    () => normalizePageSettings(parsed?.pageSettings),
    [parsed],
  );

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
          <div className="code-preview-placeholder-icon" aria-hidden="true">...</div>
          <p>{language === 'csharpPdf' ? t('preview.runningPdf') : t('preview.converting')}</p>
        </div>
      </div>
    );
  }

  // Shared: C# error
  if (language !== 'json' && convertError) {
    return (
      <div className="code-preview-pane code-preview-error">
        <div className="code-preview-error-header">
          <span className="code-preview-error-icon" aria-hidden="true">!</span>
          {language === 'csharpPdf'
            ? t('preview.pdfError')
            : language === 'csharpBase64'
              ? t('preview.base64Error')
              : t('preview.modelError')}
        </div>
        <ul className="code-preview-error-list">
          {convertError.split('\n').map((e, i) => <li key={i}>{e}</li>)}
        </ul>
      </div>
    );
  }

  // C# Code: show PDF iframe
  if (language === 'csharpPdf') {
    if (!pdfBlobUrl) {
      return (
        <div className="code-preview-pane code-preview-empty">
          <div className="code-preview-placeholder">
            <div className="code-preview-placeholder-icon">{'{ }'}</div>
            <p>{t('preview.runPdf')}</p>
            <p className="code-preview-placeholder-hint">{t('preview.pdfReturn')}</p>
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
  if ((language === 'csharpModel' || language === 'csharpBase64') && !parsed) {
    return (
      <div className="code-preview-pane code-preview-empty">
        <div className="code-preview-placeholder">
          <div className="code-preview-placeholder-icon">{'{ }'}</div>
          <p>{t('preview.runModel')}</p>
          <p className="code-preview-placeholder-hint">{t('preview.modelReturn')}</p>
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
          <p>{t('preview.emptyJson')}</p>
          <p className="code-preview-placeholder-hint">{t('preview.emptyJsonHint')}</p>
        </div>
      </div>
    );
  }

  // JSON: validation errors
  if (language === 'json' && !validation.valid) {
    return (
      <div className="code-preview-pane code-preview-error">
        <div className="code-preview-error-header">
          <span className="code-preview-error-icon" aria-hidden="true">!</span>
          {t('preview.jsonError')}
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
        exportLabel={isExporting ? t('generating') : t('exportPdf')}
      />
    </div>
  );
}
