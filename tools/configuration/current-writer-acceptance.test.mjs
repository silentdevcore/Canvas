import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';

const root = resolve(import.meta.dirname, '../..');
const read = path => readFileSync(resolve(root, path), 'utf8');

// These writer contracts do not consult the residual exception ledger.
const boundaries = [
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

test('current-output guards reject reverted writers independently of any file/hash exception', () => {
  for (const boundary of boundaries) {
    const source = read(boundary.path);
    assert.ok(source.includes(boundary.from), boundary.path);
    const reverted = source.replace(boundary.from, boundary.to);
    assert.throws(() => boundary.verify(reverted), undefined, boundary.name);
  }
});
