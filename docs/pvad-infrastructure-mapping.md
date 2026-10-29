# PVAD Compose Defaults And Existing Data

## Current Defaults

`docker-compose.yml` and `deploy/api/docker-compose.api.yml` now contain only PVAD
service, volume, network, credential-default and secret-mount names. The production
alerting and webhook overlays use the same canonical identities. New PostgreSQL
defaults are database/user `pvad` and development password `pvad-local`. They are
development defaults, not production credentials. Set a strong deployment-owned
password before production. The exporter and API resolve the same database
settings; readiness uses the container's effective database/user values.

WebApi and EF design-time defaults match the fresh local stack. **An existing
`pxa` database is not renamed, migrated, copied or discovered automatically.**
Starting the new defaults without the compatibility configuration selects new
storage. Do not interpret an empty workspace as proof that old data was deleted.

## Explicit Legacy Overlays

- Root stack: `deploy/rename/docker-compose.legacy.yml`.
- API/MCP stack: `deploy/rename/docker-compose.api-legacy.yml`.
- Binding template: `deploy/rename/legacy-bindings.env.example`.

These overlays keep canonical logical service/resource keys but bind them to
**verified existing external** volumes and networks. Physical names are required
inputs, never inferred from a project prefix. Missing inputs fail configuration;
external resources must already exist at startup. The `!override` network tag
removes inherited creation/IPAM settings; use a Compose version supporting that
tag (local configuration tests use Compose 5.1.4).

The root overlay describes a complete previously installed observability stack.
Do not invent IDs for components that were never installed. For a partial old
stack, prepare a reviewed subset of the overlay for the resources actually kept.
The API overlay separately retains database and API/keyring storage.

| Boundary | Fresh default | Legacy handling |
| --- | --- | --- |
| PostgreSQL service | `pvad-database` | Old `pxa-database` DNS alias in the overlay |
| Database/user | `pvad` / `pvad` | Overlay defaults `pxa` / `pxa`; explicit values override both client and server settings |
| API service | `pvad-webapi` | Old `pxa-webapi` DNS aliases in the API overlay |
| API/keyring volume | `pvad-api-data` | `PVAD_LEGACY_API_VOLUME` selects the actual existing volume |
| Database volume | `pvad-database-data` | `PVAD_LEGACY_DATABASE_VOLUME` selects the actual existing volume |
| Mail/monitoring volumes | `pvad-*-data` | Required `PVAD_LEGACY_*_VOLUME` bindings preserve actual storage |
| Observability/API-host networks | `pvad-*` | Required `PVAD_LEGACY_*_NETWORK` bindings reuse approved networks |
| License/SMTP/webhook secret mounts | `pvad_*` | Mount names and consumers change together; input key/secret files and their bytes stay unchanged |
| Monitoring UIDs, job labels and signed evidence | Historical identities retained | Do not rewrite stored history or immutable evidence |

Existing secret files remain supplied through `PVAD_LICENSE_*_FILE`,
`PVAD_ALERTMANAGER_SMTP_PASSWORD_FILE` and `PVAD_ALERT_WEBHOOK_SIGNING_KEY_FILE`.
Changing an in-container mount path does not rotate or replace key material.
Externally maintained Alertmanager configuration must use the new SMTP secret
path `/run/secrets/pvad_alertmanager_smtp_password` before deployment.

## Configuration Review Before Any Startup

Fill a deployment-owned copy of the binding template with inspected resource IDs.
Keep its existing project name and real credentials; do not commit them. The
following commands validate configuration only and do not start containers:

```bash
node tools/configuration/pvad-compose.mjs --env-file /absolute/local/legacy.env \
  -f docker-compose.yml -f deploy/rename/docker-compose.legacy.yml \
  --profile observability config --quiet

node tools/configuration/pvad-compose.mjs --env-file /absolute/local/legacy.env \
  -f deploy/api/docker-compose.api.yml -f deploy/rename/docker-compose.api-legacy.yml \
  --profile mcp config --quiet
```

The launcher supports conflict-checked `PXA_*` environment aliases, not automatic
physical-resource discovery. Existing storage-path overrides must reference the
canonical logical volume keys or approved absolute mounts, not undefined legacy
logical keys. Preserve `COMPOSE_PROJECT_NAME`; unchanged default/MCP networks also
remain project-scoped. Old service DNS aliases are available only with the overlay
and are not aliases for CLI commands such as `compose exec`.

For a host-run WebApi against the old local database, explicitly configure
`ConnectionStrings__PvadDatabase`; EF tooling separately supports
`PVAD_DATABASE_CONNECTION`. Do not rely on the new `pvad` development fallback to
find an old `pxa` database. Never record real connection secrets in the repository.

## Cutover And Rollback

Record actual project/container/volume/network IDs, verify backups and key access,
and retain the previous Compose definitions. Stop old database, API, mail and
telemetry writers before any approved start of the renamed services. Never run
old/new PostgreSQL or other writers on the same volume concurrently. Validate
external network topology and the reserved gateway/Grafana addresses before use.
Inspect stopped orphan containers individually; do not use `down -v` or delete
their storage. Verify retained records, authentication, MCP, health and rollback.
Prometheus host changes can alter `instance` labels even with stable job labels;
review queries, alerts and silences rather than assuming identical series.

This work validates static/configuration contracts only. No running container,
database, volume, network or secret file was modified. Full application builds,
runtime cutover and recovery remain separate. See the
[rollback runbook](pvad-rename-rollback.md).
