using System.Text;

namespace TqlInterpreter;

/// <summary>
/// Produces a flat token stream from TQL source. No INDENT/DEDENT tokens are
/// emitted; the parser uses each token's Line/Col to infer block structure.
/// Newlines are treated as whitespace and do not appear in the stream.
/// </summary>
public class Lexer(string src, string sourceName = "")
{
    private readonly string _src  = src;
    private readonly string _name = sourceName;
    private int    _pos;
    private int    _line = 1;
    private int    _col  = 1;

    private static readonly Dictionary<string, Tk> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ABORT"]    = Tk.KwAbort,    ["ADD"]      = Tk.KwAdd,
        ["AND"]      = Tk.KwAnd,      ["AS"]       = Tk.KwAs,
        ["ASC"]      = Tk.KwAsc,      ["BREAK"]    = Tk.KwBreak,
        ["BY"]       = Tk.KwBy,       ["CALL"]     = Tk.KwCall,
        ["COLUMN"]   = Tk.KwColumn,   ["CONCAT"]   = Tk.KwConcat,
        ["CONTINUE"] = Tk.KwContinue, ["CREATE"]   = Tk.KwCreate,
        ["DECIMAL"]  = Tk.KwDecimal,  ["DEFAULT"]  = Tk.KwDefault,
        ["DESC"]     = Tk.KwDesc,     ["DISTINCT"] = Tk.KwDistinct,
        ["ELSE"]     = Tk.KwElse,     ["FALSE"]    = Tk.KwFalse,
        ["FOR"]      = Tk.KwFor,      ["FROM"]     = Tk.KwFrom,
        ["FUNCTION"] = Tk.KwFunction, ["GLOBAL"]   = Tk.KwGlobal,
        ["GROUP"]    = Tk.KwGroup,    ["GUID"]     = Tk.KwGuid,
        ["IF"]       = Tk.KwIf,       ["IMPORT"]   = Tk.KwImport,
        ["IN"]       = Tk.KwIn,
        ["INSERT"]   = Tk.KwInsert,   ["INTO"]     = Tk.KwInto,
        ["IS"]       = Tk.KwIs,       ["LET"]      = Tk.KwLet,
        ["LIMIT"]    = Tk.KwLimit,    ["LOAD"]     = Tk.KwLoad,
        ["NOT"]      = Tk.KwNot,      ["NULL"]     = Tk.KwNull,
        ["ON"]       = Tk.KwOn,       ["OR"]       = Tk.KwOr,
        ["ORDER"]    = Tk.KwOrder,    ["RETURN"]   = Tk.KwReturn,
        ["ROW"]      = Tk.KwRow,      ["SAVE"]     = Tk.KwSave,
        ["SELECT"]   = Tk.KwSelect,   ["SET"]      = Tk.KwSet,
        ["TABLE"]    = Tk.KwTable,    ["TEXT"]     = Tk.KwText,
        ["TO"]       = Tk.KwTo,       ["TRUE"]     = Tk.KwTrue,
        ["UPDATE"]   = Tk.KwUpdate,   ["USING"]    = Tk.KwUsing,
        ["VALUES"]   = Tk.KwValues,   ["WHERE"]    = Tk.KwWhere,
        ["WHILE"]    = Tk.KwWhile,    ["XOR"]      = Tk.KwXor,
    };

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _src.Length)
            {
                tokens.Add(Tok(Tk.Eof, ""));
                break;
            }

            int startLine = _line, startCol = _col;
            char c = _src[_pos];

            Token? t = c switch
            {
                '(' => Advance1(Tk.LParen),
                ')' => Advance1(Tk.RParen),
                ',' => Advance1(Tk.Comma),
                ':' => Advance1(Tk.Colon),
                ';' => Advance1(Tk.Semi),
                '+' => Advance1(Tk.Plus),
                '*' => Advance1(Tk.Star),
                '/' => Advance1(Tk.Slash),
                '%' => Advance1(Tk.Percent),
                '.' when Peek(1) == '.' => Advance2(Tk.DotDot),
                '.' => Advance1(Tk.Dot),
                '<' when Peek(1) == '>' => Advance2(Tk.NotEq),
                '<' when Peek(1) == '=' => Advance2(Tk.LtEq),
                '<' => Advance1(Tk.Lt),
                '>' when Peek(1) == '=' => Advance2(Tk.GtEq),
                '>' => Advance1(Tk.Gt),
                '=' => Advance1(Tk.Eq),
                '-' => Advance1(Tk.Minus),
                '"' => ReadStringPattern(startLine, startCol),
                '\'' => ReadStringLiteral(startLine, startCol),
                '[' => ReadBracketedIdent(startLine, startCol),
                _ when char.IsLetter(c) || c == '_' => ReadIdent(startLine, startCol),
                _ when char.IsDigit(c) => ReadNumber(startLine, startCol),
                _ => null
            };

            if (t is null)
                throw new TqlParseException($"Unexpected character '{c}'", startLine, startCol);
            tokens.Add(t);
        }
        return tokens;
    }

    // ── whitespace / comments ─────────────────────────────────────────────────

    private void SkipWhitespaceAndComments()
    {
        while (_pos < _src.Length)
        {
            char c = _src[_pos];
            if (c == '#')
            {
                while (_pos < _src.Length && _src[_pos] != '\n') Advance();
            }
            else if (c == '\n') { Advance(); }   // newlines are whitespace
            else if (char.IsWhiteSpace(c)) { Advance(); }
            else break;
        }
    }

    // ── token readers ─────────────────────────────────────────────────────────

    private Token ReadIdent(int line, int col)
    {
        int start = _pos;
        while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
            Advance();
        string text = _src[start.._pos];
        Tk kind = Keywords.TryGetValue(text, out var kw) ? kw : Tk.Ident;
        return new Token(kind, text, line, col);
    }

    private Token ReadNumber(int line, int col)
    {
        int start = _pos;
        while (_pos < _src.Length && char.IsDigit(_src[_pos])) Advance();
        if (_pos < _src.Length && _src[_pos] == '.')
        {
            // peek: don't consume if it's ".." (range operator)
            if (_pos + 1 < _src.Length && _src[_pos + 1] == '.') {}
            else
            {
                Advance(); // consume '.'
                while (_pos < _src.Length && char.IsDigit(_src[_pos])) Advance();
            }
        }
        return new Token(Tk.Number, _src[start.._pos], line, col);
    }

    /// <summary>Read "..." string with {expr} interpolation markers; returns raw content between quotes.</summary>
    private Token ReadStringPattern(int line, int col)
    {
        Advance(); // consume opening "
        var sb = new StringBuilder();
        int depth = 0;
        while (_pos < _src.Length)
        {
            char c = _src[_pos];
            if (depth == 0 && c == '"') { Advance(); break; }
            if (c == '{') depth++;
            if (c == '}' && depth > 0) depth--;
            if (c == '\n') { _line++; _col = 0; } // track lines inside string
            sb.Append(c);
            Advance();
        }
        return new Token(Tk.StrPat, sb.ToString(), line, col);
    }

    /// <summary>Read '...' plain string; no interpolation.</summary>
    private Token ReadStringLiteral(int line, int col)
    {
        Advance(); // consume opening '
        var sb = new StringBuilder();
        while (_pos < _src.Length)
        {
            char c = _src[_pos];
            if (c == '\'' ) { Advance(); break; }
            if (c == '\\' && _pos + 1 < _src.Length)
            { sb.Append(_src[_pos + 1]); Advance(); Advance(); continue; }
            sb.Append(c);
            Advance();
        }
        return new Token(Tk.StrLit, sb.ToString(), line, col);
    }

    /// <summary>Read [identifier-with-hyphens].</summary>
    private Token ReadBracketedIdent(int line, int col)
    {
        Advance(); // consume '['
        var sb = new StringBuilder();
        while (_pos < _src.Length && _src[_pos] != ']')
        { sb.Append(_src[_pos]); Advance(); }
        if (_pos < _src.Length) Advance(); // consume ']'
        return new Token(Tk.BracketIdent, sb.ToString(), line, col);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Token Advance1(Tk kind) { var t = new Token(kind, _src[_pos].ToString(), _line, _col); Advance(); return t; }
    private Token Advance2(Tk kind) { var t = new Token(kind, _src[_pos..(_pos+2)], _line, _col); Advance(); Advance(); return t; }
    private Token Tok(Tk kind, string text) => new(kind, text, _line, _col);

    private char Peek(int offset = 0) =>
        _pos + offset < _src.Length ? _src[_pos + offset] : '\0';

    private void Advance()
    {
        if (_pos >= _src.Length) return;
        if (_src[_pos] == '\n') { _line++; _col = 1; } else { _col++; }
        _pos++;
    }
}
