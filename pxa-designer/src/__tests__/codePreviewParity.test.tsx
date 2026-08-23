/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import CodePreviewPane, { type ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';

const mockLivePreview = jest.fn((_props: unknown) => <div data-testid="live-preview" />);

jest.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock('@/components/Preview/LivePreview', () => ({
  __esModule: true,
  default: (props: unknown) => mockLivePreview(props),
}));

describe('generated code preview parity', () => {
  it('preserves per-page layouts and every normalized document setting', () => {
    const design: ParsedDesign = {
      id: 'mixed-code-preview',
      name: 'Mixed code preview',
      pages: [
        { id: 'portrait', elements: [] },
        {
          id: 'landscape',
          elements: [],
          layout: {
            width: 842,
            height: 595,
            orientation: 'landscape',
            backgroundColor: '#eef6ff',
            backgroundImageFit: 'cover',
            margins: { top: 12, right: 18, bottom: 24, left: 30 },
            bleedSize: 6,
          },
        },
      ],
      pageSettings: {
        width: 595,
        height: 842,
        backgroundImage: 'data:image/png;base64,AA==',
        backgroundImageFit: 'tile',
        bleedSize: 9,
        trackChanges: true,
        activeLanguages: ['en', 'ar'],
        metadata: { title: 'Generated', author: 'PXA', subject: 'Parity', keywords: 'code' },
      },
    };
    const container = document.createElement('div');
    const root = createRoot(container);

    act(() => root.render(
      <CodePreviewPane
        raw={JSON.stringify(design)}
        language="json"
        validation={{ valid: true, errors: [] }}
        parsed={design}
        pdfBlobUrl={null}
        onExport={jest.fn()}
        isExporting={false}
        isConverting={false}
        convertError={null}
      />,
    ));

    const props = mockLivePreview.mock.calls.at(-1)![0] as {
      pages: ParsedDesign['pages'];
      pageSettings: NonNullable<ParsedDesign['pageSettings']>;
      autoFitWidth: boolean;
    };
    expect(props.pages[1].layout).toEqual(design.pages[1].layout);
    expect(props.pageSettings).toMatchObject({
      backgroundImage: 'data:image/png;base64,AA==',
      backgroundImageFit: 'tile',
      bleedSize: 9,
      trackChanges: true,
      activeLanguages: ['en', 'ar'],
      metadata: { title: 'Generated', author: 'PXA', subject: 'Parity', keywords: 'code' },
    });
    expect(props.autoFitWidth).toBe(true);

    act(() => root.unmount());
  });
});
