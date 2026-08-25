import {
  pxaBuildInfoPlugin,
  pxaVersionDefines,
  readPxaBuildInfo,
} from '../shared/vitePxaVersion.js';

const buildInfo = readPxaBuildInfo();

export default {
  define: pxaVersionDefines(buildInfo),
  plugins: [pxaBuildInfoPlugin(buildInfo)],
  server: {
    port: 5178,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5086',
        changeOrigin: true,
      },
      '/connect': {
        target: 'http://localhost:5086',
        changeOrigin: false,
      },
      '/.well-known': {
        target: 'http://localhost:5086',
        changeOrigin: false,
      },
    },
    fs: {
      allow: ['..'],
    },
  },
};
