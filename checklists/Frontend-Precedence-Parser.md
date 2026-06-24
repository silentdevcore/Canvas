# Frontend: Operator-Precedence Parser in the Expression Engine

Brings the designer/preview expression engine to parity with the server. Companion to
[Migration_RDL+DevExpress.md](Migration_RDL+DevExpress.md) and
[Migration_CSharp-Expression-Eval.md](Migration_CSharp-Expression-Eval.md).

## Goal & approach

`expressionEngine.ts` evaluated arithmetic/comparison by `expression.split(op)` (first two parts only —
no precedence/associativity), and didn't handle raw `&&`/`||` at all. The server
`CanvasExpressionEvaluator` already has a correct recursive-descent parser; this ports that grammar to
TypeScript so both surfaces agree.

Precedence ladder (mirrors C#): `or → and → equality → comparison → additive → multiplicative → unary →
primary`. `+` = numeric add when both numeric else string concat; loose equality; `&&`/`||`/`!` return
bool (matches C#); unary `-`. The `createSafeContext` helpers (`$iif`, `$concat`, 7 aggregates, `Math`,
`$format`, …), dotted member access, and function calls are resolved by the parser from the context.
Special forms (template literals, `new Date(...)`, `instanceof`) and `??`/`?.` are preserved.

## Tasks

- [x] Tokenizer (`tokenize`): numbers, single/double-quoted strings, identifiers (incl. `$`, `.`),
      multi-char ops `=== !== == != <= >= && || ??` then single `< > + - * / % ! ( ) ,`; `?.` → `.`.
- [x] `Parser` (recursive descent) over the tokens with the C# precedence ladder; function calls
      (`ident(...)` → resolve to a context function, eval args, invoke), dotted resolve (`a.b.c`,
      `Math.round`), keywords `true/false/null`, `??` returns the value.
- [x] Rewire `evaluateSafeExpression`: keep literal/template/`new`/`instanceof` pre-checks, then parse;
      parser throw → `{ isValid:false }`.
- [x] Remove the dead naive helpers (`evaluateComparison`, `evaluateArithmetic`,
      `evaluateComplexExpression`, `evaluatePropertyAccess`, `evaluateFunctionCall`, `splitTopLevelArgs`,
      `isWholeFunctionCall`, `hasTopLevelOperator`). Exports unchanged.
- [x] Tests: precedence, logical, comparison+arithmetic, string concat, and a **parity table** vs the
      documented C# results; existing helper/aggregate cases still pass. Full frontend suite + `tsc`.

## Parity with `CanvasExpressionEvaluator` (server)

| Construct | Both engines |
| --- | --- |
| `2 + 3 * 4` | 14 (precedence) |
| `10 - 3 - 2` | 5 (left-assoc) |
| `+` with non-numeric | string concat |
| `A == 1 && B == 2 \|\| C == 3` | `&&` binds tighter than `\|\|`; bool result |
| `==`/`!=` | loose equality (numeric/bool/string) |
| helpers `$iif/$concat/$sum/...` | resolved from context |

## Follow-ups (out of scope)

- [ ] Short-circuit semantics for `&&`/`||` (both engines currently evaluate both sides).
- [ ] Ternary `?:` / bitwise operators (not emitted by the translator).
