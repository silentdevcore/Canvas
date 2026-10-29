import './site.css';
import { initializeBrowserTelemetry } from '../../shared/browserTelemetry.js';
import { bindDemoInteractions } from './interactions.js';
import { renderApp } from './renderers.js';
import { initializeStorageNotice } from '../../shared/storageNotice.js';
import { siteLinks } from '../../shared/siteLinks.js';
import { demos } from './demoData.js';
import { bindStatusPage, renderStatusPage } from '../../shared/statusPages.ts';

initializeBrowserTelemetry({ application: 'demo' });

function mount() {
  const requestedDemo = window.location.hash.match(/^#demo\/([^/]+)$/)?.[1];
  const routeMissing = window.location.pathname !== '/';
  const demoMissing = Boolean(requestedDemo && !demos.some((demo) => demo.id === requestedDemo));
  if (routeMissing || demoMissing) {
    document.title = 'Demo not found | PVADE Demo';
    document.querySelector('#app').innerHTML = renderStatusPage('notFound', {
      product: 'PVADE Demo',
      title: demoMissing ? 'Demo not found' : undefined,
      message: demoMissing ? 'This demo is unavailable or has moved.' : undefined,
      homeHref: siteLinks.demo,
      supportHref: siteLinks.demo,
      secondaryHref: siteLinks.documentation,
      secondaryLabel: 'Open documentation',
      allowedOrigins: new URL(siteLinks.documentation).origin,
    });
    bindStatusPage();
    return;
  }
  renderApp(document.querySelector('#app'));
  bindDemoInteractions();

  if (window.location.hash.startsWith('#demo/')) {
    document.querySelector('#demo-detail')?.scrollIntoView({ block: 'start' });
  }
}

window.addEventListener('hashchange', mount);

mount();
initializeStorageNotice();
