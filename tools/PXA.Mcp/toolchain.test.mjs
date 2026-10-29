import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const readJson = (path) => JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8'));
const manifest = readJson('./package.json');
const lock = readJson('./package-lock.json');
const nodeMajor = 22;
const compiler = readJson('./node_modules/typescript/package.json');
const compilerPath = fileURLToPath(new URL(`./node_modules/typescript/${compiler.bin.tsc}`, import.meta.url));

function runCompiler(args) {
  const result = spawnSync(process.execPath, [compilerPath, ...args], {
    encoding: 'utf8', timeout: 30_000, maxBuffer: 1024 * 1024,
  });
  assert.ifError(result.error);
  assert.equal(result.signal, null, result.stderr);
  assert.notEqual(result.status, null, result.stderr);
  return { status: result.status, output: result.stdout + result.stderr };
}

function checkSource(source) {
  const directory = mkdtempSync(join(tmpdir(), 'pxa-mcp-compiler-'));
  try {
    const input = join(directory, 'probe.ts');
    writeFileSync(input, source);
    writeFileSync(join(directory, 'tsconfig.json'), JSON.stringify({
      extends: fileURLToPath(new URL('./tsconfig.json', import.meta.url)),
      compilerOptions: {
        typeRoots: [fileURLToPath(new URL('./node_modules/@types/', import.meta.url))],
      },
      files: [input], include: [],
    }));
    return runCompiler(['--project', join(directory, 'tsconfig.json'), '--pretty', 'false']);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
}

test('Node declarations target the validated Node 22 runtime, not a newer API surface', () => {
  assert.ok(Number(process.versions.node.split('.')[0]) >= nodeMajor,
    'Run the MCP validation suite on Node 22 or newer.');
  assert.ok(manifest.devDependencies['@types/node'].startsWith(`^${nodeMajor}.`));
  assert.equal(Number(lock.packages['node_modules/@types/node'].version.split('.')[0]), nodeMajor);
});

test('declared, locked and installed MCP tooling agree', () => {
  assert.deepEqual(lock.packages[''].devDependencies, manifest.devDependencies);
  for (const name of ['@types/node', 'tsx', 'typescript']) {
    const installed = readJson(`./node_modules/${name}/package.json`);
    assert.equal(installed.version, lock.packages[`node_modules/${name}`].version, name);
  }
});

test('the reviewed transitive HTTP adapter loads on the retained SDK and Node baseline', async () => {
  const adapter = readJson('./node_modules/@hono/node-server/package.json');
  assert.equal(adapter.version, '2.1.5');
  assert.equal(adapter.version, lock.packages['node_modules/@hono/node-server'].version);
  assert.equal(adapter.engines.node, '>=20');
  assert.match(adapter.peerDependencies.hono, /\^4/);
  const hono = readJson('./node_modules/hono/package.json');
  assert.equal(Number(hono.version.split('.')[0]), 4);
  assert.equal(hono.version, lock.packages['node_modules/hono'].version);
  const sdk = readJson('./node_modules/@modelcontextprotocol/sdk/package.json');
  assert.equal(sdk.version, '1.32.2');
  assert.equal(sdk.version, lock.packages['node_modules/@modelcontextprotocol/sdk'].version);
  assert.equal(typeof (await import('@hono/node-server')).serve, 'function');
});

test('locked packages retain registry origins and integrity metadata', () => {
  for (const [name, entry] of Object.entries(lock.packages)) {
    if (!name || entry.link) continue;
    const resolved = new URL(entry.resolved);
    assert.equal(resolved.origin, 'https://registry.npmjs.org', name);
    assert.equal(resolved.username, '', name);
    assert.equal(resolved.password, '', name);
    assert.match(entry.integrity, /^(sha256|sha384|sha512)-/, name);
  }
});

test('native compiler packages and the running CLI match the locked compiler', () => {
  assert.equal(Number(compiler.version.split('.')[0]), 7);
  for (const [name, version] of Object.entries(compiler.optionalDependencies)) {
    const entry = lock.packages[`node_modules/${name}`];
    assert.ok(entry, name);
    assert.equal(version, compiler.version, name);
    assert.equal(entry.version, version, name);
    assert.equal(entry.optional, true, name);
    assert.ok(entry.os.length > 0 && entry.cpu.length > 0, name);
  }
  const nativeName = `@typescript/typescript-${process.platform}-${process.arch}`;
  assert.equal(readJson(`./node_modules/${nativeName}/package.json`).version, compiler.version);
  const result = runCompiler(['--version']);
  assert.equal(result.status, 0, result.output);
  assert.equal(result.output.trim(), `Version ${compiler.version}`);
});

test('the configured compiler accepts Node imports, narrowing and modern type syntax', () => {
  const result = checkSource(`
    import { Buffer } from 'node:buffer';
    type Result = { ok: true; value: string } | { ok: false; error: Error };
    const tools = ['validate_design', 'get_example'] as const satisfies readonly string[];
    function text(result: Result): string {
      return result.ok ? result.value : result.error.message;
    }
    const value: string = text({ ok: true, value: Buffer.from(tools[0]).toString('utf8') });
    await Promise.resolve(value);
  `);
  assert.equal(result.status, 0, result.output);
});

test('the configured compiler rejects incorrect types, nulls, implicit any and unknown Node exports', () => {
  const result = checkSource(`
    import { missingMcpExport } from 'node:fs';
    const count: number = 'not a number';
    const required: string = null;
    function unchecked(value) { return value; }
  `);
  assert.notEqual(result.status, 0, result.output);
  assert.match(result.output, /TS2305:.*missingMcpExport/);
  assert.match(result.output, /TS2322:.*string.*number/);
  assert.match(result.output, /TS2322:.*null.*string/);
  assert.match(result.output, /TS7006:.*value/);
});
