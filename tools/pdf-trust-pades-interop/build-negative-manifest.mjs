import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { resolvePadesEvidence } from './corpus-contract.mjs';

const directory = path.resolve(process.argv[2] ?? 'artifacts/pdf-trust-pades-interop');
const cases = [
  ['pvad-pades-revoked', 'PAdES-BASELINE-T', 'REVOKED_NO_POE'],
  ['pvad-pades-untrusted', 'PAdES-BASELINE-T', 'NO_CERTIFICATE_CHAIN_FOUND'],
  ['pvad-pades-revocation-unavailable', 'PAdES-BASELINE-T', 'CERTIFICATE_CHAIN_GENERAL_FAILURE'],
  ['pvad-pades-expired', 'PAdES-BASELINE-B', 'OUT_OF_BOUNDS_NOT_REVOKED'],
  ['pvad-pades-tampered-lt', 'PAdES-BASELINE-T', 'CERTIFICATE_CHAIN_GENERAL_FAILURE'],
  ['pvad-pades-tampered-lta', 'PAdES-BASELINE-LTA', 'SIG_CRYPTO_FAILURE'],
];

function describe(file) {
  const bytes = fs.readFileSync(path.join(directory, file));
  return {
    file,
    sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
    fileSizeBytes: bytes.length,
  };
}

const entries = cases.map(([id, expectedProfile, expectedSubIndication]) => {
  const [pdfFile, simpleFile, detailedFile, diagnosticFile] = resolvePadesEvidence(directory, id,
    ['.pdf', '-dss-simple.xml', '-dss-detailed.xml', '-dss-diagnostic.xml']);
  const simple = describe(simpleFile);
  const simpleXml = fs.readFileSync(path.join(directory, simple.file), 'utf8');
  if (!simpleXml.includes(`SignatureFormat="${expectedProfile}"`) ||
      !simpleXml.includes(`<SubIndication>${expectedSubIndication}</SubIndication>`)) {
    throw new Error(`${id} does not contain its expected DSS classification.`);
  }
  return {
    id,
    expectedProfile,
    expectedSubIndication,
    pdf: describe(pdfFile),
    reports: {
      simple,
      detailed: describe(detailedFile),
      diagnostic: describe(diagnosticFile),
    },
  };
});

const manifest = {
  schemaVersion: 1,
  description: 'Synthetic PVAD PAdES failure fixtures independently classified with EU DSS 6.4.',
  validator: {
    name: 'EU Digital Signature Services',
    version: '6.4',
    policy: 'QES AES/QC AES TL based',
  },
  execution: {
    network: 'disabled',
    filesystem: 'read-only',
  },
  trustEvidence: [
    describe('root-trust-anchor.cer'),
    describe('root-current.crl'),
    describe('root-signer-revoked.crl'),
    describe('untrusted-root.cer'),
    describe('expired-root.cer'),
    describe('expired-root-current.crl'),
  ],
  entries,
};

fs.writeFileSync(
  path.join(directory, 'negative-corpus.json'),
  `${JSON.stringify(manifest, null, 2)}\n`,
  'utf8',
);
