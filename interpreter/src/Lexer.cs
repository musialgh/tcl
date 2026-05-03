using System;
using System.Collections.Generic;
using System.Linq;

namespace TclInterpreter.Parsing
{
    /// <summary>
    /// Tokenizes TCL DSL source code into a stream of tokens.
    /// </summary>
    public class Lexer
    {
        private readonly string _source;
        private int _position;
        private int _line;
        private int _column;

        public Lexer(string source)
        {
            _source = source;
            _position = 0;
            _line = 1;
            _column = 1;
        }

        public IEnumerable<Token> Tokenize()
        {
            while (_position < _source.Length)
            {
                SkipWhitespaceAndComments();
                if (_position >= _source.Length)
                    break;

                var token = NextToken();
                if (token != null)
                    yield return token;
            }

            yield return new Token(TokenType.Eof, "", _line, _column);
        }

        private Token? NextToken()
        {
            var ch = Current();

            // Single-character tokens
            if (ch == '(') return CreateToken(TokenType.LeftParen);
            if (ch == ')') return CreateToken(TokenType.RightParen);
            if (ch == '{') return CreateToken(TokenType.LeftBrace);
            if (ch == '}') return CreateToken(TokenType.RightBrace);
            if (ch == '[') return CreateToken(TokenType.LeftBracket);
            if (ch == ']') return CreateToken(TokenType.RightBracket);
            if (ch == ',') return CreateToken(TokenType.Comma);
            if (ch == ':') return CreateToken(TokenType.Colon);
            if (ch == ';') return CreateToken(TokenType.Semicolon);
            if (ch == '?') return CreateToken(TokenType.Question);
            if (ch == '+') return CreateTokenOrOperator();
            if (ch == '*') return CreateToken(TokenType.Star);
            if (ch == '/') return CreateToken(TokenType.Slash);
            if (ch == '#') return CreateToken(TokenType.Hash);

            // Two-character tokens and operators
            if (ch == '-')
            {
                Advance();
                if (Current() == '>') { Advance(); return new Token(TokenType.Arrow, "->", _line, _column - 2); }
                _position--;
                _column--;
                return CreateToken(TokenType.Minus);
            }

            if (ch == '=')
            {
                Advance();
                if (Current() == '>') { Advance(); return new Token(TokenType.MapArrow, "=>", _line, _column - 2); }
                _position--;
                _column--;
                return CreateToken(TokenType.Equals);
            }

            if (ch == '<') return CreateComparisonToken();
            if (ch == '>') return CreateComparisonToken();

            if (ch == '.')
            {
                Advance();
                if (char.IsDigit(Current()))
                {
                    _position--;
                    _column--;
                    return ReadNumber();
                }
                if (Current() == '.')
                {
                    Advance();
                    return new Token(TokenType.DotDot, "..", _line, _column - 2);
                }
                _position--;
                _column--;
                return null;
            }

            // String literals
            if (ch == '"') return ReadString();

            // Numbers
            if (char.IsDigit(ch)) return ReadNumber();

            // Identifiers and keywords
            if (char.IsLetter(ch) || ch == '_') return ReadIdentifierOrKeyword();

            throw new LexicalException($"Unexpected character '{ch}' at line {_line}, column {_column}");
        }

        private Token CreateTokenOrOperator()
        {
            var line = _line;
            var col = _column;
            Advance();
            if (Current() == '+') { Advance(); return new Token(TokenType.Plus, "++", line, col); }
            _position--;
            _column--;
            return CreateToken(TokenType.Plus);
        }

        private Token CreateComparisonToken()
        {
            var ch = Current();
            var line = _line;
            var col = _column;
            Advance();

            if (Current() == '=')
            {
                Advance();
                return new Token(ch == '<' ? TokenType.LessEqual : TokenType.GreaterEqual, ch + "=", line, col);
            }

            return new Token(ch == '<' ? TokenType.Less : TokenType.Greater, ch.ToString(), line, col);
        }

        private Token ReadString()
        {
            var startLine = _line;
            var startCol = _column;
            Advance(); // skip opening quote
            var value = "";

            while (_position < _source.Length && Current() != '"')
            {
                if (Current() == '\\')
                {
                    Advance();
                    if (_position < _source.Length)
                    {
                        value += EscapeChar(Current());
                        Advance();
                    }
                }
                else
                {
                    value += Current();
                    Advance();
                }
            }

            if (_position >= _source.Length)
                throw new LexicalException($"Unterminated string at line {startLine}, column {startCol}");

            Advance(); // skip closing quote
            return new Token(TokenType.String, value, startLine, startCol);
        }

        private char EscapeChar(char ch) => ch switch
        {
            'n' => '\n',
            't' => '\t',
            'r' => '\r',
            '\\' => '\\',
            '"' => '"',
            _ => ch
        };

        private Token ReadNumber()
        {
            var startLine = _line;
            var startCol = _column;
            var value = "";

            while (_position < _source.Length && (char.IsDigit(Current()) || Current() == '.'))
            {
                value += Current();
                Advance();
            }

            return new Token(value.Contains('.') ? TokenType.Real : TokenType.Int, value, startLine, startCol);
        }

        private Token ReadIdentifierOrKeyword()
        {
            var startLine = _line;
            var startCol = _column;
            var value = "";

            while (_position < _source.Length && (char.IsLetterOrDigit(Current()) || Current() == '_'))
            {
                value += Current();
                Advance();
            }

            var type = GetKeywordType(value);
            return new Token(type, value, startLine, startCol);
        }

        private TokenType GetKeywordType(string value) => value switch
        {
            "where" => TokenType.Where,
            "if" => TokenType.If,
            "in" => TokenType.In,
            "import" => TokenType.Import,
            "as" => TokenType.As,
            "bool" => TokenType.Bool,
            "int" => TokenType.Int,
            "real" => TokenType.Real,
            "nat" => TokenType.Nat,
            "num" => TokenType.Num,
            "object" => TokenType.Object,
            "any" => TokenType.Any,
            _ => TokenType.Identifier
        };

        private void SkipWhitespaceAndComments()
        {
            while (_position < _source.Length)
            {
                if (char.IsWhiteSpace(Current()))
                {
                    Advance();
                }
                else if (Current() == '/' && Peek() == '/')
                {
                    // Skip line comment
                    while (_position < _source.Length && Current() != '\n')
                        Advance();
                }
                else if (Current() == '/' && Peek() == '*')
                {
                    // Skip block comment
                    Advance(); Advance();
                    while (_position < _source.Length && !(Current() == '*' && Peek() == '/'))
                        Advance();
                    if (_position < _source.Length)
                    {
                        Advance(); Advance();
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private Token CreateToken(TokenType type)
        {
            var token = new Token(type, Current().ToString(), _line, _column);
            Advance();
            return token;
        }

        private char Current() => _position < _source.Length ? _source[_position] : '\0';
        private char Peek() => _position + 1 < _source.Length ? _source[_position + 1] : '\0';

        private void Advance()
        {
            if (_position < _source.Length)
            {
                if (_source[_position] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _position++;
            }
        }
    }

    public enum TokenType
    {
        // Literals
        Int, Real, String, Identifier,

        // Keywords
        Where, If, In, Import, As,
        Bool, Int, Real, Nat, Num, Object, Any,

        // Punctuation
        LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket,
        Comma, Colon, Semicolon, Question, Hash,

        // Operators
        Plus, Minus, Star, Slash,
        Equals, Less, Greater, LessEqual, GreaterEqual,
        Arrow, MapArrow, DotDot,

        // Special
        Eof
    }

    public record Token(TokenType Type, string Value, int Line, int Column);

    public class LexicalException : Exception
    {
        public LexicalException(string message) : base(message) { }
    }
}
