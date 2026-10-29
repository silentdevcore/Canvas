const { TextEncoder, TextDecoder } = require('node:util');

// jsdom omits these browser APIs; use Node's real UTF-8 implementations.
globalThis.TextEncoder ??= TextEncoder;
globalThis.TextDecoder ??= TextDecoder;
