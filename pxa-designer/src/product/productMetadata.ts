import featureManifest from '../../../product-metadata/designer-features.json';
import releaseManifest from '../../../product-metadata/pxa-releases.json';

export type FeatureMaturity = 'alpha' | 'beta' | 'stable';
export type ReleaseChannel = 'alpha' | 'beta' | 'stable';
export type ReleaseChangeCategory =
  | 'added'
  | 'improved'
  | 'fixed'
  | 'security'
  | 'deprecated'
  | 'breaking';

export interface DesignerFeatureDefinition {
  id: string;
  titleKey: string;
  descriptionKey: string;
  fallbackTitle: string;
  fallbackDescription: string;
  maturity: FeatureMaturity;
  introducedIn: string;
  newUntilVersion?: string;
  defaultEnabled: boolean;
  requiredEntitlement?: string;
  documentationPath: string;
}

export interface DesignerReleaseDefinition {
  version: string;
  publishedAt: string;
  channel: ReleaseChannel;
  title: string;
  summary: string;
  documentationPath: string;
  components: string[];
  featureIds: string[];
  changes: Record<ReleaseChangeCategory, string[]>;
}

export const designerVersion =
  typeof __PXA_VERSION__ === 'undefined' ? '1.0.0' : __PXA_VERSION__;
export const designerCommit =
  typeof __PXA_BUILD_COMMIT__ === 'undefined' ? 'test' : __PXA_BUILD_COMMIT__;
export const designerBuildTime =
  typeof __PXA_BUILD_TIME__ === 'undefined'
    ? '2026-07-28T00:00:00.000Z'
    : __PXA_BUILD_TIME__;
export const designerDocumentationUrl =
  typeof __PXA_DOCUMENTATION_URL__ === 'undefined'
    ? 'http://localhost:5174'
    : __PXA_DOCUMENTATION_URL__;
export const designerFeatures =
  featureManifest.features as DesignerFeatureDefinition[];
export const designerReleases =
  releaseManifest.releases as DesignerReleaseDefinition[];

interface ParsedSemVer {
  core: [number, number, number];
  prerelease: string[];
}

const parseSemVer = (version: string): ParsedSemVer => {
  const match = /^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/.exec(version);
  return match
    ? {
      core: [Number(match[1]), Number(match[2]), Number(match[3])],
      prerelease: match[4]?.split('.') ?? [],
    }
    : { core: [0, 0, 0], prerelease: [] };
};

export const compareSemVer = (left: string, right: string): number => {
  const a = parseSemVer(left);
  const b = parseSemVer(right);
  for (let index = 0; index < 3; index += 1) {
    if (a.core[index] !== b.core[index]) return a.core[index] - b.core[index];
  }
  if (a.prerelease.length === 0 || b.prerelease.length === 0)
    return a.prerelease.length === b.prerelease.length ? 0 : a.prerelease.length === 0 ? 1 : -1;
  const length = Math.max(a.prerelease.length, b.prerelease.length);
  for (let index = 0; index < length; index += 1) {
    const aPart = a.prerelease[index];
    const bPart = b.prerelease[index];
    if (aPart === undefined || bPart === undefined)
      return aPart === bPart ? 0 : aPart === undefined ? -1 : 1;
    if (aPart === bPart) continue;
    const aNumeric = /^\d+$/.test(aPart);
    const bNumeric = /^\d+$/.test(bPart);
    if (aNumeric && bNumeric) return Number(aPart) - Number(bPart);
    if (aNumeric !== bNumeric) return aNumeric ? -1 : 1;
    return aPart.localeCompare(bPart);
  }
  return 0;
};

export const isFeatureNew = (
  feature: DesignerFeatureDefinition,
  currentVersion = designerVersion,
): boolean =>
  Boolean(
    feature.newUntilVersion &&
    compareSemVer(currentVersion, feature.introducedIn) >= 0 &&
    compareSemVer(currentVersion, feature.newUntilVersion) < 0,
  );

if (!designerReleases.some(release => release.version === designerVersion)) {
  throw new Error(`PXA release metadata is missing version ${designerVersion}.`);
}
