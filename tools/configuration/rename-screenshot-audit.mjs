import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const directories = ['websites/PVAD.Documentation/public/images/designer',
  'websites/PVAD.Documentation/public/images/pdf-trust', 'PVAD.WebApi/AdminDocumentation/images'];
const results = [];
for (const directory of directories) {
  for (const name of readdirSync(resolve(root, directory)).filter(name => name.endsWith('.png')).sort()) {
    const path = `${directory}/${name}`;
    const result = spawnSync('tesseract', [resolve(root, path), 'stdout', '-l', 'eng'], { encoding: 'utf8', timeout: 60_000 });
    if (result.error || result.status !== 0) throw new Error(`OCR failed: ${path}`);
    // Persist only brand flags and a byte fingerprint, never OCR text or identities.
    results.push({ path, sha256: createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex'),
      visibility: directory.startsWith('PVAD.WebApi/') ? 'protected-admin' : 'public',
      legacyBrandDetected: /\bPXA\b|Power\s?Dox\b/.test(result.stdout),
      canonicalBrandDetected: /\bPVADE\b|\bPVAD[PS]?\b/.test(result.stdout),
      pixelReview: 'pending', ocrIsNotApproval: true });
  }
}
mkdirSync(resolve(root, 'artifacts/rename'), { recursive: true });
writeFileSync(resolve(root, 'artifacts/rename/screenshot-ocr-audit.json'), `${JSON.stringify({ schemaVersion: 1, results }, null, 2)}\n`);
console.log(JSON.stringify({ screenshots: results.length, legacyBrandDetected: results.filter(item => item.legacyBrandDetected).length,
  output: 'artifacts/rename/screenshot-ocr-audit.json', approval: 'pending' }));
