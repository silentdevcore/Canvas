import assert from 'node:assert/strict';
import test from 'node:test';
import { imageMagickInvocation } from './image-tools.mjs';

test('ImageMagick 7 keeps explicit subcommands and conversion arguments unchanged', () => {
  for (const args of [['identify', '-format', '%w %h', 'page.png'],
    ['compare', '-metric', 'AE', 'left.png', 'right.png', '/dev/null'],
    ['page.png', '-resize', '25%', 'output.png']]) {
    assert.deepEqual(imageMagickInvocation(args, command => command === 'magick'),
      { command: 'magick', args });
  }
});

test('ImageMagick 6 uses native operations without changing metrics or image arguments', () => {
  const probe = command => ['convert', 'identify', 'compare'].includes(command);
  for (const operation of ['identify', 'compare', 'convert']) {
    const args = [operation, '-metric', 'AE', 'page with spaces.png'];
    assert.deepEqual(imageMagickInvocation(args, probe), { command: operation, args: args.slice(1) });
  }
  const args = ['left.png', '(', 'right.png', ')', '-compose', 'over', 'overlay.png'];
  assert.deepEqual(imageMagickInvocation(args, probe), { command: 'convert', args });
});

test('missing image tools fail rather than skipping visual verification', () => {
  assert.throws(() => imageMagickInvocation(['compare'], () => false), /requires 'magick' or 'compare'/);
});
