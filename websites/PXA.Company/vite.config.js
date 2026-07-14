import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = dirname(fileURLToPath(import.meta.url));

export default {
  server: {
    fs: {
      allow: ['..'],
    },
  },
  build: {
    rollupOptions: {
      input: {
        index: resolve(rootDir, 'index.html'),
        products: resolve(rootDir, 'products.html'),
        pricing: resolve(rootDir, 'pricing.html'),
        about: resolve(rootDir, 'about.html'),
        support: resolve(rootDir, 'support.html'),
        contact: resolve(rootDir, 'contact.html'),
      },
    },
  },
};
