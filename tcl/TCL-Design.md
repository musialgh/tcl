# TCL Design

TCL stands for _Tanzen Competition Language_. It is a small functional DSL for defining
ballroom-dance competition adjudication algorithms. A TCL program is a set of typed function
definitions. The interpreter loads one or more `.tcl` files, evaluates a named function against a
JSON input, and writes the result as JSON to stdout.

## Lexical Structure

- Source files use UTF-8 encoding and carry the `.tcl` extension.
- `//` and `///` introduce a line comment; everything up to the end of the line is ignored.
- Identifiers start with a letter or `_`, followed by letters, digits, or `_`. Module names in
  `import` statements may additionally contain `-`.
- Each top-level definition ends with `;`.
- Keywords (case-sensitive, lowercase): `where`, `if`, `in`, `import`, `as`, `true`, `false`.
- Type keywords: `bool`, `nat`, `int`, `real`, `num`, `string`, `object`, `any`.

### Literals

| Form | Examples | Type |
|------|----------|------|
| Integer | `0`, `42`, `-7` | `nat` / `int` |
| Decimal | `1.5`, `3.14`, `-0.5` | `real` |
| Boolean | `true`, `false` | `bool` |
| String | `"hello"`, `"line\n"` | `string` |
| Object | `{ 1 : "a", 2 : "b" }` | `object` |
| Range | `1..10` | `object` (int keys 1–10, value = key) |

String escape sequences: `\n`, `\t`, `\r`, `\\`, `\"`.

## Type System

### Value Types

| Type | Description |
|------|-------------|
| `bool` | Boolean (`true` / `false`). Stored as `BoolValue`. |
| `nat` | Natural number (non-negative integer). Sub-type of `int`. |
| `int` | Integer. Sub-type of `real`. |
| `real` | Decimal number (arbitrary precision). Sub-type of `num`. |
| `num` | Generic numeric. Accepts `nat`, `int`, or `real`. |
| `string` | Text string. |
| `object` | Ordered key → value collection (see below). |
| `any` | Top type — accepts any value. |

**Numeric sub-type hierarchy** (narrowest to widest): `bool` ⊂ `nat` ⊂ `int` ⊂ `real` ⊂ `num` ⊂ `any`.

Division always yields `real`. Mixed `int`/`real` arithmetic yields `real`. All other arithmetic
preserves the widest operand type.

### ObjectValue

`ObjectValue` is the universal collection type, backed by a sorted dictionary. Key ordering:

1. Numeric keys (ascending)
2. String keys (ascending)
3. ObjectValue keys (ascending by depth-first value traversal)

An empty `ObjectValue` also serves as the null / absent value.

### Truthiness

| Type | Truthy when |
|------|-------------|
| `bool` | `true` |
| numeric | non-zero |
| `string` | non-empty |
| `object` | non-empty |

### Object-Type Annotations (in signatures)

Inside signature declarations, object fields carry a cardinality suffix:

| Suffix | Meaning |
|--------|---------|
| _(none)_ | Exactly one entry with this key |
| `+` | One or more entries with this key prefix |
| `*` | Zero or more |
| `?` | Zero or one (optional) |

Example: `{ start_no+ : { "jury" : { adjudicator+ : nat } } }`

## Program Structure

A `.tcl` file is a sequence of top-level items, each terminated by `;`:

```
program = (import_stmt | signature_decl | function_def)*
```

### Import

```
import <module-name>;
import <module-name> as <alias>;
```

Makes all functions defined in the named module visible to subsequent definitions in the same
file. Module loading is handled externally (by passing `--script` flags to the interpreter).

### Signature Declaration

A signature declares the typed interface of a function, optionally referencing a template:

```
<name>(<template?>):
    <param> : <type>,
    <param>? : <type> = <default>
->
    <return> : <type>,
    <return>* : <type>
;
```

A signature can also copy another signature verbatim:

```
<name>(<existing-signature-name>);
```

The `?` suffix on a parameter makes it optional; `= <default>` supplies the default value.

### Function Definition

A function definition pairs a signature with a body:

```
<name>(<template?>): <signature>
<name>(<param1>, <param2>, ...) =
    <statement>,
    <statement>,
    ...
[where
    <name> = <expr>,
    <name1>, <name2> = <expr>,
    ...]
;
```

The body is a comma-separated list of statements. The `where` clause (if present) follows after
the body statements and introduces lazily-evaluated bindings.

## Expressions

### Operator Precedence (highest to lowest)

| Precedence | Operator(s) | Associativity |
|-----------|-------------|---------------|
| 7 (highest) | `expr[key]` (indexing) | left |
| 6 | `-expr`, `+expr`, `#expr`, `?expr[key]` (unary) | prefix |
| 5 | `*`, `/` | left |
| 4 | `+`, `-` | left |
| 3 | `<`, `>`, `<=`, `>=`, `=` | left |
| 2 | `expr if cond \| expr if cond \| expr` (conditional) | — |
| 1 (lowest) | `expr where name = e, ...` (inline where) | — |

### Unary Operators

| Operator | Operand | Result |
|----------|---------|--------|
| `-expr` | numeric | arithmetic negation |
| `+expr` | numeric | identity |
| `#expr` | `object` | number of fields (cardinality) |
| `?expr[key]` | `object`, key | `true` if key exists in object |

`?` is only valid as a prefix to an index expression `?obj[key]`.

### Binary Operators

| Operator | Types | Result |
|----------|-------|--------|
| `+`, `-`, `*` | numeric, numeric | numeric |
| `/` | numeric, numeric | `real` (error if divisor is zero) |
| `<`, `>`, `<=`, `>=` | numeric / string / object | `bool` |
| `=` | any, any | `bool` |

### Indexing

```
obj[key]         // single key
obj[k1][k2]      // chained — equivalent to (obj[k1])[k2]
```

Key may be any expression. Missing key throws a runtime error.

### Range Expression

```
low..high
```

Produces an `object` with integer keys from `low` to `high` (inclusive), each mapped to itself.
Useful as a loop domain: `concat(i in 1..#items, ...)`.

### Conditional Expression

```
expr1 if cond1 | expr2 if cond2 | expr_default
```

Branches are evaluated left to right; the first truthy condition wins. The final branch has no
`if` and acts as the default. All branches must be present (no implicit null fall-through).

### Object Literal

```
{ key1 : expr1, key2 : expr2 }
```

Keys may be identifiers, string literals, integer literals, or object expressions. Values can be
any expression including nested object literals.

### Function Call

```
func(arg1, arg2)
```

Arguments are positional. Built-in functions additionally accept an _iterator clause_:

```
func(var in collection, expr)
func(key, val in collection, expr)
```

See **Built-in Functions** below.

## Statements & Function Bodies

A function body is a comma-separated list of statements followed by an optional `where` clause.

### Assignment

```
name = expr
```

Binds `name` in the function's result scope.

### Multi-target Assignment

```
name1, name2, name3 = expr
```

`expr` must evaluate to an `object`. Values are extracted in sorted key order and assigned to the
names left-to-right.

### Implicit Return

The last statement in a body is the return value. If the signature declares named return fields,
those names must be assigned before the body ends.

### Where Clause

```
where
    name1 = expr1,
    name2 = expr2,
    name3, name4 = expr3
```

Bindings are **lazy**: each is evaluated at most once, only when first referenced. The order of
bindings is irrelevant. Circular dependencies are detected at runtime.

## Built-in Functions

### `map(var in collection, expr) → object`

Transforms each entry. Returns an object with the same keys; each value is replaced by `expr`
evaluated with `var` bound to the original value.

```tcl
map(score in raw_scores, score * 2)
map(key, val in table, { key : val + 1 })
```

With two loop variables the first receives the key, the second the value.

### `filter(var in collection, predicate) → object`

Retains entries where `predicate` is truthy. Keys are preserved.

```tcl
filter(mark in marks, mark > 5)
```

### `keys(collection) → object`

Returns an object mapping each key to itself. Useful to iterate over keys.

```tcl
starts = keys(marks)      // { s1 : s1, s2 : s2, ... }
```

### `first(collection) → value`

Returns the value of the first entry (lowest key). Throws if the collection is empty.

```tcl
top = first(sorted_places)
```

### `count(collection) → nat`  
### `count(var in collection, predicate) → nat`

Without iterator: counts entries whose value is truthy.  
With iterator: counts entries where `predicate` holds.

```tcl
count(votes)                         // how many non-zero votes
count(v in votes, v >= threshold)    // how many votes meet threshold
```

### `sum(collection) → real`  
### `sum(var in collection, expr) → real`

Without iterator: sums all values (must be numeric).  
With iterator: sums the result of `expr` for each entry.

```tcl
sum(scores)
sum(s in scores, s * weight)
```

### `merge(obj1, obj2, ...) → object`  
### `merge(var in collection, expr) → object`

Combines objects. Nested objects at the same key are merged recursively. A conflict on a
non-object key throws a runtime error.

```tcl
merge(base_report, extra_data)
merge(start in results, { start : summary[start] })
```

### `concat(obj1, obj2, ...) → object`  
### `concat(var in collection, expr) → object`

Concatenates objects. All keys from the right-hand side must be strictly greater than all keys
from the left-hand side; otherwise a runtime error is thrown.

```tcl
concat(place in 1..#field, { place : compute(place) })
```

### `replace(obj1, obj2, ...) → object`

Like `merge` but overwrites conflicting non-object keys instead of erroring.

```tcl
replace(old_report, corrections)
```

### `arg_min(collection) → object`

Finds the minimum value in `collection` and returns a new object containing all entries that hold
that minimum value (key → key mapping, as with `keys`). Throws if the collection is empty.

```tcl
leaders = arg_min(scores)   // all starters sharing the best (lowest) score
```

### `dump(expr) → value`

Writes the value of `expr` to stderr with source location, then returns the value unchanged.
Intended for debugging only.

```tcl
dump(intermediate_result)
```

## Template & Signature System

A _template_ is a named signature that defines an interface contract:

```tcl
template_evaluate_element:
    marks : { start_no+ : { adj_group+ : { adj+ : num } } },
    params? : { param+ : num }
->
    places : { start_no+ : { "pmin" : num, "pmax" : num, "pval" : num } },
    report? : { start_no+ : object };
```

A function implementation declares conformance by naming the template in parentheses:

```tcl
evaluate_dance_final(template_evaluate_element):
    marks : { start_no+ : { "jury" : { adj+ : nat } } }
-> ...;

evaluate_dance_final(marks) = ...;
```

This allows the calling code to dispatch to any conforming implementation by template name.

## Execution Semantics

- The interpreter evaluates a single named function, passing JSON-decoded arguments.
- JSON arrays become `ObjectValue` instances with 0-based integer keys.
- JSON `null` becomes an empty `ObjectValue`.
- Numeric JSON keys (e.g. `"1"`) are stored as integers.
- Maximum call-stack depth: 10 000.
- Errors include source file, line, and column.

### Tracing & Debugging

```bash
# Print every function call to stderr:
dotnet run ... -- func_name --input-file input.json --trace-calls

# Inline debug print (returns value unchanged):
dump(some_expr)
```
