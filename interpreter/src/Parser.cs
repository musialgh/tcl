using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TclInterpreter.Parsing
{
    public class ParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public ParseException(string message, int line = 0, int col = 0)
            : base(line > 0 ? $"{message} at line {line}, col {col}" : message)
        {
            Line = line;
            Column = col;
        }
    }

    /// <summary>
    /// Recursive-descent parser for the TCL DSL.
    /// </summary>
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;
        private int _parenDepth; // tracks nesting inside ( ) for disambiguation

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _pos = 0;
            _parenDepth = 0;
        }

        // ================================================================
        //  Location tracking helper
        // ================================================================

        private T WithLocation<T>(T node) where T : AstNode
        {
            if (_pos > 0)
            {
                var prevToken = _tokens[_pos - 1];
                node.Line = prevToken.Line;
                node.Column = prevToken.Column;
            }
            return node;
        }

        // ================================================================
        //  Public entry point
        // ================================================================

        public ProgramNode ParseProgram()
        {
            var defs = new List<TopLevelDef>();

            while (!AtEnd())
            {
                defs.Add(ParseTopLevel());
            }

            return new ProgramNode(defs);
        }

        // ================================================================
        //  Top-level definitions
        // ================================================================

        /// <summary>
        /// Top-level can be:
        ///   import ...;
        ///   name(...): ... -> ... ;           (signature/template)
        ///   name(...): ... -> ... ;  name(params) = ...  (signature then impl)
        ///   name(params) = ...                (impl only)
        /// </summary>
        private TopLevelDef ParseTopLevel()
        {
            if (Check(TokenType.KwImport))
                return ParseImport();

            // The next token must be an identifier (function/template name)
            var name = Expect(TokenType.Identifier, "Expected top-level name").Value;

            // Peek at what follows the name:
            //  '(' => could be template ref, param list for impl, or copy-shorthand
            //  ':' => signature (no template ref)

            // --- Shorthand copy: name(other_name);  ---
            // e.g.  skating_refine_r11(skating_refine_r10);
            // Detection: '(' Identifier ')' ';'
            if (Check(TokenType.LeftParen) && IsSignatureCopyShorthand())
            {
                Advance(); // '('
                var copyFrom = Expect(TokenType.Identifier, "Expected source name in copy shorthand").Value;
                Expect(TokenType.RightParen, "Expected ')'");
                Expect(TokenType.Semicolon, "Expected ';' after copy shorthand");
                return new SignatureDef(name, copyFrom, null, new List<ParamDecl>(), new List<ParamDecl>());
            }

            // --- Template ref: name(template): ... ---
            string? templateRef = null;
            if (Check(TokenType.LeftParen) && IsTemplateRefAhead())
            {
                Advance(); // '('
                templateRef = Expect(TokenType.Identifier, "Expected template name").Value;
                Expect(TokenType.RightParen, "Expected ')'");
            }

            // If next is ':', this is a signature declaration
            if (Check(TokenType.Colon))
            {
                Advance(); // ':'
                return ParseSignatureDecl(name, templateRef);
            }

            // Otherwise this must be an implementation: name(p1, p2, ...) = ...
            if (Check(TokenType.LeftParen))
            {
                return ParseFunctionImpl(name, templateRef);
            }

            throw Error($"Unexpected token '{Cur().Value}' after name '{name}'");
        }

        /// <summary>
        /// Determines if '(' Identifier ')' ';' follows (signature copy shorthand)
        /// where the Identifier is NOT followed by ':' inside (which would be params).
        /// </summary>
        private bool IsSignatureCopyShorthand()
        {
            // '(' <id> ')' ';'
            if (Peek(0).Type != TokenType.LeftParen) return false;
            if (Peek(1).Type != TokenType.Identifier) return false;
            if (Peek(2).Type != TokenType.RightParen) return false;
            if (Peek(3).Type != TokenType.Semicolon) return false;
            return true;
        }

        /// <summary>
        /// Determines if the '(' starts a template ref: '(' Identifier ')' followed by ':'
        /// </summary>
        private bool IsTemplateRefAhead()
        {
            if (Peek(0).Type != TokenType.LeftParen) return false;
            if (Peek(1).Type != TokenType.Identifier) return false;
            if (Peek(2).Type != TokenType.RightParen) return false;
            // After ')' should be ':' for signature or '(' for implementation with template
            // It's a template ref if ')' is followed by ':'
            var afterClose = Peek(3).Type;
            return afterClose == TokenType.Colon;
        }

        private ImportDef ParseImport()
        {
            Expect(TokenType.KwImport, "Expected 'import'");
            // Module name can contain hyphens: "cross-majority-skating"
            // We'll read identifiers and hyphens until we get 'as' or ';'
            var moduleName = Expect(TokenType.Identifier, "Expected module name").Value;
            while (Check(TokenType.Minus))
            {
                Advance();
                moduleName += "-" + Expect(TokenType.Identifier, "Expected identifier after '-' in module name").Value;
            }
            string? alias = null;
            if (Check(TokenType.KwAs))
            {
                Advance();
                alias = Expect(TokenType.Identifier, "Expected alias").Value;
            }
            Expect(TokenType.Semicolon, "Expected ';'");
            return new ImportDef(moduleName, alias);
        }

        /// <summary>
        /// Parse a signature declaration after we've already consumed "name(template?):".
        /// Reads input params -> output params ;  and optionally the implementation that follows.
        /// </summary>
        private TopLevelDef ParseSignatureDecl(string name, string? templateRef)
        {
            var inputs = ParseParamDeclList(TokenType.Arrow);
            Expect(TokenType.Arrow, "Expected '->'");
            var outputs = ParseParamDeclList(TokenType.Semicolon);
            Expect(TokenType.Semicolon, "Expected ';'");

            var sig = new SignatureDef(name, null, templateRef, inputs, outputs);

            // Check if an implementation immediately follows: name(params) = ...
            if (Check(TokenType.Identifier) && Cur().Value == name)
            {
                // Peek ahead to see if this is an implementation
                // name '(' ...
                if (Peek(1).Type == TokenType.LeftParen)
                {
                    Advance(); // consume the name again
                    var func = ParseFunctionImpl(name, templateRef);
                    // Attach signature to the FunctionDef
                    return func with { Signature = sig };
                }
            }

            return sig;
        }

        /// <summary>
        /// Parse function implementation: (p1, p2, ...) = statements [where bindings]
        /// Name has already been consumed.
        /// </summary>
        private FunctionDef ParseFunctionImpl(string name, string? templateRef)
        {
            Expect(TokenType.LeftParen, "Expected '('");
            var implParams = new List<string>();
            while (!Check(TokenType.RightParen) && !AtEnd())
            {
                implParams.Add(Expect(TokenType.Identifier, "Expected parameter name").Value);
                if (Check(TokenType.Comma)) Advance();
            }
            Expect(TokenType.RightParen, "Expected ')'");
            Expect(TokenType.Equals, "Expected '='");

            var body = ParseFunctionBody();
            return new FunctionDef(name, templateRef, null, implParams, body);
        }

        // ================================================================
        //  Parameter declaration lists (for signatures)
        // ================================================================

        /// <summary>
        /// Parse a comma-separated list of parameter declarations, stopping before <paramref name="stopToken"/>.
        /// </summary>
        private List<ParamDecl> ParseParamDeclList(TokenType stopToken)
        {
            var result = new List<ParamDecl>();
            while (!Check(stopToken) && !AtEnd())
            {
                result.Add(ParseParamDecl());
                if (Check(TokenType.Comma))
                    Advance();
            }
            return result;
        }

        private ParamDecl ParseParamDecl()
        {
            // Field name: either an identifier or a string literal
            string fieldName;
            if (Check(TokenType.StringLiteral))
                fieldName = Advance().Value;
            else
                fieldName = Expect(TokenType.Identifier, "Expected parameter name").Value;

            // Optional trailing '?' for optional params (before the colon)
            bool optional = false;
            if (Check(TokenType.Question))
            {
                Advance();
                optional = true;
            }

            Expect(TokenType.Colon, "Expected ':'");
            var type = ParseTypeAnnotation();

            // Optional default value: = expr
            Expression? defaultValue = null;
            if (Check(TokenType.Equals))
            {
                Advance();
                defaultValue = ParseExpression();
            }

            return new ParamDecl(fieldName, type, optional, defaultValue);
        }

        // ================================================================
        //  Type annotations
        // ================================================================

        private TypeAnnotation ParseTypeAnnotation()
        {
            if (Check(TokenType.LeftBrace))
                return ParseObjectTypeAnnotation();

            // Primitive type keyword or identifier used as type
            if (IsTypeKeyword(Cur().Type) || Check(TokenType.Identifier))
            {
                var typeName = Advance().Value;
                return new PrimitiveType(typeName);
            }

            throw Error("Expected type annotation");
        }

        private ObjectType ParseObjectTypeAnnotation()
        {
            Expect(TokenType.LeftBrace, "Expected '{'");
            var fields = new List<ObjectFieldDecl>();

            while (!Check(TokenType.RightBrace) && !AtEnd())
            {
                // Field name: identifier, string literal, int literal, or type keyword
                string fieldName;
                if (Check(TokenType.StringLiteral))
                    fieldName = Advance().Value;
                else if (Check(TokenType.IntLiteral))
                    fieldName = Advance().Value;
                else if (CheckIdent())
                    fieldName = Advance().Value;
                else
                    throw Error("Expected field name");

                // Cardinality: +, *, ?
                string cardinality = "";
                if (Check(TokenType.Plus)) { Advance(); cardinality = "+"; }
                else if (Check(TokenType.Star)) { Advance(); cardinality = "*"; }
                else if (Check(TokenType.Question)) { Advance(); cardinality = "?"; }

                Expect(TokenType.Colon, "Expected ':'");
                var fieldType = ParseTypeAnnotation();

                fields.Add(new ObjectFieldDecl(fieldName, cardinality, fieldType));

                if (Check(TokenType.Comma)) Advance();
            }

            Expect(TokenType.RightBrace, "Expected '}'");
            return new ObjectType(fields);
        }

        private static bool IsTypeKeyword(TokenType t) =>
            t == TokenType.KwBool || t == TokenType.KwInt || t == TokenType.KwReal ||
            t == TokenType.KwNat || t == TokenType.KwNum || t == TokenType.KwObject || t == TokenType.KwAny;

        // ================================================================
        //  Function body
        // ================================================================

        /// <summary>
        /// Parse the body after '='.
        /// Body = statements (separated by commas) [where bindings (sep by commas/semicolons)]
        /// A statement can be:
        ///   - A conditional chain: expr if cond | expr if cond | expr
        ///   - An assignment: target(s) = expr
        ///
        /// The body ends at the next top-level definition (detected by seeing
        /// an identifier followed by '(' or ':' at the same level, with no preceding comma).
        /// </summary>
        private FunctionBody ParseFunctionBody()
        {
            var statements = new List<Statement>();

            // Parse the main statements (before 'where')
            statements.Add(ParseStatement());
            while (Check(TokenType.Comma) && !IsWhereAhead())
            {
                Advance(); // consume comma
                if (Check(TokenType.KwWhere) || AtEnd()) break;
                statements.Add(ParseStatement());
            }

            // Optional where clause
            List<LetBinding>? whereBindings = null;
            if (Check(TokenType.KwWhere))
            {
                Advance(); // consume 'where'
                whereBindings = ParseWhereBindings();
            }

            // Consume trailing semicolons that separate function definitions
            while (Check(TokenType.Semicolon))
                Advance();

            return new FunctionBody(statements, whereBindings);
        }

        /// <summary>
        /// Check if the upcoming comma is followed by a where keyword.
        /// </summary>
        private bool IsWhereAhead()
        {
            // Check if current is comma and next is 'where'
            // but actually we haven't consumed the comma yet
            // We just need to check if after comma comes 'where'
            // No need here — the main loop will break on 'where' naturally
            return false;
        }

        private Statement ParseStatement()
        {
            // Try to detect assignment: identifier(s) '=' ...
            // Could be:  target = expr
            //            target1, target2 = expr
            //            expr if cond | expr if cond | expr   (conditional chain for whole-body return)

            // Look ahead to determine if this is an assignment
            if (IsAssignmentAhead())
            {
                return ParseAssignmentStatement();
            }

            // Otherwise parse as expression (possibly conditional chain)
            var expr = ParseConditionalChainOrExpression();
            return WrapInStatement(expr);
        }

        /// <summary>
        /// Detects patterns like:  id = ...  or  id, id, id = ...
        /// </summary>
        private bool IsAssignmentAhead()
        {
            if (!Check(TokenType.Identifier)) return false;

            int save = _pos;
            try
            {
                // Skip identifiers and commas
                while (Cur().Type == TokenType.Identifier)
                {
                    _pos++;
                    if (Cur().Type == TokenType.Comma)
                    {
                        _pos++;
                    }
                    else
                    {
                        break;
                    }
                }
                return Cur().Type == TokenType.Equals;
            }
            finally
            {
                _pos = save;
            }
        }

        private AssignmentStmt ParseAssignmentStatement()
        {
            var targets = new List<string>();
            targets.Add(Expect(TokenType.Identifier, "Expected target").Value);
            while (Check(TokenType.Comma))
            {
                // Need to distinguish "target1, target2 = expr" from
                // being at the end of a statement list
                // If next after comma is identifier followed by '=' or ','  -> more targets
                if (Peek(1).Type == TokenType.Identifier &&
                    (Peek(2).Type == TokenType.Equals || Peek(2).Type == TokenType.Comma))
                {
                    Advance(); // consume comma
                    targets.Add(Expect(TokenType.Identifier, "Expected target").Value);
                }
                else
                {
                    break;
                }
            }
            Expect(TokenType.Equals, "Expected '='");
            var value = ParseConditionalChainOrExpression();
            return new AssignmentStmt(targets, value);
        }

        /// <summary>
        /// Parse an expression that may be a conditional chain:
        ///   expr1 if cond1 | expr2 if cond2 | expr3
        /// </summary>
        private Expression ParseConditionalChainOrExpression()
        {
            var expr = ParseExpression();

            if (!Check(TokenType.KwIf))
                return expr;

            // This is a conditional chain
            // Already parsed the "then" expression; now parse "if condition | ..."
            return ParseConditionalChainRest(expr);
        }

        private Expression ParseConditionalChainRest(Expression thenExpr)
        {
            Expect(TokenType.KwIf, "Expected 'if'");
            var condition = ParseExpression();
            Expect(TokenType.Pipe, "Expected '|'");

            // Parse the else part (which may itself be another conditional chain)
            var elseExpr = ParseExpression();

            if (Check(TokenType.KwIf))
            {
                // Another branch in the chain
                elseExpr = ParseConditionalChainRest(elseExpr);
            }

            return WithLocation(new ConditionalExpr(thenExpr, condition, elseExpr));
        }

        private Statement WrapInStatement(Expression expr)
        {
            // A bare expression as a statement is treated as a single-target assignment
            // to an implicit return. We wrap it in a ConditionalChainStmt or AssignmentStmt.
            // For simplicity we create an AssignmentStmt with empty targets (= return value)
            return new AssignmentStmt(new List<string>(), expr);
        }

        // ================================================================
        //  Where bindings
        // ================================================================

        private List<LetBinding> ParseWhereBindings()
        {
            var bindings = new List<LetBinding>();

            while (!AtEnd() && !IsNextTopLevelDef())
            {
                if (!CheckIdent()) break;

                // Detect binding: could be "name = expr" or "name1, name2 = expr"
                if (!IsWhereBindingAhead()) break;

                // Collect target names
                var names = new List<string>();
                names.Add(Advance().Value);
                while (Check(TokenType.Comma) && Peek(1).Type == TokenType.Identifier)
                {
                    // Check if this comma leads to more targets followed by '='
                    int save = _pos;
                    _pos++; // skip comma
                    var nextName = Cur().Value;
                    _pos++;
                    bool moreTargets = false;
                    // Check if current is '=' or another comma followed by id
                    if (Cur().Type == TokenType.Equals || Cur().Type == TokenType.Comma)
                        moreTargets = true;
                    _pos = save;

                    if (moreTargets)
                    {
                        Advance(); // skip comma
                        names.Add(ConsumeIdent("Expected binding name"));
                    }
                    else
                        break;
                }

                Expect(TokenType.Equals, "Expected '='");
                var value = ParseConditionalChainOrExpression();

                // add binding (may be single-target or multi-target)
                bindings.Add(new LetBinding(names, value));
                
                // Bindings separated by comma or semicolon
                if (Check(TokenType.Comma))
                    Advance();
                else if (Check(TokenType.Semicolon))
                    Advance();
                else
                    break;
            }

            return bindings;
        }

        /// <summary>
        /// Check if current position starts a where binding: id [',' id]* '=' ...
        /// </summary>
        private bool IsWhereBindingAhead()
        {
            int save = _pos;
            try
            {
                // Skip identifiers and commas
                while (_pos < _tokens.Count && (Cur().Type == TokenType.Identifier || IsTypeKeyword(Cur().Type)))
                {
                    _pos++;
                    if (Cur().Type == TokenType.Comma)
                        _pos++;
                    else
                        break;
                }
                return Cur().Type == TokenType.Equals;
            }
            finally
            {
                _pos = save;
            }
        }

        /// <summary>
        /// Heuristic: are we about to see the start of a new top-level definition?
        /// Pattern: Identifier followed by '(' or ':'
        /// </summary>
        private bool IsNextTopLevelDef()
        {
            if (!Check(TokenType.Identifier)) return false;
            var next = Peek(1).Type;
            // If identifier is followed by '(' or ':' it's likely a new def
            // But we need to exclude identifiers followed by '=' which are where-bindings
            if (next == TokenType.Colon) return true;
            if (next == TokenType.LeftParen)
            {
                // Could be a function call in a where-binding or a new def
                // New def: name(template): or name(params) =
                // We check further: after matching parens, is there '=' or ':' or ';'?
                int save = _pos;
                try
                {
                    _pos += 2; // skip name and '('
                    int depth = 1;
                    while (_pos < _tokens.Count && depth > 0)
                    {
                        if (Cur().Type == TokenType.LeftParen) depth++;
                        else if (Cur().Type == TokenType.RightParen) depth--;
                        _pos++;
                    }
                    // After closing ')': '=' means impl def, ':' means sig def, ';' means copy shorthand
                    return Cur().Type == TokenType.Equals ||
                           Cur().Type == TokenType.Colon ||
                           Cur().Type == TokenType.Semicolon;
                }
                finally
                {
                    _pos = save;
                }
            }
            return false;
        }

        // ================================================================
        //  Expressions  (precedence climbing)
        // ================================================================

        private Expression ParseExpression()
        {
            return ParseComparison();
        }

        private Expression ParseComparison()
        {
            var expr = ParseAdditive();

            // Comparison operators: < > <= >= =
            // Note: '=' is overloaded (assignment vs equality).
            // In expression context after an operand, '=' is equality.
            while (Check(TokenType.Less) || Check(TokenType.Greater) ||
                   Check(TokenType.LessEqual) || Check(TokenType.GreaterEqual) ||
                   CheckEqualityInExpr())
            {
                var op = Advance().Value;
                var right = ParseAdditive();
                expr = WithLocation(new BinaryOpExpr(op, expr, right));
            }
            
            return expr;
        }

        /// <summary>
        /// '=' as equality operator in expression context.
        /// We need to distinguish from assignment '='.
        /// Heuristic: if we already have a left operand (we do, since we're in Comparison),
        /// and the next '=' is followed by something that looks like a value, it's equality.
        /// </summary>
        private bool CheckEqualityInExpr()
        {
            if (!Check(TokenType.Equals)) return false;
            // '=' at this point is equality (we're already inside an expression)
            return true;
        }

        private Expression ParseAdditive()
        {
            var expr = ParseMultiplicative();
            
            while (Check(TokenType.Plus) || Check(TokenType.Minus))
            {
                var op = Advance().Value;
                var right = ParseMultiplicative();
                expr = WithLocation(new BinaryOpExpr(op, expr, right));
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
                expr = WithLocation(new BinaryOpExpr(op, expr, right));
            }
            
            return expr;
        }

        private Expression ParseUnary()
        {
            if (Check(TokenType.Minus) || Check(TokenType.Plus) ||
                Check(TokenType.Hash) || Check(TokenType.Question))
            {
                var op = Advance().Value;
                var operand = ParseUnary();
                return WithLocation(new UnaryOpExpr(op, operand));
            }
            
            return ParsePostfix();
        }

        private Expression ParsePostfix()
        {
            var expr = ParsePrimary();

            while (Check(TokenType.LeftBracket))
            {
                Advance(); // '['
                var index = ParseExpression();
                Expect(TokenType.RightBracket, "Expected ']'");
                expr = WithLocation(new IndexExpr(expr, index));
            }
            
            return expr;
        }

        private Expression ParsePrimary()
        {
            // Integer literal
            if (Check(TokenType.IntLiteral))
            {
                var tok = Advance();
                var value = long.Parse(tok.Value, CultureInfo.InvariantCulture);

                // Check for range: intLiteral '..' expr
                if (Check(TokenType.DotDot))
                {
                    Advance();
                    var end = ParseExpression();
                    return WithLocation(new RangeExpr(WithLocation(new IntLiteral(value)), end));
                }

                return WithLocation(new IntLiteral(value));
            }

            // Real literal
            if (Check(TokenType.RealLiteral))
            {
                var tok = Advance();
                var value = decimal.Parse(tok.Value, CultureInfo.InvariantCulture);
                return WithLocation(new RealLiteral(value));
            }

            // String literal
            if (Check(TokenType.StringLiteral))
            {
                return WithLocation(new StringLiteral(Advance().Value));
            }
            
            // Boolean literals
            if (Check(TokenType.KwTrue))
            {
                Advance();
                return WithLocation(new BoolLiteral(true));
            }
            if (Check(TokenType.KwFalse))
            {
                Advance();
                return WithLocation(new BoolLiteral(false));
            }

            // Object literal: { ... }
            if (Check(TokenType.LeftBrace))
            {
                return WithLocation(ParseObjectLiteralExpr());
            }
            
            // Parenthesized expression
            if (Check(TokenType.LeftParen))
            {
                Advance();
                _parenDepth++;
                var expr = ParseExpression();
                _parenDepth--;
                Expect(TokenType.RightParen, "Expected ')'");
                return expr;
            }

            // Identifier — could be variable ref, function call, or start of range
            if (Check(TokenType.Identifier))
            {
                var name = Advance().Value;
                
                // Function call: name(...)
                if (Check(TokenType.LeftParen))
                {
                    Advance(); // '('
                    _parenDepth++;
                    var args = ParseCallArgs();
                    _parenDepth--;
                    Expect(TokenType.RightParen, "Expected ')'");
                    return WithLocation(new FunctionCallExpr(name, args));
                }

                // Range: identifier '..' expr
                if (Check(TokenType.DotDot))
                {
                    Advance();
                    var end = ParseExpression();
                    return WithLocation(new RangeExpr(WithLocation(new IdentifierExpr(name)), end));
                }

                return WithLocation(new IdentifierExpr(name));
            }

            throw Error($"Unexpected token: {Cur()}");
        }

        // ================================================================
        //  Object literal expressions
        // ================================================================

        /// <summary>
        /// { key : value, ... }  or  { key : value where ... }
        /// Keys can be expressions (identifiers, strings, numbers, complex exprs).
        /// </summary>
        private Expression ParseObjectLiteralExpr()
        {
            Expect(TokenType.LeftBrace, "Expected '{'");

            // Empty object
            if (Check(TokenType.RightBrace))
            {
                Advance();
                return WithLocation(new ObjectLiteral(new List<(Expression, Expression)>()));
            }

            var fields = new List<(Expression Key, Expression Value)>();

            while (!Check(TokenType.RightBrace) && !AtEnd())
            {
                var key = ParseExpression();
                Expect(TokenType.Colon, "Expected ':' in object literal");
                var value = ParseConditionalChainOrExpression();
                fields.Add((key, value));

                if (Check(TokenType.Comma))
                    Advance();
                else
                    break;
            }

            Expect(TokenType.RightBrace, "Expected '}'");

            Expression result = WithLocation(new ObjectLiteral(fields));

            // Check for inline 'where' — only when inside parentheses (e.g. function call args)
            // At top level of a function body, 'where' belongs to the body, not the object.
            if (_parenDepth > 0 && Check(TokenType.KwWhere))
            {
                Advance();
                var bindings = ParseInlineWhereBindings();
                result = WithLocation(new WhereExpr(result, bindings));
            }

            return result;
        }

        /// <summary>
        /// Parse bindings in an inline where (inside an expression).
        /// Ends at comma, closing brace/paren, semicolon, pipe, 'if', or next statement boundary.
        /// </summary>
        private List<LetBinding> ParseInlineWhereBindings()
        {
            var bindings = new List<LetBinding>();

            while (!AtEnd())
            {
                if (!Check(TokenType.Identifier)) break;
                // Must be  name '=' expr
                if (Peek(1).Type != TokenType.Equals) break;

                var name = Advance().Value;
                Expect(TokenType.Equals, "Expected '='");
                var value = ParseExpression();
                                bindings.Add(new LetBinding(new List<string> { name }, value));

                if (Check(TokenType.Comma))
                    Advance();
                else
                    break;
            }

            return bindings;
        }

        // ================================================================
        //  Function call arguments
        // ================================================================

        /// <summary>
        /// Parse call arguments. Handles iterator clauses:
        ///   func(var in coll, expr)
        ///   func(var1, var2 in coll, expr)
        /// </summary>
        private List<CallArg> ParseCallArgs()
        {
            var args = new List<CallArg>();

            while (!Check(TokenType.RightParen) && !AtEnd())
            {
                // Try to detect iterator clause: var [, var]* 'in' expr
                var iterClause = TryParseIteratorClause();

                if (iterClause != null)
                {
                    // The iterator is the first argument
                    args.Add(new CallArg(iterClause.Collection, iterClause));

                    if (Check(TokenType.Comma))
                        Advance();
                    continue;
                }
                
                var value = ParseConditionalChainOrExpression();
                args.Add(new CallArg(value, null));

                if (Check(TokenType.Comma))
                    Advance();
            }
            
            return args;
        }

        /// <summary>
        /// Try to parse an iterator clause: var [, var]* 'in' expr
        /// Returns null if the pattern doesn't match (and restores position).
        /// </summary>
        private IteratorClause? TryParseIteratorClause()
        {
            int save = _pos;

            // Collect identifiers separated by commas, followed by 'in'
            var vars = new List<string>();

            if (!Check(TokenType.Identifier)) return null;

            vars.Add(Cur().Value);
            _pos++;

            while (Check(TokenType.Comma))
            {
                _pos++; // skip comma
                if (Check(TokenType.Identifier))
                {
                    vars.Add(Cur().Value);
                    _pos++;
                }
                else
                {
                    // Not a valid iterator pattern
                    _pos = save;
                    return null;
                }

                // After the second+ identifier, check for 'in'
                if (Check(TokenType.KwIn)) break;
            }

            if (!Check(TokenType.KwIn))
            {
                _pos = save;
                return null;
            }

            Advance(); // consume 'in'
            var collection = ParseExpression();
            return new IteratorClause(vars, collection);
        }

        // ================================================================
        //  Token helpers
        // ================================================================

        private Token Cur() => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];
        private Token Peek(int offset)
        {
            var idx = _pos + offset;
            return idx < _tokens.Count ? _tokens[idx] : _tokens[^1];
        }

        private bool AtEnd() => Cur().Type == TokenType.Eof;

        private bool Check(TokenType type) => !AtEnd() && Cur().Type == type;

        /// <summary>
        /// Check if current token is an identifier or a type keyword used as identifier.
        /// In expression/binding context, type keywords like 'num', 'int', etc. can be identifiers.
        /// </summary>
        private bool CheckIdent() => Check(TokenType.Identifier) || IsTypeKeyword(Cur().Type);

        /// <summary>
        /// Consume current token as an identifier, accepting type keywords as identifiers.
        /// </summary>
        private string ConsumeIdent(string message)
        {
            if (CheckIdent())
                return Advance().Value;
            throw Error(message + $" (got {Cur()})");
        }

        private Token Advance()
        {
            var tok = Cur();
            if (!AtEnd()) _pos++;
            return tok;
        }

        private Token Expect(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            throw Error(message + $" (got {Cur()})");
        }

        private ParseException Error(string message) =>
            new ParseException(message, Cur().Line, Cur().Column);
    }
}
