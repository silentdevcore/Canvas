import './site.css';
import { initializeBrowserTelemetry } from '../../shared/browserTelemetry.js';
import { bindDemoInteractions } from './interactions.js';
import { renderApp } from './renderers.js';
import { initializeStorageNotice } from '../../shared/storageNotice.js';

initializeBrowserTelemetry({ application: 'demo' });

function mount() {
  renderApp(document.querySelector('#app'));
  bindDemoInteractions();

  if (window.location.hash.startsWith('#demo/')) {
    document.querySelector('#demo-detail')?.scrollIntoView({ block: 'start' });
  }
}

window.addEventListener('hashchange', mount);

mount();
initializeStorageNotice();
