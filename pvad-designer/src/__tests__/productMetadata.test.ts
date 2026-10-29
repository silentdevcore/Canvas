import {
  compareSemVer,
  designerFeatures,
  designerReleases,
  designerVersion,
  isFeatureNew,
} from '@/product/productMetadata';

describe('Designer product metadata', () => {
  test('contains unique releases and the current Designer version', () => {
    const versions = designerReleases.map(release => release.version);
    expect(new Set(versions).size).toBe(versions.length);
    expect(versions).toContain(designerVersion);
  });

  test('contains unique stable feature identifiers and known maturity values', () => {
    const ids = designerFeatures.map(feature => feature.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(designerFeatures.every(feature =>
      ['alpha', 'beta', 'stable'].includes(feature.maturity))).toBe(true);
  });

  test('expires New independently from maturity at the configured version', () => {
    const notifications = designerFeatures.find(feature =>
      feature.id === 'designer.notifications');
    expect(notifications).toBeDefined();
    expect(isFeatureNew(notifications!, '1.0.0')).toBe(true);
    expect(isFeatureNew(notifications!, '1.1.0')).toBe(false);
  });

  test('compares stable and prerelease semantic versions deterministically', () => {
    expect(compareSemVer('1.0.0', '1.1.0')).toBeLessThan(0);
    expect(compareSemVer('2.0.0', '1.9.9')).toBeGreaterThan(0);
    expect(compareSemVer('1.0.0', '1.0.0')).toBe(0);
    expect(compareSemVer('1.0.0-alpha.1', '1.0.0-beta.1')).toBeLessThan(0);
    expect(compareSemVer('1.0.0-beta.2', '1.0.0-beta.10')).toBeLessThan(0);
    expect(compareSemVer('1.0.0', '1.0.0-rc.1')).toBeGreaterThan(0);
    expect(compareSemVer('1.0.0+build.2', '1.0.0+build.1')).toBe(0);
  });
});
