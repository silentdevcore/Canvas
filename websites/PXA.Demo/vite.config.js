import { pxaVersionDefines } from '../shared/vitePxaVersion.js';

export default {
  define: pxaVersionDefines(),
  server: {
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
