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
    port: 5177,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5086',
        changeOrigin: true,
      },
    },
    fs: {
      allow: ['../..'],
    },
  },
};
