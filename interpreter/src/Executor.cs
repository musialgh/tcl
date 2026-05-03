using System;
using System.Collections.Generic;
using TclInterpreter.Parsing;
using TclInterpreter.Typing;

namespace TclInterpreter.Execution
{
    /// <summary>
    /// Executes AST nodes and evaluates expressions.
    /// </summary>
    public class Executor
    {
        private readonly Dictionary<string, FunctionDefinition> _functions;
        private int _recursionDepth;
        private readonly int _maxRecursionDepth;
        private readonly ExecutionContext _context;

        public Executor(Program program, int maxRecursionDepth = 10000)
        {
            _maxRecursionDepth = maxRecursionDepth;
            _recursionDepth = 0;
            _context = new ExecutionContext();
            _functions = new Dictionary<string, FunctionDefinition>();

            // Index all functions
            foreach (var func in program.Functions)
            {
                _functions[func.Name] = func;
            }
        }

        public DslValue ExecuteFunction(string name, Dictionary<string, DslValue> inputs)
        {
            if (!_functions.TryGetValue(name, out var func))
                throw new ExecutionException($"Function '{name}' not found");

            _recursionDepth++;
            if (_recursionDepth > _maxRecursionDepth)
                throw new ExecutionException($"Recursion depth exceeded (max: {_maxRecursionDepth})");

            try
            {
                _context.PushScope();

                // Bind input parameters
                foreach (var (paramName, value) in inputs)
                {
                    _context.SetVariable(paramName, value);
                }

                // Execute function body
                var result = ExecuteFunctionBody(func.Body);

                _context.PopScope();
                return result;
            }
            finally
            {
                _recursionDepth--;
            }
        }

        private DslValue ExecuteFunctionBody(FunctionBody body)
        {
            var results = new Dictionary<string, DslValue>();

            // Execute assignments before where clause
            foreach (var assignment in body.BeforeWhere)
            {
                var value = EvaluateExpression(assignment.Value);
                results[assignment.Target] = value;
                _context.SetVariable(assignment.Target, value);
            }

            // Execute where clause if present
            if (body.AfterWhere != null)
            {
                foreach (var assignment in body.AfterWhere)
                {
                    var value = EvaluateExpression(assignment.Value);
                    results[assignment.Target] = value;
                    _context.SetVariable(assignment.Target, value);
                }
            }

            // Return results as an object
            var resultObj = new ObjectValue();
            foreach (var (key, value) in results)
            {
                resultObj.Fields[key] = value;
            }

            return resultObj;
        }

        private DslValue EvaluateExpression(Expression expr)
        {
            return expr switch
            {
                IntLiteral il => new NumericValue(il.Value, "int"),
                RealLiteral rl => new NumericValue(rl.Value, "real"),
                StringLiteral sl => new StringValue(sl.Value),
                BoolLiteral bl => new BoolValue(bl.Value),
                Identifier id => _context.GetVariable(id.Name) ?? throw new ExecutionException($"Undefined variable: {id.Name}"),
                UnaryOp uo => EvaluateUnaryOp(uo),
                BinaryOp bo => EvaluateBinaryOp(bo),
                FunctionCall fc => EvaluateFunctionCall(fc),
                Indexing idx => EvaluateIndexing(idx),
                ObjectLiteral ol => EvaluateObjectLiteral(ol),
                ConditionalExpr ce => EvaluateConditional(ce),
                _ => throw new ExecutionException($"Unknown expression type: {expr.GetType().Name}")
            };
        }

        private DslValue EvaluateUnaryOp(UnaryOp op)
        {
            var operand = EvaluateExpression(op.Operand);

            return op.Operator switch
            {
                "-" => ApplyUnaryNegation(operand),
                "+" => operand,
                "#" => GetCardinality(operand),
                "?" => new BoolValue(operand is not null),
                _ => throw new ExecutionException($"Unknown unary operator: {op.Operator}")
            };
        }

        private DslValue ApplyUnaryNegation(DslValue operand)
        {
            if (operand is NumericValue nv)
                return new NumericValue(-nv.Value, "real");

            if (operand is ObjectValue ov)
            {
                var result = new ObjectValue();
                foreach (var (key, value) in ov.Fields)
                {
                    result.Fields[key] = ApplyUnaryNegation(value);
                }
                return result;
            }

            throw new ExecutionException("Cannot negate non-numeric value");
        }

        private DslValue GetCardinality(DslValue value)
        {
            if (value is ObjectValue ov)
                return new NumericValue(ov.Fields.Count, "int");

            throw new ExecutionException("Cardinality (#) only applies to objects");
        }

        private DslValue EvaluateBinaryOp(BinaryOp op)
        {
            var left = EvaluateExpression(op.Left);
            var right = EvaluateExpression(op.Right);

            // Numeric operations
            if (left is NumericValue lnv && right is NumericValue rnv)
            {
                return op.Operator switch
                {
                    "+" => new NumericValue(lnv.Value + rnv.Value, "real"),
                    "-" => new NumericValue(lnv.Value - rnv.Value, "real"),
                    "*" => new NumericValue(lnv.Value * rnv.Value, "real"),
                    "/" => new NumericValue(lnv.Value / rnv.Value, "real"),
                    "<" => new BoolValue(lnv.Value < rnv.Value),
                    ">" => new BoolValue(lnv.Value > rnv.Value),
                    "<=" => new BoolValue(lnv.Value <= rnv.Value),
                    ">=" => new BoolValue(lnv.Value >= rnv.Value),
                    "=" => new BoolValue(lnv.Value == rnv.Value),
                    _ => throw new ExecutionException($"Unknown binary operator: {op.Operator}")
                };
            }

            throw new ExecutionException($"Cannot apply {op.Operator} to {left.GetType().TypeName} and {right.GetType().TypeName}");
        }

        private DslValue EvaluateFunctionCall(FunctionCall fc)
        {
            // TODO: Implement built-in functions and user function calls
            throw new NotImplementedException("Function calls not yet implemented");
        }

        private DslValue EvaluateIndexing(Indexing idx)
        {
            var obj = EvaluateExpression(idx.Object);
            var index = EvaluateExpression(idx.Index);

            if (obj is not ObjectValue ov)
                throw new ExecutionException("Cannot index non-object");

            object? key = index switch
            {
                NumericValue nv => nv.Value,
                StringValue sv => sv.Value,
                _ => throw new ExecutionException("Index must be numeric or string")
            };

            if (ov.Fields.TryGetValue(key, out var value))
                return value;

            throw new ExecutionException($"Key '{key}' not found in object");
        }

        private DslValue EvaluateObjectLiteral(ObjectLiteral ol)
        {
            var result = new ObjectValue();

            foreach (var (keyExpr, valueExpr) in ol.Fields)
            {
                var keyVal = EvaluateExpression(keyExpr);
                var key = keyVal switch
                {
                    NumericValue nv => (object)nv.Value,
                    StringValue sv => sv.Value,
                    _ => throw new ExecutionException("Object keys must be numeric or string")
                };

                var value = EvaluateExpression(valueExpr);
                result.Fields[key] = value;
            }

            return result;
        }

        private DslValue EvaluateConditional(ConditionalExpr ce)
        {
            var condition = EvaluateExpression(ce.Condition);

            if (condition is BoolValue bv && bv.Value)
                return EvaluateExpression(ce.ThenExpr);

            if (ce.ElseExpr != null)
                return EvaluateExpression(ce.ElseExpr);

            throw new ExecutionException("Conditional expression failed and no else branch provided");
        }
    }

    public class ExecutionContext
    {
        private readonly Stack<Dictionary<string, DslValue>> _scopes;

        public ExecutionContext()
        {
            _scopes = new Stack<Dictionary<string, DslValue>>();
            _scopes.Push(new Dictionary<string, DslValue>());
        }

        public void PushScope()
        {
            _scopes.Push(new Dictionary<string, DslValue>());
        }

        public void PopScope()
        {
            if (_scopes.Count > 1)
                _scopes.Pop();
        }

        public void SetVariable(string name, DslValue value)
        {
            _scopes.Peek()[name] = value;
        }

        public DslValue? GetVariable(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out var value))
                    return value;
            }
            return null;
        }
    }

    public class ExecutionException : Exception
    {
        public ExecutionException(string message) : base(message) { }
    }
}
