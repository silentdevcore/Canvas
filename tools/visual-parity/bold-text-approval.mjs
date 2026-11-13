import { createHash } from 'node:crypto';
import { spawnImageMagick } from './image-tools.mjs';

export function boldTextPixelHash(file) {
  const dimensions = spawnImageMagick(['identify', '-format', '%w %h', file], { encoding: 'utf8', timeout: 30_000 });
  const pixels = spawnImageMagick([file, '-alpha', 'on', '-depth', '16', '-endian', 'MSB', 'RGBA:-'],
    { timeout: 30_000, maxBuffer: 8 * 1024 * 1024 });
  const size = dimensions.stdout?.trim();
  if (dimensions.status !== 0 || pixels.status !== 0 || !/^\d+ \d+$/.test(size ?? ''))
    throw new Error('Cannot decode exact review image pixels.');
  const [width, height] = size.split(' ').map(Number);
  if (width < 1 || height < 1 || pixels.stdout.length !== width * height * 8)
    throw new Error('Incomplete review image pixel data.');
  return createHash('sha256').update(`${size} RGBA16BE\n`).update(pixels.stdout).digest('hex');
}

export function matchesBoldTextApproval(approval, evidence) {
  if (approval?.schemaVersion !== 1 || approval.status !== 'approved' || !approval.approvedAt || !approval.approvedBy
    || !Array.isArray(approval.cases) || !Array.isArray(evidence?.cases)) return false;
  for (const key of ['platform', 'browserVersion', 'rasterPixelsPerPoint', 'fontHash'])
    if (!approval[key] || approval[key] !== evidence[key]) return false;
  if (!approval.cases.length || approval.cases.length !== evidence.cases.length
    || new Set(approval.cases.map(entry => entry.id)).size !== approval.cases.length
    || new Set(evidence.cases.map(entry => entry.id)).size !== evidence.cases.length) return false;
  return approval.cases.every(expected => {
    const actual = evidence.cases.find(entry => entry.id === expected.id);
    return actual && ['fixture', 'fixtureSha256', 'comparisonPixelSha256']
      .every(key => expected[key] && expected[key] === actual[key]);
  });
}
