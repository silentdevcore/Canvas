import { resolvePvadDevApiTarget } from "../../tools/configuration/environment.mjs";
import { publicCompatibilityRegisterPlugin } from '../shared/compatibilityRegister.js';
import { publicGuidesPlugin } from './publicGuides.js';
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
  plugins: [pvadBuildInfoPlugin(buildInfo), pvadStaticStatusPagesPlugin('PVADE Documentation'), publicGuidesPlugin(), publicCompatibilityRegisterPlugin()],
  server: {
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
