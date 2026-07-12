# Migration: Back the `IExpressionEvaluator` Stub with the Real Engine

Removes the last duplicate server-side expression implementation. Companion to
[Migration_CSharp-Expression-Eval.md](Migration_CSharp-Expression-Eval.md).

## Goal & approach

`ExpressionEvaluator` (`src/PXA.Core/Primitives/ExpressionEvaluator.cs`) was the original **stub**
implementation of `IExpressionEvaluator`: regex variable substitution plus bare `==`/`!=`, returning the
raw string for anything else. It is registered in DI (`PXA.WebApi/Program.cs`) and injected into
`TemplateExpander` (the value/visibility-resolution path), but nothing currently consumes
`ITemplateExpander` — the live export path is `DesignLayoutPlanner`, already on `PxaExpressionEvaluator`.

The stub now **delegates to `PxaExpressionEvaluator`** — the same recursive-descent engine used by the
exporters and mirrored by the frontend `expressionEngine.ts` — so the `TemplateExpander` path evaluates
the same PXA grammar (helpers, operators, and, once the aggregates branch lands, aggregates too). One
engine instead of two.

## Tasks

- [x] Rewrite `ExpressionEvaluator.EvaluateAsync` to call `PxaExpressionEvaluator.TryEvaluate`:
      true → `ExpressionResult { IsValid = true, Value }`; false → `IsValid = false` (callers already
      guard on `IsValid`). Kept the defensive `ContainsDangerousPatterns` guard; dropped `async`
      (returns `Task.FromResult`, no CS1998). Deleted the dead `ProcessExpression` /
      `EvaluateSimpleExpression` helpers.
- [x] Tests (`tests/PXA.Core.Tests/ExpressionEvaluatorTests.cs`): `$concat`/arithmetic/`$iif`/
      comparison evaluate via the real engine (things the old stub could not do); dangerous pattern →
      invalid; unparseable → invalid. Core suite green (23).
- [x] No interface/DI/`TemplateExpander` changes; no other suite references these types.
- [x] Tick the follow-up bullet in `Migration_CSharp-Expression-Eval.md`.

## Notes

- Independent of the dataset-aggregates branch (PR #17): different file, clean merge. Aggregates flow
  through automatically once both land, since this delegates to the same `PxaExpressionEvaluator`.

## Follow-ups (out of scope)

- [x] **Removed the dead `ITemplateExpander` path.** It stayed unconsumed (the live render path is
      `DesignLayoutPlanner`), so rather than wire it, the whole legacy `DesignerElement → ExpandedElement →
      PDF` pipeline was deleted: `TemplateExpander`/`ITemplateExpander`, `RepeatExpander`/`IRepeatExpander`
      (its `ExpandRepeatAsync` only served `TemplateExpander`, and depended on the `ExpandedElement`/
      `ExpansionContext` types defined in `TemplateExpander.cs`), and `PXA.Application.DocumentModelConverter`
      (the only consumer of `ExpandedElement` — a `public static` class with zero callers). Their DI
      registrations were dropped from `Program.cs`. **Kept** `ExpressionEvaluator`/`IExpressionEvaluator`
      (the tested adapter delegating to `PxaExpressionEvaluator`) and `ValueFormatter`/`IValueFormatter`
      (standalone leaf utilities still registered for injection). Solution builds clean; Core suite green.
