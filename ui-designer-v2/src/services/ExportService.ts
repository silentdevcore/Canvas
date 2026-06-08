import html2canvas from 'html2canvas';
import type { SimpleElement, Template, PageSettings, Page } from '@/types';

export type ExportFormat = 'pdf' | 'json' | 'html' | 'xml' | 'word' | 'excel' | 'png' | 'jpeg' | 'svg' | 'csv' | 'md' | 'tiff' | 'odt';

export interface FormatInfo {
  key: string;
  mimeType: string;
  extension: string;
}

let _formatsCache: FormatInfo[] | null = null;

export class ExportService {
  private static readonly API_BASE_URL = '/api';

  static convertElementsToTemplate(pages: Page[], template: Template, sharedElements: SimpleElement[] = [], pageSettings?: PageSettings) {
    return {
      id: template.id,
      name: template.name,
      category: template.category,
      description: template.description,
      version: '1.0',
      schemaVersion: '1.0',
      pageSettings: pageSettings
        ? {
            width: pageSettings.width,
            height: pageSettings.height,
            orientation: pageSettings.orientation,
            backgroundColor: pageSettings.backgroundColor,
            backgroundImage: pageSettings.backgroundImage || null,
            backgroundImageFit: pageSettings.backgroundImageFit,
            margins: pageSettings.margins,
            header: pageSettings.headerEnabled ? { height: pageSettings.headerHeight } : null,
            footer: pageSettings.footerEnabled ? { height: pageSettings.footerHeight } : null,
            bleedSize: pageSettings.bleedSize || null,
            cropMarks: pageSettings.cropMarks,
            watermark: pageSettings.globalWatermark.enabled ? pageSettings.globalWatermark : null,
            pageNumbering: pageSettings.pageNumbering.enabled ? pageSettings.pageNumbering : null,
            guides: { previewOnly: true },
          }
        : { width: 595, height: 842, orientation: 'portrait', margins: { top: 48, right: 48, bottom: 48, left: 48 } },
      metadata: pageSettings?.metadata ?? { title: '', author: '', subject: '', keywords: '' },
      exportDefaults: pageSettings?.exportDefaults ?? { quality: 'printer', embedFonts: true, compressImages: true, accessibilityTagged: false },
      pagination: pageSettings?.pagination ?? null,
      // Shared header/footer elements that appear on every page
      sharedElements: sharedElements.map(element => ({
        id: element.id,
        type: this.mapElementType(element.type),
        x: element.x,
        y: element.y,
        width: element.width,
        height: element.height,
        properties: this.extractElementProperties(element),
      })),
      // Multi-page: each page includes its own elements + shared elements merged
      pages: pages.map((page, index) => ({
        pageIndex: index,
        id: page.id,
        elements: [
          ...sharedElements.map(element => ({
            id: element.id,
            type: this.mapElementType(element.type),
            x: element.x,
            y: element.y,
            width: element.width,
            height: element.height,
            properties: this.extractElementProperties(element),
          })),
          ...page.elements.map(element => ({
            id: element.id,
            type: this.mapElementType(element.type),
            x: element.x,
            y: element.y,
            width: element.width,
            height: element.height,
            properties: this.extractElementProperties(element),
          })),
        ],
      })),
      // Flat element list for single-page backward compat (page 1 + shared)
      elements: [
        ...sharedElements,
        ...pages.flatMap(page => page.elements),
      ].map(element => ({
        id: element.id,
        type: this.mapElementType(element.type),
        x: element.x,
        y: element.y,
        width: element.width,
        height: element.height,
        properties: this.extractElementProperties(element),
      })),
      // Aggregated form metadata: all form fields sorted by tab order
      formMetadata: this.buildFormMetadata(pages, sharedElements),
    };
  }

  private static mapElementType(uiType: string): string {
    const typeMapping: Record<string, string> = {
      text:       'Text',
      richtext:   'RichText',
      image:      'Image',
      shape:      'Rectangle',
      rect:       'Rectangle',
      circle:     'Circle',
      line:       'Line',
      table:      'Table',
      chart:      'Chart',
      qrcode:     'QRCode',
      barcode:    'Barcode',
      signature:  'Signature',
      field:      'FormField',
      textarea:   'TextArea',
      checkbox:   'Checkbox',
      button:     'Button',
      dropdown:   'Dropdown',
      optionlist: 'OptionList',
      radio:      'RadioGroup',
      subsection: 'Subsection',
      area:       'Area',
      watermark:  'Watermark',
      note:       'Note',
      arrow:      'Arrow',
      draw:       'Draw',
      date:       'Date',
      highlight:  'Highlight',
      checkmark:  'CheckMark',
      pageboundary: 'PageBoundary',
      pagenumber:     'PageNumber',
      footnote:       'Footnote',
      endnote:        'Endnote',
      bookmark:       'Bookmark',
      comment:        'Comment',
      contentcontrol: 'ContentControl',
      toc:            'Toc',
    };
    return typeMapping[uiType] ?? 'Text';
  }

  private static sharedStyle(element: SimpleElement): Record<string, unknown> {
    const s = element.style ?? {};
    return {
      rotation:          s.rotation         ?? 0,
      backgroundColor:   s.backgroundColor  ?? null,
      backgroundOpacity: s.backgroundOpacity ?? 1,
      borderWidth:       s.borderWidth       ?? 0,
      borderColor:       s.borderColor       ?? null,
      borderStyle:       s.borderStyle       ?? 'none',
      borderRadius:      s.borderRadius      ?? 0,
      paddingTop:        s.paddingTop        ?? 0,
      paddingRight:      s.paddingRight      ?? 0,
      paddingBottom:     s.paddingBottom     ?? 0,
      paddingLeft:       s.paddingLeft       ?? 0,
    };
  }

  private static extractElementProperties(element: SimpleElement): Record<string, unknown> {
    const base = {
      name: element.name ?? null,
      x: element.x, y: element.y, width: element.width, height: element.height,
      ...this.sharedStyle(element),
    };

    switch (element.type) {
      case 'text':
        return {
          ...base,
          content:        element.content        || '',
          fontSize:       element.style?.fontSize       || 16,
          fontFamily:     element.style?.fontFamily     || 'Arial',
          color:          element.style?.color          || '#000000',
          fontWeight:     element.style?.fontWeight     || 'normal',
          fontStyle:      element.style?.fontStyle      || 'normal',
          textDecoration: element.style?.textDecoration || 'none',
          textAlign:      element.style?.textAlign      || 'left',
          lineHeight:     element.style?.lineHeight     ?? 1.4,
          letterSpacing:  element.style?.letterSpacing  ?? 0,
          headingLevel:   element.headingLevel ?? null,
        };

      case 'richtext':
        return {
          ...base,
          htmlContent:  element.htmlContent || '',
          fontSize:     element.style?.fontSize || 14,
          headingLevel: element.headingLevel ?? null,
        };

      case 'image':
        return {
          ...base,
          src: element.content || '',
          fitMode: element.fitMode || 'contain',
          focalX: element.focalX ?? 50,
          focalY: element.focalY ?? 50,
          cropX: element.cropX ?? 0,
          cropY: element.cropY ?? 0,
          cropWidth: element.cropWidth ?? 0,
          cropHeight: element.cropHeight ?? 0,
          preserveAspectRatio: element.preserveAspectRatio ?? false,
        };

      case 'shape':
      case 'rect':
        return {
          ...base,
          backgroundColor: element.style?.backgroundColor ?? element.style?.fill ?? 'transparent',
          borderRadius: element.style?.borderRadius ?? 0,
        };

      case 'circle':
        return {
          ...base,
          backgroundColor: element.style?.backgroundColor ?? element.style?.fill ?? 'transparent',
        };

      case 'line':
        return {
          ...base,
          color: element.style?.backgroundColor || '#9ca3af',
          thickness: element.height,
        };

      case 'table':
        return {
          ...base,
          rows:           element.style?.rows          ?? 3,
          columns:        element.style?.columns        ?? 3,
          borderWidth:    element.style?.borderWidth    ?? 1,
          borderColor:    element.style?.borderColor    || '#000000',
          cellPadding:    element.style?.cellPadding    ?? 5,
          headerRow:      element.headerRow             ?? false,
          footerRow:      element.footerRow             ?? false,
          headerBgColor:  element.headerBgColor         || '#f1f5f9',
          zebraEnabled:   element.zebraEnabled          ?? false,
          zebraColor:     element.zebraColor            || '#f9fafb',
          columnWidths:   element.columnWidths          ?? [],
          cellData:       element.cellData              ?? [],
          columnAlignments: element.columnAlignments    ?? [],
        };

      case 'chart':
        return {
          ...base,
          chartType: element.chartType || 'bar',
          chartData: element.chartData || {},
        };

      case 'qrcode':
        return {
          ...base,
          value: element.qrValue || 'https://example.com',
          size: element.qrSize || 100,
          errorCorrection: 'M',
        };

      case 'barcode':
        return {
          ...base,
          value: element.barcodeValue || '123456789012',
          format: element.barcodeType || 'CODE128',
        };

      case 'signature':
        return {
          ...base,
          label: element.signatureLabel || 'Signature',
          required: true,
        };

      case 'field':
        return {
          ...base,
          label: element.fieldLabel || 'Text field',
          name: element.fieldName || element.id,
          required: Boolean(element.required),
          inputType: 'text',
          tabIndex: element.tabIndex ?? null,
          validationMin: element.validationMin ?? null,
          validationMax: element.validationMax ?? null,
          validationPattern: element.validationPattern ?? null,
        };

      case 'textarea':
        return {
          ...base,
          label: element.fieldLabel || 'Text area',
          name: element.fieldName || element.id,
          placeholder: element.placeholder || '',
          required: Boolean(element.required),
          inputType: 'textarea',
          tabIndex: element.tabIndex ?? null,
          validationMin: element.validationMin ?? null,
          validationMax: element.validationMax ?? null,
          validationPattern: element.validationPattern ?? null,
        };

      case 'checkbox':
        return {
          ...base,
          label: element.fieldLabel || 'Checkbox',
          name: element.fieldName || element.id,
          required: Boolean(element.required),
          checked: false,
          tabIndex: element.tabIndex ?? null,
        };

      case 'button':
        return {
          ...base,
          label: element.content || 'Button',
          backgroundColor: element.style?.backgroundColor || '#3b82f6',
          color: element.style?.color || '#ffffff',
          fontSize: element.style?.fontSize || 14,
          borderRadius: element.style?.borderRadius ?? 4,
        };

      case 'dropdown':
        return {
          ...base,
          options: element.options || [],
          multiSelect: element.multiSelect ?? false,
          fontSize: element.style?.fontSize || 14,
          color: element.style?.color || '#000000',
          tabIndex: element.tabIndex ?? null,
        };

      case 'optionlist':
        return {
          ...base,
          options: element.options || [],
          ordered: element.ordered ?? false,
          fontSize: element.style?.fontSize || 14,
          color: element.style?.color || '#000000',
        };

      case 'radio':
        return {
          ...base,
          options: element.options || [],
          fontSize: element.style?.fontSize || 14,
          color: element.style?.color || '#000000',
          tabIndex: element.tabIndex ?? null,
        };

      case 'subsection':
      case 'area':
        return base;

      case 'watermark':
        return {
          ...base,
          mode: element.watermarkMode || 'text',
          content: element.content || '',
          pageScope: element.pageScope || 'all',
          pageRange: element.pageRange || '',
          color: element.style?.color || '#64748b',
          opacity: element.style?.opacity ?? 0.18,
          rotation: element.style?.rotation ?? -24,
          scale: element.style?.scale ?? 1,
          fontSize: element.style?.fontSize || 42,
        };

      case 'note':
        return {
          ...base,
          title: element.noteTitle || 'Notiz',
          body: element.noteBody || '',
          author: element.noteAuthor || '',
          collapsed: element.noteCollapsed ?? false,
          backgroundColor: element.style?.backgroundColor || '#fef3c7',
          color: element.style?.color || '#78350f',
        };

      case 'arrow':
        return {
          ...base,
          mode: element.arrowMode || 'straight',
          startMarker: element.startMarker || 'none',
          endMarker: element.endMarker || 'arrow',
          color: element.style?.color || '#dc2626',
          strokeWidth: element.style?.strokeWidth || 4,
          dashStyle: element.style?.dashStyle || 'solid',
        };

      case 'draw':
        return {
          ...base,
          tool: element.drawTool || 'pen',
          pathData: element.pathData || '',
          color: element.style?.color || '#1d4ed8',
          strokeWidth: element.style?.strokeWidth || 4,
          opacity: element.style?.opacity ?? 1,
        };

      case 'date':
        return {
          ...base,
          mode: element.dateMode || 'static',
          value: element.content || '',
          binding: element.binding || '',
          format: element.dateFormat || 'yyyy-MM-dd',
          locale: element.locale || 'de-DE',
          timezone: element.timezone || 'Europe/Berlin',
          fallbackText: element.fallbackText || '-',
          color: element.style?.color || '#111827',
          fontSize: element.style?.fontSize || 14,
        };

      case 'highlight':
        return {
          ...base,
          mode: element.markMode || 'rectangle',
          color: element.style?.backgroundColor || '#fde047',
          opacity: element.style?.opacity ?? 0.45,
          borderRadius: element.style?.borderRadius ?? 4,
          blendMode: element.style?.blendMode || 'multiply',
        };

      case 'checkmark':
        return {
          ...base,
          label: element.fieldLabel || '',
          name: element.fieldName || element.id,
          state: element.checkState || 'checked',
          color: element.style?.color || '#16a34a',
          strokeWidth: element.style?.strokeWidth || 3,
          binding: element.binding || '',
        };

      case 'pageboundary':
        return {
          ...base,
          mode: element.pageBoundaryMode || 'start',
          label: element.content || '',
          color: element.style?.color || '#7c3aed',
        };

      case 'pagenumber':
        return {
          ...base,
          format: element.numberingFormat || 'pageOfTotal',
          pageScope: element.pageScope || 'all',
          pageRange: element.pageRange || '',
          startNumber: element.startNumber || 1,
          prefix: element.prefix || '',
          suffix: element.suffix || '',
          color: element.style?.color || '#374151',
          fontSize: element.style?.fontSize || 12,
        };

      case 'footnote':
      case 'endnote':
        return {
          ...base,
          footnoteText: element.footnoteText || '',
        };

      case 'bookmark':
        return {
          ...base,
          bookmarkName: element.bookmarkName || '',
          bookmarkTarget: element.bookmarkTarget || '',
        };

      case 'comment':
        return {
          ...base,
          commentText: element.commentText || '',
          commentAuthor: element.commentAuthor || '',
          commentDate: element.commentDate || '',
          commentId: element.commentId || '',
        };

      case 'contentcontrol':
        return {
          ...base,
          contentControlType: element.contentControlType || 'richText',
          contentControlTag: element.contentControlTag || '',
          contentControlTitle: element.contentControlTitle || '',
          contentControlPlaceholder: element.contentControlPlaceholder || '',
          content: element.content || '',
        };

      case 'toc':
        return {
          ...base,
          tocTitle:           element.tocTitle           ?? 'Table of Contents',
          tocShowPageNumbers: element.tocShowPageNumbers ?? true,
          tocShowLeaderDots:  element.tocShowLeaderDots  ?? true,
          tocMinLevel:        element.tocMinLevel        ?? 1,
          tocMaxLevel:        element.tocMaxLevel        ?? 3,
          tocEntries: (element.tocEntries ?? []).map(e => ({
            text:  e.text,
            level: e.level,
            page:  e.page,
          })),
          color:    element.style?.color    ?? '#1f2937',
          fontSize: element.style?.fontSize ?? 12,
        };

      default:
        return base;
    }
  }

  static async listSupportedFormats(): Promise<FormatInfo[]> {
    if (_formatsCache) return _formatsCache;
    try {
      const res = await fetch(`${this.API_BASE_URL}/export/formats`);
      if (!res.ok) return [];
      _formatsCache = await res.json();
      return _formatsCache!;
    } catch {
      return [];
    }
  }

  static async exportViaBackend(
    format: ExportFormat,
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
    onProgress?: (msg: string) => void
  ): Promise<void> {
    onProgress?.(`Preparing ${format.toUpperCase()} export…`);

    const payload = {
      id: template.id,
      name: template.name,
      category: template.category,
      description: template.description,
      pages: pages.map(p => ({ id: p.id, elements: p.elements })),
      sharedElements,
      pageSettings: pageSettings
        ? {
            width: pageSettings.width,
            height: pageSettings.height,
            orientation: pageSettings.orientation,
            margins: pageSettings.margins,
            backgroundColor: pageSettings.backgroundColor,
            backgroundImage: pageSettings.backgroundImage,
            backgroundImageFit: pageSettings.backgroundImageFit,
            pageNumbering: pageSettings.pageNumbering.enabled ? pageSettings.pageNumbering : null,
            globalWatermark: pageSettings.globalWatermark.enabled ? pageSettings.globalWatermark : null,
            metadata: pageSettings.metadata,
            namedStyles: pageSettings.namedStyles ?? [],
            protection: pageSettings.protection ?? null,
            customProperties: pageSettings.customProperties ?? [],
            trackChanges: pageSettings.trackChanges ?? false,
            systemLanguage: navigator.language.split('-')[0],
            activeLanguages: pageSettings.activeLanguages ?? [],
            localizedProperties: pageSettings.localizedProperties ?? [],
          }
        : { width: 595, height: 842, orientation: 'portrait' },
    };

    const response = await fetch(`${this.API_BASE_URL}/export?format=${encodeURIComponent(format)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (response.status === 415) {
      const body = await response.json().catch(() => ({}));
      const supported = (body.supportedFormats as string[] | undefined)?.join(', ') ?? '';
      throw new Error(`Format '${format}' is not supported by the server. Supported: ${supported}`);
    }

    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }

    onProgress?.('Downloading…');
    const blob = await response.blob();
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;

    const disposition = response.headers.get('Content-Disposition') ?? '';
    const rfc5987 = disposition.match(/filename\*=UTF-8''([^;]+)/i);
    const plain   = disposition.match(/filename="?([^";]+)"?/i);
    const serverName = rfc5987 ? decodeURIComponent(rfc5987[1]) : plain?.[1];
    const extMap: Record<string, string> = {
      word: 'docx', excel: 'xlsx', md: 'md', jpeg: 'jpg',
      html: 'html', xml: 'xml', svg: 'svg', csv: 'csv', png: 'png',
      tiff: 'tiff', odt: 'odt',
    };
    const fallbackExt = extMap[format] ?? format;
    a.download = serverName ?? `${template.name.replace(/\s+/g, '-').toLowerCase()}.${fallbackExt}`;

    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    onProgress?.('Done!');
  }

  static async exportMultiLanguage(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
  ): Promise<void> {
    const payload = {
      id: template.id,
      name: template.name,
      category: template.category,
      description: template.description,
      pages: pages.map(p => ({ id: p.id, elements: p.elements })),
      sharedElements,
      pageSettings: pageSettings
        ? {
            width: pageSettings.width,
            height: pageSettings.height,
            orientation: pageSettings.orientation,
            margins: pageSettings.margins,
            backgroundColor: pageSettings.backgroundColor,
            metadata: pageSettings.metadata,
            systemLanguage: navigator.language.split('-')[0],
            activeLanguages: pageSettings.activeLanguages ?? [],
            localizedProperties: pageSettings.localizedProperties ?? [],
          }
        : { width: 595, height: 842, orientation: 'portrait' },
    };

    const response = await fetch(`${this.API_BASE_URL}/export/multilanguage?format=pdf`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${template.name.replace(/\s+/g, '-').toLowerCase()}-multilanguage.zip`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  static exportToJSON(template: Template, pages: Page[], sharedElements: SimpleElement[] = [], pageSettings?: PageSettings): void {
    const payload = this.convertElementsToTemplate(pages, template, sharedElements, pageSettings);
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${template.name.replace(/\s+/g, '_')}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  static async exportToPDF(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
    onProgress?: (progress: string) => void
  ): Promise<void> {
    onProgress?.('Connecting to PDF service…');

    const payload = {
      id: template.id,
      name: template.name,
      category: template.category,
      description: template.description,
      pages: pages.map(p => ({ id: p.id, elements: p.elements })),
      sharedElements,
      pageSettings: pageSettings
        ? {
            width: pageSettings.width,
            height: pageSettings.height,
            orientation: pageSettings.orientation,
            margins: pageSettings.margins,
            backgroundColor: pageSettings.backgroundColor,
            backgroundImage: pageSettings.backgroundImage,
            backgroundImageFit: pageSettings.backgroundImageFit,
            pageNumbering: pageSettings.pageNumbering.enabled ? pageSettings.pageNumbering : null,
            globalWatermark: pageSettings.globalWatermark.enabled ? pageSettings.globalWatermark : null,
            metadata: pageSettings.metadata,
            namedStyles: pageSettings.namedStyles ?? [],
            protection: pageSettings.protection ?? null,
            customProperties: pageSettings.customProperties ?? [],
            trackChanges: pageSettings.trackChanges ?? false,
          }
        : { width: 595, height: 842, orientation: 'portrait' },
    };

    const response = await fetch(`${this.API_BASE_URL}/templates/render-design`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      const msg = [err.error, err.details, err.inner].filter(Boolean).join(' — ');
      throw new Error(msg || `HTTP ${response.status}`);
    }

    onProgress?.('Downloading…');
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${template.name.replace(/\s+/g, '-').toLowerCase()}.pdf`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    onProgress?.('Done!');
  }

  static async exportJsonToPDF(payload: object, name = 'document'): Promise<void> {
    const response = await fetch(`${this.API_BASE_URL}/templates/render-design`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${name.replace(/\s+/g, '-').toLowerCase()}.pdf`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  static async exportToImage(
    pageContainerSelector: string,
    filename: string,
    format: 'png' | 'jpeg' = 'png'
  ): Promise<void> {
    const el = document.querySelector(pageContainerSelector) as HTMLElement | null;
    if (!el) throw new Error('Page container not found');

    const canvas = await html2canvas(el, {
      scale: 2,
      useCORS: true,
      backgroundColor: '#ffffff',
      logging: false,
    });

    const mimeType = format === 'jpeg' ? 'image/jpeg' : 'image/png';
    const dataUrl = canvas.toDataURL(mimeType, 0.95);
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = `${filename}.${format}`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }

  static async importPdf(file: File): Promise<object> {
    return this._importFile(file, 'import-pdf-engine');
  }

  static async importDoc(file: File): Promise<object> {
    return this._importFile(file, 'import-doc');
  }

  static async importDocx(file: File): Promise<object> {
    return this._importFile(file, 'import-docx');
  }

  static async importOdt(file: File): Promise<object> {
    return this._importFile(file, 'import-odt');
  }

  static async importImage(file: File): Promise<object> {
    return this._importFile(file, 'import-image');
  }

  static async importSvg(file: File): Promise<object> {
    return this._importFile(file, 'import-svg');
  }

  static async importPptx(file: File): Promise<object> {
    return this._importFile(file, 'import-pptx');
  }

  static async importImageAnalysis(
    file: File,
    pageWidthPt?: number,
    pageHeightPt?: number,
    options: {
      includeDiagnostics?: boolean;
      includeDebugOverlay?: boolean;
      includeFallbackImageLayer?: boolean;
      lowConfidenceThreshold?: number;
    } = {},
  ): Promise<object> {
    const form = new FormData();
    form.append('file', file);
    if (pageWidthPt)  form.append('pageWidthPt',  String(pageWidthPt));
    if (pageHeightPt) form.append('pageHeightPt', String(pageHeightPt));
    if (options.includeDiagnostics) form.append('includeDiagnostics', 'true');
    if (options.includeDebugOverlay) form.append('includeDebugOverlay', 'true');
    if (options.includeFallbackImageLayer) form.append('includeFallbackImageLayer', 'true');
    if (options.lowConfidenceThreshold !== undefined)
      form.append('lowConfidenceThreshold', String(options.lowConfidenceThreshold));
    const response = await fetch(`${this.API_BASE_URL}/document/import-image-analysis`, {
      method: 'POST',
      body: form,
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    return response.json();
  }

  static async importImageOcr(
    file: File,
    pageWidthPt?: number,
    pageHeightPt?: number,
    options: {
      languages?: string;
      includeBackgroundImage?: boolean;
      includeDiagnostics?: boolean;
      includeDebugOverlay?: boolean;
      enablePreprocessing?: boolean;
      lowConfidenceThreshold?: number;
      layoutMode?: string;
    } = {},
  ): Promise<object> {
    const form = new FormData();
    form.append('file', file);
    form.append('languages', options.languages || 'deu+eng');
    if (pageWidthPt)  form.append('pageWidthPt',  String(pageWidthPt));
    if (pageHeightPt) form.append('pageHeightPt', String(pageHeightPt));
    form.append('includeBackgroundImage', options.includeBackgroundImage === false ? 'false' : 'true');
    form.append('includeOcrPages', 'false');
    form.append('layoutMode', options.layoutMode || 'structured');
    if (options.includeDiagnostics) form.append('includeDiagnostics', 'true');
    if (options.includeDebugOverlay) form.append('includeDebugOverlay', 'true');
    if (options.enablePreprocessing) form.append('enablePreprocessing', 'true');
    if (options.lowConfidenceThreshold !== undefined)
      form.append('lowConfidenceThreshold', String(options.lowConfidenceThreshold));

    const response = await fetch(`${this.API_BASE_URL}/document/convert-image-to-pdf?debug=true`, {
      method: 'POST',
      body: form,
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    return response.json();
  }

  static async downloadImageOcrPdf(
    file: File,
    pageWidthPt?: number,
    pageHeightPt?: number,
    options: {
      languages?: string;
      includeBackgroundImage?: boolean;
      enablePreprocessing?: boolean;
      lowConfidenceThreshold?: number;
      layoutMode?: string;
    } = {},
  ): Promise<void> {
    const form = new FormData();
    form.append('file', file);
    form.append('languages', options.languages || 'deu+eng');
    if (pageWidthPt)  form.append('pageWidthPt',  String(pageWidthPt));
    if (pageHeightPt) form.append('pageHeightPt', String(pageHeightPt));
    form.append('includeBackgroundImage', options.includeBackgroundImage === false ? 'false' : 'true');
    form.append('layoutMode', options.layoutMode || 'structured');
    if (options.enablePreprocessing) form.append('enablePreprocessing', 'true');
    if (options.lowConfidenceThreshold !== undefined)
      form.append('lowConfidenceThreshold', String(options.lowConfidenceThreshold));

    const response = await fetch(`${this.API_BASE_URL}/document/convert-image-to-pdf`, {
      method: 'POST',
      body: form,
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${file.name.replace(/\.[^.]+$/, '').replace(/\s+/g, '-').toLowerCase()}.pdf`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  private static async _importFile(file: File, endpoint: string): Promise<object> {
    const form = new FormData();
    form.append('file', file);
    const response = await fetch(`${this.API_BASE_URL}/document/${endpoint}`, {
      method: 'POST',
      body: form,
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    return response.json();
  }

  static async findAndReplace(
    design: object,
    find: string,
    replace: string,
    options: { caseSensitive?: boolean; wholeWord?: boolean; useRegex?: boolean } = {}
  ): Promise<{ design: object; replacementCount: number; affectedElementIds: string[] }> {
    const response = await fetch(`${this.API_BASE_URL}/document/find-replace`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        design,
        find,
        replace,
        caseSensitive: options.caseSensitive ?? false,
        wholeWord: options.wholeWord ?? false,
        useRegex: options.useRegex ?? false,
      }),
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    return response.json();
  }

  static async cloneDesign(design: object): Promise<object> {
    const response = await fetch(`${this.API_BASE_URL}/document/clone`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ design }),
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    const result = await response.json();
    return result.design ?? result;
  }

  static async extractPages(design: object, pageNumbers: number[]): Promise<object> {
    const response = await fetch(`${this.API_BASE_URL}/document/extract-pages`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ design, pageNumbers }),
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    const result = await response.json();
    return result.design ?? result;
  }

  static async signDocx(docxBlob: Blob, certFile: File, password?: string): Promise<Blob> {
    const form = new FormData();
    form.append('docx', docxBlob, 'document.docx');
    form.append('certificate', certFile, certFile.name);
    if (password) form.append('password', password);

    const response = await fetch(`${this.API_BASE_URL}/document/sign-docx`, {
      method: 'POST',
      body: form,
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(err.error || `HTTP ${response.status}`);
    }
    return response.blob();
  }

  static validateForExport(elements: SimpleElement[]): { isValid: boolean; errors: string[] } {
    const errors: string[] = [];

    if (elements.length === 0) {
      errors.push('Template must contain at least one element');
    }

    elements.forEach((element, index) => {
      const n = index + 1;
      switch (element.type) {
        case 'text':
          if (!element.content?.trim()) errors.push(`Text element ${n} has no content`);
          break;
        case 'qrcode':
          if (!element.qrValue?.trim()) errors.push(`QR Code element ${n} has no value`);
          break;
        case 'barcode':
          if (!element.barcodeValue?.trim()) errors.push(`Barcode element ${n} has no value`);
          break;
        case 'image':
          if (!element.content?.trim()) errors.push(`Image element ${n} has no source URL`);
          break;
        case 'watermark':
          if (!element.content?.trim()) errors.push(`Watermark element ${n} has no content`);
          break;
        case 'draw':
          if (!element.pathData?.trim()) errors.push(`Draw element ${n} has no path data`);
          break;
        case 'date':
          if (element.dateMode === 'binding' && !element.binding?.trim()) errors.push(`Date element ${n} needs a binding`);
          break;
        case 'dropdown':
        case 'optionlist':
        case 'radio':
          if (!element.options?.length) errors.push(`${element.type} element ${n} has no options`);
          break;
      }
    });

    return { isValid: errors.length === 0, errors };
  }

  private static buildFormMetadata(pages: Page[], sharedElements: SimpleElement[]) {
    const FORM_TYPES = new Set(['field', 'textarea', 'checkbox', 'radio', 'dropdown', 'signature']);
    const allElements = [
      ...sharedElements,
      ...pages.flatMap((page, i) => page.elements.map(el => ({ ...el, _pageIndex: i }))),
    ] as (SimpleElement & { _pageIndex?: number })[];

    const formFields = allElements
      .filter(el => FORM_TYPES.has(el.type))
      .map(el => ({
        id: el.id,
        type: el.type,
        name: el.fieldName || el.id,
        label: el.fieldLabel || el.signatureLabel || '',
        required: Boolean(el.required),
        tabIndex: el.tabIndex ?? null,
        page: (el._pageIndex ?? 0) + 1,
        validationMin: el.validationMin ?? null,
        validationMax: el.validationMax ?? null,
        validationPattern: el.validationPattern ?? null,
        options: (el.type === 'dropdown' || el.type === 'radio' || el.type === 'optionlist') ? (el.options ?? []) : undefined,
      }))
      .sort((a, b) => {
        if (a.tabIndex !== null && b.tabIndex !== null) return a.tabIndex - b.tabIndex;
        if (a.tabIndex !== null) return -1;
        if (b.tabIndex !== null) return 1;
        return a.page - b.page;
      });

    return { fieldCount: formFields.length, fields: formFields };
  }
}

export default ExportService;
