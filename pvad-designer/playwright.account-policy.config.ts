import { defineConfig, devices } from '@playwright/test';

// UI acceptance uses explicit synthetic API responses. Actual HTTP/database and
// signature enforcement is tested separately by the account-flows native runner.
export default defineConfig({
  testDir: './e2e', testMatch: 'account-policy-review.spec.ts',
  timeout: 45_000, expect: { timeout: 12_000 }, workers: 1, retries: 0,
  outputDir: '../artifacts/account-policy-review/browser-results',
  reporter: [['list'], ['json', { outputFile: '../artifacts/account-policy-review/browser-results.json' }]],
  use: { trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium-desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'chromium-mobile', use: { ...devices['Pixel 7'] } }],
  webServer: [
    { command: 'npm run dev -- --host 127.0.0.1 --port 5186 --strictPort', url: 'http://127.0.0.1:5186', reuseExistingServer: false },
    { command: 'npm --prefix ../websites/PVAD.Account run dev -- --host 127.0.0.1 --port 5188 --strictPort',
      url: 'http://127.0.0.1:5188/login', reuseExistingServer: false },
  ],
});
