# PVADE Websites Hosting Strategy

## Websites

PVADE uses separate website deployments. Current source link defaults use the
selected target root domain `pvade.com`; they are not proof of approved DNS.
Subdomain, proxy, callback and mail cutover still require coordinated validation.

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

The fallback table below describes the shared production link configuration.
It does not establish that any host is live. Existing deployments can override
these defaults explicitly; this source change does not configure remote hosts.

| Website | Source | Build output | Local dev | Local preview | Production link default (unverified) |
| --- | --- | --- | --- | --- | --- |
| `PVAD.Company` | `websites/PVAD.Company` | `websites/PVAD.Company/dist` | `http://localhost:5173/` | `http://localhost:4173/` | `pvade.com` |
| `PVAD.Documentation` | `websites/PVAD.Documentation` | `websites/PVAD.Documentation/dist` | `http://localhost:5174/` | `http://localhost:4174/` | `docs.pvade.com` |
| `PVAD.Demo` | `websites/PVAD.Demo` | `websites/PVAD.Demo/dist` | `http://localhost:5175/` | `http://localhost:4175/` | `demos.pvade.com` |
| `PVADE Designer` | `pvad-designer` | app build output | `http://localhost:5176/` | app preview output | `designer.pvade.com` |
| `PVAD.Account` | `websites/PVAD.Account` | `websites/PVAD.Account/dist` | `http://localhost:5178/` | `http://localhost:4178/` | `account.pvade.com` |

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
- Current source production fallbacks (not verified live):
  - Company -> `https://pvade.com/`
  - Documentation -> `https://docs.pvade.com/`
  - Demo -> `https://demos.pvade.com/`
  - Designer -> `https://designer.pvade.com/`
  - Account -> `https://account.pvade.com/`
  - Operator -> `https://operator.pvade.com/`
- Production overrides use canonical Vite environment keys:
  - `VITE_PVAD_COMPANY_URL`
  - `VITE_PVAD_DOCUMENTATION_URL`
  - `VITE_PVAD_DEMO_URL`
  - `VITE_PVAD_DESIGNER_URL`
  - `VITE_PVAD_ACCOUNT_URL`
  - `VITE_PVAD_OPERATOR_URL`
- Old `VITE_PXA_*` keys remain compatibility aliases. Matching values are accepted;
  contradictory old/new values fail validation. Configure approved origins at
  build time. Legacy deployments must explicitly set their installed origins;
  old domains are not automatically added to the frontend return-URL allowlist.
- Before deploying the new domains, configure backend registration return origins,
  Designer authorization origins, OAuth callbacks, public URLs and proxy routing
  together. Backend authentication allowlists now use the same exact product origins;
  they do not include Admin, Operator, wildcards or a second legacy trust set.
  The selected technical sender is `no-reply@pvade.com`, separately from
  `support@pvade.com`. Mail transport remains disabled until SMTP/provider and
  domain verification are complete. Do not broaden allowlists with wildcard domains.
- Backend registration defaults retain the five local product origins. Designer
  authorization retains local ports 5173 and 5176 plus the canonical Designer
  HTTPS origin. Frontend return links and backend handoff origins serve different
  purposes; CORS and per-client OAuth callback registrations remain separate.
- Existing installations must explicitly override approved origins through
  `Registration:AllowedReturnOrigins` and `DesignerAuthentication:AllowedOrigins`.
  .NET configuration arrays merge by index across providers: a shorter overlay
  does not remove trailing defaults. Verify the complete effective arrays before
  deployment. For the existing legacy domain layout, these explicit replacements
  preserve local entries without implicitly trusting both generations:

  ```text
  Registration__AllowedReturnOrigins__5=https://powerdoxautomation.com
  Registration__AllowedReturnOrigins__6=https://docs.powerdoxautomation.com
  Registration__AllowedReturnOrigins__7=https://demos.powerdoxautomation.com
  Registration__AllowedReturnOrigins__8=https://designer.powerdoxautomation.com
  Registration__AllowedReturnOrigins__9=https://account.powerdoxautomation.com
  DesignerAuthentication__AllowedOrigins__2=https://designer.powerdoxautomation.com
  ```

  These are compatibility examples only, not proof of domain ownership. On-Premise
  owners must use their actual reviewed origins and inspect merged configuration.
- The Designer's default Account destination follows the new Account origin;
  existing and On-Premise installations can override it through runtime config
  `__PVAD_CONFIG__.accountUrl` (legacy `__PXA_CONFIG__` remains supported).
- Storage Notice cookie scope is unchanged: the new hosts use host-only notices;
  this change does not share authentication cookies across subdomains.
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
