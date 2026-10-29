import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { pvadBrand } from './brand.js';
import { readPvadEnvironment } from '../../tools/configuration/environment.mjs';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

function readCommit() {
  const configured = readPvadEnvironment('PVAD_BUILD_COMMIT');
  if (configured !== undefined) return configured;

  try {
    return execFileSync('git', ['rev-parse', '--short=12', 'HEAD'], {
      cwd: repositoryRoot,
      encoding: 'utf8',
    }).trim();
  } catch {
    return 'unknown';
  }
}

export function readPvadBuildInfo() {
  return {
    product: pvadBrand.technicalName,
    productVersion: readFileSync(resolve(repositoryRoot, 'VERSION'), 'utf8').trim(),
    commitId: readCommit(),
    buildTime: readPvadEnvironment('PVAD_BUILD_TIME') ?? new Date().toISOString(),
  };
}

export function pvadVersionDefines(buildInfo = readPvadBuildInfo()) {
  return {
    __PVAD_VERSION__: JSON.stringify(buildInfo.productVersion),
    __PVAD_BUILD_COMMIT__: JSON.stringify(buildInfo.commitId),
    __PVAD_BUILD_TIME__: JSON.stringify(buildInfo.buildTime),
  };
}

export function pvadBuildInfoPlugin(buildInfo = readPvadBuildInfo()) {
  return {
    name: 'pvad-build-info',
    apply: 'build',
    generateBundle() {
      this.emitFile({
        type: 'asset',
        fileName: 'pvad-build-info.json',
        source: `${JSON.stringify(buildInfo, null, 2)}\n`,
      });
    },
  };
}

const staticStatusDefinitions = {
  '404.html': {
    code: '404',
    title: 'Page not found',
    message: 'The address may be incorrect, or the page may have moved.',
    action: 'Go home',
  },
  '500.html': {
    code: '500',
    title: 'Something went wrong',
    message: `${pvadBrand.displayName} could not load this application. Your stored work is not affected.`,
    action: 'Try again',
  },
  'maintenance.html': {
    code: '503',
    title: 'Planned maintenance',
    message: `${pvadBrand.displayName} is undergoing planned maintenance. Your stored documents are not affected.`,
    action: 'Check again',
  },
};

function escapeStaticText(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

export function renderStaticStatusPage(fileName, productName = pvadBrand.displayName) {
  const status = staticStatusDefinitions[fileName];
  if (!status) throw new Error(`Unknown static PVAD status page: ${fileName}`);
  const product = escapeStaticText(productName);
  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="robots" content="noindex">
  <title>${escapeStaticText(status.title)} | ${product}</title>
  <style>
    :root{color-scheme:light;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;color:#243449;background:#f7f9fc}
    *{box-sizing:border-box}body{margin:0}main{display:grid;width:min(100% - 32px,960px);min-height:100vh;margin:auto;grid-template-rows:auto 1fr auto}
    header,footer{padding:24px 0}header{display:flex;gap:12px;align-items:center;color:#132033;font-weight:700}header b{display:grid;width:42px;height:42px;place-items:center;color:#fff;background:#184e8a;border-radius:6px;font-size:14px}
    section{align-self:center;max-width:680px;padding:48px 0}.code{margin:0 0 16px;color:#184e8a;font-size:14px;font-weight:800}h1{margin:0;color:#132033;font-size:clamp(2rem,6vw,3.5rem);line-height:1.08}p{max-width:620px;margin:20px 0 0;color:#657386;font-size:18px;line-height:1.55}
    a{display:inline-flex;min-height:44px;align-items:center;margin-top:32px;padding:8px 16px;color:#fff;background:#2468b2;border-radius:6px;font-weight:700;text-decoration:none}a:focus-visible{outline:3px solid #2f80d7;outline-offset:3px}footer{color:#657386;border-top:1px solid #d8e0ea;font-size:14px}
  </style>
</head>
<body>
  <main>
    <header><b aria-hidden="true">${pvadBrand.displayName}</b><span>${product}</span></header>
    <section aria-labelledby="status-title"><p class="code">${status.code}</p><h1 id="status-title" tabindex="-1">${status.title}</h1><p>${status.message}</p><a href="/">${status.action}</a></section>
    <footer>${pvadBrand.displayName}</footer>
  </main>
</body>
</html>\n`;
}

export function pvadStaticStatusPagesPlugin(productName) {
  return {
    name: 'pvad-static-status-pages',
    apply: 'build',
    generateBundle() {
      Object.keys(staticStatusDefinitions).forEach((fileName) => {
        this.emitFile({
          type: 'asset',
          fileName,
          source: renderStaticStatusPage(fileName, productName),
        });
      });
    },
  };
}
