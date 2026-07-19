# PXA Websites

This folder contains the public-facing PXA web properties, the customer Account portal, and the shared design system.

## Sites

| Site | Purpose | Dev URL |
| --- | --- | --- |
| `PXA.Company` | Marketing, sales, product overview | `http://localhost:5173/` |
| `PXA.Documentation` | Editor and SDK documentation | `http://localhost:5174/` |
| `PXA.Demo` | Interactive demo gallery | `http://localhost:5175/` |
| `PXA Designer` | Live product designer app | `http://localhost:5176/` |
| `PXA.Account` | Customer registration, sign-in, and self-service | `http://localhost:5178/` |

## Shared Design System

Shared styles and static layout templates live in `shared/`.

Each site imports:

```css
@import '../../shared/styles/index.css';
```

## Run Locally

Run each site in a separate terminal:

```bash
cd websites/PXA.Company
npm run dev
```

```bash
cd websites/PXA.Documentation
npm run dev
```

```bash
cd websites/PXA.Demo
npm run dev
```

```bash
cd websites/PXA.Account
npm run dev
```

The local cross-site links currently target the fixed dev ports above.

Each site has a `vite.config.js` that allows imports from `websites/shared` during local development.

Cross-site URLs are centralized in `shared/siteLinks.js`. Local development uses the fixed
ports above. Production builds default to the public domains and may override them with:

- `VITE_PXA_COMPANY_URL`
- `VITE_PXA_DOCUMENTATION_URL`
- `VITE_PXA_DEMO_URL`
- `VITE_PXA_DESIGNER_URL`
- `VITE_PXA_ACCOUNT_URL`

## Build

```bash
cd websites/PXA.Company && npm run build
cd websites/PXA.Documentation && npm run build
cd websites/PXA.Demo && npm run build
cd websites/PXA.Account && npm run build
```

Build output is ignored by Git via `websites/*/dist/`.

## Hosting

Hosting strategy lives in [`HOSTING.md`](HOSTING.md).

Recommended public properties:

- `powerdoxautomation.com`
- `docs.powerdoxautomation.com`
- `demos.powerdoxautomation.com`
- `designer.powerdoxautomation.com`
- `account.powerdoxautomation.com`

Each website should be deployed independently from its own `dist` folder.
