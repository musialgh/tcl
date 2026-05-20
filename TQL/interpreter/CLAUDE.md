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

## GROUP BY alias resolution

`GroupRows` takes `List<Func<int, TqlValue>>` key functions, not column name
strings.  `EvalSelect` builds these functions before calling `GroupRows`:

- If the GROUP BY name matches a SELECT alias → evaluate that alias's expression
  per source row.
- Otherwise → read the column from the source table directly.

This lets `GROUP BY dance` work when `dance` is a SELECT alias for
`round_element` and does not exist as a source column.

## ROW expressions

`EvalRow` derives default column names via `DefaultColName(ExprNode, int)`:

- `TableColumn tc` → `tc.ColName` (e.g. `s.start` → column named `start`)
- `IdentExpr ie` → `ie.Name`
- anything else → `col_<index>`

`EvalRow` also applies `AutoScalar` to every field value.  This ensures that
a 1×1 table projection like `placing.place_min` (a 1-row SELECT result) becomes
a scalar when placed into a ROW field.  Do **not** apply `AutoScalar` inside
`EvalTableColumn` for column projections — that breaks cases like
`LET candidates = scores.start` where the single-column table must stay a
table for the next `IN candidates` predicate.

## Numeric subscript syntax — `table[n]`

`FinishExpr` (Parser) handles `BracketIdent` tokens whose text is a decimal
integer as `TableIndex` nodes.  This covers `scores[1]`, which the lexer
tokenises as `BracketIdent("1")` because `[` always starts a bracketed token.
`TableIndex` evaluation returns `RowValAt(rid)` for n ≥ 1 and a column-name
`RowVal` for n = 0.  `.col` chained after `[n]` then hits the `RowVal` branch
of `EvalTableColumn` and returns the scalar directly.

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

## Table.RowCount vs. physical rows

`Table.RowCount` returns `_sortOrder.Count` (logical visible rows), **not** `_rows.Count` (physical rows).

`ApplyLimit` calls `SetSortOrder(keep)` which shrinks `_sortOrder` to only the kept rows, but
`_rows` retains all physically inserted rows.  If `RowCount` returned `_rows.Count`, then
`ROWS(t)` after a `LIMIT` clause would return the pre-limit row count, breaking `IF ROWS(t) = 1`
comparisons.

Never change `RowCount` back to `_rows.Count`.

## LOAD TABLE (two forms)

| Form | What it does |
|------|-------------|
| `LET t = LOAD TABLE FROM 'file.csv';` | Expression — creates and returns a new Table |
| `LOAD TABLE FROM 'file.csv' INTO target;` | Directive — replaces rows of an *existing* table |

The directive form (`LoadTableIntoDirective`) validates that every CSV column exists in the
target and is type-compatible (Decimal target ← Text CSV column → error).  Columns absent in
the CSV receive their declared `DEFAULT`.  Existing rows are discarded before loading.

## Script files

| File | Status | Notes |
|------|--------|-------|
| `tas-evaluation-interface.tql` | reference | defines `prepare_interface_output/input()` |
| `cross-majority-skating.tql` | reference | full CMS adjudication logic |
| `run-cms-finale.tql` | test entry point | calls `evaluate_round_final(TRUE)` |
| `run-cms-elimination.tql` | test entry point | calls `evaluate_round_elimination()` |
| `final_round.tql` | **outdated** | no WHEN syntax, no `{ col = val }` ROW in interpreter |

## Test data and runner

```
test-data/cms-finale-marks.csv            ← 150 rows (6 starts × 5 dances × 5 judges)
test-data/cms-elimination-marks.csv       ← same marks, places 1-3 → 1 (cross), 4-6 → 0
test-data/cms-finale-report.expected.csv  ← snapshot for finale test
test-data/cms-elimination-report.expected.csv ← snapshot for elimination test
```

Run with:
```bash
python3 run_tests.py            # auto-builds if dll missing
python3 run_tests.py --build    # force rebuild first
python3 run_tests.py finale     # filter by name
```

To update a snapshot after an intentional output change:
```bash
dotnet run --project interpreter/TqlInterpreter.csproj -c Release --no-build -- \
  run-cms-finale.tql -r test-data/cms-finale-marks.csv -i marks \
  -w test-data/cms-finale-report.expected.csv -o report
```
