## Summary

Describe the user-visible result and the reason for the change.

## Validation

List the tests, builds, and manual checks performed.

## Release Impact

- Impact: `none`, `patch`, `minor`, or `major`
- Components:
- Release fragment: `product-metadata/release-fragments/<id>.json`
- Documentation:

Every pull request to `develop` must add or update a validated release fragment.
Use impact `none` with a reason for internal-only changes. Stable pull requests
from `develop` to `main` use exactly one matching `release:*` label.
