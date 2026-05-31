import { useNavigate } from 'react-router-dom';
import { useEditorStore } from '@/store';
import { getTemplateElements, getTemplatePages } from '@/data/templateContent';
import type { TemplateDefinition } from '@/data/templates';
import type { SimpleElement } from '@/types';
import ExportService from '@/services/ExportService';

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

  const loadTemplate = (def: TemplateDefinition) => {
    const multiPages = getTemplatePages(def.id);
    const pages = multiPages
      ?? (() => {
           const specificElements = getTemplateElements(def.id);
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
    const prev = parseInt(localStorage.getItem('canvas_docs_opened') ?? '0', 10);
    localStorage.setItem('canvas_docs_opened', String(prev + 1));
    localStorage.setItem('canvas_last_template', def.name);
    navigate('/create');
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
    const prev = parseInt(localStorage.getItem('canvas_docs_opened') ?? '0', 10);
    localStorage.setItem('canvas_docs_opened', String(prev + 1));
    localStorage.setItem('canvas_last_template', 'Blank canvas');
    navigate(mode === 'code' ? '/create?mode=code' : '/create');
  };

  const loadFromFile = async (file: File): Promise<void> => {
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    const imageExts = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'tiff', 'tif'];
    let design: any;
    if (ext === 'pdf')                  design = await ExportService.importPdf(file);
    else if (ext === 'doc')             design = await ExportService.importDoc(file);
    else if (ext === 'docx')            design = await ExportService.importDocx(file);
    else if (ext === 'odt')             design = await ExportService.importOdt(file);
    else if (ext === 'svg')             design = await ExportService.importSvg(file);
    else if (ext === 'pptx')            design = await ExportService.importPptx(file);
    else if (imageExts.includes(ext))   design = await ExportService.importImage(file);
    else throw new Error(`Unsupported file type: .${ext}`);

    setCurrentTemplate({
      id: design.id ?? `import-${Date.now()}`,
      name: design.name ?? file.name.replace(/\.[^.]+$/, ''),
      category: 'imported',
      description: `Imported from ${ext.toUpperCase()}`,
      pages: design.pages ?? [{ id: 'page-1', elements: [] }],
      sharedElements: design.sharedElements ?? [],
      data: {},
    });
    navigate('/create');
  };

  return { loadTemplate, loadBlank, loadFromFile };
}
