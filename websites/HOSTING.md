# PVADE Websites Hosting Strategy

## Websites

PVADE uses separate website deployments. The table records retained legacy host
defaults, not approved target DNS. The selected target root domain is `pvade.com`;
subdomain, proxy, callback and mail cutover still require coordinated validation.

## Proposed Deployment Targets

The following **unverified** origins follow the
[candidate profile](../deploy/rename/pvade-targets.candidate.json), not a deployed
host inventory. Confirm ownership, DNS, TLS and routing before selecting them.

| Property | Proposed origin (unverified) |
| --- | --- |
| Company | `https://pvade.com` |
| Documentation | `https://docs.pvade.com` |
| Demo | `https://demos.pvade.com` |
| Designer | `https://designer.pvade.com` |
| Account | `https://account.pvade.com` |
| Operator | `https://operator.pvade.com` |

### Current Source Defaults

The existing fallback table below remains accurate for source compatibility.
It is not a recommendation to launch on the legacy domains or evidence that
either the old or proposed hosts are live. This documentation does not change
shared runtime defaults or configure any remote host.

| Website | Source | Build output | Local dev | Local preview | Retained legacy host default |
| --- | --- | --- | --- | --- | --- |
| `PVAD.Company` | `websites/PVAD.Company` | `websites/PVAD.Company/dist` | `http://localhost:5173/` | `http://localhost:4173/` | `powerdoxautomation.com` |
| `PVAD.Documentation` | `websites/PVAD.Documentation` | `websites/PVAD.Documentation/dist` | `http://localhost:5174/` | `http://localhost:4174/` | `docs.powerdoxautomation.com` |
| `PVAD.Demo` | `websites/PVAD.Demo` | `websites/PVAD.Demo/dist` | `http://localhost:5175/` | `http://localhost:4175/` | `demos.powerdoxautomation.com` |
| `PVADE Designer` | `pvad-designer` | app build output | `http://localhost:5176/` | app preview output | `designer.powerdoxautomation.com` |
| `PVAD.Account` | `websites/PVAD.Account` | `websites/PVAD.Account/dist` | `http://localhost:5178/` | `http://localhost:4178/` | `account.powerdoxautomation.com` |

## Build Commands

Run each build from the site folder:

```bash
cd websites/PVAD.Company
npm run build
```

```bash
cd websites/PVAD.Documentation
npm run build
```

```bash
cd websites/PVAD.Demo
npm run build
```

```bash
cd websites/PVAD.Account
npm run build
```

## Deployment Model

- Deploy each site independently.
- Use each site's `dist` folder as the deployment artifact.
- Keep `PVAD.Company`, `PVAD.Documentation`, `PVAD.Demo`, and `PVAD.Account` on separate production hosts or separate hosting projects.
- Keep `websites/shared` as source-only shared styling; it is bundled into each site during build.
- Do not commit generated `dist` output.
- Use the shared production HTTP contract in
  [`deploy/websites`](../deploy/websites/README.md) for Nginx-based deployments.
- Select the application-specific route allowlist from
  `deploy/websites/route-patterns.env.example`; unknown routes must return the
  local status body with HTTP 404 rather than silently becoming HTTP 200.
- Preserve origin status and cache headers at any CDN or ingress layer.

## Cross-Site Links

- MVP local links use fixed localhost ports.
- Shared URL config lives in `websites/shared/siteLinks.js`.
- Local development defaults:
  - Company -> `http://localhost:5173/`
  - Documentation -> `http://localhost:5174/`
  - Demo -> `http://localhost:5175/`
  - Designer -> `http://localhost:5176/`
  - Account -> `http://localhost:5178/`
- Current source production fallbacks (legacy; not verified live):
  - Company -> `https://powerdoxautomation.com/`
  - Documentation -> `https://docs.powerdoxautomation.com/`
  - Demo -> `https://demos.powerdoxautomation.com/`
  - Designer -> `https://designer.powerdoxautomation.com/`
  - Account -> `https://account.powerdoxautomation.com/`
- Production overrides use canonical Vite environment keys:
  - `VITE_PVAD_COMPANY_URL`
  - `VITE_PVAD_DOCUMENTATION_URL`
  - `VITE_PVAD_DEMO_URL`
  - `VITE_PVAD_DESIGNER_URL`
  - `VITE_PVAD_ACCOUNT_URL`
  - `VITE_PVAD_OPERATOR_URL`
- Old `VITE_PXA_*` keys remain compatibility aliases. Matching values are accepted;
  contradictory old/new values fail validation. Configure approved origins at
  build time instead of assuming that this document changes application defaults.
- Company page links use static HTML entries such as `/products.html`, `/pricing.html`, `/about.html`, `/support.html`, and `/contact.html`.
- Company legal links use `/terms.html`, `/privacy.html`, and `/license.html`.
- Company product detail links use nested static entries such as `/products/generator.html` and `/products/pdf-viewer.html`.
- Production hosting must publish each Company HTML entry directly.

## Pre-Deploy Checklist

- Run all public website and Account builds.
- Run `git diff --check`.
- Check for legacy `Canvas` branding in website and checklist files.
- Smoke-test local dev or preview URLs.
- Confirm Example files under `PVAD.Demo/public/examples` are synthetic and safe to publish.

## Post-MVP Hosting Work

- Add CI/build-time env var injection for production URLs.
- Add CI build jobs for all three websites.
- Add link checking for cross-site links and public example files.
- Add real deployment configuration when the hosting target is selected.
