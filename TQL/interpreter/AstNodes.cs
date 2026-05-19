namespace TqlInterpreter;

// ── base ──────────────────────────────────────────────────────────────────────

public abstract record Node(int Line, int Col);
public abstract record DirectiveNode(int Line, int Col) : Node(Line, Col);
public abstract record ExprNode(int Line, int Col) : Node(Line, Col);

// ── support types ─────────────────────────────────────────────────────────────

public record Param(string Name, ExprNode? Default);
public record Arg(ExprNode Value, string? AsName);        // value [AS name]
public record Branch(ExprNode Cond, List<DirectiveNode> Body);
public record SelectCol(ExprNode Expr, string? Alias);
public record OrderByCol(ExprNode Expr, bool Desc);
public record LimitClause(ExprNode Count, List<string>? OnDistinct);
public record UsingCol(string Src, string? Tgt);           // src [AS tgt]
public record RowField(string? Name, ExprNode Value);      // [name AS] value
public record PatternPart(string? Text, ExprNode? Expr);   // exactly one non-null

public record SelectSpec(
    List<SelectCol> Cols,
    ExprNode        From,
    string?         FromAlias,
    ExprNode?       Where,
    List<string>    GroupBy,
    bool            GroupDesc,
    List<OrderByCol> OrderBy,
    LimitClause?    Limit);

// ── directives ────────────────────────────────────────────────────────────────

public record ImportDirective       (string Path, int Line, int Col) : DirectiveNode(Line, Col);
public record GlobalDeclDirective   (List<string> Names, int Line, int Col) : DirectiveNode(Line, Col);
public record GlobalAssignDirective (string Name, ExprNode Value, int Line, int Col) : DirectiveNode(Line, Col);
public record LetDirective          (string Name, ExprNode Value, int Line, int Col) : DirectiveNode(Line, Col);
public record FunctionDef           (string Name, List<Param> Params, List<DirectiveNode> Body, int Line, int Col) : DirectiveNode(Line, Col);
public record CallDirective         (string FuncName, List<Arg> Args, int Line, int Col) : DirectiveNode(Line, Col);
public record ReturnDirective       (ExprNode? Value, int Line, int Col) : DirectiveNode(Line, Col);
public record AbortDirective        (ExprNode Message, int Line, int Col) : DirectiveNode(Line, Col);
public record ForRangeDirective     (string Var, ExprNode From, ExprNode To, bool Desc, List<DirectiveNode> Body, int Line, int Col) : DirectiveNode(Line, Col);
public record ForInDirective        (string Var, ExprNode Source, List<DirectiveNode> Body, int Line, int Col) : DirectiveNode(Line, Col);
public record ForListDirective      (string Var, List<ExprNode> Values, List<DirectiveNode> Body, int Line, int Col) : DirectiveNode(Line, Col);
public record WhileDirective        (ExprNode Cond, List<DirectiveNode> Body, int Line, int Col) : DirectiveNode(Line, Col);
public record BreakDirective        (int Line, int Col) : DirectiveNode(Line, Col);
public record ContinueDirective     (int Line, int Col) : DirectiveNode(Line, Col);
public record IfDirective           (List<Branch> Branches, List<DirectiveNode>? ElseBody, int Line, int Col) : DirectiveNode(Line, Col);
public record InsertIntoDirective   (string Target, List<ExprNode> Sources, List<UsingCol>? Using, int Line, int Col) : DirectiveNode(Line, Col);
public record UpdateFromDirective   (string Target, ExprNode Source, List<UsingCol>? Using, int Line, int Col) : DirectiveNode(Line, Col);
public record ConcatToDirective     (string Target, ExprNode Source, List<UsingCol>? Using, int Line, int Col) : DirectiveNode(Line, Col);
public record AddColumnDirective    (string ColName, ColumnType ColType, ExprNode? Default, string TableName, bool IfNotExists, int Line, int Col) : DirectiveNode(Line, Col);
public record SaveTableDirective    (string TableName, ExprNode Path, int Line, int Col) : DirectiveNode(Line, Col);
public record LoadTableIntoDirective(ExprNode Path, string Target, int Line, int Col) : DirectiveNode(Line, Col);
public record ExprDirective         (ExprNode Expr, int Line, int Col) : DirectiveNode(Line, Col);

// ── expressions ───────────────────────────────────────────────────────────────

public record NullLit          (int Line, int Col) : ExprNode(Line, Col);
public record BoolLit          (bool V, int Line, int Col) : ExprNode(Line, Col);
public record NumberLit        (decimal V, int Line, int Col) : ExprNode(Line, Col);
public record StringLit        (string V, int Line, int Col) : ExprNode(Line, Col);          // '...'
public record StringPattern    (List<PatternPart> Parts, int Line, int Col) : ExprNode(Line, Col); // "..."
public record IdentExpr        (string Name, int Line, int Col) : ExprNode(Line, Col);
public record BracketedIdent   (string Name, int Line, int Col) : ExprNode(Line, Col);        // [name]
public record BinaryOp         (string Op, ExprNode L, ExprNode R, int Line, int Col) : ExprNode(Line, Col);
public record UnaryOp          (string Op, ExprNode Operand, int Line, int Col) : ExprNode(Line, Col);
public record IsNullExpr       (ExprNode Operand, bool Negated, int Line, int Col) : ExprNode(Line, Col);
public record InExpr           (ExprNode Value, bool Negated, ExprNode ColExpr, int Line, int Col) : ExprNode(Line, Col);
public record ValuesMatchExpr  (ExprNode RowExpr, int Line, int Col) : ExprNode(Line, Col);
public record ParenExpr        (ExprNode Inner, int Line, int Col) : ExprNode(Line, Col);
public record FuncCallExpr     (string Name, List<Arg> Args, int Line, int Col) : ExprNode(Line, Col);
public record SelectExpr       (SelectSpec S, int Line, int Col) : ExprNode(Line, Col);
public record UpdateSetExpr    (string Target, ExprNode? Where, ExprNode SourceRow, List<UsingCol>? Using, int Line, int Col) : ExprNode(Line, Col);
public record InsertExpr       (string Target, List<ExprNode> Sources, List<UsingCol>? Using, int Line, int Col) : ExprNode(Line, Col);
public record RowExpr          (List<RowField> Fields, int Line, int Col) : ExprNode(Line, Col);
public record TableIndex       (ExprNode TableExpr, ExprNode Index, int Line, int Col) : ExprNode(Line, Col);
public record TableColumn      (ExprNode TableExpr, string ColName, int Line, int Col) : ExprNode(Line, Col);
public record CreateTableExpr  (int Line, int Col) : ExprNode(Line, Col);
public record LoadTableExpr    (ExprNode Path, int Line, int Col) : ExprNode(Line, Col);
