import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  testMatch: /(?:pdf-viewer-trust-authenticated|designer-library-capture)\.spec\.ts/,
  grep: /(?:Viewer UI completes PDF Trust workflow|Designer UI captures favorites)/,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  workers: 1,
  retries: 0,
  maxFailures: 1,
  reporter: [['list']],
  outputDir: '../artifacts/rename/viewer-capture-test-results',
  use: { trace: 'retain-on-failure', screenshot: 'only-on-failure', actionTimeout: 15_000 },
  projects: [
    { name: 'viewer-desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 1000 } } },
    { name: 'viewer-mobile', testMatch: /pdf-viewer-trust-authenticated\.spec\.ts/, use: { ...devices['Pixel 5'] } },
  ],
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 16176 --strictPort',
    url: 'http://127.0.0.1:16176',
    reuseExistingServer: false,
    timeout: 60_000,
  },
});
