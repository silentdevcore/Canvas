import {
  pxaBuildInfoPlugin,
  pxaStaticStatusPagesPlugin,
  pxaVersionDefines,
  readPxaBuildInfo,
} from '../shared/vitePxaVersion.js';

const buildInfo = readPxaBuildInfo();

export default {
  define: pxaVersionDefines(buildInfo),
  plugins: [pxaBuildInfoPlugin(buildInfo), pxaStaticStatusPagesPlugin('PXA.Demo')],
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
