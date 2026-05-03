using System;
using System.Collections.Generic;

namespace TclInterpreter.Parsing
{
    /// <summary>
    /// Abstract base class for all AST nodes.
    /// </summary>
    public abstract record AstNode;

    // ===== Program Structure =====

    public record Program(List<FunctionDefinition> Functions) : AstNode;

    public record FunctionDefinition(
        string Name,
        string? TemplateRef,
        FunctionSignature Signature,
        FunctionBody Body
    ) : AstNode;

    public record FunctionSignature(
        List<Parameter> InputParams,
        List<Parameter> OutputParams
    ) : AstNode;

    public record Parameter(
        string Name,
        TypeAnnotation Type,
        object? DefaultValue = null,
        bool IsOptional = false
    ) : AstNode;

    public record FunctionBody(
        List<Assignment> BeforeWhere,
        List<Assignment>? AfterWhere = null
    ) : AstNode;

    // ===== Type System =====

    public abstract record TypeAnnotation : AstNode;

    public record PrimitiveType(string Name) : TypeAnnotation;

    public record ObjectType(
        Dictionary<string, (TypeAnnotation Type, string Cardinality)> Fields
    ) : TypeAnnotation;

    public record RangeType(int Start, int End) : TypeAnnotation;

    // ===== Expressions =====

    public abstract record Expression : AstNode;

    public record IntLiteral(long Value) : Expression;

    public record RealLiteral(decimal Value) : Expression;

    public record StringLiteral(string Value) : Expression;

    public record BoolLiteral(bool Value) : Expression;

    public record Identifier(string Name) : Expression;

    public record ObjectLiteral(List<(Expression Key, Expression Value)> Fields) : Expression;

    public record ArrayLiteral(List<Expression> Elements) : Expression;

    public record RangeLiteral(int Start, int End) : Expression;

    public record FunctionCall(
        string Name,
        List<FunctionArgument> Arguments
    ) : Expression;

    public record FunctionArgument(string? Label, Expression Value, IteratorClause? Iterator = null) : AstNode;

    public record IteratorClause(string Variable, Expression IterableExpression) : AstNode;

    public record UnaryOp(string Operator, Expression Operand) : Expression;

    public record BinaryOp(string Operator, Expression Left, Expression Right) : Expression;

    public record Indexing(Expression Object, Expression Index) : Expression;

    public record ConditionalExpr(
        Expression Condition,
        Expression ThenExpr,
        Expression? ElseExpr = null
    ) : Expression;

    public record WhereExpr(
        Expression MainExpr,
        List<Assignment> Bindings
    ) : Expression;

    // ===== Statements =====

    public record Assignment(string Target, Expression Value) : AstNode;
}
