import { resolvePvadDevApiTarget } from "../../tools/configuration/environment.mjs";
import { publicCompatibilityRegisterPlugin } from '../shared/compatibilityRegister.js';
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
  plugins: [pvadBuildInfoPlugin(buildInfo), pvadStaticStatusPagesPlugin('PVADE Admin'), publicCompatibilityRegisterPlugin()],
  server: {
    port: 5177,
    strictPort: true,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
    fs: {
      allow: ['../..'],
    },
  },
};
