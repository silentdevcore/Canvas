---
name: pxa-release-author
description: Review PXA repository changes and create or correct structured release fragments. Use when finishing a feature, fix, refactor, migration, documentation change, or release preparation that needs a customer-facing release note and Semantic Version impact.
---

# PXA Release Author

Create one accurate release fragment for the current logical change.

## Workflow

1. Read `VERSION`, `tools/versioning/README.md`, and
   `product-metadata/release-fragment.schema.json`.
2. Compare the current branch with its `develop` merge base. Include staged,
   unstaged, and committed branch changes.
3. Identify affected components and observable behavior. Inspect public API,
   persistence, configuration, compatibility, UI, and documentation changes.
4. Select the impact:
   - `none`: internal-only work with no shipped behavior change.
   - `patch`: compatible fix, documentation correction, or internal improvement
     that affects the shipped product.
   - `minor`: backward-compatible capability or meaningful user-facing
     improvement.
   - `major`: incompatible API, data, configuration, SDK, or workflow change.
5. Create or update one JSON file in
   `product-metadata/release-fragments/`. Use a stable lowercase ID.
6. Write the summary in concise customer-facing English. State the result, not
   implementation mechanics. Do not claim behavior that the diff does not prove.
   Remove internal ticket references, credentials, customer identifiers, email
   addresses, network addresses, and unreviewed security details.
   For a `security` fragment, set `securityReviewed` to `true` only after this
   review has been completed.
7. Add known feature IDs and root-relative Documentation links. Never add
   internal tickets, credentials, customer data, exploit details, or secrets.
8. Run:

   ```bash
   node tools/versioning/pxa-version.mjs check
   node tools/versioning/pxa-version.mjs fragments
   node --test tools/versioning/pxa-version.test.mjs
   ```

9. Report the selected impact, affected components, fragment path, and any
   missing migration or documentation work.

## Example Prompts

- "Use `$pxa-release-author` to add the release fragment for my current change."
- "Use `$pxa-release-author` to verify whether this fix is Patch or Minor."
- "Use `$pxa-release-author` to review all pending fragments before release preparation."

## Guardrails

- Do not change `VERSION` for a feature or fix pull request.
- Do not edit the published release manifest directly during normal feature work.
- Do not create tags, merge branches, publish releases, push containers, or
  start deployments.
- Do not infer release notes from commit messages alone.
- A breaking fragment must use `major`, category `breaking`, and
  `breaking: true`.
- An internal-only fragment must use `none` and include a meaningful reason.
- If unrelated logical changes are present, create separate fragments rather
  than combining misleading summaries.
