import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { publicGuidePaths } from '../publicGuides.js';

const guide = readFileSync(new URL('../../../docs/guides/PVAD.Designer-Preview.md', import.meta.url), 'utf8');
const site = readFileSync(new URL('../src/main.js', import.meta.url), 'utf8');

test('shared rendering workflow is linked and packaged in public Documentation', () => {
  assert.ok(publicGuidePaths.includes('docs/guides/PVAD.Designer-Preview.md'));
  assert.ok(site.includes('href: \'../../docs/guides/PVAD.Designer-Preview.md\''));
  assert.match(guide, /Select the language tab/);
  assert.match(guide, /Export from Preview/);
});
test('preview guide distinguishes display content, immutable identities and unverified output variants', () => {
  for (const phrase of ['stable element IDs', 'form field names', 'local placeholder',
    'complete implementation of arbitrary .NET date-format', 'cropping/focal parity'])
    assert.ok(guide.includes(phrase), phrase);
  assert.match(guide, /do not rewrite the saved template/);
  assert.match(guide, /not the PDF file itself/);
});
test('image fit contract explains real export geometry and keeps unsupported variants explicit', () => {
  for (const phrase of ['`contain`', '`cover`', '`fill`', '`none`', 'one point per pixel',
    'PNG alpha transparency', 'preserveAspectRatio: true', 'EXIF autorotation', 'temporary image files'])
    assert.ok(guide.includes(phrase), phrase);
});
test('date documentation defines modes, defaults, portable formats and explicit limits', () => {
  for (const phrase of ['Dates, Timezones And Binding', 'yyyy-MM-dd', 'Gregorian calendar',
    'date-only', 'millisecond', 'seven fractional digits', '256 characters', 'not a live ticking clock',
    'unknown', 'directional runs', 'no source template'])
    assert.ok(guide.toLowerCase().replace(/\s+/g, ' ').includes(phrase.toLowerCase()), phrase);
});

test('date layout documentation explains paragraph geometry and visible overflow without resizing', () => {
  for (const phrase of ['Date Layout', 'asymmetric padding', 'line height', 'per-side borders',
    'empty date', 'overflow warning', 'physical page'])
    assert.ok(guide.toLowerCase().replace(/\s+/g, ' ').includes(phrase.toLowerCase()), phrase);
  assert.match(guide, /does not resize or move/);
  assert.match(guide, /unbroken date string is not split/);
});

test('packaged font documentation states supported families, consumers, distribution and limits', () => {
  for (const phrase of ['Serif And Monospace Fonts', 'Liberation', 'bold-italic', 'same local',
    'Rich Text', 'table-cell', 'licenses/Liberation-2.1.5.LICENSE.txt', 'SDK callers',
    'Form-widget', 'not certify arbitrary', 'fractional character widths', 'shared elements'])
    assert.ok(guide.includes(phrase), phrase);
});
