# Migration: Group-Scoped Aggregates (v1 — server-side)

Per-group totals for group footer/header bands. Companion to
[Migration_Dataset-Aggregates.md](Migration_Dataset-Aggregates.md).

## Goal & approach

Whole-dataset aggregates (`$sum(DataSet, "Total")`) shipped already. Group footers need the **current
group's** total. The blocker was that `DesignLayoutPlanner.ExpandRepeats` only did per-row repeat — no
per-group partitioning — so a group footer rendered once with no notion of "this group's rows".

**Convention:** a group-scoped aggregate references a reserved **`$group`** identifier =
the current group's row subset, injected per group by the planner. e.g. `$sum($group, "Total")`.
`ExpressionTranslator.GroupScopeToken` is the shared constant.

## Tasks

- [x] `ExpressionTranslator`: accept `$group` as the dataset token (new `GroupScopeToken` const); the
      aggregate leaf emits `$sum($group, "Field")` when the scope token is passed.
- [x] DevExpress converter: a control in a `GroupHeaderBand`/`GroupFooterBand` translates aggregates with
      the `$group` scope (was the group field path).
- [x] `DesignLayoutPlanner.ExpandRepeats`: when a repeat element is a **group band** (group field from
      `style.devExpressGroup.fields` or a generic `style.groupField`) and `Repeat.DataPath` resolves to a
      dataset array, render **once per distinct group key** (stable first-seen order); each clone binds the
      group key as a scalar and exposes `$group` = that group's rows, then evaluates the expression. Plain
      per-row repeats and static elements are unchanged.
- [x] Evaluators need no change — `$group` is just an identifier resolved from the data dict; the shipped
      aggregate helpers handle the row subset.
- [x] Tests: translator (`$group` accepted), DevExpress converter (group footer → `$sum($group, "Total")`),
      end-to-end HTML export (two regions → each region's own total, not the grand total).

## Scope (v1) / limitations

- **Server-side only** (`DesignLayoutPlanner`, all exporters). Frontend `repeatExpander.ts` preview parity
  is a follow-up.
- **Single group field**; one backing dataset present in the payload.
- **DevExpress runtime caveat:** DevExpress reports carry no named dataset, so a DevExpress group band's
  `Repeat.DataPath` (the group field) usually won't resolve to an array at export time — the **translation**
  to `$group` is correct, but per-group expansion only fires when a backing dataset array is supplied.
  The planner mechanism is fully exercised by the end-to-end export test via a constructed dataset.

## Follow-ups

- [x] **Band-based converters emit the generic `style["groupField"]`** so the planner's per-group
      expansion fires for them. FastReport/Telerik/Stimulsoft/Jasper set `frx/trdx/mrt/jrxmlGroup` metadata
      but not the generic `groupField` the planner reads (`TryGetGroupField`), so `$group` was never
      injected and their group aggregates rendered the grand total. Each now also sets `style["groupField"]`
      (Jasper extracts it from the `$F{field}` group expression). Validated by
      `DesignLayoutPlannerTests.BuildPages_ShouldRenderGroupScopedAggregate_PerGroupViaGroupField`
      (North 30 / South 5, not the grand 35).
- [x] **Frontend `repeatExpander.ts` per-group parity** — added `partitionByGroup(rows, groupField)`
      (mirrors `DesignLayoutPlanner.PartitionByGroup`; stable first-seen order, dotted fields). Note the live
      designer preview/export render **server-side** (`/api/templates/render-design`, `/api/export` →
      `DesignLayoutPlanner`); the frontend `template/*` engine is a standalone/unit-tested implementation,
      so parity is proven by `__tests__/repeatExpander.test.ts` (`$sum($group, "Amount")` → per-group totals).
- [x] **RDL converter: emit `$group` for textboxes inside a group region.** A `List`/grouped region now
      propagates its group field + dataset to its child `ReportItems` (`ParseReportItems` carries
      `groupField`/`groupDataSet`; `RdlGroupField` extracts the field from `=Fields!X.Value`). A free-standing
      textbox that sits inside such a group and holds an aggregate is translated with `$group` (not the
      dataset), and gets `style["groupField"]` + a `Repeat` over the region dataset so the planner renders it
      once per group. Validated by `Convert_AggregateInListGroup_ScopesToGroupNotDataset`
      (`=Sum(Fields!Amount.Value)` → `$sum($group, "Amount")`, groupField `Country`, Repeat `Sales`).
      *Limitation:* aggregates inside Tablix group-footer **cells** (grid `CellData`) still scope to the
      dataset — that needs compound per-group table-row rendering (tracked in Code-Migration-SyncfusionRdl P2).
- [ ] Multi-level / nested groups; running totals (`RunningValue`).
