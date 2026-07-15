# PXA Websites Hosting Strategy

## Websites

PXA uses three separate website deployments:

| Website | Source | Build output | Local dev | Local preview | Recommended public host |
| --- | --- | --- | --- | --- | --- |
| `PXA.Company` | `websites/PXA.Company` | `websites/PXA.Company/dist` | `http://localhost:5173/` | `http://localhost:4173/` | `powerdoxautomation.com` |
| `PXA.Documentation` | `websites/PXA.Documentation` | `websites/PXA.Documentation/dist` | `http://localhost:5174/` | `http://localhost:4174/` | `docs.powerdoxautomation.com` |
| `PXA.Demo` | `websites/PXA.Demo` | `websites/PXA.Demo/dist` | `http://localhost:5175/` | `http://localhost:4175/` | `demos.powerdoxautomation.com` |
| `PXA Designer` | `pxa-designer` | app build output | `http://localhost:5173/` | app preview output | `designer.powerdoxautomation.com` |

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
  - Designer -> `http://localhost:5173/`
- Production defaults:
  - Company -> `https://powerdoxautomation.com/`
  - Documentation -> `https://docs.powerdoxautomation.com/`
  - Demo -> `https://demos.powerdoxautomation.com/`
  - Designer -> `https://designer.powerdoxautomation.com/`
- Production can override defaults with Vite env vars:
  - `VITE_PXA_COMPANY_URL`
  - `VITE_PXA_DOCUMENTATION_URL`
  - `VITE_PXA_DEMO_URL`
  - `VITE_PXA_DESIGNER_URL`
- Company page links use static HTML entries such as `/products.html`, `/pricing.html`, `/about.html`, `/support.html`, and `/contact.html`.
- Company legal links use `/terms.html`, `/privacy.html`, and `/license.html`.
- Company product detail links use nested static entries such as `/products/generator.html` and `/products/pdf-viewer.html`.
- Production hosting must publish each Company HTML entry directly.

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
