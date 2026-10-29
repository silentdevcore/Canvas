import assert from 'node:assert/strict';
import test from 'node:test';
import { publicGuidesPlugin, publicGuidePaths } from '../publicGuides.js';

test('public guide downloads are packaged from an explicit non-operator allowlist', () => {
  const emitted = [];
  publicGuidesPlugin().generateBundle.call({ emitFile: value => emitted.push(value) });
  for (const path of publicGuidePaths) {
    const asset = emitted.find(value => value.fileName === path);
    assert.ok(asset, path);
    assert.ok(asset.source.toString().startsWith('#'), path);
  }
  for (const asset of emitted) {
    assert.ok(asset.fileName.startsWith('docs/'));
    assert.doesNotMatch(asset.fileName, /operator|checklists|legal-evidence|\.\.\//i);
  }
});

test('release-linked chart and code workspace guides are included in the public build', () => {
  const emitted = [];
  publicGuidesPlugin().generateBundle.call({ emitFile: value => emitted.push(value) });
  for (const path of ['docs/charts.md', 'docs/code-designer-workspace.md']) {
    assert.ok(publicGuidePaths.includes(path));
    assert.ok(emitted.some(asset => asset.fileName === path));
  }
});
