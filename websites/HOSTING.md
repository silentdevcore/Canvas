# PXA Websites Hosting Strategy

## Websites

PXA uses three separate website deployments:

| Website | Source | Build output | Local dev | Local preview | Recommended public host |
| --- | --- | --- | --- | --- | --- |
| `PXA.Company` | `websites/PXA.Company` | `websites/PXA.Company/dist` | `http://localhost:5173/` | `http://localhost:4173/` | `powerdoxautomation.com` |
| `PXA.Documentation` | `websites/PXA.Documentation` | `websites/PXA.Documentation/dist` | `http://localhost:5174/` | `http://localhost:4174/` | `docs.powerdoxautomation.com` |
| `PXA.Demo` | `websites/PXA.Demo` | `websites/PXA.Demo/dist` | `http://localhost:5175/` | `http://localhost:4175/` | `demos.powerdoxautomation.com` |

## Build Commands

Run each build from the site folder:

```bash
cd websites/PXA.Company
npm run build
```

```bash
cd websites/PXA.Documentation
npm run build
```

```bash
cd websites/PXA.Demo
npm run build
```

## Deployment Model

- Deploy each site independently.
- Use each site's `dist` folder as the deployment artifact.
- Keep `PXA.Company`, `PXA.Documentation`, and `PXA.Demo` on separate production hosts or separate hosting projects.
- Keep `websites/shared` as source-only shared styling; it is bundled into each site during build.
- Do not commit generated `dist` output.

## Cross-Site Links

- MVP local links use fixed localhost ports.
- Shared URL config lives in `websites/shared/siteLinks.js`.
- Local development defaults:
  - Company -> `http://localhost:5173/`
  - Documentation -> `http://localhost:5174/`
  - Demo -> `http://localhost:5175/`
- Production defaults:
  - Company -> `https://powerdoxautomation.com/`
  - Documentation -> `https://docs.powerdoxautomation.com/`
  - Demo -> `https://demos.powerdoxautomation.com/`
- Production can override defaults with Vite env vars:
  - `VITE_PXA_COMPANY_URL`
  - `VITE_PXA_DOCUMENTATION_URL`
  - `VITE_PXA_DEMO_URL`
- Company page links use clean paths such as `/products`, `/pricing`, `/about`, `/support`, and `/contact`.
- Production hosting must route those Company paths back to the Company `index.html` entry.

## Pre-Deploy Checklist

- Run all three website builds.
- Run `git diff --check`.
- Check for legacy `Canvas` branding in website and checklist files.
- Smoke-test local dev or preview URLs.
- Confirm Example files under `PXA.Demo/public/examples` are synthetic and safe to publish.

## Post-MVP Hosting Work

- Add CI/build-time env var injection for production URLs.
- Add CI build jobs for all three websites.
- Add link checking for cross-site links and public example files.
- Add real deployment configuration when the hosting target is selected.
