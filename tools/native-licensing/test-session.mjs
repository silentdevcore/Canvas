import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { appendFileSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createTestLicenses } from './with-test-license.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex');
const trustFile = join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json');
const buildParent = join(root, 'artifacts/native-test-sessions');
const keys = ['PVAD_NATIVE_TEST_SESSION', 'PVAD_NATIVE_TEST_BUILD_ROOT', 'PVAD_NATIVE_LICENSE_PATH',
  'NativeLicensing__LicensePath', 'ArtifactsPath', 'UseArtifactsOutput', 'PvadLicenseTrustStorePath', 'PvadNativeLicenseTestBootstrap'];

export function createNativeTestSession() {
  const secret = mkdtempSync(join(tmpdir(), 'pvad-native-session-'));
  const id = randomUUID();
  const output = join(buildParent, id);
  try {
    mkdirSync(output, { recursive: true, mode: 0o700 });
    const files = createTestLicenses(secret);
    const descriptor = join(secret, 'session.json');
    const build = join(output, 'build');
    const session = { schemaVersion: 1, id, root, secret, output, build, files,
      sourceTrustSha256: hash(trustFile), artifactHashes: Object.fromEntries(Object.values(files).map(path => [path, hash(path)])) };
    writeFileSync(descriptor, JSON.stringify(session), { mode: 0o600 });
    return { descriptor, environment: sessionEnvironment(descriptor) };
  } catch (error) {
    rmSync(secret, { recursive: true, force: true });
    rmSync(output, { recursive: true, force: true });
    throw error;
  }
}

function readSession(descriptor, verifyArtifacts = true) {
  assert.ok(isAbsolute(descriptor) && basename(descriptor) === 'session.json', 'Invalid native test session path.');
  const secret = dirname(descriptor);
  assert.ok(/^pvad-native-session-[A-Za-z0-9]+$/.test(basename(secret)) &&
    realpathSync(dirname(secret)) === realpathSync(tmpdir()), 'Session storage must be an owned temporary directory.');
  assert.ok(lstatSync(secret).isDirectory() && !lstatSync(secret).isSymbolicLink() && lstatSync(descriptor).isFile() && !lstatSync(descriptor).isSymbolicLink());
  const session = JSON.parse(readFileSync(descriptor, 'utf8'));
  assert.equal(session.schemaVersion, 1);
  assert.match(session.id, /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/);
  assert.equal(session.root, root);
  assert.equal(session.secret, secret);
  assert.equal(session.output, join(buildParent, session.id));
  assert.equal(session.build, join(session.output, 'build'));
  assert.equal(realpathSync(buildParent), join(realpathSync(root), 'artifacts', 'native-test-sessions'));
  assert.ok(lstatSync(session.output).isDirectory() && !lstatSync(session.output).isSymbolicLink());
  assert.equal(realpathSync(session.output), join(realpathSync(buildParent), session.id));
  assert.deepEqual(session.files, { trustPath: join(secret, 'trust.json'), pdf: join(secret, 'pdf.json'),
    word: join(secret, 'word.json'), spreadsheet: join(secret, 'spreadsheet.json'),
    importer: join(secret, 'importer.json'),
    integrated: join(secret, 'integrated.json'), expired: join(secret, 'expired.json') });
  if (verifyArtifacts) {
    assert.equal(hash(trustFile), session.sourceTrustSha256, 'Source trust was modified.');
    assert.deepEqual(Object.keys(session.artifactHashes).sort(), Object.values(session.files).sort());
    for (const path of Object.values(session.files)) {
      assert.ok(lstatSync(path).isFile() && !lstatSync(path).isSymbolicLink());
      assert.equal(hash(path), session.artifactHashes[path], 'Synthetic test artifacts changed.');
    }
  }
  return session;
}

export function sessionEnvironment(descriptor) {
  const session = readSession(descriptor);
  return { PVAD_NATIVE_TEST_SESSION: descriptor, PVAD_NATIVE_TEST_BUILD_ROOT: session.build,
    PVAD_NATIVE_LICENSE_PATH: session.files.integrated, NativeLicensing__LicensePath: session.files.integrated,
    ArtifactsPath: session.build, UseArtifactsOutput: 'true',
    PvadLicenseTrustStorePath: session.files.trustPath, PvadNativeLicenseTestBootstrap: 'true' };
}

export function nativeTestEnvironment(environment = process.env) {
  if (!environment.PVAD_NATIVE_TEST_SESSION) return {};
  const expected = sessionEnvironment(environment.PVAD_NATIVE_TEST_SESSION);
  for (const key of keys) assert.equal(environment[key], expected[key], `Native test configuration drift: ${key}`);
  return expected;
}

export function closeNativeTestSession(descriptor) {
  // Validate deletion boundaries separately: damaged fixtures must not prevent cleanup.
  const session = readSession(descriptor, false);
  let validationError;
  try { readSession(descriptor); } catch (error) { validationError = error; }
  rmSync(session.build, { recursive: true, force: true });
  rmSync(session.secret, { recursive: true, force: true });
  assert.ok(!existsSync(session.build) && !existsSync(session.secret), 'Native test cleanup is incomplete.');
  writeFileSync(join(session.output, 'cleanup.json'), JSON.stringify({ syntheticOnly: true,
    temporaryFilesRemoved: true, sourceTrustUnchanged: hash(trustFile) === session.sourceTrustSha256,
    artifactsUnchanged: !validationError }, null, 2) + '\n');
  if (validationError) throw validationError;
}

export async function withNativeTestSession(callback) {
  if (process.env.PVAD_NATIVE_TEST_SESSION) {
    nativeTestEnvironment();
    return await callback();
  }
  const session = createNativeTestSession();
  const previous = Object.fromEntries(keys.map(key => [key, process.env[key]]));
  try {
    Object.assign(process.env, session.environment);
    return await callback();
  } finally {
    for (const key of keys) {
      if (previous[key] === undefined) delete process.env[key]; else process.env[key] = previous[key];
    }
    closeNativeTestSession(session.descriptor);
  }
}

export function nativeTestProjectPaths(project, configuration = 'Debug', environment = process.env) {
  assert.ok(!isAbsolute(project) && !project.split(/[\\/]/).includes('..') && project.endsWith('.csproj'));
  nativeTestEnvironment(environment);
  const output = execFileSync('dotnet', ['msbuild', project, '-nologo',
    '-getProperty:TargetPath,ProjectAssetsFile,MSBuildProjectExtensionsPath', `-p:Configuration=${configuration}`],
    { cwd: root, env: environment, encoding: 'utf8', timeout: 60_000 }).trim();
  const properties = JSON.parse(output).Properties;
  const path = properties.TargetPath;
  assert.ok(isAbsolute(path) && path.endsWith('.dll'), 'MSBuild did not return a target assembly path.');
  if (environment.PVAD_NATIVE_TEST_SESSION)
    assert.ok(path.startsWith(environment.PVAD_NATIVE_TEST_BUILD_ROOT + sep), 'Target escaped the isolated test build.');
  return { assembly: path, assets: properties.ProjectAssetsFile, extensions: properties.MSBuildProjectExtensionsPath };
}

export const nativeTestAssemblyPath = (project, configuration = 'Debug', environment = process.env) =>
  nativeTestProjectPaths(project, configuration, environment).assembly;

export function prepareGitHubSession(environment = process.env) {
  assert.equal(environment.GITHUB_ACTIONS, 'true', 'CI preparation is reserved for GitHub Actions.');
  assert.ok(!environment.PVAD_NATIVE_TEST_SESSION, 'A native test session is already active.');
  assert.ok(environment.GITHUB_ENV && environment.RUNNER_TEMP);
  const destination = realpathSync(environment.GITHUB_ENV);
  const parent = realpathSync(environment.RUNNER_TEMP);
  const local = relative(parent, destination);
  assert.ok(local && !local.startsWith('..') && !isAbsolute(local) && lstatSync(destination).isFile(), 'Invalid GitHub environment file.');
  const session = createNativeTestSession();
  try {
    const text = Object.entries(session.environment).map(([key, value]) => {
      assert.ok(!/[\r\n]/.test(value), 'Unsafe workflow environment value.');
      return `${key}=${value}\n`;
    }).join('');
    appendFileSync(destination, text);
  } catch (error) { closeNativeTestSession(session.descriptor); throw error; }
}

async function execute(command) {
  assert.ok(command.length && command[0] !== '--', 'Expected a command after exec --.');
  return await withNativeTestSession(() => new Promise((yes, no) => {
    const child = spawn(command[0], command.slice(1), { cwd: root, env: process.env, stdio: 'inherit',
      shell: false, detached: process.platform !== 'win32' });
    let interrupted = false, timer;
    const signal = value => {
      if (!child.pid) return;
      try {
        if (process.platform === 'win32') child.kill(value);
        else process.kill(-child.pid, value);
      } catch (error) { if (error.code !== 'ESRCH') throw error; }
    };
    const stop = () => {
      interrupted = true;
      signal('SIGTERM');
      timer ??= setTimeout(() => {
        if (process.platform === 'win32' && child.pid)
          execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
        else signal('SIGKILL');
      }, 5000);
    };
    const release = () => { clearTimeout(timer); process.off('SIGINT', stop); process.off('SIGTERM', stop); };
    process.on('SIGINT', stop); process.on('SIGTERM', stop);
    child.once('error', error => { release(); no(error); });
    child.once('close', code => {
      release();
      if (code === 0 && !interrupted) yes(); else no(new Error(`Native test command failed (${code}).`));
    });
  }));
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if (process.argv.length === 3 && process.argv[2] === 'prepare-ci') prepareGitHubSession();
    else if (process.argv.length === 3 && process.argv[2] === 'cleanup-ci') {
      if (process.env.PVAD_NATIVE_TEST_SESSION) closeNativeTestSession(process.env.PVAD_NATIVE_TEST_SESSION);
    } else if (process.argv[2] === 'exec' && process.argv[3] === '--') await execute(process.argv.slice(4));
    else throw new Error('Usage: test-session.mjs prepare-ci|cleanup-ci|exec -- <command> [arguments]');
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
