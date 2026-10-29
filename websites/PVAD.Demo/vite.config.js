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
  plugins: [pvadBuildInfoPlugin(buildInfo), pvadStaticStatusPagesPlugin('PVADE Demo')],
  server: {
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
    fs: {
      allow: ['..', '../../tools/configuration'],
    },
  },
};
