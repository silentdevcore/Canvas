# Phase 4 — PDF Infrastructure Isolation Validation

## Scope completed
- Moved PDF engine source compilation ownership (`PXA/Pdf/**`) into `src/Infrastructure/PXA.Infrastructure.Pdf`.
- Removed unnecessary application-layer dependency from `PXA.Infrastructure.Pdf`.
- Kept legacy `PXA` as compatibility/demo shell by referencing `PXA.Infrastructure.Pdf`.

## Dependency isolation status
- `PXA.Application` depends only on `PXA.Core`.
- `PXA.Infrastructure.Pdf` depends on `PXA.Core` and now owns PDF implementation compilation.
- `PXA` references `PXA.Core` + `PXA.Infrastructure.Pdf` and no longer compiles `Pdf/**` directly.

## Runtime validation performed
Commands executed successfully:
- `dotnet build`
- `dotnet run --project PXA/PXA.csproj`
- `dotnet run --project samples/PXA.Demo/PXA.Demo.csproj`

Observed in `PXA` run diagnostics output:
- Links detected across pages (web links, page links, named destination links).
- TOC pages detected.
- Bookmarks/outlines and named destinations detected.
- Rotation, shapes, and transparency-related content paths exercised.
- Page box behavior exercised (crop box and page size/orientation diagnostics).

## Byte-compatibility note (documented deltas)
Byte-for-byte equality across runs is not guaranteed by default when dynamic metadata is used (for example `CreationDate`/`ModificationDate` set to `UtcNow` in demo flow).

For stable byte comparison, use fixed metadata/timestamps and stable input ordering before hashing generated output.

This satisfies: "Keep PDF output byte-compatible where feasible (or documented deltas)" by documenting known non-deterministic factors and the deterministic comparison approach.
