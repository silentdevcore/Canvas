import { mkdir, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, '..', '..');

export function validateLegalSnapshot(snapshot) {
  if (!snapshot || snapshot.schemaVersion !== 1)
    throw new Error('Legal snapshot schemaVersion must be 1.');
  if (!Number.isFinite(Date.parse(snapshot.generatedAt)))
    throw new Error('Legal snapshot generatedAt must be an ISO timestamp.');
  if (!['de', 'en'].includes(snapshot.locale))
    throw new Error('Legal snapshot locale must be de or en.');
  if (typeof snapshot.audience !== 'string' || !snapshot.audience)
    throw new Error('Legal snapshot audience is required.');
  if (!Array.isArray(snapshot.documents) || snapshot.documents.length === 0)
    throw new Error('Legal snapshot must contain at least one published document.');

  const keys = new Set();
  for (const document of snapshot.documents) {
    if (!document || typeof document.key !== 'string' || !document.key)
      throw new Error('Every legal snapshot document requires a key.');
    if (keys.has(document.key))
      throw new Error(`Legal snapshot contains duplicate key "${document.key}".`);
    keys.add(document.key);
    if (typeof document.version !== 'string' || !document.version)
      throw new Error(`Legal snapshot document "${document.key}" requires a version.`);
    if (!/^[a-f0-9]{64}$/.test(document.contentHash ?? ''))
      throw new Error(`Legal snapshot document "${document.key}" has an invalid content hash.`);
    if (typeof document.renderedHtml !== 'string' || !document.renderedHtml)
      throw new Error(`Legal snapshot document "${document.key}" has no rendered content.`);
    const effectiveAt = Date.parse(document.effectiveAt);
    if (!Number.isFinite(effectiveAt) || effectiveAt > Date.parse(snapshot.generatedAt))
      throw new Error(`Legal snapshot document "${document.key}" has an invalid effective date.`);
  }

  snapshot.documents.sort((left, right) => left.key.localeCompare(right.key));
  return snapshot;
}

export async function exportLegalSnapshot({
  apiBase,
  locale,
  audience,
  output,
  fetchImpl = fetch,
}) {
  const endpoint = new URL('/api/pxa/v1/legal/snapshot', apiBase);
  endpoint.searchParams.set('locale', locale);
  endpoint.searchParams.set('audience', audience);
  const response = await fetchImpl(endpoint, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok)
    throw new Error(`Legal snapshot API returned HTTP ${response.status}.`);
  const snapshot = validateLegalSnapshot(await response.json());
  const destination = resolve(output);
  const temporary = `${destination}.tmp`;
  await mkdir(dirname(destination), { recursive: true });
  try {
    await writeFile(temporary, `${JSON.stringify(snapshot, null, 2)}\n`, {
      encoding: 'utf8',
      mode: 0o644,
    });
    await rename(temporary, destination);
  } catch (error) {
    await rm(temporary, { force: true });
    throw error;
  }
  return { destination, documents: snapshot.documents.length };
}

function parseArguments(arguments_) {
  const values = new Map();
  for (let index = 0; index < arguments_.length; index += 2) {
    const name = arguments_[index];
    const value = arguments_[index + 1];
    if (!name?.startsWith('--') || !value)
      throw new Error(`Invalid argument near "${name ?? ''}".`);
    values.set(name.slice(2), value);
  }
  return {
    apiBase: values.get('api') ??
      process.env.PXA_LEGAL_API_BASE ??
      'http://localhost:5086',
    locale: values.get('locale') ??
      process.env.PXA_LEGAL_LOCALE ??
      'en',
    audience: values.get('audience') ??
      process.env.PXA_LEGAL_AUDIENCE ??
      'All',
    output: values.get('output') ??
      resolve(
        repositoryRoot,
        `websites/PXA.Company/public/legal-snapshots/${
          values.get('locale') ?? process.env.PXA_LEGAL_LOCALE ?? 'en'
        }.json`),
  };
}

const isMain = process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  try {
    const result = await exportLegalSnapshot(parseArguments(process.argv.slice(2)));
    process.stdout.write(
      `Exported ${result.documents} legal documents to ${result.destination}.\n`);
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  }
}
