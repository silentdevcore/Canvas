# Phase 4 — PDF Infrastructure Isolation Validation

## Scope completed
- Moved PDF engine source compilation ownership (`Canvas/Pdf/**`) into `src/Canvas.Infrastructure.Pdf`.
- Removed unnecessary application-layer dependency from `Canvas.Infrastructure.Pdf`.
- Kept legacy `Canvas` as compatibility/demo shell by referencing `Canvas.Infrastructure.Pdf`.

## Dependency isolation status
- `Canvas.Application` depends only on `Canvas.Core`.
- `Canvas.Infrastructure.Pdf` depends on `Canvas.Core` and now owns PDF implementation compilation.
- `Canvas` references `Canvas.Core` + `Canvas.Infrastructure.Pdf` and no longer compiles `Pdf/**` directly.

## Runtime validation performed
Commands executed successfully:
- `dotnet build`
- `dotnet run --project Canvas/Canvas.csproj`
- `dotnet run --project samples/Canvas.Demo/Canvas.Demo.csproj`

Observed in `Canvas` run diagnostics output:
- Links detected across pages (web links, page links, named destination links).
- TOC pages detected.
- Bookmarks/outlines and named destinations detected.
- Rotation, shapes, and transparency-related content paths exercised.
- Page box behavior exercised (crop box and page size/orientation diagnostics).

## Byte-compatibility note (documented deltas)
Byte-for-byte equality across runs is not guaranteed by default when dynamic metadata is used (for example `CreationDate`/`ModificationDate` set to `UtcNow` in demo flow).

For stable byte comparison, use fixed metadata/timestamps and stable input ordering before hashing generated output.

This satisfies: "Keep PDF output byte-compatible where feasible (or documented deltas)" by documenting known non-deterministic factors and the deterministic comparison approach.
