using System;
using System.Collections.Generic;

namespace TclInterpreter.Parsing
{
    // ========================================================================
    //  Base
    // ========================================================================

    public abstract record AstNode;

    // ========================================================================
    //  Top-level program
    // ========================================================================

    /// <summary>A complete program: a collection of top-level definitions.</summary>
    public record ProgramNode(
        List<TopLevelDef> Definitions
    ) : AstNode;

    /// <summary>Any top-level definition.</summary>
    public abstract record TopLevelDef : AstNode;

    /// <summary>import foo as bar;</summary>
    public record ImportDef(
        string ModuleName,
        string? Alias
    ) : TopLevelDef;

    // ========================================================================
    //  Template / Signature declarations
    // ========================================================================

    /// <summary>
    /// A template or function signature declaration (no implementation body).
    /// Examples:
    ///   template_evaluate_element: marks: {...}, params?: {...} -> report?: {...}, places: {...};
    ///   skating_refine_r11(skating_refine_r10);   // signature-copy shorthand
    /// </summary>
    public record SignatureDef(
        string Name,
        string? CopyFrom,           // non-null for shorthand "name(other);" declarations
        string? TemplateRef,        // non-null if "name(template): ..."
        List<ParamDecl> Inputs,
        List<ParamDecl> Outputs
    ) : TopLevelDef;

    /// <summary>
    /// A function definition with implementation.
    /// </summary>
    public record FunctionDef(
        string Name,
        string? TemplateRef,        // optional template conformance
        SignatureDef? Signature,    // null when signature is separate or copied
        List<string> ImplParams,    // parameter names listed in the implementation header
        FunctionBody Body
    ) : TopLevelDef;

    // ========================================================================
    //  Parameters & types
    // ========================================================================

    public record ParamDecl(
        string Name,
        TypeAnnotation Type,
        bool IsOptional,            // trailing '?' on the name
        Expression? DefaultValue    // e.g. for_place : int = 1
    ) : AstNode;

    public abstract record TypeAnnotation : AstNode;

    /// <summary>Primitive type name: bool, int, nat, real, num, object, any.</summary>
    public record PrimitiveType(string Name) : TypeAnnotation;

    /// <summary>Object type with field patterns: { field+ : Type, ... }</summary>
    public record ObjectType(
        List<ObjectFieldDecl> Fields
    ) : TypeAnnotation;

    /// <summary>A single field in an ObjectType declaration.</summary>
    public record ObjectFieldDecl(
        string Name,           // field name (may be an identifier or a string literal key like "pmin")
        string Cardinality,    // "", "+", "*", "?"
        TypeAnnotation Type
    ) : AstNode;

    // ========================================================================
    //  Function body
    // ========================================================================

    /// <summary>
    /// The body of a function implementation.
    /// Before the optional 'where', we have the result assignments / return expression.
    /// After 'where', we have local bindings.
    /// </summary>
    public record FunctionBody(
        List<Statement> Statements,       // assignments or a conditional return chain
        List<LetBinding>? WhereBindings   // bindings after 'where' keyword
    ) : AstNode;

    /// <summary>A statement in the "before-where" part of a function body.</summary>
    public abstract record Statement : AstNode;

    /// <summary>target = expr   or   target1, target2 = expr</summary>
    public record AssignmentStmt(
        List<string> Targets,
        Expression Value
    ) : Statement;

    /// <summary>
    /// A conditional return: expr1 if cond1 | expr2 if cond2 | expr3
    /// (Top-level only, not nested inside other expressions.)
    /// </summary>
    public record ConditionalChainStmt(
        List<(Expression Condition, Expression Value)> Branches,
        Expression? DefaultValue       // final branch without condition
    ) : Statement;

    /// <summary>Binding in a where clause: name = expr  (possibly with trailing comma/semicolon).</summary>
    public record LetBinding(
        string Name,
        Expression Value
    ) : AstNode;

    // ========================================================================
    //  Expressions
    // ========================================================================

    public abstract record Expression : AstNode;

    public record IntLiteral(long Value) : Expression;
    public record RealLiteral(decimal Value) : Expression;
    public record StringLiteral(string Value) : Expression;
    public record BoolLiteral(bool Value) : Expression;
    public record IdentifierExpr(string Name) : Expression;

    /// <summary>{ key: value, key2: value2, ... }</summary>
    public record ObjectLiteral(List<(Expression Key, Expression Value)> Fields) : Expression;

    /// <summary>start..end  range expression (both sides are expressions).</summary>
    public record RangeExpr(Expression Start, Expression End) : Expression;

    /// <summary>
    /// func(arg1, arg2, ...)
    /// Arguments may include iterator clauses: func(x in coll, expr)
    /// </summary>
    public record FunctionCallExpr(
        string Name,
        List<CallArg> Arguments
    ) : Expression;

    /// <summary>A single argument in a function call.</summary>
    public record CallArg(
        Expression Value,
        IteratorClause? Iterator   // non-null when this arg is "var in expr"
    ) : AstNode;

    /// <summary>var in expr  —  iterator clause inside a call argument.</summary>
    public record IteratorClause(
        List<string> Variables,     // one or more iteration variable names
        Expression Collection
    ) : AstNode;

    public record UnaryOpExpr(string Operator, Expression Operand) : Expression;
    public record BinaryOpExpr(string Operator, Expression Left, Expression Right) : Expression;

    /// <summary>obj[index]</summary>
    public record IndexExpr(Expression Object, Expression Index) : Expression;

    /// <summary>
    /// Inline conditional: thenExpr if condition | elseExpr
    /// These form a chain: expr1 if cond1 | expr2 if cond2 | expr3
    /// </summary>
    public record ConditionalExpr(
        Expression ThenExpr,
        Expression Condition,
        Expression ElseExpr
    ) : Expression;

    /// <summary>
    /// Inline where: expr where binding1 = ..., binding2 = ...
    /// </summary>
    public record WhereExpr(
        Expression MainExpr,
        List<LetBinding> Bindings
    ) : Expression;
}
