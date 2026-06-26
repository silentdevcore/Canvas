# Migration: Dataset Aggregates in Migrated Expressions

> **Status:** shipped — PR [#17](https://github.com/silentdevcore/Canvas/pull/17)
> (branch `migration-dataset-aggregates`, commit `28ddb270`).

Completes the expression feature for the most common real-report construct: **footer/group totals**.
Companion to [Migration_RDL+DevExpress.md](Migration_RDL+DevExpress.md) and
[Migration_CSharp-Expression-Eval.md](Migration_CSharp-Expression-Eval.md).

## Goal & approach

Single-row expressions already translate (RDL/DevExpress → Canvas grammar) and evaluate in the frontend
(`expressionEngine.ts`) and server exports (`CanvasExpressionEvaluator`). Aggregates (`Sum(Fields!Total.Value)`)
were the gap — `ExpressionTranslator` returned null for them, so a footer total rendered its literal
source text instead of a computed value.

**Aggregate helpers take the dataset as their first argument**, so the expression is fully self-resolving
from the existing payload/context (datasets are already reachable by name) — **no new context plumbing**:

```
Sum(Fields!Total.Value)        --(dataset "Orders")-->  $sum(Orders, "Total")
Count(Fields!Id.Value)                            -->   $count(Orders, "Id")
DevExpress Sum([Qty]) in a group footer over Region --> $sum(Region, "Qty")
```

`arg0` resolves (identifier → the dataset array of row dicts); `arg1` is the quoted field name. The helper
reads the field per row, coerces numeric, and reduces.

## Scope (v1)

- Functions: **`Sum`, `Avg`/`Average`, `Count`, `Min`, `Max`, `First`, `Last`**.
- **Dataset scope** = the whole named dataset the element belongs to (report-footer semantics).
  - **DevExpress:** a control in a `GroupHeaderBand`/`GroupFooterBand` uses that group's `GroupDataPath`.
  - **RDL:** a data region (`DataSetName`) scope, or — for a free-standing aggregate textbox — the
    report's **sole** `<DataSet>` (`RawReport.DefaultDataSetName`).
- Both surfaces: frontend preview/designer **and** server-side exports (parity helpers).
- **No regression:** unknown dataset / unknown function / non-array arg → expression stays raw and the
  token-substitution fallback applies.

## Tasks

- [x] `ExpressionTranslator`: aggregate leaf (`Sum|Avg|Count|Min|Max|First|Last` → `$sum/$avg/...`) +
      optional `dataSetName` param on `TranslateRdl`/`TranslateDevExpress`; emits `$fn(DataSet, "Field")`
      only for a single bare-identifier field and a valid-identifier dataset, else returns null (raw kept).
- [x] RDL converter (`RdlToDesignConverter`): pass the element's dataset (or the report's sole dataset,
      `DefaultDataSetName`, parsed from `<DataSets>`) into `TranslateRdl`.
- [x] DevExpress converter (`XtraReportToDesignConverter`): pass the group footer/header `GroupDataPath`
      into `TranslateDevExpress`.
- [x] Frontend `expressionEngine.ts`: 7 aggregate helpers over a rows array (`aggField`/`aggNums`).
- [x] C# `CanvasExpressionEvaluator`: 7 aggregate helpers in the function dispatch; `IsNumeric`/`ToNumber`
      extended to `long/int/short/byte/float/decimal` (JSON integers arrive as `long`).
- [x] `DesignLayoutPlanner`: verified — non-repeat footer elements already get the full payload, so the
      named dataset is reachable; no change needed.
- [x] Tests: translator (6), C# evaluator (3), frontend (4), RDL converter (1), DevExpress converter (1),
      end-to-end HTML export (1). Suites green: RDL 87, DevExpress 75, Core 22, Export 178, frontend 140.

## Supported functions (v1)

| Source | Translated to | Server + frontend result |
| --- | --- | --- |
| `Sum(Fields!X.Value)` | `$sum(DataSet, "X")` | numeric sum of column X over the dataset |
| `Avg`/`Average` | `$avg(DataSet, "X")` | mean (0 over empty) |
| `Count` | `$count(DataSet[, "X"])` | row count (or non-null count of X) |
| `Min` / `Max` | `$min` / `$max` | numeric min/max (0 over empty) |
| `First` / `Last` | `$first` / `$last` | X of the first/last row |

## Follow-ups (out of v1)

- [x] **Group-scoped** aggregates (sum within the *current* group only) — done (server-side v1) via the
      `$group` convention + per-group expansion. See [Group-Scoped-Aggregates.md](Group-Scoped-Aggregates.md).
- [x] Other converters (FastReport / Telerik / Stimulsoft / Jasper) — group-scoped aggregates wired.
  - **FastReport** (`FrxToDesignConverter`): a fully-bracketed expression (`[Sum([Items.Total])]`) is
    stripped and routed through `TranslateDevExpress` ([Source.Column] refs match that dialect), passing
    `$group` for group-band elements. Mixed literal+field text (`"Page [Page]"`) stays raw.
  - **Telerik** (`TrdxToDesignConverter`): dot field refs (`Fields.X.Value`) are rewritten to the RDL bang
    form and routed through `TranslateRdl` with `$group` for `GroupHeader/FooterSection` elements.
  - **Stimulsoft** (`MrtToDesignConverter`): a fully-braced expression (`{Sum(Customers.Total)}`) has its
    `DataSource.Column` refs rewritten to `[Column]` and routed through `TranslateDevExpress` with `$group`
    for group-band elements; lone `{SystemVar}` tokens stay raw.
  - **Jasper** (`JrxmlToDesignConverter`): aggregates are *variables*, not inline functions — a group-reset
    `<variable calculation="Sum">` over `$F{field}` referenced via `$V{name}` maps to `$sum($group, "field")`
    (Average/Count/Lowest/Highest/First too). Report/page-scoped variables stay as references.
  - **Scope note:** like the DevExpress converter, only group-band aggregates resolve (to `$group`);
    report/page-footer grand totals need a dataset name and are a follow-up.
- [ ] `RunningValue`, conditional aggregates (`Sum(IIf(...))`), aggregate over a *computed* per-row
      expression (`Sum(Qty * Price)`).
- [ ] Custom RDL `<Code>` functions.
