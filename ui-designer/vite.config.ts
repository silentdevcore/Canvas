import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    // Enable code splitting
    rollupOptions: {
      output: {
        manualChunks: (id) => {
          // Separate vendor chunks for better caching
          if (id.includes('node_modules')) {
            if (id.includes('react') || id.includes('react-dom')) {
              return 'vendor';
            }
            if (id.includes('@dnd-kit')) {
              return 'dnd';
            }
            if (id.includes('zustand')) {
              return 'ui';
            }
            return 'vendor';
          }
        },
      },
    },
    // Enable tree shaking
    minify: 'terser',
    terserOptions: {
      compress: {
        drop_console: true,
        drop_debugger: true,
      },
      mangle: true,
    },
    // Optimize chunk size
    chunkSizeWarningLimit: 600,
  },
  // Enable source maps for debugging
  sourcemap: false,
  // Optimize dependencies
  optimizeDeps: {
    include: ['react', 'react-dom', '@dnd-kit/core', 'zustand'],
  },
})
