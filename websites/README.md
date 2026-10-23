# PVADE Websites

This folder contains the public-facing PVADE web properties, the customer Account portal, and the shared design system. Application directories use the technical PVAD prefix.

## Sites

| Site | Purpose | Dev URL |
| --- | --- | --- |
| `PVAD.Company` | Marketing, sales, product overview | `http://localhost:5173/` |
| `PVAD.Documentation` | Editor and SDK documentation | `http://localhost:5174/` |
| `PVAD.Demo` | Interactive demo gallery | `http://localhost:5175/` |
| `PVADE Designer` | Live product designer app | `http://localhost:5176/` |
| `PVAD.Account` | Customer registration, sign-in, and self-service | `http://localhost:5178/` |

## Shared Design System

Shared styles and static layout templates live in `shared/`.

Each site imports:

```css
@import '../../shared/styles/index.css';
```

## Run Locally

Run each site in a separate terminal:

```bash
cd websites/PVAD.Company
npm run dev
```

```bash
cd websites/PVAD.Documentation
npm run dev
```

```bash
cd websites/PVAD.Demo
npm run dev
```

```bash
cd websites/PVAD.Account
npm run dev
```

The local cross-site links currently target the fixed dev ports above.

Each site has a `vite.config.js` that allows imports from `websites/shared` during local development.

Cross-site URLs are centralized in `shared/siteLinks.js`. Local development uses the fixed
ports above. Production builds retain the legacy host defaults documented in
`HOSTING.md`; configure approved target hosts with the canonical keys:

- `VITE_PVAD_COMPANY_URL`
- `VITE_PVAD_DOCUMENTATION_URL`
- `VITE_PVAD_DEMO_URL`
- `VITE_PVAD_DESIGNER_URL`
- `VITE_PVAD_ACCOUNT_URL`
- `VITE_PVAD_OPERATOR_URL`

Corresponding `VITE_PXA_*` keys remain compatibility inputs. Matching aliases
are accepted; conflicting canonical/legacy values are rejected. Listing proposed
hosts below does not change the runtime defaults or authorize a DNS cutover.

## Build

```bash
cd websites/PVAD.Company && npm run build
cd websites/PVAD.Documentation && npm run build
cd websites/PVAD.Demo && npm run build
cd websites/PVAD.Account && npm run build
```

Build output is ignored by Git via `websites/*/dist/`.

## Hosting

Hosting strategy lives in [`HOSTING.md`](HOSTING.md).

Proposed target properties for the selected PVADE domain, not verified live
deployments. Retained runtime defaults are documented separately in `HOSTING.md`:

- `pvade.com`
- `docs.pvade.com`
- `demos.pvade.com`
- `designer.pvade.com`
- `account.pvade.com`

Each website should be deployed independently from its own `dist` folder.
