import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { createRequire } from 'node:module';
import { extractExample, main } from './activation-guide-check.mjs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const guide = read('../../docs/guides/PVAD.Native-Library-Activation.md');

test('the actual guide contains one complete compiler-ready C# class, not a copied test-only implementation', () => {
  const example = extractExample(guide);
  for (const method of ['Activate(', 'ActivateFromStream(', 'CreatePdf()', 'TryLoadReplacement(', 'Inspect()']) assert.ok(example.includes(method));
  assert.ok(example.includes('PVAD.Generator.Pdf.CreateDocument()'));
  assert.ok(!example.includes('PRIVATE KEY'));
  assert.throws(() => extractExample('No example'), /exactly one/);
  assert.throws(() => extractExample(guide + '\n```csharp\npublic class Extra {}\n```'), /exactly one/);
  assert.equal(extractExample(guide + '\n```json\n{"text":"```csharp"}\n```'), example);
});

test('the lifecycle guide documents every native product and stable diagnostic without activating server roles', () => {
  const inventory = JSON.parse(read('../../product-metadata/native-library-products.v1.json'));
  for (const product of inventory.products) assert.ok(guide.includes('`' + product.id + '`'), product.id);
  for (let code = 0; code <= 15; code++) assert.ok(guide.includes('PVADLIC' + String(code).padStart(3, '0')));
  for (const text of ['seven days', '30 days', 'no grace', 'entire', 'not a transferable token', 'grants are not',
    'no file watcher', 'No public NuGet', 'source trust register is intentionally empty', 'explicitly issued native Trial'])
    assert.ok(guide.replace(/\s+/g, ' ').includes(text), text);
  assert.ok(read('../../checklists/PVAD.SDK-Roadmap.md').includes('not the embedded .NET engine'));
  assert.ok(read('../../checklists/PVAD.Subscription-Licensing.md').includes('do not implicitly grant local engine'));
});

test('public Documentation ships the exact guide and provides a Code path link, not operator material', async () => {
  const { publicGuidesPlugin, publicGuidePaths } = await import('../../websites/PVAD.Documentation/publicGuides.js');
  assert.ok(publicGuidePaths.includes('docs/guides/PVAD.Native-Library-Activation.md'));
  assert.ok(publicGuidePaths.every(path => !path.startsWith('operator-docs/')));
  const assets = [];
  publicGuidesPlugin().generateBundle.call({ emitFile: asset => assets.push(asset) });
  const file = assets.find(item => item.fileName === 'docs/guides/PVAD.Native-Library-Activation.md');
  assert.equal(file.source.toString(), guide);
  const source = read('../../websites/PVAD.Documentation/src/main.js');
  assert.ok(source.includes("label: 'Native library activation guide', href: '../../docs/guides/PVAD.Native-Library-Activation.md'"));
});

test('verification has no input overrides and retains only safe evidence, not signed fixtures or binaries', () => {
  assert.throws(() => main(['other-guide.md']), /No guide, output, trust/);
  const source = read('./activation-guide-check.mjs');
  for (const text of ['marked.lexer', 'exampleSha256', 'guideSha256', 'regularOutputs(projects), beforeBuild', 'rmSync(secret', 'rmSync(work', 'publicationPerformed: false'])
    assert.ok(source.includes(text));
  assert.ok(!source.includes('AcceptNPOIOSMFLicense'));
});

test('primary licensing scope stays fixed and new findings do not become additional implementation checkboxes', () => {
  const plan = read('../../checklists/PVAD.Native-Library-Licensing-And-Protection.md');
  const primary = plan.split('### Current Verification Evidence')[0];
  assert.equal([...primary.matchAll(/^- \[[ x]\]/gm)].length, 76);
  const deferred = plan.split('## Later Findings Outside Original Scope')[1]?.split('\n## ')[0];
  assert.ok(deferred?.includes('JSON naming audit'));
  assert.ok(!/^- \[[ x]\]/m.test(deferred));
});

test('Linux guide verification uses the existing PDF tools and uploads only report/cleanup JSON', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('../../.github/workflows/native-library-licensing.yml'));
  const job = workflow.jobs['generator-package-protection'];
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/activation-guide-check.mjs'));
  const artifact = job.steps.find(step => step.with?.name === 'native-activation-guide-evidence');
  assert.deepEqual(artifact.with.path.trim().split('\n').map(path => path.trim()), ['artifacts/native-activation-guide/*/report.json', 'artifacts/native-activation-guide/*/cleanup.json']);
});
