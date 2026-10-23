import { execFileSync } from 'node:child_process';

export function parseBatch(bytes) {
  const blobs = new Map();
  let offset = 0;
  while (offset < bytes.length) {
    const end = bytes.indexOf(10, offset);
    if (end < 0) throw new Error('Truncated Git batch header');
    const match = /^((?:[a-f0-9]{40}|[a-f0-9]{64})) blob (\d+)$/.exec(bytes.subarray(offset, end).toString());
    if (!match) throw new Error('Unexpected Git batch record');
    const size = Number(match[2]);
    const start = end + 1;
    if (!Number.isSafeInteger(size) || start + size >= bytes.length || bytes[start + size] !== 10)
      throw new Error('Truncated Git batch payload');
    blobs.set(match[1], bytes.subarray(start, start + size));
    offset = start + size + 1;
  }
  return blobs;
}

export function readGitBlobs(root, ids) {
  const unique = [...new Set(ids)];
  if (!unique.every(id => /^(?:[a-f0-9]{40}|[a-f0-9]{64})$/.test(id)))
    throw new Error('Invalid Git blob identity');
  if (unique.length === 0) return new Map();
  return parseBatch(execFileSync('git', ['cat-file', '--batch'], {
    cwd: root, input: `${unique.join('\n')}\n`, maxBuffer: 256 * 1024 * 1024,
  }));
}
