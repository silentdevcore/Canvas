import { defineConfig, devices } from '@playwright/test';

const applications = ['Company', 'Documentation', 'Demo', 'Designer', 'Admin', 'Account'];
const links = Object.fromEntries(applications.filter(name => name !== 'Admin').map(name => [
  `VITE_PVAD_${name.toUpperCase()}_URL`, `http://localhost:${15173 + applications.indexOf(name)}/`,
]));
const env = { ...links, PVAD_DEV_API_URL: 'http://127.0.0.1:15086' };

export default defineConfig({
  testDir: './e2e', testMatch: /rename-hosts\.spec\.ts/, workers: 1, retries: 0,
  timeout: 60_000, expect: { timeout: 15_000 }, reporter: [['list']],
  outputDir: '../artifacts/rename/frontend-hosts',
  use: { ...devices['Desktop Chrome'], trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  webServer: [
    { command: 'node ../tools/configuration/frontend-smoke-api.mjs',
      env: { PVAD_ALLOW_FRONTEND_SMOKE: 'true' }, url: 'http://127.0.0.1:15086/health/ready', reuseExistingServer: false },
    ...applications.map((name, index) => {
      const path = name === 'Designer' ? '.' : `../websites/PVAD.${name}`;
      return { command: `npm --prefix ${path} run build && npm --prefix ${path} run preview -- --port ${15173 + index} --strictPort`,
        env, url: `http://localhost:${15173 + index}/`, reuseExistingServer: false, timeout: 120_000 };
    }),
  ],
});
