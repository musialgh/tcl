using System;
using System.Collections.Generic;
using System.Linq;

namespace TclInterpreter.Parsing
{
    /// <summary>
    /// Recursive descent parser for TCL DSL.
    /// </summary>
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _position;

        public Parser(IEnumerable<Token> tokens)
        {
            _tokens = tokens.ToList();
            _position = 0;
        }

        public Program Parse()
        {
            var functions = new List<FunctionDefinition>();

            while (!IsAtEnd())
            {
                if (Check(TokenType.Import))
                {
                    SkipImportStatement();
                }
                else
                {
                    functions.Add(ParseFunctionDefinition());
                }
            }

            return new Program(functions);
        }

        private FunctionDefinition ParseFunctionDefinition()
        {
            // Parse signature
            var signatureName = Consume(TokenType.Identifier, "Expected function name").Value;
            
            var templateRef = Check(TokenType.LeftParen) ? null : ParseTemplateRef();
            
            Consume(TokenType.Colon, "Expected ':' after function signature");
            
            var signature = ParseFunctionSignature();
            
            Consume(TokenType.Arrow, "Expected '->' before function body");
            
            // Parse outputs
            var outputParams = ParseParameterList();
            Consume(TokenType.Semicolon, "Expected ';' after output parameters");
            
            // Combine signature
            var fullSignature = new FunctionSignature(new List<Parameter>(), outputParams);
            
            // Parse implementation
            var body = ParseFunctionBody();
            
            return new FunctionDefinition(signatureName, templateRef, fullSignature, body);
        }

        private string? ParseTemplateRef()
        {
            if (Check(TokenType.LeftParen))
            {
                Advance();
                var templateName = Consume(TokenType.Identifier, "Expected template name").Value;
                Consume(TokenType.RightParen, "Expected ')' after template name");
                return templateName;
            }
            return null;
        }

        private FunctionSignature ParseFunctionSignature()
        {
            var inputParams = ParseParameterList();
            return new FunctionSignature(inputParams, new List<Parameter>());
        }

        private List<Parameter> ParseParameterList()
        {
            var parameters = new List<Parameter>();
            
            while (!Check(TokenType.Arrow) && !Check(TokenType.Semicolon) && !IsAtEnd())
            {
                var name = Consume(TokenType.Identifier, "Expected parameter name").Value;
                Consume(TokenType.Colon, "Expected ':' after parameter name");
                var type = ParseType();
                
                parameters.Add(new Parameter(name, type));
                
                if (Check(TokenType.Comma))
                    Advance();
                else
                    break;
            }
            
            return parameters;
        }

        private TypeAnnotation ParseType()
        {
            if (Check(TokenType.LeftBrace))
            {
                return ParseObjectType();
            }
            else if (Check(TokenType.Identifier))
            {
                var typeName = Advance().Value;
                return new PrimitiveType(typeName);
            }
            
            throw new ParseException("Expected type annotation");
        }

        private ObjectType ParseObjectType()
        {
            Consume(TokenType.LeftBrace, "Expected '{'");
            var fields = new Dictionary<string, (TypeAnnotation, string)>();
            
            while (!Check(TokenType.RightBrace))
            {
                var fieldName = Consume(TokenType.Identifier, "Expected field name").Value;
                var cardinality = "";
                
                if (Check(TokenType.Plus) || Check(TokenType.Star) || Check(TokenType.Question))
                {
                    cardinality = Advance().Value;
                }
                
                Consume(TokenType.Colon, "Expected ':' after field name");
                var fieldType = ParseType();
                
                fields[fieldName] = (fieldType, cardinality);
                
                if (Check(TokenType.Comma))
                    Advance();
            }
            
            Consume(TokenType.RightBrace, "Expected '}'");
            return new ObjectType(fields);
        }

        private FunctionBody ParseFunctionBody()
        {
            var beforeWhere = new List<Assignment>();
            
            while (!Check(TokenType.Where) && !IsAtEnd())
            {
                beforeWhere.Add(ParseAssignment());
                
                if (Check(TokenType.Comma))
                    Advance();
            }
            
            List<Assignment>? afterWhere = null;
            if (Check(TokenType.Where))
            {
                Advance();
                afterWhere = new List<Assignment>();
                
                while (!IsAtEnd())
                {
                    afterWhere.Add(ParseAssignment());
                    
                    if (Check(TokenType.Comma))
                        Advance();
                    else
                        break;
                }
            }
            
            return new FunctionBody(beforeWhere, afterWhere);
        }

        private Assignment ParseAssignment()
        {
            var target = Consume(TokenType.Identifier, "Expected identifier").Value;
            Consume(TokenType.Equals, "Expected '=' in assignment");
            var value = ParseExpression();
            
            return new Assignment(target, value);
        }

        private Expression ParseExpression()
        {
            return ParseConditional();
        }

        private Expression ParseConditional()
        {
            var expr = ParseComparison();
            
            if (Check(TokenType.If))
            {
                Advance();
                var condition = ParseComparison();
                var pipe = Consume(TokenType.Question, "Expected '|' in conditional").Value;
                var thenExpr = ParseComparison();
                
                Expression? elseExpr = null;
                if (Check(TokenType.Question))
                {
                    Advance();
                    elseExpr = ParseConditional();
                }
                
                return new ConditionalExpr(condition, thenExpr, elseExpr);
            }
            
            return expr;
        }

        private Expression ParseComparison()
        {
            var expr = ParseAdditive();
            
            while (Check(TokenType.Less) || Check(TokenType.Greater) || 
                   Check(TokenType.Equals) || Check(TokenType.LessEqual) || Check(TokenType.GreaterEqual))
            {
                var op = Advance().Value;
                var right = ParseAdditive();
                expr = new BinaryOp(op, expr, right);
            }
            
            return expr;
        }

        private Expression ParseAdditive()
        {
            var expr = ParseMultiplicative();
            
            while (Check(TokenType.Plus) || Check(TokenType.Minus))
            {
                var op = Advance().Value;
                var right = ParseMultiplicative();
                expr = new BinaryOp(op, expr, right);
            }
            
            return expr;
        }

        private Expression ParseMultiplicative()
        {
            var expr = ParseUnary();
            
            while (Check(TokenType.Star) || Check(TokenType.Slash))
            {
                var op = Advance().Value;
                var right = ParseUnary();
                expr = new BinaryOp(op, expr, right);
            }
            
            return expr;
        }

        private Expression ParseUnary()
        {
            if (Check(TokenType.Minus) || Check(TokenType.Plus) || Check(TokenType.Hash) || Check(TokenType.Question))
            {
                var op = Advance().Value;
                var operand = ParseUnary();
                return new UnaryOp(op, operand);
            }
            
            return ParsePostfix();
        }

        private Expression ParsePostfix()
        {
            var expr = ParsePrimary();
            
            while (true)
            {
                if (Check(TokenType.LeftBracket))
                {
                    Advance();
                    var index = ParseExpression();
                    Consume(TokenType.RightBracket, "Expected ']'");
                    expr = new Indexing(expr, index);
                }
                else
                {
                    break;
                }
            }
            
            return expr;
        }

        private Expression ParsePrimary()
        {
            if (Check(TokenType.Int))
            {
                var value = long.Parse(Advance().Value);
                return new IntLiteral(value);
            }
            
            if (Check(TokenType.Real))
            {
                var value = decimal.Parse(Advance().Value);
                return new RealLiteral(value);
            }
            
            if (Check(TokenType.String))
            {
                var value = Advance().Value;
                return new StringLiteral(value);
            }
            
            if (Check(TokenType.LeftBrace))
            {
                return ParseObjectLiteral();
            }
            
            if (Check(TokenType.Identifier))
            {
                var name = Advance().Value;
                
                if (Check(TokenType.LeftParen))
                {
                    Advance();
                    var args = ParseFunctionArguments();
                    Consume(TokenType.RightParen, "Expected ')'");
                    return new FunctionCall(name, args);
                }
                
                return new Identifier(name);
            }
            
            if (Check(TokenType.LeftParen))
            {
                Advance();
                var expr = ParseExpression();
                Consume(TokenType.RightParen, "Expected ')'");
                return expr;
            }
            
            throw new ParseException($"Unexpected token: {Current().Value}");
        }

        private Expression ParseObjectLiteral()
        {
            Consume(TokenType.LeftBrace, "Expected '{'");
            var fields = new List<(Expression, Expression)>();
            
            while (!Check(TokenType.RightBrace))
            {
                var key = ParseExpression();
                Consume(TokenType.Colon, "Expected ':'");
                var value = ParseExpression();
                fields.Add((key, value));
                
                if (Check(TokenType.Comma))
                    Advance();
            }
            
            Consume(TokenType.RightBrace, "Expected '}'");
            return new ObjectLiteral(fields);
        }

        private List<FunctionArgument> ParseFunctionArguments()
        {
            var args = new List<FunctionArgument>();
            
            while (!Check(TokenType.RightParen) && !IsAtEnd())
            {
                string? label = null;
                IteratorClause? iterator = null;
                
                // Check for iterator clause
                if (Check(TokenType.Identifier) && PeekAhead(1)?.Type == TokenType.In)
                {
                    var varName = Advance().Value;
                    Advance(); // consume 'in'
                    var iterable = ParseExpression();
                    iterator = new IteratorClause(varName, iterable);
                }
                
                var value = ParseExpression();
                args.Add(new FunctionArgument(label, value, iterator));
                
                if (Check(TokenType.Comma))
                    Advance();
            }
            
            return args;
        }

        private void SkipImportStatement()
        {
            Consume(TokenType.Import, "Expected 'import'");
            Consume(TokenType.Identifier, "Expected module name");
            if (Check(TokenType.As))
            {
                Advance();
                Consume(TokenType.Identifier, "Expected alias");
            }
            if (Check(TokenType.Semicolon))
                Advance();
        }

        // ===== Helper Methods =====

        private bool Check(TokenType type) => !IsAtEnd() && Current().Type == type;

        private Token Advance()
        {
            if (!IsAtEnd())
                _position++;
            return Previous();
        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type))
                return Advance();
            
            throw new ParseException($"{message} at {Current().Value}");
        }

        private bool IsAtEnd() => Current().Type == TokenType.Eof;

        private Token Current() => _tokens[_position];

        private Token Previous() => _tokens[_position - 1];

        private Token? PeekAhead(int offset)
        {
            var idx = _position + offset;
            return idx < _tokens.Count ? _tokens[idx] : null;
        }
    }

    public class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }
}
