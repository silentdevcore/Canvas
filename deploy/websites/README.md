# PXA Website Status Hosting

This directory defines the production HTTP contract for every independently
deployed PXA browser application. The application build output is mounted at
`/usr/share/nginx/html`, and `nginx.conf.template` is processed by the official
Nginx container entrypoint.

Required environment values:

- `PXA_API_UPSTREAM`: same-origin API upstream, including the trailing slash,
  for example `http://pxa-api:8080/`.
- `PXA_SPA_ROUTE_PATTERN`: the anchored route pattern for the deployed app.
  Approved patterns are listed in `route-patterns.env.example`.

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
node --test deploy/websites/status-hosting.test.mjs
```

After building PXA.Company, run the real Nginx boundary test with:

```bash
sh deploy/websites/status-hosting-smoke-test.sh
```

Post-deployment checks should request one valid deep link, one unknown path,
`/api/pxa/v1/version`, and a deliberately unavailable upstream. Verify the HTTP
status, `Content-Type`, `Cache-Control`, response body, and correlation headers
at the public hostname rather than only inside the container.
