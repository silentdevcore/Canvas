import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const source = await readFile(new URL('../src/main.js', import.meta.url), 'utf8');
const releases = JSON.parse(await readFile(
  new URL('../../../product-metadata/pxa-releases.json', import.meta.url),
  'utf8',
));
const features = JSON.parse(await readFile(
  new URL('../../../product-metadata/designer-features.json', import.meta.url),
  'utf8',
));

test('release notes use shared manifests and searchable navigation', () => {
  assert.match(source, /pxaReleaseManifest/);
  assert.match(source, /designerFeatureManifest/);
  assert.match(source, /<summary>Release Notes<\/summary>/);
  assert.match(source, /href="#designer-feature-status"/);
  assert.match(source, /data-release-filter="stable"/);
  assert.match(source, /data-release-filter="beta"/);
  assert.match(source, /data-release-filter="alpha"/);
});

test('every release and feature has a unique stable identifier', () => {
  const versions = releases.releases.map(release => release.version);
  const featureIds = features.features.map(feature => feature.id);
  assert.equal(new Set(versions).size, versions.length);
  assert.equal(new Set(featureIds).size, featureIds.length);
  assert.ok(versions.includes('1.0.0'));
  assert.deepEqual(
    releases.releases.map(release => release.publishedAt),
    releases.releases.map(release => release.publishedAt).toSorted().reverse(),
  );
  for (const release of releases.releases) {
    assert.ok(release.featureIds.every(featureId => featureIds.includes(featureId)));
  }
});

test('public release source excludes internal and sensitive operational content', () => {
  const publicContent = JSON.stringify(releases).toLowerCase();
  for (const forbidden of ['password=', 'private key', 'customer email', 'internal ticket']) {
    assert.doesNotMatch(publicContent, new RegExp(forbidden));
  }
});
