import { pxaVersionDefines } from '../shared/vitePxaVersion.js';

export default {
  define: pxaVersionDefines(),
  server: {
    port: 5177,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5086',
        changeOrigin: true,
      },
    },
    fs: {
      allow: ['..'],
    },
  },
};
