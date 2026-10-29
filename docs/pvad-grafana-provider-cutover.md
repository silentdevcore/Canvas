# Grafana Provider Rename Preparation

The installed dashboard provider and folder retain their historical identities.
This is a persisted placement and authorization boundary, not just a display label.
Datasource display labels already use PVAD; dashboard and datasource UIDs remain
stable. This preparation does not migrate a running Grafana installation.

## Offline Candidate

```bash
node tools/configuration/rename-grafana-provider.mjs
node --test tools/configuration/rename-grafana-provider.test.mjs
```

The generator writes only ignored files under
`artifacts/rename/grafana-provider/`:

- `dashboards.candidate.yml`: provider and folder labels changed to PVAD.
- `dashboards.original.yml`: exact original provisioning bytes.
- `report.json`: source checksum, preserved boundaries and outstanding gates.

The active provisioning, Compose mounts, datasource configuration, dashboards,
database and volumes are untouched. Source drift, extra providers, explicit folder
UIDs or changed provisioning policies require manual review. There is no apply
command. The report always records `deploymentAuthorized: false`.

## Before Activation

1. Identify the Grafana instance, organization and storage backend. Inventory
   dashboards by UID, current folder UID, folder permissions, direct dashboard
   permissions, teams, service accounts and saved links. Use metadata only; do not
   export credentials into the repository.
2. Stop concurrent writers and capture an approved, consistent Grafana database
   backup plus provisioning and plugin configuration. Demonstrate restoration in
   an isolated instance. A provisioning file is not a database backup.
3. Decide explicitly whether to rename an existing folder in place or move
   dashboards into a new folder. The generated candidate alone does neither
   safely: folder names do not prove folder identity or equivalent permissions.
   Resolve any existing PVAD folder collision before proceeding.
4. In staging, test the chosen placement strategy with the pinned Grafana image.
   Check provisioning logs, duplicate or stale dashboards, UIDs, saved links,
   datasource queries, operator access and denied non-operator access. Compare
   effective permissions before and after; do not inherit broader access.
5. Record operator approval and a rollback window before changing the active
   source or mounting a candidate. Do not run competing provisioning generations
   against the same database.
6. Rehearse rollback of both provisioning and persisted state. Merely restoring
   the original YAML does not restore moved dashboards or folder permissions;
   `disableDeletion` is not a rollback mechanism. Verify access and dashboard
   inventory again after restoration.

No live cutover, permission migration, restore drill or production approval is
claimed by the offline tests. The local development owner remains responsible
for approving and exercising those steps.
