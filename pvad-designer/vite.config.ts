import { resolvePvadDevApiTarget } from "../tools/configuration/environment.mjs";
const apiTarget = resolvePvadDevApiTarget();
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'
import { pvadBuildInfoPlugin, pvadStaticStatusPagesPlugin, readPvadBuildInfo } from '../websites/shared/vitePvadVersion.js'
import { readPvadEnvironment } from '../tools/configuration/environment.mjs'

const buildInfo = readPvadBuildInfo()
const version = buildInfo.productVersion

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react(), pvadBuildInfoPlugin(buildInfo), pvadStaticStatusPagesPlugin('PVADE Designer')],
  define: {
    __PVAD_VERSION__: JSON.stringify(version),
    __PVAD_BUILD_COMMIT__: JSON.stringify(buildInfo.commitId),
    __PVAD_BUILD_TIME__: JSON.stringify(buildInfo.buildTime),
    __PVAD_VISUAL_PARITY_ENABLED__: JSON.stringify(
      process.env.VITE_ENABLE_VISUAL_PARITY === 'true',
    ),
    __PVAD_DOCUMENTATION_URL__: JSON.stringify(
      readPvadEnvironment('VITE_PVAD_DOCUMENTATION_URL') ?? 'http://localhost:5174',
    ),
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      '@/components': path.resolve(__dirname, './src/components'),
      '@/hooks': path.resolve(__dirname, './src/hooks'),
      '@/utils': path.resolve(__dirname, './src/utils'),
      '@/data': path.resolve(__dirname, './src/data'),
      '@/styles': path.resolve(__dirname, './src/styles'),
    },
  },
  server: {
    port: 5176,
    strictPort: true,
    host: true,
    fs: {
      allow: [path.resolve(__dirname, '..')],
    },
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        headers: {
          'X-PVAD-Application': 'designer',
        },
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
