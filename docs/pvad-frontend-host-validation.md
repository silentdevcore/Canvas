# PVAD Frontend Host Validation

## Verified Boundaries

- Six development API proxies target the local WebApi at port 5086. Designer
  sends `X-PVAD-Application: designer`; Account discovery/OAuth proxies retain the
  incoming origin. The structural contract test covers all six configurations.
- The real desktop/mobile Account-to-Designer handoff is recorded in the
  [rename test report](../checklists/PVAD.Rename-Test-Report.md).
- Website links accept reviewed PVAD environment keys and their PXA aliases;
  contradictory inputs fail with key-only errors. Customer-specific base paths
  and percent-encoded spaces remain supported.
- Return URLs require a configured product origin and reject credentials,
  backslashes and raw control/space characters. Admin is not a public return
  target. These checks do not replace server-side handoff allowlisting.
- Designer runtime scripts may set `__PVAD_CONFIG__.accountUrl` before bundle
  loading. The legacy `__PXA_CONFIG__` reader remains, with conflict detection.
- Local Nginx container tests verify SPA 404 handling, asset/shell caching and
  canonical/legacy API outage responses. The proxy preserves `PXAAPI018` and adds
  `PVADAPI018` as `canonicalCode`; its current display title uses PVAD.

## Deployment Checks Still Required

The container test uses an intentionally unavailable API upstream and a Company
build. It does not certify successful proxying, OAuth discovery or cookies on
every production host. Before release, test each actual TLS host, forwarding
headers, API upstream, Account allowlist and runtime script configuration through
the deployed reverse proxy/CDN. Verify cookie isolation and login return paths
there. Never infer production readiness solely from localhost or source tests.

The Nginx template consumes `PVAD_API_UPSTREAM` and `PVAD_SPA_ROUTE_PATTERN`.
Its executable `15-pvad-environment.envsh` hook accepts the corresponding legacy
PXA inputs, rejects contradictory aliases and requires nonempty configuration
before template substitution. Install the hook and template together; a template
update alone is not a compatible cutover. Local container checks cover both new
and legacy-only inputs. See the [hosting instructions](../deploy/websites/README.md).
