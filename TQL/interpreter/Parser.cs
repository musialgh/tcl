using System.Globalization;
using System.Text;

namespace TqlInterpreter;

/// <summary>
/// Recursive-descent parser. Block structure is inferred from token positions
/// (line/col) — no INDENT/DEDENT tokens. A directive ends at ';'. After ':', if
/// the next token is on the same line the body is a single inline directive;
/// otherwise the body is all directives whose starting column >= the first
/// directive's column.
/// </summary>
public class Parser(List<Token> tokens)
{
    private readonly List<Token> _tokens = tokens;
    private int _pos;

    // ── public entry ─────────────────────────────────────────────────────────

    public List<DirectiveNode> ParseProgram() => ParseBlock(0);

    // ── token access ──────────────────────────────────────────────────────────

    private Token Peek(int offset = 0)
    {
        int i = _pos + offset;
        return i < _tokens.Count ? _tokens[i] : _tokens[^1];
    }

    private Token Consume()
    {
        var t = Peek();
        if (t.Kind != Tk.Eof) _pos++;
        return t;
    }

    private Token Expect(Tk kind)
    {
        var t = Peek();
        if (t.Kind != kind) Err($"Expected {kind}, got '{t.Text}'", t);
        return Consume();
    }

    private bool Match(Tk kind) { if (Peek().Kind == kind) { Consume(); return true; } return false; }

    private static void Err(string msg, Token t) =>
        throw new TqlParseException(msg, t.Line, t.Col);

    /// <summary>Accept any name-like token (keyword or ident or bracketed).</summary>
    private string ExpectName()
    {
        var t = Peek();
        if (t.Kind == Tk.BracketIdent || t.Kind == Tk.Ident || (int)t.Kind >= (int)Tk.KwAbort)
        { Consume(); return t.Text; }
        Err($"Expected name, got '{t.Text}'", t);
        return "";
    }

    // ── block / body helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Parse directives whose first-token col >= baseCol, stopping on EOF,
    /// dedent, or an ELSE keyword (which belongs to an enclosing IF).
    /// </summary>
    private List<DirectiveNode> ParseBlock(int baseCol)
    {
        var list = new List<DirectiveNode>();
        while (true)
        {
            var next = Peek();
            if (next.Kind == Tk.Eof) break;
            if (next.Col < baseCol)  break;
            if (next.Kind == Tk.KwElse) break;
            list.Add(ParseDirective());
        }
        return list;
    }

    /// <summary>
    /// Called immediately after the colon of a block-opening statement.
    /// colonLine is the line the colon was on; if the next token is on
    /// that same line the body is one inline directive, otherwise indented.
    /// </summary>
    private List<DirectiveNode> ParseBodyAfterColon(int colonLine)
    {
        var next = Peek();
        if (next.Kind == Tk.Eof) return [];
        if (next.Line == colonLine) return [ParseDirective()];
        return ParseBlock(next.Col);
    }

    // ── directive dispatch ────────────────────────────────────────────────────

    private DirectiveNode ParseDirective()
    {
        var tok = Peek();
        return tok.Kind switch
        {
            Tk.KwImport   => ParseImport(),
            Tk.KwGlobal   => ParseGlobal(),
            Tk.KwLet      => ParseLet(),
            Tk.KwFunction => ParseFunctionDef(),
            Tk.KwCall     => ParseCallDir(),
            Tk.KwReturn   => ParseReturn(),
            Tk.KwAbort    => ParseAbort(),
            Tk.KwFor      => ParseFor(),
            Tk.KwWhile    => ParseWhile(),
            Tk.KwBreak    => ParseBreak(),
            Tk.KwContinue => ParseContinue(),
            Tk.KwIf       => ParseIf(),
            Tk.KwInsert   => ParseInsertDir(),
            Tk.KwUpdate   => ParseUpdateDir(),
            Tk.KwConcat   => ParseConcatDir(),
            Tk.KwAdd      => ParseAddColDir(),
            Tk.KwSave     => ParseSaveDir(),
            Tk.KwLoad     => ParseLoadDir(),
            _             => ParseExprOrAssignDir()
        };
    }

    // ── individual directives ─────────────────────────────────────────────────

    private ImportDirective ParseImport()
    {
        var tok = Consume();
        // accept both 'path' and "path" (StrPat without interpolation)
        var pathTok = Peek();
        string path = pathTok.Kind switch
        {
            Tk.StrLit => Consume().Text,
            Tk.StrPat => Consume().Text,
            _ => throw new TqlParseException($"Expected path string, got '{pathTok.Text}'", pathTok.Line, pathTok.Col)
        };
        Expect(Tk.Semi);
        return new ImportDirective(path, tok.Line, tok.Col);
    }

    private DirectiveNode ParseGlobal()
    {
        var tok = Consume(); // GLOBAL
        string first = ExpectName();
        if (Match(Tk.Eq))
        {
            var val = ParseExpr(); Expect(Tk.Semi);
            return new GlobalAssignDirective(first, val, tok.Line, tok.Col);
        }
        var names = new List<string> { first };
        while (Match(Tk.Comma)) names.Add(ExpectName());
        Expect(Tk.Semi);
        return new GlobalDeclDirective(names, tok.Line, tok.Col);
    }

    private LetDirective ParseLet()
    {
        var tok = Consume(); // LET
        string name = ExpectName();
        Expect(Tk.Eq);
        var val = ParseExpr(); Expect(Tk.Semi);
        return new LetDirective(name, val, tok.Line, tok.Col);
    }

    private FunctionDef ParseFunctionDef()
    {
        var tok = Consume(); // FUNCTION
        string name = ExpectName();
        Expect(Tk.LParen);
        var parms = ParseParamList();
        Expect(Tk.RParen);
        var c = Expect(Tk.Colon);
        var body = ParseBodyAfterColon(c.Line);
        return new FunctionDef(name, parms, body, tok.Line, tok.Col);
    }

    private List<Param> ParseParamList()
    {
        var ps = new List<Param>();
        if (Peek().Kind == Tk.RParen) return ps;
        ps.Add(ParseParam());
        while (Match(Tk.Comma)) ps.Add(ParseParam());
        return ps;
    }

    private Param ParseParam()
    {
        string name = ExpectName();
        ExprNode? def = Match(Tk.Eq) ? ParseExpr() : null;
        return new Param(name, def);
    }

    private CallDirective ParseCallDir()
    {
        var tok = Consume(); // CALL
        string name = ExpectName();
        Expect(Tk.LParen);
        var args = ParseArgList();
        Expect(Tk.RParen);
        Expect(Tk.Semi);
        return new CallDirective(name, args, tok.Line, tok.Col);
    }

    private ReturnDirective ParseReturn()
    {
        var tok = Consume(); // RETURN
        if (Peek().Kind == Tk.Semi) { Consume(); return new ReturnDirective(null, tok.Line, tok.Col); }
        var val = ParseExpr(); Expect(Tk.Semi);
        return new ReturnDirective(val, tok.Line, tok.Col);
    }

    private AbortDirective ParseAbort()
    {
        var tok = Consume(); // ABORT
        var msg = ParseExpr(); Expect(Tk.Semi);
        return new AbortDirective(msg, tok.Line, tok.Col);
    }

    private DirectiveNode ParseFor()
    {
        var tok = Consume(); // FOR
        string var_ = ExpectName();
        Expect(Tk.KwIn);

        // FOR x IN (list)
        if (Peek().Kind == Tk.LParen && !IsSelectNext(1))
        {
            Consume();
            var vals = new List<ExprNode> { ParseExpr() };
            while (Match(Tk.Comma)) vals.Add(ParseExpr());
            Expect(Tk.RParen);
            var c0 = Expect(Tk.Colon);
            return new ForListDirective(var_, vals, ParseBodyAfterColon(c0.Line), tok.Line, tok.Col);
        }

        // parse the source; detect range (expr .. expr)
        var src = ParseAddSub(); // parse left side of possible ..
        if (Peek().Kind == Tk.DotDot)
        {
            Consume();
            var to = ParseAddSub();
            bool desc = Match(Tk.KwDesc); if (!desc) Match(Tk.KwAsc);
            var c1 = Expect(Tk.Colon);
            return new ForRangeDirective(var_, src, to, desc, ParseBodyAfterColon(c1.Line), tok.Line, tok.Col);
        }

        // finish parsing the source expression (postfix .col and [idx] on top of what we have)
        src = FinishExpr(src);
        var c2 = Expect(Tk.Colon);
        return new ForInDirective(var_, src, ParseBodyAfterColon(c2.Line), tok.Line, tok.Col);
    }

    // true if the token at _pos+offset is SELECT
    private bool IsSelectNext(int offset) =>
        _pos + offset < _tokens.Count && _tokens[_pos + offset].Kind == Tk.KwSelect;

    private WhileDirective ParseWhile()
    {
        var tok = Consume(); // WHILE
        var cond = ParseExpr();
        var c = Expect(Tk.Colon);
        return new WhileDirective(cond, ParseBodyAfterColon(c.Line), tok.Line, tok.Col);
    }

    private BreakDirective ParseBreak()
    {
        var tok = Consume(); Expect(Tk.Semi);
        return new BreakDirective(tok.Line, tok.Col);
    }

    private ContinueDirective ParseContinue()
    {
        var tok = Consume(); Expect(Tk.Semi);
        return new ContinueDirective(tok.Line, tok.Col);
    }

    private IfDirective ParseIf()
    {
        var tok = Consume(); // IF
        int ifCol = tok.Col;
        var branches = new List<Branch>();
        List<DirectiveNode>? elseBody = null;

        var cond = ParseExpr();
        var c = Expect(Tk.Colon);
        branches.Add(new Branch(cond, ParseBodyAfterColon(c.Line)));

        while (Peek().Kind == Tk.KwElse && Peek().Col == ifCol)
        {
            Consume(); // ELSE
            if (Peek().Kind == Tk.KwIf)
            {
                Consume(); // IF
                var ec = ParseExpr();
                var ec_ = Expect(Tk.Colon);
                branches.Add(new Branch(ec, ParseBodyAfterColon(ec_.Line)));
            }
            else
            {
                var ec_ = Expect(Tk.Colon);
                elseBody = ParseBodyAfterColon(ec_.Line);
                break;
            }
        }
        return new IfDirective(branches, elseBody, tok.Line, tok.Col);
    }

    private InsertIntoDirective ParseInsertDir()
    {
        var tok = Consume(); // INSERT
        Expect(Tk.KwInto);
        string target = ExpectName();
        Expect(Tk.KwValues);
        var (sources, using_) = ParseValuesList();
        Expect(Tk.Semi);
        return new InsertIntoDirective(target, sources, using_, tok.Line, tok.Col);
    }

    private (List<ExprNode> sources, List<UsingCol>? using_) ParseValuesList()
    {
        var sources = new List<ExprNode> { ParseExpr() };
        // comma after a ROW() is multi-row; stop if next is USING (not an expression start)
        while (Peek().Kind == Tk.Comma && !IsUsingNext())
        { Consume(); sources.Add(ParseExpr()); }
        var using_ = Match(Tk.KwUsing) ? ParseUsingList() : null;
        return (sources, using_);
    }

    private bool IsUsingNext() =>
        Peek(1).Kind == Tk.KwUsing; // comma already consumed by caller check

    private DirectiveNode ParseUpdateDir()
    {
        var tok = Consume(); // UPDATE
        string target = ExpectName();
        if (Match(Tk.KwFrom))
        {
            var src = ParseExpr();
            var using_ = Match(Tk.KwUsing) ? ParseUsingList() : null;
            Expect(Tk.Semi);
            return new UpdateFromDirective(target, src, using_, tok.Line, tok.Col);
        }
        ExprNode? where = Match(Tk.KwWhere) ? ParseExpr() : null;
        Expect(Tk.KwSet);
        var row = ParseExpr();
        var usingSet = Match(Tk.KwUsing) ? ParseUsingList() : null;
        Expect(Tk.Semi);
        return new ExprDirective(new UpdateSetExpr(target, where, row, usingSet, tok.Line, tok.Col), tok.Line, tok.Col);
    }

    private ConcatToDirective ParseConcatDir()
    {
        var tok = Consume(); // CONCAT
        Expect(Tk.KwTo);
        string target = ExpectName();
        Expect(Tk.KwFrom);
        var src = ParseExpr();
        var using_ = Match(Tk.KwUsing) ? ParseUsingList() : null;
        Expect(Tk.Semi);
        return new ConcatToDirective(target, src, using_, tok.Line, tok.Col);
    }

    private AddColumnDirective ParseAddColDir()
    {
        var tok = Consume(); // ADD
        Expect(Tk.KwColumn);
        string colName = ExpectName();
        ColumnType ctype = ParseColType();
        ExprNode? def = Match(Tk.KwDefault) ? ParseExpr() : null;
        Expect(Tk.KwTo);
        Expect(Tk.KwTable);
        string tblName = ExpectName();
        bool ifNE = false;
        if (Peek().Kind == Tk.KwIf)
        {
            Consume(); // IF
            Expect(Tk.KwNot);
            // "EXISTS" is not a keyword — it's an identifier
            if (Peek().Kind == Tk.Ident && Peek().Text.Equals("EXISTS", StringComparison.OrdinalIgnoreCase))
                Consume();
            ifNE = true;
        }
        Expect(Tk.Semi);
        return new AddColumnDirective(colName, ctype, def, tblName, ifNE, tok.Line, tok.Col);
    }

    private ColumnType ParseColType()
    {
        var t = Peek();
        if (t.Kind == Tk.KwDecimal) { Consume(); return ColumnType.Decimal; }
        if (t.Kind == Tk.KwText)    { Consume(); return ColumnType.Text; }
        if (t.Kind == Tk.KwGuid)    { Consume(); return ColumnType.Guid; }
        Err($"Expected DECIMAL/TEXT/GUID, got '{t.Text}'", t);
        return ColumnType.Text;
    }

    private SaveTableDirective ParseSaveDir()
    {
        var tok = Consume(); // SAVE
        Expect(Tk.KwTable);
        string name = ExpectName();
        Expect(Tk.KwInto);
        var path = ParseExpr(); Expect(Tk.Semi);
        return new SaveTableDirective(name, path, tok.Line, tok.Col);
    }

    /// <summary>LOAD TABLE FROM &lt;path&gt; INTO &lt;target&gt;</summary>
    private LoadTableIntoDirective ParseLoadDir()
    {
        var tok = Consume(); // LOAD
        Expect(Tk.KwTable);
        Expect(Tk.KwFrom);
        var path = ParseExpr();
        Expect(Tk.KwInto);
        string target = ExpectName();
        Expect(Tk.Semi);
        return new LoadTableIntoDirective(path, target, tok.Line, tok.Col);
    }

    /// <summary>
    /// Parse an expression statement; also detects bare `name = expr` as LET.
    /// </summary>
    private DirectiveNode ParseExprOrAssignDir()
    {
        var tok = Peek();
        var expr = ParseExpr();
        // bare assignment: `name = rhs;` treated as LET
        if (expr is BinaryOp { Op: "=" } bo && bo.L is IdentExpr id)
        {
            Expect(Tk.Semi);
            return new LetDirective(id.Name, bo.R, id.Line, id.Col);
        }
        Expect(Tk.Semi);
        return new ExprDirective(expr, tok.Line, tok.Col);
    }

    // ── USING / arg lists ─────────────────────────────────────────────────────

    private List<UsingCol> ParseUsingList()
    {
        var list = new List<UsingCol> { ParseUsingCol() };
        while (Match(Tk.Comma)) list.Add(ParseUsingCol());
        return list;
    }

    private UsingCol ParseUsingCol()
    {
        string src = ExpectName();
        string? tgt = Match(Tk.KwAs) ? ExpectName() : null;
        return new UsingCol(src, tgt);
    }

    private List<Arg> ParseArgList()
    {
        var args = new List<Arg>();
        if (Peek().Kind == Tk.RParen) return args;
        args.Add(ParseArg());
        while (Match(Tk.Comma)) args.Add(ParseArg());
        return args;
    }

    private Arg ParseArg()
    {
        var val = ParseExpr();
        string? asName = Match(Tk.KwAs) ? ExpectName() : null;
        return new Arg(val, asName);
    }

    // ── expression grammar ────────────────────────────────────────────────────
    //  precedence (low→high): OR/XOR, AND, NOT, comparison, +/-, */%, unary, postfix, primary

    public ExprNode ParseExpr() => ParseOr();

    private ExprNode ParseOr()
    {
        var left = ParseAnd();
        while (Peek().Kind is Tk.KwOr or Tk.KwXor)
        {
            var op = Consume();
            left = new BinaryOp(op.Text.ToUpperInvariant(), left, ParseAnd(), left.Line, left.Col);
        }
        return left;
    }

    private ExprNode ParseAnd()
    {
        var left = ParseNot();
        while (Peek().Kind == Tk.KwAnd)
        { Consume(); left = new BinaryOp("AND", left, ParseNot(), left.Line, left.Col); }
        return left;
    }

    private ExprNode ParseNot()
    {
        var t = Peek();
        if (t.Kind == Tk.KwNot) { Consume(); return new UnaryOp("NOT", ParseNot(), t.Line, t.Col); }
        return ParseComparison();
    }

    private ExprNode ParseComparison()
    {
        var left = ParseAddSub();
        var t = Peek();
        // IS [NOT] NULL
        if (t.Kind == Tk.KwIs)
        {
            Consume();
            bool neg = Match(Tk.KwNot);
            Expect(Tk.KwNull);
            return new IsNullExpr(left, neg, t.Line, t.Col);
        }
        // [NOT] IN expr
        if (t.Kind == Tk.KwNot && Peek(1).Kind == Tk.KwIn)
        { Consume(); Consume(); return new InExpr(left, true, ParseAddSub(), t.Line, t.Col); }
        if (t.Kind == Tk.KwIn)
        { Consume(); return new InExpr(left, false, ParseAddSub(), t.Line, t.Col); }
        // standard comparison operators
        if (t.Kind is Tk.Eq or Tk.NotEq or Tk.Lt or Tk.LtEq or Tk.Gt or Tk.GtEq)
        { Consume(); return new BinaryOp(t.Text, left, ParseAddSub(), t.Line, t.Col); }
        return left;
    }

    private ExprNode ParseAddSub()
    {
        var left = ParseMulDiv();
        while (Peek().Kind is Tk.Plus or Tk.Minus)
        {
            var op = Consume();
            left = new BinaryOp(op.Text, left, ParseMulDiv(), left.Line, left.Col);
        }
        return left;
    }

    private ExprNode ParseMulDiv()
    {
        var left = ParseUnary();
        while (Peek().Kind is Tk.Star or Tk.Slash or Tk.Percent)
        {
            var op = Consume();
            left = new BinaryOp(op.Text, left, ParseUnary(), left.Line, left.Col);
        }
        return left;
    }

    private ExprNode ParseUnary()
    {
        var t = Peek();
        if (t.Kind == Tk.Minus) { Consume(); return new UnaryOp("-", ParseUnary(), t.Line, t.Col); }
        if (t.Kind == Tk.Plus)  { Consume(); return new UnaryOp("+", ParseUnary(), t.Line, t.Col); }
        return ParsePostfix(ParsePrimary());
    }

    /// <summary>Apply postfix .col operators to a completed primary.</summary>
    private ExprNode ParsePostfix(ExprNode inner) => FinishExpr(inner);

    /// <summary>
    /// Given a partially-built expression, consume any trailing .col or [idx]
    /// operators, but NOT ( — call sites that want function calls handle that
    /// in ParsePrimary via IdentExpr lookahead.
    /// </summary>
    private ExprNode FinishExpr(ExprNode inner)
    {
        while (true)
        {
            if (Peek().Kind == Tk.Dot)
            {
                var t = Consume();
                string col = ExpectName();
                inner = new TableColumn(inner, col, t.Line, t.Col);
            }
            else if (Peek().Kind == Tk.BracketIdent)
            {
                var t = Consume();
                if (!decimal.TryParse(t.Text, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var n))
                    throw new TqlParseException($"Expected numeric index in [...], got '{t.Text}'", t.Line, t.Col);
                inner = new TableIndex(inner, new NumberLit(n, t.Line, t.Col), t.Line, t.Col);
            }
            else break;
        }
        return inner;
    }

    // ── primary expressions ───────────────────────────────────────────────────

    private ExprNode ParsePrimary()
    {
        var t = Peek();
        switch (t.Kind)
        {
            case Tk.KwNull:  Consume(); return new NullLit(t.Line, t.Col);
            case Tk.KwTrue:  Consume(); return new BoolLit(true,  t.Line, t.Col);
            case Tk.KwFalse: Consume(); return new BoolLit(false, t.Line, t.Col);

            case Tk.Number:
                Consume();
                return new NumberLit(decimal.Parse(t.Text, CultureInfo.InvariantCulture), t.Line, t.Col);

            case Tk.StrLit:
            {
                Consume();
                var sb = new StringBuilder(t.Text);
                while (Peek().Kind is Tk.StrLit or Tk.StrPat)
                    sb.Append(Consume().Text);
                return new StringLit(sb.ToString(), t.Line, t.Col);
            }

            case Tk.StrPat:
            {
                Consume();
                var raw = new StringBuilder(t.Text);
                while (Peek().Kind is Tk.StrPat or Tk.StrLit)
                    raw.Append(Consume().Text);
                return ParseStringPattern(raw.ToString(), t.Line, t.Col);
            }

            case Tk.BracketIdent:
                Consume(); return new BracketedIdent(t.Text, t.Line, t.Col);

            case Tk.Ident:
            {
                Consume();
                // check for function call: ident (
                if (Peek().Kind == Tk.LParen)
                {
                    Consume(); // (
                    var args = ParseArgList();
                    Expect(Tk.RParen);
                    return new FuncCallExpr(t.Text, args, t.Line, t.Col);
                }
                return new IdentExpr(t.Text, t.Line, t.Col);
            }

            case Tk.LParen:
            {
                Consume();
                var inner = ParseExpr();
                Expect(Tk.RParen);
                return new ParenExpr(inner, t.Line, t.Col);
            }

            // aggregate / built-in functions written as keywords: COUNT(*), SUM(x), etc.
            case Tk.KwSelect:    return ParseSelectExpr();
            case Tk.KwRow:       return ParseRowExpr();
            case Tk.KwCreate:    return ParseCreateTable(t);
            case Tk.KwLoad:      return ParseLoadTable(t);
            case Tk.KwInsert:    return ParseInsertExpr(t);
            case Tk.KwUpdate:    return ParseUpdateExpr(t);
            case Tk.KwValues:    return ParseValuesMatch(t);

            // '*' usable as COUNT(*) argument
            case Tk.Star:
                Consume(); return new IdentExpr("*", t.Line, t.Col);

            default:
                // any keyword can be used as a variable or function name
                if ((int)t.Kind >= (int)Tk.KwAbort)
                {
                    Consume();
                    if (Peek().Kind == Tk.LParen)
                    {
                        Consume(); // (
                        var args = ParseArgList();
                        Expect(Tk.RParen);
                        return new FuncCallExpr(t.Text, args, t.Line, t.Col);
                    }
                    return new IdentExpr(t.Text, t.Line, t.Col);
                }
                Err($"Unexpected '{t.Text}' in expression", t);
                return null!;
        }
    }

    private CreateTableExpr ParseCreateTable(Token t)
    { Consume(); Expect(Tk.KwTable); return new CreateTableExpr(t.Line, t.Col); }

    private LoadTableExpr ParseLoadTable(Token t)
    { Consume(); Expect(Tk.KwTable); Expect(Tk.KwFrom); return new LoadTableExpr(ParseExpr(), t.Line, t.Col); }

    private InsertExpr ParseInsertExpr(Token t)
    {
        Consume(); Expect(Tk.KwInto);
        string tgt = ExpectName();
        Expect(Tk.KwValues);
        var (s, u) = ParseValuesList();
        return new InsertExpr(tgt, s, u, t.Line, t.Col);
    }

    private UpdateSetExpr ParseUpdateExpr(Token t)
    {
        Consume();
        string tgt = ExpectName();
        ExprNode? where = Match(Tk.KwWhere) ? ParseExpr() : null;
        Expect(Tk.KwSet);
        var row = ParseExpr();
        var using_ = Match(Tk.KwUsing) ? ParseUsingList() : null;
        return new UpdateSetExpr(tgt, where, row, using_, t.Line, t.Col);
    }

    private ValuesMatchExpr ParseValuesMatch(Token t)
    {
        Consume(); // VALUES
        // MATCH is not a keyword — accept it as an ident
        if (Peek().Kind == Tk.Ident && Peek().Text.Equals("MATCH", StringComparison.OrdinalIgnoreCase))
            Consume();
        return new ValuesMatchExpr(ParseExpr(), t.Line, t.Col);
    }

    // ── SELECT ────────────────────────────────────────────────────────────────

    private SelectExpr ParseSelectExpr()
    {
        var tok = Consume(); // SELECT
        var cols = new List<SelectCol>();

        if (Peek().Kind == Tk.Star)
        { Consume(); cols.Add(new SelectCol(new IdentExpr("*", tok.Line, tok.Col), null)); }
        else
        {
            cols.Add(ParseSelectCol());
            while (Peek().Kind == Tk.Comma) { Consume(); cols.Add(ParseSelectCol()); }
        }

        Expect(Tk.KwFrom);
        var from = ParseExpr();
        string? alias = Match(Tk.KwAs) ? ExpectName() : null;

        ExprNode? where = null;
        if (Match(Tk.KwWhere)) where = ParseExpr();

        var groupBy = new List<string>();
        bool groupDesc = false;
        if (Match(Tk.KwGroup))
        {
            Expect(Tk.KwBy);
            groupBy.Add(ExpectName());
            while (Match(Tk.Comma)) groupBy.Add(ExpectName());
            groupDesc = Match(Tk.KwDesc);
            if (!groupDesc) Match(Tk.KwAsc);
        }

        var orderBy = new List<OrderByCol>();
        if (Match(Tk.KwOrder))
        {
            Expect(Tk.KwBy);
            orderBy.Add(ParseOrderByCol());
            while (Match(Tk.Comma)) orderBy.Add(ParseOrderByCol());
        }

        LimitClause? limit = null;
        if (Match(Tk.KwLimit))
        {
            var count = ParseExpr();
            List<string>? onDistinct = null;
            if (Match(Tk.KwOn))
            {
                Expect(Tk.KwDistinct);
                onDistinct = [ExpectName()];
                while (Match(Tk.Comma)) onDistinct.Add(ExpectName());
            }
            limit = new LimitClause(count, onDistinct);
        }

        return new SelectExpr(
            new SelectSpec(cols, from, alias, where, groupBy, groupDesc, orderBy, limit),
            tok.Line, tok.Col);
    }

    private SelectCol ParseSelectCol()
    {
        var expr = ParseExpr();
        string? alias = Match(Tk.KwAs) ? ExpectName() : null;
        return new SelectCol(expr, alias);
    }

    private OrderByCol ParseOrderByCol()
    {
        var expr = ParseExpr();
        bool desc = Match(Tk.KwDesc);
        if (!desc) Match(Tk.KwAsc);
        return new OrderByCol(expr, desc);
    }

    // ── ROW ───────────────────────────────────────────────────────────────────

    private RowExpr ParseRowExpr()
    {
        var tok = Consume(); // ROW
        Expect(Tk.LParen);
        var fields = new List<RowField>();
        if (Peek().Kind != Tk.RParen)
        {
            fields.Add(ParseRowField());
            while (Match(Tk.Comma)) fields.Add(ParseRowField());
        }
        Expect(Tk.RParen);
        return new RowExpr(fields, tok.Line, tok.Col);
    }

    private RowField ParseRowField()
    {
        var expr = ParseExpr();
        string? name = Match(Tk.KwAs) ? ExpectName() : null;
        if (name is null)
        {
            if (expr is IdentExpr id)        name = id.Name;
            else if (expr is BracketedIdent bi) name = bi.Name;
        }
        return new RowField(name, expr);
    }

    // ── string pattern interpolation ─────────────────────────────────────────

    private StringPattern ParseStringPattern(string raw, int line, int col)
    {
        var parts = new List<PatternPart>();
        var text  = new StringBuilder();
        int i = 0;
        while (i < raw.Length)
        {
            char c = raw[i];
            if (c == '\\' && i + 1 < raw.Length)
            {
                text.Append(raw[i + 1] switch { 'n' => '\n', 't' => '\t', _ => raw[i + 1] });
                i += 2; continue;
            }
            if (c == '{')
            {
                if (text.Length > 0) { parts.Add(new PatternPart(text.ToString(), null)); text.Clear(); }
                int depth = 1, start = ++i;
                while (i < raw.Length && depth > 0)
                {
                    if (raw[i] == '{') depth++;
                    else if (raw[i] == '}') { if (--depth == 0) break; }
                    i++;
                }
                string exprSrc = raw[start..i];
                if (i < raw.Length) i++; // skip '}'
                var sub = new Parser(new Lexer(exprSrc).Tokenize());
                parts.Add(new PatternPart(null, sub.ParseExpr()));
                continue;
            }
            text.Append(c); i++;
        }
        if (text.Length > 0) parts.Add(new PatternPart(text.ToString(), null));
        return new StringPattern(parts, line, col);
    }
}
