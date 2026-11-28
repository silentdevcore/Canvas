import { readFileSync, statSync } from 'node:fs';
import { isAbsolute, resolve, win32 } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { validateTrust } from './pvad-native-licensing.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const emptyGuid = '00000000-0000-0000-0000-000000000000';
const guid = value => typeof value === 'string' && /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(value) && value !== emptyGuid;
const text = value => typeof value === 'string' && value.trim().length > 0 && value.length <= 512;
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const exactKeys = (value, keys) => object(value) && Object.keys(value).sort().join('|') === [...keys].sort().join('|');
const optionalText = value => value === null || text(value);
const evidenceKeys = ['publicTrustReview', 'keyMountReview', 'backupRecoveryDrill', 'databaseMigrationReview',
  'localAcceptance', 'distributionReview', 'releaseApproval'];
const readinessKeys = ['hostingProvider', 'secretStorage', 'keyCustodian', 'backupCustodian', 'recoveryReviewer'];

// Static, read-only preparation check. Never opens PrivateKeyPath, generates a
// key, connects to a service, writes configuration, or claims deployment approval.
export function inspectManufacturer(config, readiness, trust, mode = 'prepare') {
  const errors = new Set(), pending = new Set();
  const require = (condition, code) => { if (!condition) errors.add(code); };
  const need = (condition, code) => { if (!condition) pending.add(code); };
  require(mode === 'prepare' || mode === 'deployment', 'MODE_INVALID');
  require(exactKeys(config, ['NativeLicenseIssuer', 'NativeLicenseAutomation']), 'CONFIG_SHAPE_INVALID');
  const issuer = config?.NativeLicenseIssuer;
  const automation = config?.NativeLicenseAutomation;
  require(exactKeys(issuer, ['Enabled', 'KeyId', 'PrivateKeyPath', 'AllowedUserIds']), 'ISSUER_SHAPE_INVALID');
  require(exactKeys(automation, ['Enabled', 'FreeRenewalEnabled', 'IssuerUserId', 'PolicyVersion', 'Model',
    'ValidityMonths', 'ValidityDays', 'UpdatesUntil']), 'AUTOMATION_SHAPE_INVALID');
  if (object(issuer) && object(automation)) {
    require(typeof issuer.Enabled === 'boolean' && typeof automation.Enabled === 'boolean', 'ENABLE_FLAGS_INVALID');
    require(automation.FreeRenewalEnabled === true && automation.Model === 'Subscription' && automation.ValidityMonths === 3 &&
      automation.ValidityDays === null && automation.UpdatesUntil === null, 'FREE_TERMS_INVALID');
    require(typeof automation.PolicyVersion === 'string' && /^[a-z0-9][a-z0-9.-]{0,63}$/.test(automation.PolicyVersion), 'POLICY_VERSION_INVALID');
    require(automation.IssuerUserId === emptyGuid || guid(automation.IssuerUserId), 'ISSUER_ID_INVALID');
    require(optionalText(issuer.KeyId) && (issuer.KeyId === null || issuer.KeyId.length <= 100), 'KEY_ID_INVALID');
    require(optionalText(issuer.PrivateKeyPath) && (issuer.PrivateKeyPath === null ||
      isAbsolute(issuer.PrivateKeyPath) || win32.isAbsolute(issuer.PrivateKeyPath)), 'KEY_PATH_INVALID');
    const ids = issuer.AllowedUserIds;
    require(Array.isArray(ids) && ids.length <= 128 && ids.every(guid) && new Set(ids.map(id => id.toLowerCase())).size === ids.length,
      'ALLOWLIST_INVALID');
    need(guid(automation.IssuerUserId), 'ISSUER_ID_REQUIRED');
    need(Array.isArray(ids) && guid(automation.IssuerUserId) && ids.some(id => typeof id === 'string' &&
      id.toLowerCase() === automation.IssuerUserId.toLowerCase()), 'ISSUER_ALLOWLIST_MATCH_REQUIRED');
    need(text(issuer.KeyId), 'KEY_ID_REQUIRED');
    need(text(issuer.PrivateKeyPath), 'PRIVATE_KEY_MOUNT_PATH_REQUIRED');
    if (mode === 'prepare') require(issuer.Enabled === false && automation.Enabled === false, 'PREPARATION_MUST_BE_DISABLED');
    if (mode === 'deployment') require(issuer.Enabled === true && automation.Enabled === true, 'DEPLOYMENT_FLAGS_DISABLED');
  }
  require(exactKeys(readiness, ['schemaVersion', 'manufacturerName', ...readinessKeys, 'evidence']) && readiness?.schemaVersion === 1 &&
    readiness?.manufacturerName === 'PVAD', 'READINESS_SHAPE_INVALID');
  for (const key of readinessKeys) {
    require(optionalText(readiness?.[key]), 'READINESS_VALUE_INVALID');
    need(text(readiness?.[key]), `${key.toUpperCase()}_REQUIRED`);
  }
  require(exactKeys(readiness?.evidence, evidenceKeys), 'EVIDENCE_SHAPE_INVALID');
  for (const key of evidenceKeys) {
    require(optionalText(readiness?.evidence?.[key]), 'EVIDENCE_VALUE_INVALID');
    need(text(readiness?.evidence?.[key]), `${key.toUpperCase()}_REQUIRED`);
  }
  // Reject PEM material in metadata without ever printing the supplied content.
  require(!/-----BEGIN /i.test(JSON.stringify([config, readiness])), 'KEY_MATERIAL_FORBIDDEN');
  try {
    validateTrust(trust);
    need(trust.keys.some(key => key.keyId === issuer?.KeyId), 'EMBEDDED_PUBLIC_KEY_REQUIRED');
    require(!trust.keys.some(key => key.keyId === issuer?.KeyId && /^(ephemeral-|synthetic-)/i.test(key.keyId)), 'TEST_KEY_FORBIDDEN');
  } catch { errors.add('PUBLIC_TRUST_INVALID'); }
  const staticChecksPassed = errors.size === 0 && (mode === 'prepare' || pending.size === 0);
  return { schemaVersion: 1, mode, staticChecksPassed, readyForLiveChecks: mode === 'deployment' && staticChecksPassed,
    productionReady: false, liveVerificationRequired: true, errors: [...errors].sort(), pending: [...pending].sort() };
}

function readJson(path) {
  if (!statSync(path).isFile() || statSync(path).size > 1024 * 1024) throw new Error();
  return JSON.parse(readFileSync(path, 'utf8'));
}

export function runPreflight(args) {
  const flags = {};
  if (args.length % 2 !== 0) throw new Error();
  for (let i = 0; i < args.length; i += 2) {
    if (!['--mode', '--config', '--readiness', '--trust'].includes(args[i]) || Object.hasOwn(flags, args[i])) throw new Error();
    flags[args[i]] = args[i + 1];
  }
  if (flags['--mode'] && !['prepare', 'deployment'].includes(flags['--mode'])) throw new Error();
  return inspectManufacturer(
    readJson(resolve(flags['--config'] ?? resolve(root, 'PVAD.WebApi/configuration/native-manufacturer-free-start.example.json'))),
    readJson(resolve(flags['--readiness'] ?? resolve(root, 'operator-docs/native-manufacturer-readiness.example.json'))),
    readJson(resolve(flags['--trust'] ?? resolve(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json'))), flags['--mode'] ?? 'prepare');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const result = runPreflight(process.argv.slice(2));
    console.log(JSON.stringify(result, null, 2));
    process.exitCode = result.staticChecksPassed ? 0 : 1;
  } catch {
    console.error(JSON.stringify({ code: 'INPUT_INVALID', productionReady: false,
      usage: 'manufacturer-preflight.mjs [--mode prepare|deployment] [--config JSON] [--readiness JSON] [--trust JSON]' }));
    process.exitCode = 1;
  }
}
