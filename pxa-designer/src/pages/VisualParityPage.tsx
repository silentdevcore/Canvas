import { useEffect, useState } from 'react';
import LivePreview from '@/components/Preview/LivePreview';
import SimplePxaSurface from '@/components/Editor/SimplePxaSurface';
import { normalizePageSettings, useEditorStore } from '@/store';
import type { Page, PageSettings, SimpleElement, Template } from '@/types';
import { DesignerAuthContextProvider } from '@/auth/DesignerAuthGate';
import { designerPermissions } from '@/auth/designerPermissions';
import allElementsFixture from '../../../test-assets/visual-parity/all-elements.json';
import mixedLayoutFixture from '../../../test-assets/page-layout/mixed-layout.json';
import eInvoiceFixture from '../../../test-assets/visual-parity/e-invoice.json';
import eInvoiceMultiPageFixture from '../../../test-assets/visual-parity/e-invoice-multipage.json';
import eInvoiceLongDescriptionFixture from '../../../test-assets/visual-parity/e-invoice-long-description.json';
import eInvoiceMultiTaxFixture from '../../../test-assets/visual-parity/e-invoice-multi-tax.json';

type Fixture = {
  id: string;
  name: string;
  category: string;
  description: string;
  pages: Page[];
  sharedElements: SimpleElement[];
  pageSettings: Partial<PageSettings>;
};

const noop = () => undefined;
const parityUser = {
  id: 'visual-parity-user',
  username: 'visual-parity',
  email: 'visual-parity@localhost.invalid',
  displayName: 'Visual parity',
  roles: ['Designer'],
  permissions: Object.values(designerPermissions),
  organizations: [],
  activeOrganizationId: null,
  lastLoginAt: null,
};

export default function VisualParityPage() {
  const [ready, setReady] = useState(false);
  const search = new URLSearchParams(location.search);
  const surface = search.get('surface') ?? 'preview';
  // Keyed by fixture filename, matching the key tools/visual-parity/run.mjs derives.
  const fixtures: Record<string, unknown> = {
    'all-elements': allElementsFixture,
    'mixed-layout': mixedLayoutFixture,
    'e-invoice': eInvoiceFixture,
    'e-invoice-multipage': eInvoiceMultiPageFixture,
    'e-invoice-long-description': eInvoiceLongDescriptionFixture,
    'e-invoice-multi-tax': eInvoiceMultiTaxFixture,
  };
  const selectedFixture = fixtures[search.get('fixture') ?? ''] ?? allElementsFixture;
  const design = selectedFixture as unknown as Fixture;
  const template: Template = {
    id: design.id,
    name: design.name,
    category: design.category,
    description: design.description,
  };

  useEffect(() => {
    useEditorStore.setState({
      pageSettings: normalizePageSettings({
        ...design.pageSettings,
        gridVisible: false,
        showMarginGuide: false,
        showSafeArea: false,
      }),
      currentPreviewLanguage: 'en',
      selectedElementId: null,
    });
    document.documentElement.dataset.visualParity = 'true';
    setReady(true);
    return () => { delete document.documentElement.dataset.visualParity; };
  }, [design.pageSettings]);

  if (!ready) return null;

  if (surface === 'canvas') {
    return (
      <DesignerAuthContextProvider user={parityUser}>
      <SimplePxaSurface
        template={template}
        elements={design.pages[0]?.elements ?? []}
        pages={design.pages}
        currentPageIndex={0}
        sharedElements={design.sharedElements ?? []}
        onElementAdd={noop}
        onElementUpdate={noop}
        onElementDelete={noop}
        onElementReorder={noop}
        onPreview={noop}
        onBack={noop}
        onPageAdd={noop}
        onPageDelete={noop}
        onPageDuplicate={noop}
        onPageSelect={noop}
        onPageMove={noop}
        onSharedElementAdd={noop}
        onSharedElementUpdate={noop}
        onSharedElementDelete={noop}
        onTemplateRename={noop}
        visualParityMode
      />
      </DesignerAuthContextProvider>
    );
  }

  return (
    // LivePreview calls useDesignerAuth for its export gating, so the preview surface needs the
    // same auth context the canvas surface already had. Without it the preview never renders and
    // the parity run hangs waiting for a surface that will never appear.
    <DesignerAuthContextProvider user={parityUser}>
      <LivePreview
        template={template}
        pages={design.pages}
        sharedElements={design.sharedElements ?? []}
        pageSettings={normalizePageSettings(design.pageSettings)}
        onBack={noop}
        onExport={noop}
        visualParityMode
      />
    </DesignerAuthContextProvider>
  );
}
