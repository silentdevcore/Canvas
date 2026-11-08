import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';

const root = resolve(import.meta.dirname, '../..');
const read = path => readFileSync(resolve(root, path), 'utf8');

// These writer contracts do not consult the residual exception ledger.
const boundaries = [
  ...[
    ['pades', 'PVADE PAdES Interoperability Corpus'],
    ['pades/negative', 'PVADE PAdES Negative Trust Corpus'],
  ].map(([directory, title]) => ({
    name: `${directory} public catalog uses current presentation over original evidence`,
    path: `websites/PVAD.Documentation/public/downloads/pdf-trust/${directory}/index.html`,
    from: `<h1>${title}</h1>`, to: `<h1>${title.replace('PVADE', 'PXA')}</h1>`,
    verify(source) {
      assert.ok(source.includes(`<title>${title}</title>`));
      assert.ok(source.includes(`<h1>${title}</h1>`));
      assert.doesNotMatch(source.replace(/<[^>]+>/g, ''), /pxa|power\s*dox/i);
      assert.ok(source.includes('Downloads retain their original filenames and recorded producer identities'));
    },
  })),
  {
    name: 'Account authentication errors project display text without changing raw code branches',
    path: 'websites/PVAD.Account/src/main.ts',
    from: 'escapeHtml(displaySystemMessage(apiError.message))', to: 'escapeHtml(apiError.message)',
    verify(source) {
      const handler = source.split('function bindForm(')[1]?.split('\nfunction bindEvents(')[0];
      assert.ok(handler);
      assert.equal([...handler.matchAll(/displaySystemMessage\(apiError\.message\)/g)].length, 4);
      assert.ok(handler.includes('apiError.code === PROBLEM_CODE_VERIFICATION_REQUIRED'));
      assert.ok(handler.includes('apiError.code === PROBLEM_CODE_ACCOUNT_DISABLED'));
      assert.ok(handler.includes('apiError.code === PROBLEM_CODE_ORGANIZATION_SUSPENDED'));
    },
  },
  {
    name: 'Admin login and action errors project text without applying aliases to identifiers',
    path: 'websites/PVAD.Admin/src/main.js',
    from: 'error.textContent = displaySystemMessage(requestError.message)', to: 'error.textContent = requestError.message',
    verify(source) {
      assert.equal([...source.matchAll(/error\.textContent = displaySystemMessage\(requestError\.message\)/g)].length, 2);
      assert.doesNotMatch(source, /escapeHtml\([^()\n]*\.error\b/);
      assert.ok(source.includes('escapeHtml(license.keyId)'));
      assert.ok(source.includes('escapeHtml(license.licenseNumber)'));
      assert.ok(source.includes('escapeHtml(event.action)'));
    },
  },
  {
    name: 'Admin parses structured JSON error media types without losing raw diagnostic data',
    path: 'websites/PVAD.Admin/src/api.js',
    from: "contentType.includes('+json')", to: 'false',
    verify(source) {
      assert.ok(source.includes("(response.headers.get('content-type') || '').toLowerCase()"));
      assert.ok(source.includes("contentType.includes('application/json') || contentType.includes('+json')"));
      assert.ok(source.includes('error.code = body?.code'));
      assert.ok(source.includes('error.body = body'));
    },
  },
  {
    name: 'project failure text uses display aliases without changing source names',
    path: 'pvad-designer/src/components/Project/ProjectImportNotice.tsx',
    from: 'displaySystemMessage(state.message)', to: 'state.message',
    verify(source) {
      assert.ok(source.includes('<p>{displaySystemMessage(state.message)}</p>'));
      assert.ok(source.includes('displaySystemMessage(diagnostic.code ==='));
      assert.ok(source.includes('` (${diagnostic.source})`'));
    },
  },
  {
    name: 'workspace failure banners project diagnostics only at the render boundary',
    path: 'pvad-designer/src/components/CodeEditor/LiveCodeEditor.tsx',
    from: 'role="alert">{displaySystemMessage(error)}', to: 'role="alert">{error}',
    verify(source) {
      assert.ok(source.includes('role="alert">{displaySystemMessage(error)}'));
    },
  },
  {
    name: 'CSharp error previews project messages without rewriting source or JSON validation',
    path: 'pvad-designer/src/components/CodeEditor/CodePreviewPane.tsx',
    from: '<li key={i}>{displaySystemMessage(e)}</li>', to: '<li key={i}>{e}</li>',
    verify(source) {
      assert.ok(source.includes("convertError.split('\\n').map((e, i) => <li key={i}>{displaySystemMessage(e)}</li>)"));
      assert.ok(source.includes('validation.errors.map((e, i) => <li key={i}>{e}</li>)'));
    },
  },
  {
    name: 'PDF signing and blocked-mutation states share the current diagnostic presentation',
    path: 'pvad-designer/src/features/pdf-viewer/PdfViewer.tsx',
    from: 'role="status">{displaySystemMessage(signingStatus)}', to: 'role="status">{signingStatus}',
    verify(source) {
      assert.ok(source.includes('role="status">{displaySystemMessage(signingStatus)}'));
      assert.ok(source.includes('${displaySystemMessage(trustError.message)}'));
    },
  },
  {
    name: 'all five internal step-up policy names are canonical',
    path: 'PVAD.WebApi/Security/PvadStepUpAuthorization.cs',
    from: '"pvad.security.recent-authentication"', to: '"pxa.security.recent-authentication"',
    verify(source) {
      const block = source.split('public static class PvadSecurityPolicies')[1]?.split('public sealed class')[0];
      assert.ok(block);
      const names = [...block.matchAll(/public const string \w+ = "([^"]+)";/g)].map(match => match[1]);
      assert.equal(names.length, 5);
      assert.ok(names.every(name => name.startsWith('pvad.')));
    },
  },
  {
    name: 'migration access uses the canonical route after input normalization',
    path: 'PVAD.WebApi/Infrastructure/PvadDesignerAccessMiddleware.cs',
    from: 'segments[1], "pvad"', to: 'segments[1], "pxa"',
    verify(source) {
      assert.match(source, /string\.Equals\(segments\[1\], "pvad", StringComparison.OrdinalIgnoreCase\)/);
      assert.doesNotMatch(source, /string\.Equals\(segments\[1\], "pxa"/);
    },
  },
  {
    name: 'MCP runtime user, group and copied-file ownership agree',
    path: 'tools/PVAD.Mcp/Dockerfile', from: 'USER pvad', to: 'USER pxa',
    verify(source) {
      assert.match(source, /addgroup -S pvad && adduser -S -G pvad/);
      assert.match(source, /--chown=pvad:pvad/);
      assert.match(source, /^USER pvad$/m);
    },
  },
  {
    name: 'chart metadata writes the current key and retains an explicit old reader key',
    path: 'src/Core/PVAD.Core/Primitives/ChartMetadataCodec.cs',
    from: 'PdfInfoKey = "PVADChartsV2"', to: 'PdfInfoKey = "PXAChartsV2"',
    verify(source) {
      assert.match(source, /public const string PdfInfoKey = "PVADChartsV2"/);
      assert.match(source, /public const string LegacyPdfInfoKey = "PXAChartsV2"/);
      const mapper = read('PVAD.WebApi/Infrastructure/DesignJsonMapper.cs');
      assert.match(mapper, /CustomProperties\[PVAD\.Core\.Primitives\.ChartMetadataCodec\.PdfInfoKey\]/);
      assert.doesNotMatch(mapper, /CustomProperties\[[^\]]*LegacyPdfInfoKey\]/);
    },
  },
  {
    name: 'redacted images remove the old marker and emit the canonical marker',
    path: 'src/Importing/PVAD.Importer/Redaction/PdfImageRedactionEngine.cs',
    from: 'RedactedMarker = "PVADRedacted"', to: 'RedactedMarker = "PXARedacted"',
    verify(source) {
      assert.match(source, /const string RedactedMarker = "PVADRedacted"/);
      assert.match(source, /dictionary\[LegacyRedactedMarker\] = null/);
      assert.match(source, /dictionary\[RedactedMarker\] = new PdfBoolean\(true\)/);
      assert.match(source, /dictionary\[RedactedMarker\] \?\? dictionary\[LegacyRedactedMarker\]/);
    },
  },
  {
    name: 'parser report writers never emit the previous parser ID',
    path: 'tools/pdf-parser-fuzzer/Program.cs',
    from: 'Parsers: new[] { "pvad" }', to: 'Parsers: new[] { "pxa" }',
    verify(source) {
      assert.match(source, /Parsers: new\[\] \{ "pvad" \}/);
      const outcomes = [...source.matchAll(/new ParserOutcome\(\s*"([^"]+)"/g)].map(match => match[1]);
      assert.equal(outcomes.length, 4);
      assert.ok(outcomes.every(parser => parser === 'pvad'));
    },
  },
  {
    name: 'webhook generations share the same computed values without changing signatures',
    path: 'src/Observability/PVAD.Observability.WebhookRelay/Program.cs',
    from: '"X-PVAD-Webhook-Timestamp"', to: '"X-PXA-Webhook-Timestamp"',
    verify(source) {
      assert.equal([...source.matchAll(/WebhookSignature.Create\(/g)].length, 1);
      for (const [suffix, variable] of [['Timestamp', 'timestampValue'], ['Signature', 'signature'], ['Idempotency-Key', 'idempotencyKey']])
        for (const identity of ['PVAD', 'PXA'])
          assert.ok(source.includes(`outbound.Headers.Add("X-${identity}-Webhook-${suffix}", ${variable});`));
    },
  },
  {
    name: 'SMTP uses the same outbox correlation for both header generations',
    path: 'PVAD.WebApi/Services/Mail/SmtpMailTransport.cs',
    from: '"X-PVAD-Outbox-Id"', to: '"X-PXA-Outbox-Id"',
    verify(source) {
      for (const identity of ['PVAD', 'PXA'])
        assert.ok(source.includes(`mail.Headers.Add("X-${identity}-Outbox-Id", message.OutboxId.ToString());`));
    },
  },
];

for (const boundary of boundaries)
  test(boundary.name, () => boundary.verify(read(boundary.path)));

test('Account page errors and license validation notices use display projections only at output', () => {
  for (const name of ['closure', 'developerAccess', 'licenses', 'organization', 'profile', 'security', 'subscription', 'usage']) {
    const source = read(`websites/PVAD.Account/src/pages/${name}.ts`);
    assert.ok(source.includes('escapeHtml(displaySystemMessage(state.error))'), name);
    assert.doesNotMatch(source, /escapeHtml\(state\.error\)/, name);
    assert.doesNotMatch(source, /error\.textContent = \(submitError as ApiError\)\.message/, name);
  }
  const licenses = read('websites/PVAD.Account/src/pages/licenses.ts');
  assert.ok(licenses.includes('escapeHtml(displaySystemMessage(validity))'));
  assert.ok(licenses.includes('escapeHtml(license.licenseNumber)'));
  assert.ok(read('websites/PVAD.Account/src/pages/legalReview.ts').includes('displaySystemMessage(apiError.message)'));
});

test('current-output guards reject reverted writers independently of any file/hash exception', () => {
  for (const boundary of boundaries) {
    const source = read(boundary.path);
    assert.ok(source.includes(boundary.from), boundary.path);
    const reverted = source.replace(boundary.from, boundary.to);
    assert.throws(() => boundary.verify(reverted), undefined, boundary.name);
  }
});
