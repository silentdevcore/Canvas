import './site.css';
import { bindDemoInteractions } from './interactions.js';
import { renderApp } from './renderers.js';

function mount() {
  renderApp(document.querySelector('#app'));
  bindDemoInteractions();

  if (window.location.hash.startsWith('#demo/')) {
    document.querySelector('#demo-detail')?.scrollIntoView({ block: 'start' });
  }
}

window.addEventListener('hashchange', mount);

mount();
