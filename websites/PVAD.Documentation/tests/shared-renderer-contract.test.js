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
