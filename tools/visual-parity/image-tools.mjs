import { spawnSync } from 'node:child_process';

const available = new Map();
function hasTool(command) {
  if (!available.has(command)) {
    available.set(command, spawnSync(command, ['-version'], { stdio: 'ignore' }).status === 0);
  }
  return available.get(command);
}

export function imageMagickInvocation(args, probe = hasTool) {
  if (probe('magick')) return { command: 'magick', args };
  const operation = ['identify', 'compare', 'convert'].includes(args[0]) ? args[0] : 'convert';
  if (!probe(operation)) throw new Error(`ImageMagick requires 'magick' or '${operation}' on PATH.`);
  return { command: operation, args: args[0] === operation ? args.slice(1) : args };
}

export function spawnImageMagick(args, options) {
  const invocation = imageMagickInvocation(args);
  return spawnSync(invocation.command, invocation.args, options);
}

export function requireImageMagick() {
  for (const operation of ['convert', 'identify', 'compare']) imageMagickInvocation([operation]);
}
