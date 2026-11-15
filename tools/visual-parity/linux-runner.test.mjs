import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const dockerfile = readFileSync(new URL('./linux/Dockerfile', import.meta.url), 'utf8');
const runner = readFileSync(new URL('./linux/run.sh', import.meta.url), 'utf8');

test('Linux parity uses the project browser, Node 22 and .NET 10 without a privileged runtime user', () => {
  assert.match(dockerfile, /FROM node:22-bookworm-slim AS node/);
  assert.match(dockerfile, /FROM mcr\.microsoft\.com\/dotnet\/sdk:10\.0-noble AS dotnet/);
  assert.match(dockerfile, /FROM mcr\.microsoft\.com\/playwright:v1\.64\.1-noble/);
  assert.match(dockerfile, /USER pwuser/);
  assert.match(dockerfile, /HOME=\/tmp\/pvad-linux-home/);
});

test('the runner extracts a commit snapshot into temporary container storage and retains evidence', () => {
  assert.match(runner, /mktemp -d \/tmp\/pvad-linux-parity/);
  assert.match(runner, /tar -xf \/input\/source\.tar -C "\$workspace"/);
  assert.match(runner, /PVAD_BUILD_COMMIT:\?/);
  assert.match(runner, /tee \/evidence\/run\.log/);
  assert.match(runner, /cp -R artifacts\/visual-parity "\/evidence\/\$pack"/);
  assert.match(runner, /browserVersion: browser\.version\(\)/);
  assert.doesNotMatch(runner, /UPDATE_BASELINE|docker\.sock|git push|git commit/);
});

test('both font and catalog packs run sequentially with enforcement even if the first pack fails', () => {
  assert.match(runner, /for pack in font-families all-elements; do/);
  assert.match(runner, /export PVAD_VISUAL_PARITY_ENFORCE=true/);
  assert.match(runner, /overall=1/);
  assert.match(runner, /exit "\$overall"/);
  assert.doesNotMatch(runner, /exit "\$result"/);
});
