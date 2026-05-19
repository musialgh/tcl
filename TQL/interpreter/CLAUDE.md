# TQL Interpreter — Developer Notes

## What this is

A C# (.NET 10) tree-walking interpreter for **TQL** (TAS Query Language), a DSL for
ballroom dance competition adjudication algorithms.  The binary is `tql`.

```
tql <script.tql> {[-r <file>] -i <table>} {[-w <file>] -o <table>}
```

Multiple tables can be read from one file (blank-line separated).  `-` as a
file name resets to stdin / stdout.

## Build

```bash
dotnet build TqlInterpreter.csproj -c Release
```

## Pipeline

```
source → Lexer → [Token] → Parser → AST → Executor
                                            ↑
CSV input ──────────────────── GlobalScope.Variables
```

## Key language rules

- **Keywords are uppercase-only.**  `GROUP`, `ROW`, `ROUND` etc. are keywords;
  `group`, `row`, `round` are plain identifiers.  The lexer dict uses
  `StringComparer.Ordinal`.
- **`"..."` is interpolated** (`StrPat` token).  The parser splits it on
  `{expr}` and builds a `StringPattern` AST node.  **`'...'` is a plain string
  literal** (`StrLit`), no interpolation.
- **`IMPORT` accepts both** `'path'` and `"path"` (the interface file uses
  double-quoted paths).
- **Semicolons end every directive.**  There is no implicit newline terminator.
- **`#` is a line comment.**

## Block structure — no INDENT/DEDENT tokens

The lexer emits a flat token stream; newlines are stripped.  The parser infers
block structure from `Token.Line` and `Token.Col`:

- After a `:`, if the *next* token is on the **same line** → single inline
  directive.
- Otherwise → `ParseBlock(next.Col)`, which consumes directives while their
  first token's column `>=` the block's base column.
- The colon token's line (not the keyword's line) is what matters — always
  capture `var c = Expect(Tk.Colon)` before calling `ParseBodyAfterColon(c.Line)`.

## Scope

`GlobalScope` holds variables and function definitions.  `LocalScope` wraps it:

- **`IsGlobal = true`** — used for top-level code; `Bind()` writes to
  `GlobalScope.Variables` directly.  No per-file local scope at top level.
- **`GLOBAL name`** — imports an existing global into a function's local view.
- **`GLOBAL name = expr`** — creates/updates a global and makes it locally
  visible.

## Notable naming quirk

`TableColumn` has two fields that would both be called `Col`:

| Field | Type | Meaning |
|-------|------|---------|
| `ColName` | `string` | the column name being accessed |
| `Col` | `int` | token column position (inherited from `Node`) |

Always use `tc.ColName` for the column name and `tc.Col` for error positions.

## SELECT / aggregate evaluation

`EvalAggExpr` handles SELECT columns that mix aggregates with scalars:

- Pure aggregate (`FuncCallExpr` with aggregate name) → `ComputeAgg()`
- `BinaryOp` containing aggregates → recursively resolves both sides, then
  calls **`ApplyBinaryOp(op, l, r, line, col)`** on the two already-evaluated
  values.  Do **not** re-wrap in `new BinaryOp(…, new NullLit, new NullLit)`
  and pass to `Eval` — that was the original bug and produces NULL op NULL.
- Non-aggregate fallback → evaluate against the first row of the group.

`ContainsAgg(ExprNode)` recursively checks whether an expression tree contains
any SQL aggregate call.

## FOR IN iteration

- Source is a `TableColumn` (e.g. `scores.start`) or a single-column table
  → the loop variable is bound to each **scalar cell value**.
- Source is a multi-column table → the loop variable is bound to each
  **`RowVal`**.

## Table.AddColumn on non-empty tables

`Array.Resize` cannot take a `ref` to a dictionary indexer.  The pattern is:

```csharp
var arr = _rows[rid];
Array.Resize(ref arr, newLen);
arr[newLen - 1] = defaultVal;
_rows[rid] = arr;
```

## Control flow

`BREAK`, `CONTINUE`, and `RETURN` are implemented via internal exceptions
(`BreakSignal`, `ContinueSignal`, `ReturnSignal`) that are caught by the
corresponding loop / function executor.  They must never escape the interpreter.

## Script files

| File | Status | Notes |
|------|--------|-------|
| `tas-evaluation-interface.tql` | reference | defines `prepare_interface()` |
| `cross-majority-skating.tql` | reference | full CMS adjudication logic |
| `final_round.tql` | **outdated** | no WHEN syntax, no `{ col = val }` ROW in interpreter |
