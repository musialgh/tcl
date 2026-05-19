# TQL Interpreter — Implementation Plan

**Location**: `TQL/interpreter/` (parallel to `TQL/lsp/`), added to the existing `DSL.sln`.

**Target**: `.NET 10`, mirroring the TCL interpreter setup.

---

## File Structure

```
TQL/interpreter/
├── TqlInterpreter.csproj
├── Program.cs        — CLI entry point + argument parsing
├── Token.cs          — token type enum + Token record
├── Lexer.cs          — tokenizer with indentation tracking
├── AstNodes.cs       — all AST nodes as C# records (with Line/Col)
├── Parser.cs         — recursive-descent parser
├── TypeSystem.cs     — TqlValue hierarchy + Table class
├── Scope.cs          — GlobalScope + LocalScope
├── Executor.cs       — tree-walking interpreter
├── CsvIO.cs          — CSV read/write (`;` delimiter, multi-table)
└── Builtins.cs       — scalar built-in function implementations
```

---

## 1. CLI (`Program.cs`)

Left-to-right argument scan — maintain `currentReader` (default stdin) and `currentWriter` (default stdout):

| Arg | Effect |
|-----|--------|
| `<script>` | first positional arg is the entry script |
| `-r <file>` / `-r -` | set `currentReader` to file (or stdin) |
| `-w <file>` / `-w -` | set `currentWriter` to file (or stdout) |
| `-i <table>` | schedule read of `<table>` from `currentReader` |
| `-o <table>` | schedule write of `<table>` to `currentWriter` |

Execution sequence:
1. Read all scheduled input tables → inject into `GlobalScope`
2. Execute script (and any `IMPORT`ed scripts) against `GlobalScope`
3. Write all scheduled output tables from `GlobalScope`

Multiple `-i` from the same file: use the blank-line separator defined by the `SAVE TABLE` format.

---

## 2. Type System (`TypeSystem.cs`)

**`TqlValue`** — abstract base, with static `TqlValue.Null`:
- `DecimalValue(decimal V)` — covers DECIMAL, int, bool (TRUE=1, FALSE=0)
- `TextValue(string V)` — TEXT
- `GuidValue(Guid V)` — GUID
- `NullValue` — singleton
- `TableValue(Table T)` — wraps a Table

Comparison and arithmetic coerce types where semantically valid (DECIMAL ↔ bool, NULL propagation).

**`Table`** — following the design-doc recommendation:

```csharp
class Table {
    Dictionary<string, int>                        ColumnList;  // name→index
    List<Dictionary<TqlValue, SortedSet<int>>>     Indices;     // per-col: value→rowIds
    SortedDictionary<int, int>                     SortOrder;   // viewPos→rowId
    SortedDictionary<int, TqlValue[]>              Rows;        // rowId→cells
    TqlValue[]                                     Defaults;
    ColumnType[]                                   ColumnTypes;
    int                                            NextRowId;
}
```

Key methods (all documented):
- `AddColumn(name, type, defaultVal, ifNotExists)` — appends schema entry, rebuilds index slot
- `InsertRow(TqlValue[] values)` — appends to Rows, updates Indices, appends to SortOrder
- `UpdateCell(int rowId, int colIdx, TqlValue newVal)` — updates Rows + Indices
- `SetSortOrder(IEnumerable<int> orderedRowIds)` — replaces SortOrder
- `GetRowsInOrder()` — yields rows in SortOrder sequence
- `LookupByValue(int colIdx, TqlValue val)` → `SortedSet<int>` — fast equality filter
- `Clone()` — deep copy for function argument pass-by-value semantics

---

## 3. Lexer (`Lexer.cs`)

**Indentation model** (Python-style with TQL continuation rule):
- Track an indent stack; emit `INDENT`/`DEDENT` tokens
- A *logical line* ends at `;` (statement terminator) or `:` followed by a block
- A physical line that is **indented strictly deeper** than the logical-line opener is a **continuation line** of the same directive (not a new indent level)

**Token kinds**: keywords (`IMPORT`, `GLOBAL`, `LET`, `FUNCTION`, `CALL`, `RETURN`, `ABORT`, `FOR`, `IN`, `WHILE`, `BREAK`, `CONTINUE`, `IF`, `ELSE`, `SELECT`, `FROM`, `WHERE`, `GROUP`, `ORDER`, `BY`, `LIMIT`, `ON`, `DISTINCT`, `INSERT`, `INTO`, `VALUES`, `UPDATE`, `CONCAT`, `TO`, `SET`, `ADD`, `COLUMN`, `CREATE`, `LOAD`, `TABLE`, `SAVE`, `USING`, `AS`, `IS`, `NOT`, `NULL`, `TRUE`, `FALSE`, `AND`, `OR`, `XOR`, `ASC`, `DESC`, `DEFAULT`, `DECIMAL`, `TEXT`, `GUID`, `ROW`), `IDENT`, `BRACKETED_IDENT` (`[name-with-hyphens]`), `NUMBER`, `STRING_PATTERN` (`"..."`), `STRING_LITERAL` (`'...'`), operators (`=`, `<>`, `<`, `<=`, `>`, `>=`, `+`, `-`, `*`, `/`, `%`, `..`), punctuation (`(`, `)`, `[`, `]`, `,`, `:`, `.`, `;`), `INDENT`, `DEDENT`, `NEWLINE`, `EOF`.

---

## 4. AST Nodes (`AstNodes.cs`)

All `record` types with `int Line, int Col`. No docstrings — function heads carry a single-line comment.

**Directives** (sealed hierarchy from `DirectiveNode`):

```
ImportDirective(string Path)
GlobalDeclDirective(string[] Names)                        // GLOBAL a, b, c
GlobalAssignDirective(string Name, ExprNode Value)         // GLOBAL a = expr
LetDirective(string Name, ExprNode Value)
FunctionDef(string Name, List<Param> Params, List<DirectiveNode> Body)
CallDirective(string FuncName, List<Arg> Args)             // explicit CALL
ReturnDirective(ExprNode? Value)
AbortDirective(ExprNode Message)
ForRangeDirective(string Var, ExprNode From, ExprNode To, bool Desc, List<DirectiveNode> Body)
ForInDirective(string Var, ExprNode Source, List<DirectiveNode> Body)  // table or table.col
ForListDirective(string Var, List<ExprNode> Values, List<DirectiveNode> Body)
WhileDirective(ExprNode Cond, List<DirectiveNode> Body)
BreakDirective()
ContinueDirective()
IfDirective(List<Branch> Branches, List<DirectiveNode>? ElseBody)  // IF, ELSE IF, ELSE
InsertIntoDirective(string Target, ExprNode Source, List<UsingCol>? Using)
UpdateFromDirective(string Target, ExprNode Source, List<UsingCol>? Using)
ConcatToDirective(string Target, ExprNode Source, List<UsingCol>? Using)
AddColumnDirective(string Col, ColumnType Type, ExprNode? Default, string Table, bool IfNotExists)
SaveTableDirective(string TableName, ExprNode Path)
ExprDirective(ExprNode Expr)                               // function call without CALL keyword
```

**Expressions** (sealed hierarchy from `ExprNode`):

```
NullLit()
BoolLit(bool V)
NumberLit(decimal V)
StringLit(string V)                    // '...'
StringPattern(List<PatternPart> Parts) // "..." with {expr} holes
IdentExpr(string Name)
BracketedIdent(string Name)            // [group-1]
BinaryOp(BinOp Op, ExprNode L, ExprNode R)
UnaryOp(UnOp Op, ExprNode Operand)    // NOT, -, +
IsNullExpr(ExprNode Operand, bool Negated)
InExpr(ExprNode Value, bool Negated, ExprNode ColumnExpr)
ValuesMatchExpr(ExprNode RowExpr)
ParenExpr(ExprNode Inner)
FuncCallExpr(string Name, List<Arg> Args)
SelectExpr(SelectSpec S)
UpdateSetExpr(string Target, ExprNode? Where, ExprNode SourceRow, List<UsingCol>? Using)
InsertExpr(string Target, ExprNode Source, List<UsingCol>? Using)
RowExpr(List<RowField> Fields)         // ROW(...)
TableIndex(ExprNode Table, ExprNode Index)
TableColumn(ExprNode Table, string Col)
CreateTable()
LoadTable(ExprNode Path)
```

**Support types**: `Param(string Name, ExprNode? Default)`, `Arg(ExprNode Value, string? AsName)`, `Branch(ExprNode Cond, List<DirectiveNode> Body)`, `SelectSpec` (all SELECT clauses), `UsingCol(string Src, string? Tgt)`, `RowField(string? Name, ExprNode Value)`, `PatternPart(string? Text, ExprNode? Expr)`.

---

## 5. Parser (`Parser.cs`)

**Block rule**: after `:` — if a non-whitespace token follows on the same line, parse one directive as a single-statement body; otherwise expect `INDENT`…`DEDENT`.

**Disambiguation**:
- `FOR x IN n..m` → `ForRangeDirective`
- `FOR x IN expr.col` → `ForInDirective` with `TableColumn` source
- `FOR x IN expr` → `ForInDirective` with table source
- `name(…)` at statement level without `CALL` → `ExprDirective`
- `UPDATE` with `FROM` → directive; with `WHERE`/`SET` → expression
- `INSERT INTO` as directive when not inside an expression context

---

## 6. Scope (`Scope.cs`)

```csharp
class GlobalScope {
    Dictionary<string, TqlValue>    Variables;
    Dictionary<string, FunctionDef> Functions;
}

class LocalScope {
    GlobalScope                  Global;
    Dictionary<string, TqlValue> Locals;
    HashSet<string>              Imported;   // names made visible via GLOBAL decl

    TqlValue Resolve(string name);           // Locals → Imported globals → error
    void Bind(string name, TqlValue val);    // always local
    void ImportGlobal(string name);          // GLOBAL decl in function body
    void WriteGlobal(string name, TqlValue val); // GLOBAL name = expr
}
```

---

## 7. Executor (`Executor.cs`)

**Control flow**: `BreakException`, `ContinueException`, `ReturnException(TqlValue)` — thrown and caught at the appropriate boundary.

**FOR IN semantics**:
- `ForRangeDirective` → integer counter
- `ForInDirective` with `TableColumn` source → scalar values (one per row)
- `ForInDirective` with plain table → row objects (each row as a `RowValue` with named fields)

**SELECT evaluation pipeline**:
1. Evaluate `FROM` expression → source `Table`; handle optional `AS alias`
2. Evaluate `WHERE` against each row (column names in scope)
3. If `GROUP BY`: partition rows, evaluate aggregate columns per group
4. Evaluate all `SELECT` columns (scalar or aggregate)
5. `ORDER BY`: apply sort, update `SortOrder`
6. `LIMIT n`: keep first n rows; `ON DISTINCT cols`: count distinct value-groups instead of rows

**String interpolation**: evaluate `{expr}` parts, convert to string, concatenate.

**`GLOBAL a, b, c` inside functions**: each name is registered in `LocalScope.Imported` so `Resolve` returns the global's value.

**USING clause** for `INSERT`/`UPDATE`/`CONCAT TO`: maps source column names to target column names before writing.

---

## 8. Built-in Functions (`Builtins.cs`)

Scalar: `IF(cond, t, f)`, `ROWS(table)`, `GUID()`

Aggregate (evaluated inside SELECT GROUP BY): `SUM`, `COUNT(*)`, `COUNT(expr)`, `MIN`, `MAX`, `AVG`, `MEDIAN`, `FIRST`

---

## 9. CSV I/O (`CsvIO.cs`)

**Format** (per `SAVE TABLE` spec):
- Delimiter: `;`
- All values (including column headers) quoted with `"`; embedded `"` → `\"`
- Row 0: column names
- Data rows follow
- Trailing blank line (enables multi-table files)

**Read**: infer column types (DECIMAL if parseable, TEXT otherwise); stop at blank line.
**Write**: serialize all rows in `SortOrder`; emit trailing blank line.

---

## 10. Error Handling

- `TqlParseException(msg, line, col)` — from Lexer/Parser
- `TqlRuntimeException(msg, line, col)` — from Executor (node's Line/Col)
- `TqlAbortException(msg)` — from `ABORT` directive
- Top-level `Main` catches all, writes to stderr, exits non-zero.

---

## Notable design decisions

| Decision | Rationale |
|----------|-----------|
| Single file per concern, no partial classes | mirrors TCL interpreter style |
| `Table.Indices` enables O(log n) equality filters | as per design doc |
| `SortOrder` is separate from row storage | ORDER BY doesn't move physical rows |
| `cross-majority-skating.tql` treated as non-authoritative | user confirmed it may contain bugs |
