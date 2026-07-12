# Migration: RDL + DevExpress Expression Execution

Cross-converter work to make **migrated report expressions executable** in the PXA designer/preview,
rather than preserved as inert source-dialect strings. Companion to
[Code-Migration-SyncfusionRdl.md](Code-Migration-SyncfusionRdl.md) and
[Code-Migration-DevExpressReport.md](Code-Migration-DevExpressReport.md).

## Goal & approach

Migrated reports keep their source expressions raw on `ElementDto.Expression`
(RDL `=IIf(Fields!Paid.Value,"Yes","No")`, DevExpress `[Qty] * [Price]`). The designer/preview evaluates
`element.expression` with the **existing** frontend engine
(`ui-designer-v2/src/template/expressionEngine.ts`), which can't parse those dialects — so they never run.

**We do not build a new engine.** A shared `ExpressionTranslator` rewrites the source dialect into the
grammar the existing engine evaluates (function-call helper form + simple operators), so standard
single-row expressions become first-class, executable PXA expressions. The original is preserved on
`style.rdlExpression` / `style.devExpressExpression` for review.

## Scope (v1)

- **Standard single-row** expressions; **RDL + DevExpress** dialects; **designer/preview** (frontend) surface.
- The frontend engine is naive (no ternary / `&&` / `||`; comma-split args), so the translator targets
  **function-call helpers** (`$iif`, `$and`, `$or`, `$not`, `$concat`, `$coalesce`) and the engine's
  arg-splitter is made paren/quote-aware. Simple binary arithmetic/comparison use the engine's operators.

## Tasks

- [x] `ExpressionTranslator` (PXA.Migration.Abstractions): precedence-aware, quote/paren-safe transform
      with RDL and DevExpress front-ends → PXA grammar; returns null when not safely translatable.
- [x] RDL converter emits translated `Expression` for compound expressions (raw kept on `style.rdlExpression`).
- [x] DevExpress converter emits translated `Expression` (raw kept on `style.devExpressExpression`).
- [x] `expressionEngine.ts`: add helper fns ($iif/$switch/$concat/$and/$or/$not/$coalesce) + paren/quote-aware
      function-call argument splitting. **Also fixed a latent engine dispatch bug** where the function-call and
      arithmetic paths were unreachable (comparison fell through to property-access first) — they now route correctly.
- [x] Tests: translator unit tests (13); RDL + DevExpress converter tests updated; frontend
      `expressionEngine.test.ts` evaluating translated `$concat`/`$iif`/nested/`$and`/arithmetic. Full
      frontend suite 136/136; RDL 80, DevExpress 74.

## Supported constructs (v1)

| Source | Translated to |
| --- | --- |
| RDL `Fields!X.Value` / `Parameters!P.Value`; DevExpress `[X]` / `[Ds.X]` | identifier `X` / `P` (last segment) |
| RDL `&` (concat) | `$concat(a, b, …)` |
| RDL `IIf(c,a,b)` / DevExpress `Iif(c,a,b)` | `$iif(c, a, b)` |
| RDL `Switch(c1,v1,…)` | nested `$iif(…)` |
| RDL `<>` ; bare `=` (equality) | `!=` ; `==` |
| RDL `And`/`AndAlso`, `Or`/`OrElse`, `Not` | `$and(…)`, `$or(…)`, `$not(…)` |
| arithmetic `+ - * / %` (binary) | same operators (engine-evaluated) |

## Follow-ups (out of v1)

- [x] Dataset **aggregates** (`Sum`/`Avg`/`Count`/`Min`/`Max`/`First`/`Last`) — done via the dataset-as-first-arg
      helper form. See [Migration_Dataset-Aggregates.md](Migration_Dataset-Aggregates.md).
- [ ] Custom RDL `<Code>` functions — arbitrary VB, not executable here (stay preserved).
- [x] **C# server-side exporter** expression evaluation (Image/HTML/Word/PDF) — done via
      `PxaExpressionEvaluator` in `DesignLayoutPlanner`. See [Migration_CSharp-Expression-Eval.md](Migration_CSharp-Expression-Eval.md).
- [x] Full operator-precedence parser in the frontend engine — done; recursive-descent parser mirroring
      `PxaExpressionEvaluator`. See [Frontend-Precedence-Parser.md](Frontend-Precedence-Parser.md).
