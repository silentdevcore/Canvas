import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(join(root, 'pvad-designer/package.json'));
const { JSDOM } = require('jsdom');
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const text = path => readFileSync(path, 'utf8');
const json = path => JSON.parse(text(path));
const noticeRegistryDirectory = join(root, 'assets/licenses/native-packages');

export function safeNoticePath(directory, path) {
  assert.equal(typeof path, 'string');
  const normalized = path.replaceAll('\\', '/');
  assert.ok(normalized && !isAbsolute(normalized) && !/^[A-Za-z]:/.test(normalized) &&
    normalized.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe notice path.');
  const base = realpathSync(directory), destination = resolve(base, normalized);
  assert.ok(destination.startsWith(base + sep), 'Notice escaped its package.');
  let current = base;
  for (const part of normalized.split('/')) {
    current = join(current, part);
    assert.ok(!lstatSync(current).isSymbolicLink(), 'Notice symlinks are not accepted.');
  }
  assert.ok(lstatSync(destination).isFile() && lstatSync(destination).size <= 2 * 1024 * 1024, 'Invalid notice file.');
  return destination;
}

export function readNugetMetadata(nuspec) {
  const dom = new JSDOM(nuspec, { contentType: 'text/xml' });
  try {
    assert.equal(dom.window.document.doctype, null, 'External XML declarations are not accepted.');
    const metadata = dom.window.document.querySelector('metadata');
    assert.ok(metadata);
    const value = name => metadata.querySelector(':scope > ' + name)?.textContent.trim() ?? null;
    const license = metadata.querySelector(':scope > license');
    const repository = metadata.querySelector(':scope > repository');
    const result = { id: value('id'), version: value('version'), authors: value('authors'),
      copyright: value('copyright'), projectUrl: value('projectUrl'),
      license: license ? { type: license.getAttribute('type'), value: license.textContent.trim() } : null,
      repository: repository ? { url: repository.getAttribute('url'), commit: repository.getAttribute('commit') } : null,
      licenseUrl: value('licenseUrl'), requireLicenseAcceptance: value('requireLicenseAcceptance') === 'true' };
    assert.match(result.id ?? '', /^[A-Za-z0-9][A-Za-z0-9_.-]*$/);
    assert.ok(result.version && !/[\\/]/.test(result.version));
    return result;
  } finally { dom.window.close(); }
}

function noticeBytes(path) {
  const bytes = readFileSync(path);
  // Some vendor RTF notices have one C-style terminal NUL. Validate only that
  // exact wrapper, while preserving every source byte in the packaged copy.
  const terminalRtf = /\.rtf$/i.test(path) && bytes.at(-1) === 0 &&
    bytes.subarray(0, 6).equals(Buffer.from('{\\rtf1', 'ascii')) &&
    bytes.subarray(0, -1).toString('latin1').trimEnd().endsWith('}');
  const content = terminalRtf ? bytes.subarray(0, -1) : bytes;
  const decoded = content[0] === 0xff && content[1] === 0xfe
    ? new TextDecoder('utf-16le', { fatal: true }).decode(content)
    : content[0] === 0xfe && content[1] === 0xff
      ? new TextDecoder('utf-16be', { fatal: true }).decode(content)
      : content.toString('latin1');
  assert.ok(!decoded.includes('\0'), 'Notices must be text, not binary data.');
  assert.ok(!/-----BEGIN (?:EC |RSA |ENCRYPTED )?PRIVATE KEY-----/.test(decoded), 'Private keys cannot be packaged as notices.');
  return bytes;
}

export function loadNoticeSources(directory = noticeRegistryDirectory) {
  const registry = JSON.parse(text(safeNoticePath(directory, 'manifest.v1.json')));
  assert.equal(registry.schemaVersion, 1);
  assert.ok(Array.isArray(registry.packages));
  const keys = new Set();
  for (const record of registry.packages) {
    assert.match(record.id, /^[A-Za-z0-9][A-Za-z0-9_.-]*$/);
    assert.match(record.version, /^[A-Za-z0-9_.+-]+$/);
    const key = `${record.id}/${record.version}`.toLowerCase();
    assert.ok(!keys.has(key), 'Duplicate pinned notice source.'); keys.add(key);
    assert.equal(record.legalApproval, 'not-granted');
    for (const value of [record.nuspecSha256, record.packageSha256]) assert.match(value, /^[a-f0-9]{64}$/);
    assert.match(record.sourceCommit, /^[a-f0-9]{40}$/);
    assert.match(record.repositoryUrl, /^https:\/\/github\.com\/[\w.-]+\/[\w.-]+$/);
    assert.ok(['nuspec-repository-commit', 'version-matched-upstream-tag-not-embedded-commit'].includes(record.sourceBinding));
    if (record.sourceBinding !== 'nuspec-repository-commit')
      assert.ok([record.version, 'v' + record.version, 'v' + record.version + '.0'].includes(record.releaseTag), 'Unmatched release tag.');
    assert.ok(Array.isArray(record.files) && record.files.some(file => file.role === 'license'));
    const files = new Set();
    for (const file of record.files) {
      assert.ok(['license', 'notice'].includes(file.role));
      assert.match(file.sha256, /^[a-f0-9]{64}$/);
      const prefix = `${record.id}/${record.version}/`;
      assert.ok(file.file.startsWith(prefix), 'Source notice belongs to another package.');
      assert.ok(!files.has(file.file.toLowerCase()), 'Duplicate source notice.'); files.add(file.file.toLowerCase());
      const relativeSource = file.file.slice(prefix.length);
      const expectedUrl = `${record.repositoryUrl.replace('https://github.com/', 'https://raw.githubusercontent.com/')}/${record.sourceCommit}/${relativeSource}`;
      assert.equal(file.sourceUrl, expectedUrl, 'Source URL is not bound to the pinned repository commit.');
      assert.equal(hash(noticeBytes(safeNoticePath(directory, file.file))), file.sha256, 'Pinned source notice changed.');
    }
  }
  return registry;
}

export function collectNugetNotices(assets, destination, registryDirectory = noticeRegistryDirectory) {
  mkdirSync(destination, { recursive: false });
  const registry = loadNoticeSources(registryDirectory);
  const records = [];
  for (const [key, library] of Object.entries(assets.libraries).filter(([, value]) => value.type === 'package').sort(([a], [b]) => a.localeCompare(b, 'en'))) {
    const [id, version, extra] = key.split('/');
    assert.ok(!extra && /^[A-Za-z0-9][A-Za-z0-9_.-]*$/.test(id) && /^[A-Za-z0-9_.+-]+$/.test(version));
    const nuspecName = id.toLowerCase() + '.nuspec';
    const packageDirectory = Object.keys(assets.packageFolders).map(folder => {
      assert.ok(isAbsolute(folder));
      const path = resolve(folder, library.path);
      assert.ok(path.startsWith(resolve(folder) + sep), 'Package escaped its NuGet cache.');
      if (existsSync(path)) assert.ok(realpathSync(path).startsWith(realpathSync(folder) + sep) && !lstatSync(path).isSymbolicLink(), 'Package cache symlinks are not accepted.');
      return path;
    }).find(path => existsSync(join(path, nuspecName)));
    assert.ok(packageDirectory, 'Resolved NuGet metadata is unavailable: ' + key);
    const source = safeNoticePath(packageDirectory, nuspecName);
    const metadata = readNugetMetadata(text(source));
    assert.equal(metadata.id.toLowerCase(), id.toLowerCase());
    assert.equal(metadata.version.toLowerCase(), version.toLowerCase());
    const declared = metadata.license?.type === 'file' ? metadata.license.value.replaceAll('\\', '/') : null;
    const files = [...new Set([...library.files.filter(file => /(?:^|\/)(?:(?:[^/]+[-_. ])?licen[cs]e[^/]*|copying[^/]*|notices?[^/]*|third[-_. ]?party[-_. ]?notices?[^/]*|copyright[^/]*)$/i.test(file)), ...(declared ? [declared] : [])])].sort();
    const copied = [];
    for (const file of files) {
      const path = safeNoticePath(packageDirectory, file), bytes = noticeBytes(path);
      const output = join(destination, id, version, file.replaceAll('\\', '/'));
      mkdirSync(dirname(output), { recursive: true }); copyFileSync(path, output);
      copied.push({ file: relative(destination, output).split(sep).join('/'), sha256: hash(bytes), bytes: bytes.length,
        role: file === declared || /(?:^|\/)(?:licen[cs]e|copying)/i.test(file) ? 'license' : 'notice', source: 'resolved-nuget-cache' });
    }
    const upstream = registry.packages.find(record => record.id.toLowerCase() === id.toLowerCase() && record.version.toLowerCase() === version.toLowerCase());
    if (upstream) {
      assert.equal(hash(readFileSync(source)), upstream.nuspecSha256, 'Pinned package metadata changed: ' + key);
      assert.deepEqual(metadata.license, upstream.declaredLicense, 'Pinned declared license changed: ' + key);
      const repositoryUrl = (metadata.repository?.url ?? metadata.projectUrl)?.replace(/\/$/, '');
      assert.equal(repositoryUrl, upstream.repositoryUrl, 'Pinned source repository changed: ' + key);
      if (upstream.sourceBinding === 'nuspec-repository-commit')
        assert.equal(metadata.repository?.commit, upstream.sourceCommit, 'Pinned source commit changed: ' + key);
      else assert.ok(!metadata.repository?.commit, 'Embedded source commit must take priority over a release tag.');
      const archive = join(packageDirectory, `${id.toLowerCase()}.${version.toLowerCase()}.nupkg`);
      const archiveInfo = lstatSync(archive);
      assert.ok(!archiveInfo.isSymbolicLink() && archiveInfo.isFile() && archiveInfo.size <= 128 * 1024 * 1024, 'Invalid pinned package archive.');
      assert.equal(hash(readFileSync(archive)), upstream.packageSha256, 'Pinned package archive changed: ' + key);
      for (const file of upstream.files) {
        const path = safeNoticePath(registryDirectory, file.file), bytes = noticeBytes(path);
        const output = join(destination, id, version, 'upstream', file.file.slice(`${upstream.id}/${upstream.version}/`.length));
        mkdirSync(dirname(output), { recursive: true }); copyFileSync(path, output);
        copied.push({ file: relative(destination, output).split(sep).join('/'), sha256: hash(bytes), bytes: bytes.length,
          role: file.role, source: 'pinned-upstream-source', sourceUrl: file.sourceUrl });
      }
    }
    records.push({ ...metadata, nuspecSha256: hash(readFileSync(source)), notices: copied,
      textStatus: copied.some(item => item.role === 'license') ? 'included-unreviewed' : 'license-text-required',
      sourcePin: upstream ? { packageSha256: upstream.packageSha256, repositoryUrl: upstream.repositoryUrl,
        sourceCommit: upstream.sourceCommit, sourceBinding: upstream.sourceBinding, releaseTag: upstream.releaseTag ?? null } : null,
      legalApproval: 'not-granted', source: 'resolved-nuget-cache',
      reviewGate: id.toLowerCase() === 'npoi' ? 'existing-npoi-distribution-decision' : null });
  }
  return records;
}

export function preparePackageDocuments({ name, version, assets, destination, documentation }) {
  assert.match(name, /^PVAD\.[A-Za-z0-9.]+$/);
  assert.match(version, /^\d+\.\d+\.\d+$/);
  assert.ok(!existsSync(destination), 'Never replace an existing documentation bundle.');
  mkdirSync(destination, { recursive: false });
  const legalRoot = join(root, 'product-metadata/legal-documents/en');
  const manifestPath = join(legalRoot, 'manifest.json'), legal = json(manifestPath);
  const license = legal.documents.find(value => value.key === 'license');
  assert.ok(license?.version && license.file === 'license.md');
  const compatibility = join(root, legal.compatibilityReference.file);
  assert.equal(hash(readFileSync(compatibility)), legal.compatibilityReference.sha256, 'Legal reference pin changed.');
  mkdirSync(join(destination, 'legal')); mkdirSync(join(destination, 'reference'));
  const legalBytes = readFileSync(join(legalRoot, license.file));
  copyFileSync(join(legalRoot, license.file), join(destination, 'legal/license-agreement-candidate.md'));
  copyFileSync(compatibility, join(destination, 'reference/compatibility-identifiers.v1.json'));
  copyFileSync(join(root, 'docs/guides/PVAD.Native-Library-Activation.md'), join(destination, 'ACTIVATION.md'));
  copyFileSync(join(root, 'tools/native-licensing/package-assets/LICENSE-REFERENCE.md'), join(destination, 'LICENSE-REFERENCE.md'));
  copyFileSync(join(root, 'tools/native-licensing/package-assets/README.md'), join(destination, 'README.md'));
  copyFileSync(documentation.file, join(destination, name + '.xml'));
  assert.equal(hash(readFileSync(documentation.file)), documentation.xmlSha256);
  const notices = collectNugetNotices(assets, join(destination, 'third-party'));
  writeFileSync(join(destination, 'third-party/inventory.json'), JSON.stringify({ schemaVersion: 1,
    scope: 'resolved-nuget-closure-including-build-dependencies-not-native-os-audit', packages: notices }, null, 2) + '\n');
  const files = {};
  function visit(directory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const file = join(directory, entry.name);
      if (entry.isDirectory()) visit(file);
      else {
        assert.ok(entry.isFile());
        const local = relative(destination, file).split(sep).join('/');
        files[local === name + '.xml' ? `lib/net10.0/${local}` : 'docs/' + local] = hash(readFileSync(file));
      }
    }
  }
  visit(destination);
  const result = { schemaVersion: 1, package: name, version, publicationReady: false,
    legal: { status: 'unapproved-candidate-not-contractual-acceptance', version: license.version,
      authoritativeLocale: legal.authoritativeLocale, governingLaw: legal.governingLaw,
      sourceSha256: hash(legalBytes), sourceManifestSha256: hash(readFileSync(manifestPath)),
      compatibilityReference: legal.compatibilityReference },
    xml: { assembly: documentation.assembly, sourceMembers: documentation.sourceMembers,
      publicMembers: documentation.publicMembers, removedMembers: documentation.removedMembers,
      xmlSha256: documentation.xmlSha256, sourceSha256: documentation.sourceSha256,
      assemblySha256: documentation.assemblySha256, completeApiCommentCoverageClaimed: false },
    thirdPartyPackages: notices.length,
    missingLicenseTexts: notices.filter(item => item.textStatus === 'license-text-required').map(item => item.id + '/' + item.version),
    nativeAssetAndFontNoticeReviewRequired: true, legalApprovalGranted: false, files };
  assert.equal(result.xml.assembly, name);
  writeFileSync(join(destination, 'documentation-manifest.json'), JSON.stringify(result, null, 2) + '\n');
  return result;
}
