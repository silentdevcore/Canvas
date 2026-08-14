import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const requiredKeys = [
  'terms', 'privacy', 'cookie-storage', 'imprint', 'withdrawal', 'dpa', 'license',
];

export function validateLegalContent(repositoryRoot = root) {
  const directory = join(repositoryRoot, 'product-metadata/legal-documents/en');
  const manifest = JSON.parse(readFileSync(join(directory, 'manifest.json'), 'utf8'));
  const errors = [];

  if (manifest.authoritativeLocale !== 'en')
    errors.push('English must be the authoritative Legal locale.');
  if (manifest.governingLaw !== 'Switzerland')
    errors.push('Switzerland must be the governing-law baseline.');

  const keys = manifest.documents?.map((document) => document.key) ?? [];
  for (const key of requiredKeys) {
    if (!keys.includes(key)) errors.push(`Missing required Legal document '${key}'.`);
  }
  if (new Set(keys).size !== keys.length) errors.push('Legal document keys must be unique.');
  if (keys.length !== requiredKeys.length) errors.push('The initial Legal catalog must contain exactly seven documents.');

  for (const document of manifest.documents ?? []) {
    const source = readFileSync(join(directory, document.file), 'utf8');
    if (!source.startsWith('# ')) errors.push(`${document.file} must start with a level-one heading.`);
    if (source.length < 900) errors.push(`${document.file} is not a substantive Legal document.`);
    if (!/effective date/i.test(source)) errors.push(`${document.file} must describe its effective date.`);
    if (/German (?:text|wording|version) is (?:the )?authoritative/i.test(source))
      errors.push(`${document.file} incorrectly treats German as authoritative.`);
    if (!document.version || !document.audience || typeof document.requiresAcceptance !== 'boolean')
      errors.push(`${document.file} has incomplete publication metadata.`);
  }

  const combined = (manifest.documents ?? [])
    .map((document) => readFileSync(join(directory, document.file), 'utf8'))
    .join('\n');
  for (const placeholder of [
    '[PXA LEGAL ENTITY NAME]',
    '[LEGAL FORM]',
    '[STREET, POSTCODE, CITY, CANTON, SWITZERLAND]',
    '[LEGAL EMAIL ADDRESS]',
  ]) {
    if (!combined.includes(placeholder)) errors.push(`Missing controlled operator placeholder ${placeholder}.`);
  }
  if (!/mandatory (?:consumer|local|law|rights|protections)/i.test(combined))
    errors.push('The global Legal set must preserve mandatory local rights.');

  return { manifest, errors };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = validateLegalContent();
  if (result.errors.length) {
    for (const error of result.errors) console.error(`- ${error}`);
    process.exitCode = 1;
  } else {
    console.log(`Validated ${result.manifest.documents.length} English Swiss-law Legal candidates.`);
  }
}
