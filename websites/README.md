# PXA Websites

This folder contains the three public-facing PXA web properties and the shared design system.

## Sites

| Site | Purpose | Dev URL |
| --- | --- | --- |
| `PXA.Company` | Marketing, sales, product overview | `http://localhost:5173/` |
| `PXA.Documentation` | Editor and SDK documentation | `http://localhost:5174/` |
| `PXA.Demo` | Interactive demo gallery | `http://localhost:5175/` |

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

The local cross-site links currently target the fixed dev ports above.

Each site has a `vite.config.js` that allows imports from `websites/shared` during local development.

## Build

```bash
cd websites/PXA.Company && npm run build
cd websites/PXA.Documentation && npm run build
cd websites/PXA.Demo && npm run build
```

Build output is ignored by Git via `websites/*/dist/`.
