# PVAD Website Status Hosting

The optional [Designer security profile](DESIGNER-SECURITY.md) is a draft with
separate enforced and report-only rules. It is not enabled by the shared
template alone and is not deployment approved.

This directory defines the production HTTP contract for every independently
deployed PVADE browser application. The application build output is mounted at
`/usr/share/nginx/html`, and `nginx.conf.template` is processed by the official
Nginx container entrypoint. Install the executable
`15-pvad-environment.envsh` at
`/docker-entrypoint.d/15-pvad-environment.envsh` alongside the template. The official
entrypoint sources this hook before its template substitution step.

Deploy the hook and template atomically. Updating only the template is not a
supported deployment: legacy inputs require the hook to resolve canonical values.
The hook must retain executable permissions in the image or read-only bind mount.

Required environment values:

- `PVAD_API_UPSTREAM`: same-origin API upstream, including the trailing slash,
  for example `http://pvad-api:8080/`.
- `PVAD_SPA_ROUTE_PATTERN`: the anchored route pattern for the deployed app.
  Approved patterns are listed in `route-patterns.env.example`.

Legacy `PXA_API_UPSTREAM` and `PXA_SPA_ROUTE_PATTERN` remain accepted inputs.
Matching aliases are accepted; contradictory aliases fail before template
generation with key names only. An explicitly empty canonical value never falls
back to the legacy value. Missing/empty required values and unanchored route
patterns also fail. Nginx remains responsible for final upstream/regex syntax
validation. The example per-application variables are labels for copying values,
not automatically selected settings.

The route allowlist is intentional. Known client routes return `index.html` with
HTTP 200, while unknown routes return the local `404.html` body with HTTP 404.
Nginx failures use `500.html`, and an operator can enter maintenance mode by
returning HTTP 503 after configuring `error_page 503 /maintenance.html`.

The hosting contract also requires:

- `/api/` responses pass through unchanged, including Problem Details status;
- status pages use `Cache-Control: no-store`;
- `index.html` uses `no-cache, no-store, must-revalidate`;
- content-hashed assets may use a one-year immutable cache;
- the proxy must not redirect an unknown path to `/`;
- CDN or ingress layers must preserve the origin status and headers.

Validate the checked-in contract with:

```bash
node --test deploy/websites/*.test.mjs
```

After building PVAD.Company, run the real Nginx boundary test with:

```bash
sh deploy/websites/status-hosting-smoke-test.sh
```

This executes the same HTTP checks twice: once with canonical inputs and once
with legacy-only inputs. `PVAD_STATUS_SMOKE_PORT` selects the local port; its
`PXA_STATUS_SMOKE_PORT` alias follows the same conflict rules. The default is 4183.

Post-deployment checks should request one valid deep link, one unknown path,
`/api/pvad/v1/version`, its `/api/pxa/v1/version` compatibility alias, and a
deliberately unavailable upstream. Verify the HTTP
status, `Content-Type`, `Cache-Control`, response body, and correlation headers
at the public hostname rather than only inside the container.
