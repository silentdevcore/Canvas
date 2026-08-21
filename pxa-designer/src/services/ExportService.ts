import html2canvas from 'html2canvas';
import type { SimpleElement, Template, PageSettings, Page } from '@/types';

export type ExportFormat = 'pdf' | 'json' | 'html' | 'xml' | 'word' | 'excel' | 'png' | 'jpeg' | 'svg' | 'csv' | 'md' | 'tiff' | 'odt';

export interface FormatInfo {
  key: string;
  mimeType: string;
  extension: string;
  supportsMultiPage?: boolean;
  supportsImages?: boolean;
  supportsRichText?: boolean;
  supportsFormFields?: boolean;
  multiPagePackaging?: 'native' | 'zip';
}

let _formatsCache: FormatInfo[] | null = null;

export class ExportService {
  private static readonly API_BASE_URL = '/api';

  private static async assetDataUrl(assetId: string): Promise<string> {
    const response = await fetch(`/api/pxa/v1/designer/assets/${encodeURIComponent(assetId)}/content`);
    if (!response.ok) throw new Error(`Image asset could not be loaded (${response.status}).`);
    const blob = await response.blob();
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = () => reject(reader.error ?? new Error('Image asset could not be read.'));
      reader.readAsDataURL(blob);
    });
  }

  private static async hydrateAssetElements(elements: SimpleElement[]): Promise<SimpleElement[]> {
    return Promise.all(elements.map(async element => element.type === 'image' && element.assetId
      ? { ...element, content: await this.assetDataUrl(element.assetId) }
      : element));
  }

  private static async hydrateAssetPages(pages: Page[]): Promise<Page[]> {
    return Promise.all(pages.map(async page => ({
      ...page,
      elements: await this.hydrateAssetElements(page.elements),
    })));
  }

  static safeFileStem(name: string | null | undefined): string {
    const normalized = (name ?? '').trim()
      .replace(/[<>:"/\\|?*\u0000-\u001f\s]+/g, '-')
      .replace(/^[.-]+|[.-]+$/g, '')
      .slice(0, 180);
    return normalized || 'document';
  }

  static buildDesignPayload(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
  ) {
    const settings = pageSettings ?? ({ width: 595, height: 842, orientation: 'portrait' } as PageSettings);
    return {
      id: template.id,
      name: template.name.trim() || 'Untitled document',
      category: template.category,
      description: template.description,
      pages: pages.map(page => ({ id: page.id, elements: page.elements })),
      sharedElements,
      pageSettings: {
        width: settings.width,
        height: settings.height,
        orientation: settings.orientation,
        unit: settings.unit,
        backgroundColor: settings.backgroundColor,
        backgroundImage: settings.backgroundImage || null,
        backgroundImageFit: settings.backgroundImageFit,
        margins: settings.margins,
        pageNumbering: settings.pageNumbering?.enabled ? settings.pageNumbering : null,
        globalWatermark: settings.globalWatermark?.enabled ? settings.globalWatermark : null,
        metadata: settings.metadata,
        namedStyles: settings.namedStyles ?? [],
        protection: settings.protection ?? null,
        encryption: settings.encryption?.enabled ? settings.encryption : null,
        customProperties: settings.customProperties ?? [],
        trackChanges: settings.trackChanges ?? false,
        systemLanguage: settings.systemLanguage ?? navigator.language.split('-')[0],
        activeLanguages: settings.activeLanguages ?? [],
        localizedProperties: settings.localizedProperties ?? [],
        targetLanguage: settings.targetLanguage ?? null,
      },
    };
  }

  private static async buildHydratedDesignPayload(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[],
    pageSettings?: PageSettings,
  ) {
    const [hydratedPages, hydratedSharedElements] = await Promise.all([
      this.hydrateAssetPages(pages),
      this.hydrateAssetElements(sharedElements),
    ]);
    return this.buildDesignPayload(template, hydratedPages, hydratedSharedElements, pageSettings);
  }

  private static responseFileName(response: Response): string | undefined {
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const rfc5987 = disposition.match(/filename\*=UTF-8''([^;]+)/i);
    const plain = disposition.match(/filename="?([^";]+)"?/i);
    return rfc5987 ? decodeURIComponent(rfc5987[1]) : plain?.[1];
  }

  private static downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }

  private static async readBlobPrefix(blob: Blob, length: number): Promise<Uint8Array> {
    const slice = blob.slice(0, length);
    if (typeof slice.arrayBuffer === 'function') {
      return new Uint8Array(await slice.arrayBuffer());
    }

    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onerror = () => reject(reader.error ?? new Error('Unable to inspect the exported file.'));
      reader.onload = () => resolve(new Uint8Array(reader.result as ArrayBuffer));
      reader.readAsArrayBuffer(slice);
    });
  }

  private static async validateArtifact(blob: Blob, format: string, contentType: string): Promise<void> {
    const actualType = contentType.split(';')[0].trim().toLowerCase();
    const signatures: Record<string, { mime: string[]; bytes?: number[]; text?: RegExp }> = {
      pdf: { mime: ['application/pdf'], bytes: [0x25, 0x50, 0x44, 0x46, 0x2d] },
      word: { mime: ['application/vnd.openxmlformats-officedocument.wordprocessingml.document'], bytes: [0x50, 0x4b] },
      excel: { mime: ['application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'], bytes: [0x50, 0x4b] },
      odt: { mime: ['application/vnd.oasis.opendocument.text'], bytes: [0x50, 0x4b] },
      png: { mime: ['image/png'], bytes: [0x89, 0x50, 0x4e, 0x47] },
      jpeg: { mime: ['image/jpeg'], bytes: [0xff, 0xd8, 0xff] },
      tiff: { mime: ['image/tiff'], bytes: [0x49, 0x49] },
      svg: { mime: ['image/svg+xml'], text: /<svg\b/i },
      html: { mime: ['text/html'], text: /<!doctype html|<html\b/i },
      xml: { mime: ['application/xml', 'text/xml'], text: /<\?xml|<[a-z_][\w:.-]*/i },
      csv: { mime: ['text/csv'] },
      md: { mime: ['text/markdown'] },
      json: { mime: ['application/json'], text: /^\s*[\[{]/ },
      zip: { mime: ['application/zip'], bytes: [0x50, 0x4b] },
    };
    const expected = signatures[format];
    if (!expected) return;
    if (!expected.mime.includes(actualType))
      throw new Error(`Export returned '${actualType || 'unknown'}' instead of ${expected.mime.join(' or ')}.`);
    const prefix = await this.readBlobPrefix(blob, 256);
    if (expected.bytes && !expected.bytes.every((value, index) => prefix[index] === value))
      throw new Error(`Export returned invalid ${format.toUpperCase()} data.`);
    if (expected.text && !expected.text.test(new TextDecoder().decode(prefix)))
      throw new Error(`Export returned invalid ${format.toUpperCase()} text.`);
  }

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
      borderTopWidth:    s.borderTopWidth    ?? null,
      borderTopColor:    s.borderTopColor    ?? null,
      borderTopStyle:    s.borderTopStyle    ?? null,
      borderRightWidth:  s.borderRightWidth  ?? null,
      borderRightColor:  s.borderRightColor  ?? null,
      borderRightStyle:  s.borderRightStyle  ?? null,
      borderBottomWidth: s.borderBottomWidth ?? null,
      borderBottomColor: s.borderBottomColor ?? null,
      borderBottomStyle: s.borderBottomStyle ?? null,
      borderLeftWidth:   s.borderLeftWidth   ?? null,
      borderLeftColor:   s.borderLeftColor   ?? null,
      borderLeftStyle:   s.borderLeftStyle   ?? null,
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
      visibleExpression: element.visibleExpression ?? null,
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
          assetId: element.assetId ?? null,
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
          chart: element.chart,
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

    const payload = await this.buildHydratedDesignPayload(template, pages, sharedElements, pageSettings);

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
    const isPageArchive = pages.length > 1 && ['png', 'jpeg', 'tiff', 'svg'].includes(format);
    await this.validateArtifact(blob, isPageArchive ? 'zip' : format, response.headers.get('Content-Type') ?? blob.type);
    const serverName = this.responseFileName(response);
    const extMap: Record<string, string> = {
      word: 'docx', excel: 'xlsx', md: 'md', jpeg: 'jpg',
      html: 'html', xml: 'xml', svg: 'svg', csv: 'csv', png: 'png',
      tiff: 'tiff', odt: 'odt',
    };
    const fallbackExt = extMap[format] ?? format;
    const fallbackName = isPageArchive
      ? `${this.safeFileStem(template.name)}-${format}-pages.zip`
      : `${this.safeFileStem(template.name)}.${fallbackExt}`;
    this.downloadBlob(blob, serverName ?? fallbackName);
    onProgress?.('Done!');
  }

  static async exportMultiLanguage(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
  ): Promise<void> {
    const payload = await this.buildHydratedDesignPayload(template, pages, sharedElements, pageSettings);

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
    await this.validateArtifact(blob, 'zip', response.headers.get('Content-Type') ?? blob.type);
    this.downloadBlob(blob, this.responseFileName(response) ?? `${this.safeFileStem(template.name)}-multilanguage.zip`);
  }

  static exportToJSON(template: Template, pages: Page[], sharedElements: SimpleElement[] = [], pageSettings?: PageSettings): void {
    const payload = this.buildDesignPayload(template, pages, sharedElements, pageSettings);
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    this.downloadBlob(blob, `${this.safeFileStem(template.name)}.json`);
  }

  static async exportToPDF(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
    onProgress?: (progress: string) => void
  ): Promise<Blob> {
    const artifact = await this.renderDesignPdfArtifact(template, pages, sharedElements, pageSettings, onProgress);
    this.downloadBlob(artifact.blob, artifact.fileName ?? `${this.safeFileStem(template.name)}.pdf`);
    onProgress?.('Done!');
    return artifact.blob;
  }

  static async renderDesignPdfBlob(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
    onProgress?: (progress: string) => void
  ): Promise<Blob> {
    return (await this.renderDesignPdfArtifact(template, pages, sharedElements, pageSettings, onProgress)).blob;
  }

  private static async renderDesignPdfArtifact(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
    onProgress?: (progress: string) => void,
  ): Promise<{ blob: Blob; fileName?: string }> {
    onProgress?.('Connecting to PDF service…');

    const payload = await this.buildHydratedDesignPayload(template, pages, sharedElements, pageSettings);

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
    await this.validateArtifact(blob, 'pdf', response.headers.get('Content-Type') ?? blob.type);
    return { blob, fileName: this.responseFileName(response) };
  }

  static async printToPDF(
    template: Template,
    pages: Page[],
    sharedElements: SimpleElement[] = [],
    pageSettings?: PageSettings,
    messages: { blocked?: string; preparing?: string; title?: string } = {},
  ): Promise<void> {
    const printWindow = window.open('', '_blank');
    if (!printWindow) throw new Error(messages.blocked ?? 'The PDF print window was blocked by the browser.');
    printWindow.opener = null;
    printWindow.document.title = messages.title ?? `Printing ${template.name}`;
    printWindow.document.body.textContent = messages.preparing ?? 'Preparing PDF for printing...';
    try {
      const blob = await this.renderDesignPdfBlob(template, pages, sharedElements, pageSettings);
      const url = URL.createObjectURL(blob);
      printWindow.location.replace(url);
      window.setTimeout(() => {
        try { printWindow.print(); } finally {
          window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        }
      }, 900);
    } catch (error) {
      printWindow.close();
      throw error;
    }
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
    await this.validateArtifact(blob, 'pdf', response.headers.get('Content-Type') ?? blob.type);
    this.downloadBlob(blob, this.responseFileName(response) ?? `${this.safeFileStem(name)}.pdf`);
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

  static async importMarkdown(file: File, assetBaseUri?: string): Promise<object> {
    return this._importFile(
      file,
      'import-markdown',
      assetBaseUri?.trim() ? { assetBaseUri: assetBaseUri.trim() } : undefined,
    );
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

  private static async _importFile(
    file: File,
    endpoint: string,
    fields?: Record<string, string>,
  ): Promise<object> {
    const form = new FormData();
    form.append('file', file);
    Object.entries(fields ?? {}).forEach(([key, value]) => form.append(key, value));
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
