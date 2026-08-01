# PXA Legal Snapshot Export

PXA.Company can continue serving the last verified published legal text while
the Legal API is unavailable. Registration and checkout never use this static
copy to authorize a transaction.

Before a production Company deployment, export the effective public documents
from the production API and build the site:

```bash
PXA_LEGAL_API_BASE=https://api.powerdoxautomation.com \
PXA_LEGAL_LOCALE=en \
PXA_LEGAL_AUDIENCE=All \
npm --prefix websites/PXA.Company run build:deployment
```

The exporter validates the schema, content hashes, effective dates, unique
document keys, and non-empty published-document collection. It writes
`public/legal-snapshots/<locale>.json` atomically, and Vite copies that file
into the Company deployment. Generated JSON snapshots are deployment artifacts
and are intentionally ignored by Git.

Generate German and English snapshots separately when both sites or locale
routes are deployed. A failed export must stop the deployment; do not reuse an
unverified file from a build workspace.

For a one-off export, the script also accepts `--api`, `--locale`, `--audience`,
and `--output` arguments through `npm run snapshot:legal -- ...`.
