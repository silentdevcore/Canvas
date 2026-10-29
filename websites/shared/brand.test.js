import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import { pvadBrand } from './brand.js';
import { renderPvadFooter } from './footer.js';

test('owned website and Designer styles use canonical technical symbols', () => {
  const visit = directory => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const path = new URL(`${entry.name}${entry.isDirectory() ? '/' : ''}`, directory);
      if (entry.isDirectory()) visit(path);
      else if (entry.name.endsWith('.css'))
        assert.doesNotMatch(fs.readFileSync(path, 'utf8'), /(?:\.|--)pxa-/, path.pathname);
    }
  };
  visit(new URL('./styles/', import.meta.url));
  visit(new URL('../../pvad-designer/src/styles/', import.meta.url));
  for (const name of ['Company', 'Documentation', 'Demo', 'Account', 'Admin']) {
    const path = new URL(`../PVAD.${name}/src/site.css`, import.meta.url);
    assert.doesNotMatch(fs.readFileSync(path, 'utf8'), /(?:\.|--)pxa-/, path.pathname);
  }
});

test('technical identity and public product branding stay distinct', () => {
  assert.equal(pvadBrand.technicalName, 'PVAD');
  assert.equal(pvadBrand.displayName, 'PVADE');
  assert.equal(pvadBrand.products.pdf, 'PVADP');
  assert.equal(pvadBrand.products.spreadsheet, 'PVADS');
  const footer = renderPvadFooter('PVADE Documentation');
  assert.match(footer, /<strong>PVADE<\/strong>/);
  assert.match(footer, />PVADP<\/a>/);
  assert.match(footer, />PVADS<\/a>/);
  assert.doesNotMatch(footer, /Power Dox Automation|\bPXA\b/);
});

test('frontend package and lockfile identities use the technical prefix', () => {
  for (const relative of [
    'pvad-designer', 'tools/PVAD.Mcp', 'websites/PVAD.Company',
    'websites/PVAD.Documentation', 'websites/PVAD.Demo',
    'websites/PVAD.Admin', 'websites/PVAD.Account',
  ]) {
    const base = new URL(`../../${relative}/`, import.meta.url);
    const pkg = JSON.parse(fs.readFileSync(new URL('package.json', base), 'utf8'));
    assert.match(pkg.name, /^pvad-/);
    const lockPath = new URL('package-lock.json', base);
    if (!fs.existsSync(lockPath)) continue;
    const lock = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
    assert.equal(lock.name, pkg.name);
    assert.equal(lock.packages[''].name, pkg.name);
  }
});

test('all six Designer languages use the approved product navigation names', () => {
  for (const language of ['en', 'de', 'fr', 'es', 'it', 'ar']) {
    const locale = JSON.parse(fs.readFileSync(new URL(
      `../../pvad-designer/src/locales/${language}/common.json`, import.meta.url,
    ), 'utf8'));
    assert.equal(locale.nav.pdf, pvadBrand.products.pdf);
    assert.equal(locale.nav.spreadsheet, pvadBrand.products.spreadsheet);
  }
});

test('application entrypoints use current text branding without missing scaffold artwork', () => {
  for (const relative of [
    'pvad-designer', 'websites/PVAD.Company', 'websites/PVAD.Documentation',
    'websites/PVAD.Demo', 'websites/PVAD.Admin', 'websites/PVAD.Account',
  ]) {
    const html = fs.readFileSync(new URL(`../../${relative}/index.html`, import.meta.url), 'utf8');
    assert.match(html, /<title>PVADE(?:\s|<)/, relative);
    assert.doesNotMatch(html, /\/vite\.svg|Power Dox Automation|\bPXA\b/, relative);
    assert.match(html, /name="description"/, relative);
    if (/designer|Admin|Account/.test(relative))
      assert.match(html, /name="robots" content="noindex, nofollow"/, relative);
    for (const link of html.matchAll(/<link\b[^>]*href="([^"]+)"[^>]*>/g)) {
      if (!/rel="(?:icon|manifest|apple-touch-icon)"/.test(link[0])) continue;
      const asset = link[1];
      assert.ok(!asset.startsWith('http') && !asset.startsWith('//'), relative);
      assert.ok(fs.existsSync(new URL(`../../${relative}/public/${asset.replace(/^\//, '')}`,
        import.meta.url)), `${relative}: missing ${asset}`);
    }
  }
});

test('public brand links expose the market name to assistive technology', () => {
  for (const [file, label] of [
    ['websites/PVAD.Company/src/main.js', 'PVADE Company home'],
    ['websites/PVAD.Demo/src/renderers.js', 'PVADE Demo home'],
    ['websites/PVAD.Documentation/src/main.js', 'PVADE Documentation home'],
  ]) {
    const source = fs.readFileSync(new URL(`../../${file}`, import.meta.url), 'utf8');
    assert.ok(source.includes(`aria-label="${label}"`), file);
    assert.doesNotMatch(source, /aria-label="(?:PVAD\.|PXA)/, file);
  }
});

test('Company HTML metadata uses current branding on every page, not only the root', () => {
  function inspect(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      if (['node_modules', 'dist', 'public'].includes(entry.name)) continue;
      const path = new URL(entry.name, directory);
      if (entry.isDirectory()) inspect(new URL(`${entry.name}/`, directory));
      else if (entry.name.endsWith('.html')) {
        const source = fs.readFileSync(path, 'utf8');
        for (const metadata of source.matchAll(/<title>[\s\S]*?<\/title>|<meta\b[^>]*>/g))
          assert.doesNotMatch(metadata[0], /\bPXA\b|Power Dox Automation/, path.pathname);
      }
    }
  }
  inspect(new URL('../PVAD.Company/', import.meta.url));
});
