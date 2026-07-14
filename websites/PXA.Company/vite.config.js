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
        productGenerator: resolve(rootDir, 'products/generator.html'),
        productMigration: resolve(rootDir, 'products/migration.html'),
        productImporter: resolve(rootDir, 'products/importer.html'),
        productDesigner: resolve(rootDir, 'products/designer.html'),
        productPdfViewer: resolve(rootDir, 'products/pdf-viewer.html'),
        productSpreadsheet: resolve(rootDir, 'products/spreadsheet.html'),
        pricing: resolve(rootDir, 'pricing.html'),
        about: resolve(rootDir, 'about.html'),
        support: resolve(rootDir, 'support.html'),
        contact: resolve(rootDir, 'contact.html'),
        terms: resolve(rootDir, 'terms.html'),
        privacy: resolve(rootDir, 'privacy.html'),
        license: resolve(rootDir, 'license.html'),
      },
    },
  },
};
