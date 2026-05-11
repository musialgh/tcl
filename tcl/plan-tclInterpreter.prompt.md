# Plan: TCL Interpreter in C#

## TL;DR
Build a command-line C# interpreter that parses and executes a functional DSL for adjudication algorithms. Parse the two .tcl files to extract function signatures and implementations, then execute any function via CLI by accepting JSON input (named parameters) and returning JSON output. The interpreter uses an AST-based execution model with a functional programming semantics, supporting recursion with configurable depth limits.

**Approach**: 1. Parser: Lex/parse the DSL syntax into an AST
2. Type system: Map DSL types to runtime objects with validation
3. Executor: Walk the AST to evaluate expressions with proper scoping
4. JSON bridge: Convert between JSON input ↔ DSL runtime values, validate output keys
5. CLI: Accept function name + JSON params, return JSON results or error

---

## Steps

**Phase 1: Parser & Type System**
1. Create lexer to tokenize DSL syntax (identifiers, keywords, operators, literals, strings)
2. Build recursive descent parser for:
   - Function signatures (name, template ref, input params, output params)
   - Function implementations (assignments, where-clauses, conditionals)
   - Type annotations (primitives, objects with key patterns, ranges)
   - Expressions (operators, function calls, indexing, map/filter/etc)
3. Represent AST nodes for all language constructs
4. Define type system with narrowing hierarchy:
   - Primitive narrowing: `any > num > real` and `any > num > int > nat > bool`
   - Object narrowing: `any > object`
   - Map DSL types to C# types (use `decimal` for `real`/`num` for precision; 64-bit for `int`/`nat`)
   - Template matching: signatures referring to a template narrow permitted inputs/outputs (runtime verification optional) as compared to the template definition
   - Object key cardinality: `+` (one or more), `*` (zero or more), `?` (optional)

**Phase 2: Built-in Functions & Object Model**
5. Implement object model as `map[key -> value]` with ordering:
   - Numeric keys sort lower than string keys
   - Empty object `{}` sorts lowest
   - Notation order irrelevant (no implicit numerical index)
6. Implement all built-in functions:
   - Collection: `map`, `filter`, `keys`, `first` (error on empty), `count`, `sum`
   - Aggregation (context-aware):
     - `concat()`: merge-like, requires keys from later args all > keys from earlier args (append behavior); error on conflict
     - `merge()`: recursively combines sub-objects by key; error on conflicting keys (even with identical values)
     - `replace()`: merge-like but later args overwrite earlier ones
   - Aggregate behavior: work on keys/indices range if first arg has "in" clause; otherwise all supplied args
   - Type-specific: `count_and_sum`, `count_and_sum_maj`, `compute_places`
   - Operators: unary `-` (apply recursively through object tree), `+`, `#` (cardinality), `?` (key existence); binary `<`, `>`, `=`, `<=`, `>=`, `+`, `-`, `*`, `/`, indexing `[key]`
7. Object comparison (<, >, =): flattened depth-first traversal in increasing key order; compare values only, not keys

**Phase 3: Executor & Variable Binding**
7. Design execution environment:
   - Variable scope stack for function parameters and `where` bindings
   - Recursion depth counter with configurable limit (default: 10000)
   - Error reporting with position info where available
8. Variable binding rules:
   - Before `where` clause: only return value tags (e.g., `report`, `places`) may be assignment targets
   - After `where` clause: resolve all missing bindings lazily; new symbols may be bound here
   - Lazy evaluation: compute `where` expressions only when needed
9. Implement evaluator that walks AST:
   - Resolve variable references from scope stack
   - Apply unary operators recursively through object trees (e.g., `-report` negates all numeric values)
   - Apply binary operators element-wise
   - Handle conditional expressions: `if cond | true_expr | false_expr` (pattern chain)
   - Evaluate assignment sequences; return tagged output values as named-field object
   - `first()` on empty object raises runtime error
10. Multi-value returns: object with named fields matching signature output names

**Phase 4: JSON Bridge & Validation**
10. JSON deserialization:
    - Parse input JSON object → map parameter names to values
    - Convert JSON primitives/objects → DSL runtime values
    - Validate input types against function signature
11. JSON serialization:
    - Traverse output object and **validate** that all object keys are strings or numbers only (no nested objects as keys)
    - If any non-primitive key detected, raise error: "Object keys may only be strings or numbers"
    - Serialize to JSON with proper number formatting (preserve `real` precision, ensure `int` outputs integers)
12. Handle optional parameters: if not provided, may be unbound or use default value (if specified in signature)

**Phase 5: CLI & Main**
13. CLI entry point:
    - Arguments: `tcl <function_name> [--recursion-limit <N>] [--input-file <path>]`
    - Default: read JSON from stdin or accept as argument
    - Options: output formatting (compact vs. pretty), debug mode
    - Exit codes: 0 for success, non-zero for errors
14. Error handling:
    - Parse errors: show line/column and error message
    - Type errors: show parameter name, expected type, got type
    - Runtime errors: show call stack and error detail
    - Object key validation errors: clear message with offending key

**Phase 6: Testing & Refinement**
15. Create test suite:
    - Unit tests for each built-in function
    - Integration tests using the skating functions from the files (complex recursive case)
    - Edge cases: empty collections, single-element collections, deeply nested objects
16. Validate against original functions:
    - Test `evaluate_dance_elimination`, `evaluate_dance_final`, `evaluate_round_final`, etc.
    - Confirm output matches expected structure

---

## Relevant Files

**For Implementation:**
- Parser target files: [adjudication.tcl](adjudication.tcl), [cross-majority-skating.tcl](cross-majority-skating.tcl) — extract function definitions
- Core modules to create:
  - `Lexer.cs` — tokenization
  - `Parser.cs` — AST construction from tokens
  - `AstNodes.cs` — all AST node types
  - `TypeSystem.cs` — type definitions and validation
  - `Executor.cs` — AST walker and expression evaluator
  - `BuiltInFunctions.cs` — implementation of all built-in ops
  - `JsonBridge.cs` — JSON ↔ DSL value conversion with validation
  - `Program.cs` — CLI entry point and error handling
  - `Tests/` — unit and integration tests

---

## Verification

1. **Parser validation**: Parse both .tcl files without syntax errors, extract all 7 functions with correct signatures
2. **Type system**: Validate that input JSON matches function parameter types; catch mismatches
3. **Built-ins**: Each built-in function returns correct output for representative inputs (especially `map`, `filter`, `merge`)
4. **Object key validation**: Attempt to output result with `{ objkey : value }` where `objkey` is an object; verify error is raised
5. **Integration**: Run `evaluate_dance_elimination` with sample marks input → verify output has correct structure and places are ordered
6. **Skating functions**: Execute `evaluate_round_final_skating` with multi-dance input → verify recursive calls and R10/R11 reports computed
7. **Recursion limit**: Set limit to 100, call recursive function that would exceed → verify error occurs gracefully
8. **CLI**: Test via command line:
   ```bash
   tcl evaluate_dance_elimination --input-file test_marks.json
   tcl compute_places --recursion-limit 5000 --input-file test_scores.json > output.json
   ```

---

## Decisions

- **Extensibility**: Interpreter designed to handle any valid DSL program, not hardcoded to current functions. Allows reuse when DSL grows.
- **Named parameters**: JSON object with field names matching signature, not positional. More readable and error-resistant.
- **Object key validation**: Runtime, not parse-time. Allows complex intermediate computations with object keys (as used in `skating_refine_r11`); error only when result needs serialization.
- **Multi-return format**: Named-field object (e.g., `{"report": {...}, "places": {...}}`). Preserves semantics of multiple output bindings.
- **Number precision**: Use `decimal` for `real` and `num`; `int` and `nat` as 64-bit integers. Matches DSL intent of exact decimal arithmetic.
- **Object model**: Implemented as `SortedMap[key -> value]` where numeric keys sort lower than string keys; empty object sorts lowest; notation order irrelevant.
- **Object comparison**: Uses depth-first traversal in increasing key order; compares values only (not keys), allowing different key arrangements.
- **Recursive negation**: Unary `-` applies recursively to all numeric values in nested object trees (element-wise for full tree).
- **Aggregation semantics**:
  - `merge()`: recursively combines corresponding sub-objects by key; strict error on conflicting keys (even with identical values).
  - `concat()`: merge-like with validation that keys from later args are all strictly greater than earlier args (guarantees append behavior).
  - `replace()`: merge-like but allows conflicts; later arguments overwrite earlier values.
  - Context: all three work on keys/indices range if first arg has "in" clause; otherwise all supplied args.
- **Lazy evaluation**: `where` clauses evaluate only when needed (short-circuit for recursive functions). Reduces computation for expensive sub-expressions.
- **Recursion limit**: Configurable, default 10k. Prevents stack overflow on malformed recursion while allowing legitimate deep calls like skating algorithms.
- **Variable binding**: Before `where`, only return tags (`report`, `places`, etc.) as assignment targets; after `where`, new bindings allowed and resolved lazily.
- **Type verification**: Optional feature (configurable); violations lead to runtime errors in code if unchecked.

---

## Further Considerations

1. **Aggregation "in" clause semantics**: When a function like `sum(dance in marks, ...)` is called, the "in" clause indicates iteration over keys/indices. Ensure parser correctly identifies and executor correctly distributes work over the key range.

2. **concat() validation**: The concat function requires strict key ordering across arguments. Consider adding detailed error messages showing which keys violate the ordering constraint when validation fails.

3. **merge() strict conflict detection**: Even identical values cause conflict errors in `merge()`. This is strict by design; ensure runtime checks compare both key *and* value during merge to distinguish from `replace()`.

4. **Object key serialization edge case**: Numeric keys (e.g., `1`, `2`) must serialize correctly in JSON; verify that integer keys remain integers and don't become strings. String keys like `"10"` should remain strings to preserve ordering semantics.
