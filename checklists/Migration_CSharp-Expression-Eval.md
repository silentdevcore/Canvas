# Migration: C# Server-Side Expression Evaluation

Completes the expression feature end-to-end: migrated RDL/DevExpress expressions, already executable in
the frontend designer/preview, now also evaluate in **server-side exports**. Companion to
[Migration_RDL+DevExpress.md](Migration_RDL+DevExpress.md).

## Goal & approach

`DesignLayoutPlanner` (used by every exporter — PDF/Image/HTML/Word/SVG/ODT) only token-substituted
`{{X}}`; it never evaluated `Expression`, so conditional/arithmetic cells exported their literal template
(`Sum({{Total}})`, `{{Qty}} * {{Price}}`). A new `PxaExpressionEvaluator` — a proper recursive-descent
evaluator mirroring the frontend `expressionEngine.ts` grammar — is invoked during layout planning so the
**translated** `Expression` is computed against the row/parameter data and written to `Content`.

## Scope (v1)

- Standard single-row expressions (exactly what the `ExpressionTranslator` emits).
- **Out (follow-ups):** dataset aggregates (`Sum`/`Avg`/`Count`); custom `<Code>`; backing the
  `IExpressionEvaluator`/`TemplateExpander` (DesignerElement) path used by Application use-cases.

## Tasks

- [x] `PxaExpressionEvaluator` (PXA.Core.Primitives): recursive-descent over a single-row data dict;
      `static bool TryEvaluate(expr, data, out value)` returning false (→ caller falls back) when unparseable.
- [x] Wire into `DesignLayoutPlanner`: `ApplyRepeatItem` (repeat rows) and a new `ApplyStaticData`
      (non-repeat) path evaluate `Expression` → set `Content`/`BarcodeValue`; token-substitution fallback kept.
      Non-repeat elements now also resolve `{{tokens}}` from the payload (parameter defaults).
- [x] Tests: 6 evaluator unit tests (PXA.Core.Tests) + an end-to-end HTML export test asserting the
      computed per-row value (`Ada Lovelace`/`Alan Turing`, no literal `$concat`). Core 19, Export 177 green.
- [x] Tick the "C# server-side exporter evaluation" follow-up in `Migration_RDL+DevExpress.md`.

## Supported constructs (v1)

| Construct | Notes |
| --- | --- |
| Literals: string / number / bool / null | |
| Identifiers / `a.b` member access | resolved from the row/parameter data dict; unknown → null |
| `* / % + -` | `+` = numeric add when both numeric, else string concat (matches frontend) |
| `== != < <= > >=`, `&& \|\| !`, unary `-` | |
| `$iif`, `$switch`, `$concat`, `$and`, `$or`, `$not`, `$coalesce` | same helper set the translator emits |
| Unknown functions (`Sum`, `Format`, …) | `TryEvaluate` → false → token-substituted template fallback |

## Follow-ups (out of v1)

- [x] Dataset aggregates (`Sum`/`Avg`/`Count`/`Min`/`Max`/`First`/`Last`) — done; `PxaExpressionEvaluator`
      gained the aggregate helpers. See [Migration_Dataset-Aggregates.md](Migration_Dataset-Aggregates.md).
- [ ] Custom RDL `<Code>` functions.
- [x] Back `IExpressionEvaluator` (stub) with this evaluator — done; `ExpressionEvaluator` now delegates
      to `PxaExpressionEvaluator`. See [Migration_Backed-ExpressionEvaluator.md](Migration_Backed-ExpressionEvaluator.md).
      (Wiring an actual `ITemplateExpander` consumer remains a follow-up.)
