import assert from 'node:assert/strict';
import test from 'node:test';
import { renderApp, statusClass } from '../src/renderers.js';
import { demos } from '../src/demoData.js';

test('Demo renders the current brand and configured navigation for every selected example', () => {
  const oldWindow = globalThis.window;
  try {
    for (const demo of demos) {
      globalThis.window = { location: { hash: `#demo/${demo.id}` } };
      const root = { innerHTML: '' };
      renderApp(root);
      assert.match(root.innerHTML, /aria-label="PVADE Demo home"/);
      assert.match(root.innerHTML, />Live demo<\/a>/);
      assert.match(root.innerHTML, />Contact sales<\/a>/);
      assert.ok(root.innerHTML.includes(demo.title));
      assert.doesNotMatch(root.innerHTML, /href="undefined/);
    }
  } finally {
    if (oldWindow === undefined) delete globalThis.window;
    else globalThis.window = oldWindow;
  }
});

test('unfinished examples retain explicit maturity statuses', () => {
  assert.equal(statusClass('Ready'), 'pvad-status--ready');
  assert.equal(statusClass('Preview'), 'pvad-status--preview');
  assert.equal(statusClass('Planned'), 'pvad-status--planned');
});
