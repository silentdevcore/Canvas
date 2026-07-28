import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'
import { execFileSync } from 'node:child_process'
import packageJson from './package.json'

const commit = (() => {
  try {
    return execFileSync('git', ['rev-parse', '--short=12', 'HEAD'], {
      cwd: path.resolve(__dirname, '..'),
      encoding: 'utf8',
    }).trim()
  } catch {
    return 'unknown'
  }
})()
const buildTime = new Date().toISOString()

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    __PXA_DESIGNER_VERSION__: JSON.stringify(packageJson.version),
    __PXA_DESIGNER_COMMIT__: JSON.stringify(commit),
    __PXA_DESIGNER_BUILD_TIME__: JSON.stringify(buildTime),
    __PXA_DOCUMENTATION_URL__: JSON.stringify(
      process.env.VITE_PXA_DOCUMENTATION_URL ?? 'http://localhost:5174',
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
    proxy: {
      '/api': {
        target: 'http://localhost:5086',
        changeOrigin: true,
        headers: {
          'X-PXA-Application': 'designer',
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
