import path from 'node:path';

export function trustBaselinePath(root, name, platform = process.platform) {
  if (!['visual-baseline', 'operation-visual-baseline'].includes(name)) {
    throw new Error(`Unknown PDF Trust baseline: ${name}`);
  }
  if (platform !== 'darwin' && platform !== 'linux') {
    throw new Error(`No reviewed PDF Trust visual baseline for platform '${platform}'.`);
  }
  return path.join(root, 'test-assets', 'pdf-trust', `${name}${platform === 'linux' ? '.linux' : ''}.json`);
}
