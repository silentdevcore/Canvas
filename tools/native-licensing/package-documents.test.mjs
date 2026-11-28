import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { collectNugetNotices, loadNoticeSources, preparePackageDocuments, readNugetMetadata, safeNoticePath,
  requiredPublicDocumentation, validatePublicDocumentation } from './package-documents.mjs';
import { migrationProjects } from './joint-package-rehearsal.mjs';

const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const read = file => readFileSync(new URL(file, import.meta.url), 'utf8');
const nuspec = license => `<?xml version="1.0"?><package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>Synthetic.Library</id><version>1.2.3</version><authors>Synthetic author</authors><copyright>Synthetic copyright</copyright>${license}<requireLicenseAcceptance>true</requireLicenseAcceptance></metadata></package>`;
function temporary(callback) {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-package-doc-test-'));
  try { return callback(directory); } finally { rmSync(directory, { recursive: true, force: true }); }
}
function fixture(directory, license = '<license type="expression">MIT</license>', files = ['LICENSE.txt', 'THIRD-PARTY-NOTICES.txt']) {
  const cache = join(directory, 'cache'), packageDirectory = join(cache, 'synthetic.library/1.2.3');
  mkdirSync(packageDirectory, { recursive: true });
  writeFileSync(join(packageDirectory, 'synthetic.library.nuspec'), nuspec(license));
  for (const file of files) writeFileSync(join(packageDirectory, file), 'Synthetic original notice\r\n');
  return { packageDirectory, assets: { packageFolders: { [cache]: {} },
    libraries: { 'Synthetic.Library/1.2.3': { type: 'package', path: 'synthetic.library/1.2.3', files } } } };
}

function pinnedFixture(directory, { version = '1.2.3', tag = false } = {}) {
  const data = fixture(directory, '<license type="expression">MIT</license>', []);
  const commit = 'a'.repeat(40), repository = 'https://github.com/Synthetic/Library';
  const source = nuspec('<license type="expression">MIT</license>').replace('</metadata>',
    `<repository url="${repository}"${tag ? '' : ` commit="${commit}"`}/></metadata>`);
  writeFileSync(join(data.packageDirectory, 'synthetic.library.nuspec'), source);
  writeFileSync(join(data.packageDirectory, 'synthetic.library.1.2.3.nupkg'), 'Synthetic archive bytes');
  const registryDirectory = join(directory, 'registry'), file = `Synthetic.Library/${version}/LICENSE`;
  mkdirSync(join(registryDirectory, 'Synthetic.Library', version), { recursive: true });
  writeFileSync(join(registryDirectory, file), 'Original upstream license\r\n');
  const record = { id: 'Synthetic.Library', version, repositoryUrl: repository, sourceCommit: commit,
    sourceBinding: tag ? 'version-matched-upstream-tag-not-embedded-commit' : 'nuspec-repository-commit',
    ...(tag ? { releaseTag: 'v' + version } : {}),
    nuspecSha256: hash(source), packageSha256: hash('Synthetic archive bytes'),
    declaredLicense: { type: 'expression', value: 'MIT' }, legalApproval: 'not-granted',
    files: [{ file, sourceUrl: `https://raw.githubusercontent.com/Synthetic/Library/${commit}/LICENSE`,
      sha256: hash('Original upstream license\r\n'), role: 'license' }] };
  const save = () => writeFileSync(join(registryDirectory, 'manifest.v1.json'), JSON.stringify({ schemaVersion: 1, packages: [record] }));
  save();
  return { ...data, registryDirectory, record, save };
}

test('NuGet XML metadata retains declared license and copyright without granting acceptance', () => {
  const metadata = readNugetMetadata(nuspec('<license type="file">LICENSE.txt</license>'));
  assert.equal(metadata.id, 'Synthetic.Library');
  assert.deepEqual(metadata.license, { type: 'file', value: 'LICENSE.txt' });
  assert.equal(metadata.requireLicenseAcceptance, true);
  assert.equal(metadata.copyright, 'Synthetic copyright');
  assert.throws(() => readNugetMetadata('<!DOCTYPE package [<!ENTITY private SYSTEM "file:///unused">]>' + nuspec('').replace('<?xml version="1.0"?>', '')));
});

test('notice collection preserves exact bytes and reports supplied texts as unreviewed', () => temporary(directory => {
  const { assets } = fixture(directory);
  const destination = join(directory, 'notices');
  const [record] = collectNugetNotices(assets, destination);
  assert.equal(record.textStatus, 'included-unreviewed');
  assert.equal(record.legalApproval, 'not-granted');
  assert.equal(record.notices.length, 2);
  for (const notice of record.notices) {
    const bytes = readFileSync(join(destination, notice.file));
    assert.equal(bytes.toString(), 'Synthetic original notice\r\n');
    assert.equal(hash(bytes), notice.sha256);
  }
}));

test('an SPDX expression alone is not substituted with an invented license text', () => temporary(directory => {
  const { assets } = fixture(directory, '<license type="expression">MIT</license>', []);
  const [record] = collectNugetNotices(assets, join(directory, 'notices'));
  assert.equal(record.textStatus, 'license-text-required');
  assert.deepEqual(record.notices, []);
}));

test('pinned upstream originals are collected offline and retain package, source and byte provenance', () => temporary(directory => {
  const { assets, registryDirectory, record } = pinnedFixture(directory);
  const destination = join(directory, 'notices');
  const [result] = collectNugetNotices(assets, destination, registryDirectory);
  assert.equal(result.textStatus, 'included-unreviewed');
  assert.equal(result.legalApproval, 'not-granted');
  assert.equal(result.requireLicenseAcceptance, true);
  assert.equal(result.sourcePin.packageSha256, record.packageSha256);
  assert.equal(result.sourcePin.sourceCommit, record.sourceCommit);
  assert.equal(result.sourcePin.sourceBinding, 'nuspec-repository-commit');
  assert.equal(result.notices[0].sourceUrl, record.files[0].sourceUrl);
  assert.equal(result.notices[0].file, 'Synthetic.Library/1.2.3/upstream/LICENSE');
  assert.equal(hash(readFileSync(join(destination, result.notices[0].file))), record.files[0].sha256);
}));

test('package, metadata and original-text tampering fail instead of silently reusing a source', () => {
  for (const file of ['synthetic.library.1.2.3.nupkg', 'synthetic.library.nuspec', 'upstream']) temporary(directory => {
    const { assets, registryDirectory, record, packageDirectory } = pinnedFixture(directory);
    writeFileSync(file === 'upstream' ? join(registryDirectory, record.files[0].file) : join(packageDirectory, file), 'Changed bytes');
    assert.throws(() => collectNugetNotices(assets, join(directory, 'notices'), registryDirectory));
  });
});

test('old-version source records cannot supply notices to a different package version', () => temporary(directory => {
  const { assets, registryDirectory } = pinnedFixture(directory, { version: '1.2.2' });
  const [result] = collectNugetNotices(assets, join(directory, 'notices'), registryDirectory);
  assert.equal(result.textStatus, 'license-text-required');
  assert.equal(result.sourcePin, null);
  assert.deepEqual(result.notices, []);
}));

test('release-tag provenance stays distinct from an embedded package source commit', () => temporary(directory => {
  const { assets, registryDirectory, record, save } = pinnedFixture(directory, { tag: true });
  const [result] = collectNugetNotices(assets, join(directory, 'notices'), registryDirectory);
  assert.equal(result.sourcePin.sourceBinding, 'version-matched-upstream-tag-not-embedded-commit');
  assert.equal(result.sourcePin.releaseTag, 'v1.2.3');
  record.releaseTag = 'main'; save();
  assert.throws(() => loadNoticeSources(registryDirectory), /Unmatched release tag/);
}));

test('source pins cannot override repository, embedded commit or declared-license identities', () => {
  for (const mutate of [r => { r.sourceCommit = 'b'.repeat(40); r.files[0].sourceUrl = r.files[0].sourceUrl.replace('a'.repeat(40), r.sourceCommit); },
    r => { r.repositoryUrl = 'https://github.com/Synthetic/Other'; r.files[0].sourceUrl = r.files[0].sourceUrl.replace('/Library/', '/Other/'); },
    r => { r.declaredLicense.value = 'Apache-2.0'; },
    r => { r.sourceBinding = 'version-matched-upstream-tag-not-embedded-commit'; r.releaseTag = 'v1.2.3'; }]) temporary(directory => {
    const { assets, registryDirectory, record, save } = pinnedFixture(directory); mutate(record); save();
    assert.throws(() => collectNugetNotices(assets, join(directory, 'notices'), registryDirectory));
  });
});

test('duplicate, mutable, foreign, traversal and approval-granting source records are rejected', () => {
  for (const mutate of [r => { r.files.push({ ...r.files[0] }); },
    r => { r.files[0].sourceUrl = r.files[0].sourceUrl.replace('a'.repeat(40), 'main'); },
    r => { r.files[0].file = 'Other.Package/1.2.3/LICENSE'; },
    r => { r.files[0].file = 'Synthetic.Library/1.2.3/../../../outside'; },
    r => { r.legalApproval = 'granted'; }]) temporary(directory => {
    const { registryDirectory, record, save } = pinnedFixture(directory); mutate(record); save();
    assert.throws(() => loadNoticeSources(registryDirectory));
  });
  temporary(directory => {
    const { registryDirectory, record } = pinnedFixture(directory);
    writeFileSync(join(registryDirectory, 'manifest.v1.json'), JSON.stringify({ schemaVersion: 1, packages: [record, record] }));
    assert.throws(() => loadNoticeSources(registryDirectory), /Duplicate pinned/);
  });
});

test('supplementary Unicode license notices are retained without claiming the main library license', () => temporary(directory => {
  const { assets } = fixture(directory, '<license type="expression">MIT</license>', ['UNICODE-LICENSE.txt']);
  const [result] = collectNugetNotices(assets, join(directory, 'notices'));
  assert.equal(result.textStatus, 'license-text-required');
  assert.equal(result.notices.length, 1);
  assert.equal(result.notices[0].role, 'notice');
}));

test('all acquired originals are hash-pinned, with release-tag limitations explicit', () => {
  assert.match(readFileSync(new URL('../../.gitattributes', import.meta.url), 'utf8'), /^assets\/licenses\/native-packages\/\*\* -text$/m);
  const registry = loadNoticeSources();
  assert.equal(registry.packages.length, 30);
  assert.equal(registry.packages.flatMap(p => p.files).length, 42);
  assert.deepEqual(registry.packages.filter(p => p.sourceBinding !== 'nuspec-repository-commit').map(p => p.id).sort(),
    ['ExcelNumberFormat', 'MathNet.Numerics.Signed', 'Tesseract', 'ZXing.Net']);
  const nodaTime = registry.packages.find(p => p.id === 'NodaTime');
  const provenance = JSON.parse(readFileSync(new URL('../../assets/licenses/nodatime-provenance.json', import.meta.url)));
  assert.equal(nodaTime.sourceCommit, provenance.sourceCommit);
  assert.equal(nodaTime.packageSha256, provenance.packageSha256);
  assert.equal(nodaTime.files.find(f => f.role === 'license').sha256, provenance.licenseSha256);
  assert.equal(nodaTime.files.find(f => f.role === 'notice').sha256, provenance.noticeSha256);
});

test('extensionless and nested notices keep their exact relative paths', () => temporary(directory => {
  const { assets, packageDirectory } = fixture(directory, '<license type="file">LICENSE</license>', ['LICENSE']);
  mkdirSync(join(packageDirectory, 'nested'));
  writeFileSync(join(packageDirectory, 'nested/NOTICE'), 'Nested original notice');
  assets.libraries['Synthetic.Library/1.2.3'].files.push('nested/NOTICE');
  const destination = join(directory, 'notices');
  const [record] = collectNugetNotices(assets, destination);
  assert.deepEqual(record.notices.map(item => item.file), [
    'Synthetic.Library/1.2.3/LICENSE', 'Synthetic.Library/1.2.3/nested/NOTICE',
  ]);
  for (const notice of record.notices) assert.equal(hash(readFileSync(join(destination, notice.file))), notice.sha256);
}));

test('legacy and UTF-16 notice encodings retain their original bytes, not a replacement UTF-8 text', () => {
  for (const bytes of [Buffer.from([0x43, 0x6f, 0x70, 0x79, 0xe9]),
    Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from('Original copyright', 'utf16le')])]) temporary(directory => {
    const { assets, packageDirectory } = fixture(directory);
    writeFileSync(join(packageDirectory, 'LICENSE.txt'), bytes);
    const destination = join(directory, 'notices');
    const [record] = collectNugetNotices(assets, destination);
    const notice = record.notices.find(item => item.file.endsWith('LICENSE.txt'));
    assert.deepEqual(readFileSync(join(destination, notice.file)), bytes);
    assert.equal(notice.sha256, hash(bytes));
  });
});

test('vendor RTF may retain one terminal NUL, but embedded NULs and binary disguises fail', () => {
  temporary(directory => {
    const { assets, packageDirectory } = fixture(directory, '<license type="expression">MIT</license>', ['ThirdPartyNotices.rtf']);
    const original = Buffer.from('{\\rtf1\\ansi Original notice}\r\n\0');
    writeFileSync(join(packageDirectory, 'ThirdPartyNotices.rtf'), original);
    const destination = join(directory, 'notices');
    const [result] = collectNugetNotices(assets, destination);
    assert.equal(result.textStatus, 'license-text-required');
    assert.deepEqual(readFileSync(join(destination, result.notices[0].file)), original);
    assert.equal(result.notices[0].sha256, hash(original));
  });
  for (const [file, content] of [['NOTICE.txt', '{\\rtf1 Notice}\0'],
    ['NOTICE.rtf', '{\\rtf1 Embedded\0notice}\0'], ['NOTICE.rtf', '{\\rtf1 Notice}\0\0'],
    ['NOTICE.rtf', 'Binary text\0'], ['NOTICE.rtf', '{\\rtf1 Unclosed\0'],
    ['NOTICE.rtf', Buffer.concat([Buffer.from([0xfb, 0xdc, 0xf2, 0xf4, 0xe6, 0xb1]), Buffer.from(' Notice}\0')])],
    ['NOTICE.rtf', '{\\rtf1 -----BEGIN PRIVATE KEY-----}\0']]) temporary(directory => {
    const { assets, packageDirectory } = fixture(directory, '<license type="expression">MIT</license>', [file]);
    writeFileSync(join(packageDirectory, file), content);
    assert.throws(() => collectNugetNotices(assets, join(directory, 'notices')));
  });
});

test('private keys in notice files are rejected in supported text encodings', () => {
  for (const bytes of [Buffer.from('-----BEGIN PRIVATE KEY-----'),
    Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from('-----BEGIN EC PRIVATE KEY-----', 'utf16le')])]) temporary(directory => {
    const { assets, packageDirectory } = fixture(directory);
    writeFileSync(join(packageDirectory, 'LICENSE.txt'), bytes);
    assert.throws(() => collectNugetNotices(assets, join(directory, 'notices')), /Private keys/);
  });
});

test('missing declared files, metadata drift and invalid paths fail instead of claiming complete notices', () => {
  for (const license of ['<license type="file">absent.txt</license>', '<license type="file">../secret.txt</license>']) temporary(directory => {
    const { assets } = fixture(directory, license);
    assert.throws(() => collectNugetNotices(assets, join(directory, 'notices')));
  });
  temporary(directory => {
    const { assets, packageDirectory } = fixture(directory);
    writeFileSync(join(packageDirectory, 'synthetic.library.nuspec'), nuspec('').replace('1.2.3', '1.2.4'));
    assert.throws(() => collectNugetNotices(assets, join(directory, 'notices')));
  });
});

test('notice traversal, symlinks and binary files are rejected', () => temporary(directory => {
  const { assets, packageDirectory } = fixture(directory);
  for (const path of ['../outside', '/absolute', 'C:\\absolute', 'nested/../../outside']) assert.throws(() => safeNoticePath(packageDirectory, path));
  const outside = join(directory, 'outside.txt'); writeFileSync(outside, 'not public');
  symlinkSync(directory, join(packageDirectory, 'linked'), 'junction');
  assert.throws(() => safeNoticePath(packageDirectory, 'linked/outside.txt'));
  writeFileSync(join(packageDirectory, 'LICENSE.txt'), 'binary\0not a license');
  assert.throws(() => collectNugetNotices(assets, join(directory, 'notices')));
}));

test('package bundles bind immutable legal references, public XML and offline guide without publication claims', () => temporary(directory => {
  const { assets } = fixture(directory);
  const file = join(directory, 'public.xml'), xml = '<doc><assembly><name>PVAD.Fixture</name></assembly><members/></doc>';
  writeFileSync(file, xml);
  const destination = join(directory, 'bundle');
  const result = preparePackageDocuments({ name: 'PVAD.Fixture', version: '1.1.0', assets, destination,
    documentation: { file, assembly: 'PVAD.Fixture', publicMembers: 0, sourceMembers: 1, removedMembers: 1,
      xmlSha256: hash(xml), sourceSha256: 'a'.repeat(64), assemblySha256: 'b'.repeat(64) } });
  assert.equal(result.publicationReady, false);
  assert.equal(result.legalApprovalGranted, false);
  assert.equal(result.legal.status, 'unapproved-candidate-not-contractual-acceptance');
  assert.equal(result.legal.sourceSha256, hash(readFileSync(new URL('../../product-metadata/legal-documents/en/license.md', import.meta.url))));
  assert.equal(result.files['lib/net10.0/PVAD.Fixture.xml'], hash(xml));
  assert.equal(result.files['docs/ACTIVATION.md'], hash(readFileSync(new URL('../../docs/guides/PVAD.Native-Library-Activation.md', import.meta.url))));
  assert.equal(result.files['docs/README.md'], hash(read('./package-assets/README.md')));
  assert.ok(readFileSync(join(destination, 'LICENSE-REFERENCE.md'), 'utf8').includes('not a new license agreement'));
  assert.ok(!JSON.stringify(result).includes(directory));
  assert.throws(() => preparePackageDocuments({ destination, name: 'PVAD.Fixture', version: '1.1.0' }));
}));

test('offline instructions use parsed local-only configuration, pinned packages and locked restores', async () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { JSDOM } = require('jsdom');
  const { marked } = await import(pathToFileURL(require.resolve('marked')).href);
  const source = read('./package-assets/README.md');
  const blocks = marked.lexer(source).filter(token => token.type === 'code');
  const xml = blocks.filter(token => token.lang === 'xml');
  assert.equal(xml.length, 2);
  const config = new JSDOM(xml[0].text, { contentType: 'text/xml' });
  const project = new JSDOM(xml[1].text, { contentType: 'text/xml' });
  try {
    assert.equal(config.window.document.doctype, null);
    const sources = config.window.document.querySelector('configuration > packageSources');
    assert.deepEqual([...sources.children].map(node => node.tagName), ['clear', 'add']);
    assert.equal(sources.querySelector('add').getAttribute('value'), './offline-feed');
    const audits = config.window.document.querySelector('configuration > auditSources');
    assert.deepEqual([...audits.children].map(node => node.tagName), ['clear']);
    assert.equal(project.window.document.querySelector('TargetFramework').textContent, 'net10.0');
    assert.equal(project.window.document.querySelector('RestorePackagesWithLockFile').textContent, 'true');
    const reference = project.window.document.querySelector('PackageReference');
    assert.equal(reference.getAttribute('Include'), 'PVAD.Generator');
    assert.match(reference.getAttribute('Version'), /^\d+\.\d+\.\d+$/);
    const commands = blocks.filter(token => token.lang === 'bash').map(token => token.text).join('\n');
    for (const flag of ['--configfile ./NuGet.Config', '--packages ./restored-packages', '--use-lock-file', '--locked-mode', '--no-restore'])
      assert.ok(commands.includes(flag));
    assert.ok(!commands.includes('nuget push') && !commands.includes('AcceptNPOI'));
    assert.ok(source.includes('not yet published or approved'));
    assert.ok(source.includes('not\na security scan'));
  } finally { config.window.close(); project.window.close(); }
});

test('protected packaging uses the public XML and binds every included documentation file', () => {
  const targets = read('./protected-package.targets');
  for (const value of ['PvadPackageDocumentationDirectory', '<DocumentationFile>', 'docs/README.md', 'documentation-manifest.json']) assert.ok(targets.includes(value));
  assert.ok(targets.includes('DocumentationProjectOutputGroupOutput Remove="@(DocumentationProjectOutputGroupOutput)"'));
  // NuGet appends the relative source path for directory targets; extensionless
  // filenames in PackagePath are otherwise interpreted as directories too.
  assert.ok(targets.includes('Pack="true" PackagePath="docs/"'));
  const runner = read('./joint-package-rehearsal.mjs');
  for (const value of ['PackageDocumentationProbe', 'Public documentation changed after protection', 'preparePackageDocuments', 'packageDocumentation, documentationPublicationReady: false']) assert.ok(runner.includes(value));
  const consumer = read('./GeneratorPackageProbe/Program.cs');
  for (const value of ['package-documentation-checksum:', 'package-documentation-bound-to-dll:', 'package-documentation-no-legal-approval:']) assert.ok(consumer.includes(value));
  const probe = read('./PackageDocumentationProbe/Program.cs');
  for (const value of ['DocumentationCommentId.GetSymbolsForDeclarationId', 'MetadataImportOptions.All', 'DtdProcessing.Prohibit', 'sourceMembers', 'ExternallyVisible']) assert.ok(probe.includes(value));
  assert.ok(!probe.includes('Assembly.Load') && !probe.includes('LoadFromAssemblyPath'));
});

test('all nineteen formerly empty modules enforce compiler documentation and metadata coverage', () => {
  assert.equal(requiredPublicDocumentation.length, 19);
  assert.equal(new Set(requiredPublicDocumentation).size, 19);
  for (const name of requiredPublicDocumentation) {
    const project = readFileSync(new URL('../../' + migrationProjects[name], import.meta.url), 'utf8');
    assert.ok(project.includes('<GenerateDocumentationFile>true</GenerateDocumentationFile>'));
    assert.ok(!project.includes('<NoWarn>$(NoWarn);CS1591</NoWarn>'));
    for (const code of ['CS1591', 'CS1570', 'CS1572', 'CS1573', 'CS1574', 'CS1584']) assert.ok(project.includes(code), name + ':' + code);
    validatePublicDocumentation(name, { publicTypes: 2, documentedPublicTypes: 2, undocumentedPublicTypes: [] });
    for (const evidence of [{ publicTypes: 0, documentedPublicTypes: 0, undocumentedPublicTypes: [] },
      { publicTypes: 2, documentedPublicTypes: 1, undocumentedPublicTypes: ['T:Missing'] },
      { publicTypes: 2, documentedPublicTypes: 2, undocumentedPublicTypes: ['T:Missing'] }])
      assert.throws(() => validatePublicDocumentation(name, evidence));
  }
  const runner = read('./joint-package-rehearsal.mjs');
  assert.ok(runner.includes('validatePublicDocumentation(name, before)'));
  assert.ok(runner.includes('validatePublicDocumentation(name, after)'));
});
