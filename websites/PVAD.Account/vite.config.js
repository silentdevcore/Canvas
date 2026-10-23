import { resolvePvadDevApiTarget } from "../../tools/configuration/environment.mjs";
const apiTarget = resolvePvadDevApiTarget();
import {
  pvadBuildInfoPlugin,
  pvadStaticStatusPagesPlugin,
  pvadVersionDefines,
  readPvadBuildInfo,
} from '../shared/vitePvadVersion.js';

const buildInfo = readPvadBuildInfo();

export default {
  define: pvadVersionDefines(buildInfo),
  plugins: [pvadBuildInfoPlugin(buildInfo), pvadStaticStatusPagesPlugin('PVADE Account')],
  server: {
    port: 5178,
    strictPort: true,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
      '/connect': {
        target: apiTarget,
        changeOrigin: false,
      },
      '/.well-known': {
        target: apiTarget,
        changeOrigin: false,
      },
    },
    fs: {
      allow: ['..', '../../tools/configuration'],
    },
  },
};
