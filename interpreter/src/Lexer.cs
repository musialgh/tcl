using System;
using System.Collections.Generic;
using System.Text;

namespace TclInterpreter.Parsing
{
    public enum TokenType
    {
        // Literals
        IntLiteral, RealLiteral, StringLiteral, Identifier,

        // Type keywords
        KwBool, KwInt, KwReal, KwNat, KwNum, KwObject, KwAny,

        // Other keywords
        KwWhere, KwIf, KwIn, KwImport, KwAs, KwTrue, KwFalse,

        // Punctuation
        LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket,
        Comma, Colon, Semicolon, Hash, Pipe,

        // Operators
        Plus, Minus, Star, Slash, Question,
        Equals, Less, Greater, LessEqual, GreaterEqual,
        Arrow,      // ->
        DotDot,     // ..

        // Special
        Eof
    }

    public record Token(TokenType Type, string Value, int Line, int Column)
    {
        public override string ToString() => $"{Type}('{Value}') at {Line}:{Column}";
    }

    public class LexicalException : Exception
    {
        public LexicalException(string message) : base(message) { }
    }

    /// <summary>
    /// Tokenizes TCL DSL source code into a stream of tokens.
    /// </summary>
    public class Lexer
    {
        private readonly string _source;
        private int _pos;
        private int _line;
        private int _col;

        public Lexer(string source)
        {
            _source = source;
            _pos = 0;
            _line = 1;
            _col = 1;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_pos < _source.Length)
            {
                SkipWhitespaceAndComments();
                if (_pos >= _source.Length)
                    break;

                var token = NextToken();
                if (token != null)
                    tokens.Add(token);
            }
            tokens.Add(new Token(TokenType.Eof, "", _line, _col));
            return tokens;
        }

        private Token? NextToken()
        {
            var ch = Cur();
            var line = _line;
            var col = _col;

            // Single-character tokens
            switch (ch)
            {
                case '(': Advance(); return new Token(TokenType.LeftParen, "(", line, col);
                case ')': Advance(); return new Token(TokenType.RightParen, ")", line, col);
                case '{': Advance(); return new Token(TokenType.LeftBrace, "{", line, col);
                case '}': Advance(); return new Token(TokenType.RightBrace, "}", line, col);
                case '[': Advance(); return new Token(TokenType.LeftBracket, "[", line, col);
                case ']': Advance(); return new Token(TokenType.RightBracket, "]", line, col);
                case ',': Advance(); return new Token(TokenType.Comma, ",", line, col);
                case ':': Advance(); return new Token(TokenType.Colon, ":", line, col);
                case ';': Advance(); return new Token(TokenType.Semicolon, ";", line, col);
                case '#': Advance(); return new Token(TokenType.Hash, "#", line, col);
                case '|': Advance(); return new Token(TokenType.Pipe, "|", line, col);
                case '*': Advance(); return new Token(TokenType.Star, "*", line, col);
                case '/': Advance(); return new Token(TokenType.Slash, "/", line, col);
            }

            // + (just plus, no ++ needed)
            if (ch == '+')
            {
                Advance();
                return new Token(TokenType.Plus, "+", line, col);
            }

            // - or ->
            if (ch == '-')
            {
                Advance();
                if (Cur() == '>')
                {
                    Advance();
                    return new Token(TokenType.Arrow, "->", line, col);
                }
                return new Token(TokenType.Minus, "-", line, col);
            }

            // = (assignment / equality)
            if (ch == '=')
            {
                Advance();
                return new Token(TokenType.Equals, "=", line, col);
            }

            // < <= 
            if (ch == '<')
            {
                Advance();
                if (Cur() == '=')
                {
                    Advance();
                    return new Token(TokenType.LessEqual, "<=", line, col);
                }
                return new Token(TokenType.Less, "<", line, col);
            }

            // > >=
            if (ch == '>')
            {
                Advance();
                if (Cur() == '=')
                {
                    Advance();
                    return new Token(TokenType.GreaterEqual, ">=", line, col);
                }
                return new Token(TokenType.Greater, ">", line, col);
            }

            // ? (key existence operator)
            if (ch == '?')
            {
                Advance();
                return new Token(TokenType.Question, "?", line, col);
            }

            // . or ..
            if (ch == '.')
            {
                Advance();
                if (Cur() == '.')
                {
                    Advance();
                    return new Token(TokenType.DotDot, "..", line, col);
                }
                // Stand-alone dot — shouldn't occur in valid DSL; treat as error
                throw new LexicalException($"Unexpected single '.' at line {line}, column {col}");
            }

            // String literals
            if (ch == '"') return ReadString();

            // Numbers
            if (char.IsDigit(ch)) return ReadNumber();

            // Identifiers and keywords
            if (char.IsLetter(ch) || ch == '_') return ReadIdentifierOrKeyword();

            throw new LexicalException($"Unexpected character '{ch}' at line {_line}, column {_col}");
        }

        private Token ReadString()
        {
            var startLine = _line;
            var startCol = _col;
            Advance(); // skip opening quote
            var sb = new StringBuilder();

            while (_pos < _source.Length && Cur() != '"')
            {
                if (Cur() == '\\')
                {
                    Advance();
                    if (_pos < _source.Length)
                    {
                        sb.Append(Cur() switch
                        {
                            'n' => '\n', 't' => '\t', 'r' => '\r',
                            '\\' => '\\', '"' => '"',
                            var c => c
                        });
                        Advance();
                    }
                }
                else
                {
                    sb.Append(Cur());
                    Advance();
                }
            }

            if (_pos >= _source.Length)
                throw new LexicalException($"Unterminated string at line {startLine}, column {startCol}");

            Advance(); // skip closing quote
            return new Token(TokenType.StringLiteral, sb.ToString(), startLine, startCol);
        }

        private Token ReadNumber()
        {
            var startLine = _line;
            var startCol = _col;
            var sb = new StringBuilder();
            bool hasDot = false;

            while (_pos < _source.Length && (char.IsDigit(Cur()) || (Cur() == '.' && Peek() != '.')))
            {
                if (Cur() == '.') hasDot = true;
                sb.Append(Cur());
                Advance();
            }

            var value = sb.ToString();
            return new Token(hasDot ? TokenType.RealLiteral : TokenType.IntLiteral, value, startLine, startCol);
        }

        private Token ReadIdentifierOrKeyword()
        {
            var startLine = _line;
            var startCol = _col;
            var sb = new StringBuilder();

            while (_pos < _source.Length && (char.IsLetterOrDigit(Cur()) || Cur() == '_'))
            {
                sb.Append(Cur());
                Advance();
            }

            var value = sb.ToString();
            var type = value switch
            {
                "where"  => TokenType.KwWhere,
                "if"     => TokenType.KwIf,
                "in"     => TokenType.KwIn,
                "import" => TokenType.KwImport,
                "as"     => TokenType.KwAs,
                "true"   => TokenType.KwTrue,
                "false"  => TokenType.KwFalse,
                "bool"   => TokenType.KwBool,
                "int"    => TokenType.KwInt,
                "real"   => TokenType.KwReal,
                "nat"    => TokenType.KwNat,
                "num"    => TokenType.KwNum,
                "object" => TokenType.KwObject,
                "any"    => TokenType.KwAny,
                _        => TokenType.Identifier
            };
            return new Token(type, value, startLine, startCol);
        }

        private void SkipWhitespaceAndComments()
        {
            while (_pos < _source.Length)
            {
                if (char.IsWhiteSpace(Cur()))
                {
                    Advance();
                }
                else if (Cur() == '/' && Peek() == '/')
                {
                    // Line comment (includes /// doc comments)
                    while (_pos < _source.Length && Cur() != '\n')
                        Advance();
                }
                else if (Cur() == '/' && Peek() == '*')
                {
                    Advance(); Advance();
                    while (_pos < _source.Length && !(Cur() == '*' && Peek() == '/'))
                        Advance();
                    if (_pos < _source.Length) { Advance(); Advance(); }
                }
                else
                {
                    break;
                }
            }
        }

        private char Cur() => _pos < _source.Length ? _source[_pos] : '\0';
        private char Peek() => _pos + 1 < _source.Length ? _source[_pos + 1] : '\0';

        private void Advance()
        {
            if (_pos < _source.Length)
            {
                if (_source[_pos] == '\n') { _line++; _col = 1; }
                else { _col++; }
                _pos++;
            }
        }
    }
}
