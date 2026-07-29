# PXA Versioning

`VERSION` is the only authoritative PXA product version.

```bash
node tools/versioning/pxa-version.mjs current
node tools/versioning/pxa-version.mjs check
node tools/versioning/pxa-version.mjs fragments
node tools/versioning/pxa-version.mjs validate-change --base-ref origin/develop
node tools/versioning/pxa-version.mjs prepare patch
node tools/versioning/pxa-version.mjs prepare minor
node tools/versioning/pxa-version.mjs prepare major
node tools/versioning/pxa-version.mjs prepare-fragments --dry-run \
  --summary "Customer-facing release summary." \
  --format markdown
node tools/versioning/pxa-version.mjs prepare-fragments \
  --summary "Customer-facing release summary."
```

Every pull request to `develop` adds or updates one structured JSON file in
`product-metadata/release-fragments/`. The fragment records its Semantic Version
impact, affected components, change category, customer-facing summary, and
optional feature or Documentation references. Use the `pxa-release-author`
skill to review a change and draft the fragment.

`prepare-fragments` calculates the highest pending impact, creates the complete
shared release entry, synchronizes all version files, and consumes the pending
fragments. A Major release additionally requires `--confirm-major`.
With `--dry-run`, the command calculates the same release and renders JSON or
Markdown without changing `VERSION`, the release manifest, or pending fragments.

After `prepare`, replace every placeholder in
`product-metadata/pxa-releases.json`, list the affected components and add
customer-facing changes. The release PR from `develop` to `main` must carry
exactly one matching `release:patch`, `release:minor` or `release:major` label.

Stable tags and GitHub Releases are created by CI after the PR is merged.
Feature branches target `develop`. Hotfix branches start from `main`, release
at least a patch version and must then be merged back into `develop`.
