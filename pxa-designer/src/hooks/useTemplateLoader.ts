import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useEditorStore } from '@/store';
import { getTemplateElementsLocalized, getTemplatePagesLocalized } from '@/data/templateContent.i18n';
import type { TemplateDefinition } from '@/data/templates';
import type { SimpleElement } from '@/types';
import ExportService from '@/services/ExportService';

// `pxa_docs_opened`/`pxa_last_template` are the current keys; `canvas_*` are
// read as a fallback so a count/name saved before this rename isn't lost.
function recordDocOpened(name: string): void {
  const prev = parseInt(
    localStorage.getItem('pxa_docs_opened') ?? localStorage.getItem('canvas_docs_opened') ?? '0',
    10,
  );
  localStorage.setItem('pxa_docs_opened', String(prev + 1));
  localStorage.setItem('pxa_last_template', name);
}

function createStarterElements(template: TemplateDefinition): SimpleElement[] {
  const now = Date.now();
  const isFormTemplate = ['invoice', 'receipt', 'certificate', 'letter'].includes(template.category);

  const baseElements: SimpleElement[] = [
    {
      id: `title-${now}`,
      type: 'text',
      x: 72,
      y: 72,
      width: 360,
      height: 54,
      content: template.name,
      style: { fontSize: 24, color: '#101828', fontWeight: 'bold' }
    },
    {
      id: `intro-${now}`,
      type: 'richtext',
      x: 72,
      y: 138,
      width: 420,
      height: 86,
      htmlContent: `<p><strong>${template.category.toUpperCase()}</strong></p><p>${template.description}</p>`
    },
    {
      id: `signature-${now}`,
      type: 'signature',
      x: 72,
      y: 660,
      width: 300,
      height: 96,
      signatureLabel: 'Signature'
    }
  ];

  if (!isFormTemplate) return baseElements;

  return [
    ...baseElements,
    {
      id: `field-name-${now}`,
      type: 'field',
      x: 72,
      y: 260,
      width: 300,
      height: 64,
      fieldLabel: 'Full name',
      fieldName: 'full_name',
      required: true
    },
    {
      id: `field-email-${now}`,
      type: 'field',
      x: 72,
      y: 348,
      width: 300,
      height: 64,
      fieldLabel: 'Email address',
      fieldName: 'email',
      required: true
    },
    {
      id: `checkbox-${now}`,
      type: 'checkbox',
      x: 72,
      y: 442,
      width: 320,
      height: 44,
      fieldLabel: 'I confirm the information is correct',
      fieldName: 'confirmation',
      required: false
    }
  ];
}

export function useTemplateLoader() {
  const { setCurrentTemplate, updatePageSettings } = useEditorStore();
  const navigate = useNavigate();
  const { i18n } = useTranslation();

  const loadTemplate = (def: TemplateDefinition) => {
    const multiPages = getTemplatePagesLocalized(def.id, i18n.language);
    const pages = multiPages
      ?? (() => {
           const specificElements = getTemplateElementsLocalized(def.id, i18n.language);
           const elements = specificElements.length > 0 ? specificElements : createStarterElements(def);
           return [{ id: 'page-1', elements }];
         })();
    setCurrentTemplate({
      ...def,
      pages,
      sharedElements: [],
      data: {}
    });
    // For presentation/widescreen templates, apply pixel units and hide margin guides
    if (def.format === 'widescreen' && def.pageWidth && def.pageHeight) {
      updatePageSettings({
        width: def.pageWidth,
        height: def.pageHeight,
        orientation: 'landscape',
        unit: 'px',
        showMarginGuide: false,
      });
    } else if (def.pageWidth && def.pageHeight) {
      updatePageSettings({ width: def.pageWidth, height: def.pageHeight });
    }
    recordDocOpened(def.name);
    navigate('/pdf/create');
  };

  const loadBlank = (mode: 'editor' | 'code' = 'editor') => {
    setCurrentTemplate({
      id: `blank-${Date.now()}`,
      name: 'Untitled document',
      category: 'blank',
      description: '',
      pages: [{ id: 'page-1', elements: [] }],
      sharedElements: [],
      data: {}
    });
    recordDocOpened('Blank document');
    navigate(mode === 'code' ? '/pdf/create?mode=code' : '/pdf/create');
  };

  const loadFromFile = async (
    file:         File,
    formatId?:    string,
    pageWidthPt?: number,
    pageHeightPt?: number,
    options: {
      includeImageAnalysisDiagnostics?: boolean;
      includeImageAnalysisDebugOverlay?: boolean;
      includeImageAnalysisFallbackLayer?: boolean;
      imageOcrLanguages?: string;
      includeImageOcrBackgroundImage?: boolean;
      includeImageOcrDiagnostics?: boolean;
      includeImageOcrDebugOverlay?: boolean;
      enableImageOcrPreprocessing?: boolean;
      imageOcrLowConfidenceThreshold?: number;
      imageOcrLayoutMode?: string;
    } = {},
  ): Promise<void> => {
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    const imageExts = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'tiff', 'tif'];
    let design: any;
    let imageAnalysisMeta: any = null;
    let imageOcrMeta: any = null;
    if (formatId === 'image-analysis') {
      const result: any = await ExportService.importImageAnalysis(
        file,
        pageWidthPt,
        pageHeightPt,
        {
          includeDiagnostics: options.includeImageAnalysisDiagnostics,
          includeDebugOverlay: options.includeImageAnalysisDebugOverlay,
          includeFallbackImageLayer: options.includeImageAnalysisFallbackLayer,
        },
      );
      if (result?.design) {
        design = result.design;
        imageAnalysisMeta = {
          diagnostics: result.diagnostics,
          debugOverlay: result.debugOverlay,
        };
      } else {
        design = result;
      }
    }
    else if (formatId === 'image-ocr') {
      const result: any = await ExportService.importImageOcr(
        file,
        pageWidthPt,
        pageHeightPt,
        {
          languages: options.imageOcrLanguages,
          includeBackgroundImage: options.includeImageOcrBackgroundImage,
          includeDiagnostics: options.includeImageOcrDiagnostics,
          includeDebugOverlay: options.includeImageOcrDebugOverlay,
          enablePreprocessing: options.enableImageOcrPreprocessing,
          lowConfidenceThreshold: options.imageOcrLowConfidenceThreshold,
          layoutMode: options.imageOcrLayoutMode,
        },
      );
      if (result?.design) {
        design = result.design;
        imageOcrMeta = {
          diagnostics: result.diagnostics,
          warnings: result.warnings,
          ocrPages: result.ocrPages,
          debugOverlay: result.debugOverlay,
          importedAt: new Date().toISOString(),
        };
      } else {
        design = result;
      }
    }
    else if (ext === 'pdf')             design = await ExportService.importPdf(file);
    else if (ext === 'doc')             design = await ExportService.importDoc(file);
    else if (ext === 'docx')            design = await ExportService.importDocx(file);
    else if (ext === 'odt')             design = await ExportService.importOdt(file);
    else if (ext === 'svg')             design = await ExportService.importSvg(file);
    else if (ext === 'pptx')            design = await ExportService.importPptx(file);
    else if (imageExts.includes(ext))   design = await ExportService.importImage(file);
    else throw new Error(`Unsupported file type: .${ext}`);

    const pages = design.pages ?? [{ id: 'page-1', elements: [] }];
    const pagesWithDebugOverlay = imageAnalysisMeta?.debugOverlay && design.pageSettings?.width && design.pageSettings?.height
      ? [
          ...pages,
          {
            id: 'image-analysis-debug-overlay',
            elements: [
              {
                id: `image-analysis-debug-overlay-${Date.now()}`,
                type: 'image',
                x: 0,
                y: 0,
                width: design.pageSettings.width,
                height: design.pageSettings.height,
                content: imageAnalysisMeta.debugOverlay,
                fitMode: 'fill',
                locked: true,
                style: { imageAnalysisType: 'debug-overlay' },
              },
            ],
          },
        ]
      : pages;
    const pagesWithOcrDebugOverlay = imageOcrMeta?.debugOverlay && design.pageSettings?.width && design.pageSettings?.height
      ? [
          ...pagesWithDebugOverlay,
          {
            id: 'image-ocr-debug-overlay',
            elements: [
              {
                id: `image-ocr-debug-overlay-${Date.now()}`,
                type: 'image',
                x: 0,
                y: 0,
                width: design.pageSettings.width,
                height: design.pageSettings.height,
                content: imageOcrMeta.debugOverlay,
                fitMode: 'fill',
                locked: true,
                style: { imageOcrType: 'debug-overlay' },
              },
            ],
          },
        ]
      : pagesWithDebugOverlay;

    setCurrentTemplate({
      id: design.id ?? `import-${Date.now()}`,
      name: design.name ?? file.name.replace(/\.[^.]+$/, ''),
      category: 'imported',
      description: `Imported from ${ext.toUpperCase()}`,
      pages: pagesWithOcrDebugOverlay,
      sharedElements: design.sharedElements ?? [],
      data: {
        ...(imageAnalysisMeta ? { imageAnalysis: imageAnalysisMeta } : {}),
        ...(imageOcrMeta ? { imageOcr: imageOcrMeta } : {}),
      },
    });
    // Apply page dimensions returned by the backend so the canvas matches the import
    if (design.pageSettings?.width && design.pageSettings?.height) {
      updatePageSettings({
        width:       design.pageSettings.width,
        height:      design.pageSettings.height,
        orientation: design.pageSettings.orientation ?? 'portrait',
      });
    }
    navigate('/pdf/create');
  };

  return { loadTemplate, loadBlank, loadFromFile };
}
