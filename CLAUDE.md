# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

A C# interpreter for **TCL** (Tanzen Competition Language) — a functional DSL for defining ballroom dance competition adjudication algorithms. The interpreter reads TCL script files (`.tcl`) and a JSON input, evaluates a named function, and writes the result as JSON to stdout.

The three `.tcl` files at the repo root are the actual adjudication programs. The `tcl` directory contains the C# interpreter that runs them.

## Build and run

```bash
# Build (Release is the normal target)
dotnet build tclTclInterpreter.csproj -c Release

# Run a function (auto-detects *.tcl in cwd)
dotnet run --project tclTclInterpreter.csproj -c Release -- \
  <function_name> --input-file input.json

# Explicit script loading (typical for tests)
dotnet run --project tclTclInterpreter.csproj -c Release -- \
  wrap_round_final_skating \
  --input-file tcltests/test_final_round_tso_P_skating_in.json \
  --script adjudication.tcl \
  --script cross-majority-skating.tcl \
  --script cms-round-wrapper.tcl

# Debug: trace all function calls to stderr
  --trace-calls

# Debug: dump a value inline in a TCL expression
  dump(some_expr)   # writes to stderr, returns the value unchanged
```

## Run tests

```bash
# Run all integration tests (builds first, then runs each case)
./tcltests/run_tests.sh

# Run a single case manually
dotnet run --project tclTclInterpreter.csproj -c Release --no-build -- \
  evaluate_dance_elimination \
  --input-file tcltests/test_elimination_dance_001_in.json \
  --script adjudication.tcl --script cross-majority-skating.tcl
```

Tests are `*_in.json` / `*_out.json` pairs in `tcltests/`. They are registered by appending a pipe-delimited line to the `TESTS` array in `tcltests/run_tests.sh`.

## Architecture

The interpreter is a classic pipeline:

```
TCL source → Lexer → [Token] → Parser → ProgramNode (AST)
                                                 ↓
JSON input → JsonBridge.DeserializeParameters → Executor.ExecuteFunction → DslValue
                                                                                  ↓
                                                            JsonBridge.SerializeResult → JSON output
```

**`Lexer.cs`** — single-pass tokenizer; `//` and `///` comments are stripped. Produces `Token` records with line/column.

**`Parser.cs`** — recursive-descent; builds a tree of `AstNode` records from `AstNodes.cs`. Key grammar points:
- A program is a list of `SignatureDef`, `FunctionDef`, or `ImportDef`.
- A function body has optional `where` bindings after a `where` keyword; those bind names lazily.
- Conditionals: `expr if cond | expr if cond | default` — both inline (`ConditionalExpr`) and as a statement (`ConditionalChainStmt`).
- Iterator clauses `(x in collection, expr)` appear only inside built-in calls.

**`AstNodes.cs`** — all AST nodes are C# `record` types; every node carries `Line`/`Column` for error messages.

**`TypeSystem.cs`** — defines `DslValue` and the type hierarchy used at runtime:
- `NumericValue` (stores `decimal`; tracks sub-type: `num`, `real`, `int`, `nat`)
- `BoolValue`
- `StringValue`
- `ObjectValue` — the universal collection type, backed by `SortedDictionary<object, DslValue>` with custom key ordering: numeric keys < string keys < ObjectValue keys.

**`Executor.cs`** — tree-walking interpreter. All built-in functions are implemented here as `Eval*` methods, not in `BuiltInFunctions.cs` (that file is an empty placeholder). Built-ins: `map`, `filter`, `keys`, `first`, `count`, `sum`, `merge`, `concat`, `replace`, `arg_min`, `dump`.

**`JsonBridge.cs`** — JSON ↔ DSL conversion. JSON arrays become objects with 0-based `long` keys. JSON null becomes an empty `ObjectValue`. Numeric JSON keys (e.g. `"1"`) are stored as `long`.

## Key DSL concepts

- **`where` bindings are lazy**: each binding is wrapped in `LazyBinding` and only evaluated on first access (circular dependency is detected).
- **Multi-target assignment**: `a, b = some_func(...)` unpacks an object by key order (alphabetically sorted string keys, or numerically sorted integer keys).
- **Unary `?`**: key-existence check — `?obj[key]` returns `bool`.
- **Unary `#`**: cardinality — `#obj` returns the number of fields.
- **Iterator syntax** (`x in coll`): only valid as an argument to built-in functions like `map` and `filter`; user-defined functions receive plain positional arguments.
- **`merge`** requires non-overlapping keys (conflicts throw). **`concat`** requires the RHS keys to be strictly greater than all LHS keys. **`replace`** overwrites.
- **Template declarations** (`template_evaluate_element`, `template_evaluate_round` in `adjudication.tcl`) define interface contracts that function signatures can conform to via `function_name(template_name): ...`.

## TCL script files

| File | Purpose |
|------|---------|
| `adjudication.tcl` | Template signatures only — the abstract interfaces |
| `cross-majority-skating.tcl` | All adjudication logic (elimination, final, skating round) |
| `cms-round-wrapper.tcl` | High-level wrappers that compose dance-level and round-level evaluation |

All three are typically loaded together when running end-to-end.
