# PXA Versioning

`VERSION` is the only authoritative PXA product version.

```bash
node tools/versioning/pxa-version.mjs current
node tools/versioning/pxa-version.mjs check
node tools/versioning/pxa-version.mjs prepare patch
node tools/versioning/pxa-version.mjs prepare minor
node tools/versioning/pxa-version.mjs prepare major
```

After `prepare`, replace every placeholder in
`product-metadata/pxa-releases.json`, list the affected components and add
customer-facing changes. The release PR from `develop` to `main` must carry
exactly one matching `release:patch`, `release:minor` or `release:major` label.

Stable tags and GitHub Releases are created by CI after the PR is merged.
Feature branches target `develop`. Hotfix branches start from `main`, release
at least a patch version and must then be merged back into `develop`.
