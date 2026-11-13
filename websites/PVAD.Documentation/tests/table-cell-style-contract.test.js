import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../src/main.js', import.meta.url), 'utf8');
const table = source.slice(source.indexOf("title: 'Table',"), source.indexOf("title: 'Table',") + 4200);

test('table guide documents the real sidebar and cell-selection workflow', () => {
  assert.match(table, /Tables and Charts/);
  assert.match(table, /row and column selectors/);
  assert.match(table, /Live Preview and export PDF/);
});
test('cell-style properties and explicit false are explained with an editable example', () => {
  for (const field of ['backgroundColor', 'fontFamily', 'fontSize', 'bold', 'italic', 'padding', 'borderTop', 'borderBottom'])
    assert.ok(table.includes(field), `Missing table field ${field}`);
  assert.match(table, /"bold": false/);
  assert.match(table, /zero-based/);
  assert.match(table, /relative column weights/);
});
test('shared-edge precedence and zero-width suppression are explicit', () => {
  assert.match(table, /zero width hides/);
  assert.match(table, /shared edges prefer explicit rules/);
  assert.match(table, /right\/bottom cell on a tie/);
});
test('native pagination guide is linked without promising automatic Designer page creation', () => {
  const guide = readFileSync(new URL('../../../docs/guides/PVAD.Styled-Table-Pagination.md', import.meta.url), 'utf8');
  assert.match(source, /href="\.\.\/\.\.\/docs\/guides\/PVAD\.Styled-Table-Pagination\.md"/);
  for (const token of ['AddSimpleTable', 'AutoRowHeight', 'RepeatFooterOnEachChunk', 'RowLineHeights', 'InvalidOperationException'])
    assert.ok(guide.includes(token), `Missing flow contract ${token}`);
  assert.match(guide, /does\s+not enable automatic page creation in the Designer/);
  assert.match(guide, /before painting any table chunk/);
});
