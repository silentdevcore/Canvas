import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
const { parse } = require('parse5');

export function inspectDocfxOutput(directory) {
  const root = resolve(directory);
  const files = [];
  function visit(path) {
    for (const entry of readdirSync(path, { withFileTypes: true })) {
      const target = resolve(path, entry.name);
      if (entry.isDirectory()) visit(target);
      else if (entry.isFile() && entry.name.endsWith('.html')) files.push(target);
    }
  }
  visit(root);
  assert.ok(files.length > 0, 'DocFX output must contain HTML.');
  const pages = new Map();
  for (const file of files) {
    const anchors = new Set(), links = [];
    function walk(node) {
      const attributes = Object.fromEntries((node.attrs ?? []).map(({ name, value }) => [name, value]));
      if (attributes.id) anchors.add(attributes.id);
      if (node.tagName === 'a' && attributes.name) anchors.add(attributes.name);
      for (const key of ['href', 'src']) if (attributes[key]) links.push(attributes[key]);
      for (const child of node.childNodes ?? []) walk(child);
    }
    walk(parse(readFileSync(file, 'utf8')));
    pages.set(file, { anchors, links });
  }
  const failures = [];
  let checkedLinks = 0;
  for (const [file, page] of pages) {
    const base = new URL(relative(root, file).split(sep).map(encodeURIComponent).join('/'), 'https://docfx-output.invalid/');
    for (const link of page.links) {
      let url;
      try { url = new URL(link, base); } catch {
        failures.push({ page: relative(root, file), link, reason: 'invalid-url' });
        continue;
      }
      if (url.origin !== base.origin) continue;
      checkedLinks++;
      let path;
      try { path = resolve(root, `.${decodeURIComponent(url.pathname)}`); } catch {
        failures.push({ page: relative(root, file), link, reason: 'invalid-encoding' });
        continue;
      }
      if (existsSync(path) && statSync(path).isDirectory()) path = resolve(path, 'index.html');
      if (!existsSync(path) || !statSync(path).isFile()) {
        failures.push({ page: relative(root, file), link, reason: 'missing-file' });
      } else if (url.hash && pages.has(path)) {
        let fragment;
        try { fragment = decodeURIComponent(url.hash.slice(1)); } catch { fragment = url.hash.slice(1); }
        if (!pages.get(path).anchors.has(fragment))
          failures.push({ page: relative(root, file), link, reason: 'missing-anchor' });
      }
    }
  }
  return { htmlFiles: files.length, checkedLinks, failures };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../docs/_site');
  const report = inspectDocfxOutput(root);
  console.log(JSON.stringify(report, null, 2));
  if (report.failures.length) process.exitCode = 1;
}
