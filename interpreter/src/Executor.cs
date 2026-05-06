using System;
using System.Collections.Generic;
using System.Linq;
using TclInterpreter.Parsing;
using TclInterpreter.Typing;

namespace TclInterpreter.Execution
{
    public class ExecutionException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public ExecutionException(string message, int line = 0, int col = 0)
            : base(line > 0 ? $"{message} at line {line}, col {col}" : message)
        {
            Line = line;
            Column = col;
        }
    }

    public class Environment
    {
        private readonly Stack<Dictionary<string, DslValue>> _scopes = new();

        public Environment()
        {
            _scopes.Push(new Dictionary<string, DslValue>());
        }

        public void PushScope() => _scopes.Push(new Dictionary<string, DslValue>());

        public void PopScope()
        {
            if (_scopes.Count > 1) _scopes.Pop();
        }

        public void Set(string name, DslValue value) => _scopes.Peek()[name] = value;

        public DslValue? Get(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out var value))
                    return value;
            }
            return null;
        }

        /// <summary>Get all variables visible in the current environment (from all scopes).</summary>
        public Dictionary<string, DslValue> GetAllVisible()
        {
            var result = new Dictionary<string, DslValue>();
            // Iterate scopes from innermost to outermost
            var scopeList = _scopes.ToList();
            for (int i = scopeList.Count - 1; i >= 0; i--)
            {
                foreach (var (key, value) in scopeList[i])
                {
                    if (!result.ContainsKey(key))
                        result[key] = value;
                }
            }
            return result;
        }
    }

    public class LazyBinding
    {
        private readonly Func<DslValue> _evaluate;
        private DslValue? _cached;
        private bool _evaluated;
        private bool _evaluating;

        public LazyBinding(Func<DslValue> evaluate)
        {
            _evaluate = evaluate;
        }

        public DslValue Value
        {
            get
            {
                if (_evaluated) return _cached!;
                if (_evaluating)
                    throw new ExecutionException("Circular dependency in where-clause bindings");
                _evaluating = true;
                _cached = _evaluate();
                _evaluated = true;
                _evaluating = false;
                return _cached!;
            }
        }
    }

    internal class LazyValue : DslValue
    {
        public LazyBinding Binding { get; }
        public LazyValue(LazyBinding binding) { Binding = binding; }
        public override DslType GetDslType() =>
            throw new ExecutionException("LazyValue accessed before resolution");
    }

    public class Executor
    {
        private readonly Dictionary<string, FunctionDef> _functions = new();
        private readonly Dictionary<string, SignatureDef> _signatures = new();
        private int _recursionDepth;
        private readonly int _maxRecursionDepth;

        public Executor(ProgramNode program, int maxRecursionDepth = 10000)
        {
            _maxRecursionDepth = maxRecursionDepth;
            foreach (var def in program.Definitions)
            {
                switch (def)
                {
                    case FunctionDef fd:
                        _functions[fd.Name] = fd;
                        if (fd.Signature != null) _signatures[fd.Name] = fd.Signature;
                        break;
                    case SignatureDef sd:
                        _signatures[sd.Name] = sd;
                        break;
                }
            }
        }

        public DslValue ExecuteFunction(string name, Dictionary<string, DslValue> inputs)
        {
            if (!_functions.TryGetValue(name, out var func))
                throw new ExecutionException($"Function '{name}' not found");
            return CallFunction(func, inputs);
        }

        private DslValue CallFunction(FunctionDef func, Dictionary<string, DslValue> namedArgs)
        {
            _recursionDepth++;
            if (_recursionDepth > _maxRecursionDepth)
                throw new ExecutionException($"Recursion depth exceeded (max: {_maxRecursionDepth})");
            try
            {
                var env = new Environment();
                for (int i = 0; i < func.ImplParams.Count; i++)
                {
                    var p = func.ImplParams[i];
                    if (namedArgs.TryGetValue(p, out var v))
                        env.Set(p, v);
                    else
                    {
                        var dv = GetDefaultValue(func.Name, p);
                        if (dv != null) env.Set(p, dv);
                    }
                }
                // Extract return variable names from signature
                // First try signature attached to function; if not, look up in signatures dict
                var sig = func.Signature;
                if (sig == null && _signatures.TryGetValue(func.Name, out var lookupSig))
                    sig = lookupSig;
                var returnVarNames = sig?.Outputs?.Select(o => o.Name).ToList() ?? new List<string>();
                return ExecuteBody(func.Body, env, returnVarNames);
            }
            finally { _recursionDepth--; }
        }

        private DslValue? GetDefaultValue(string funcName, string paramName)
        {
            if (!_signatures.TryGetValue(funcName, out var sig)) return null;
            foreach (var p in sig.Inputs)
                if (p.Name == paramName && p.DefaultValue != null)
                    return Eval(p.DefaultValue, new Environment());
            if (sig.CopyFrom != null && _signatures.TryGetValue(sig.CopyFrom, out var src))
                foreach (var p in src.Inputs)
                    if (p.Name == paramName && p.DefaultValue != null)
                        return Eval(p.DefaultValue, new Environment());
            return null;
        }

        private ExecutionException ThrowAtExpr(string message, Expression expr)
        {
            return new ExecutionException(message, expr.Line, expr.Column);
        }

        private DslValue ExecuteBody(FunctionBody body, Environment env, List<string> returnVarNames)
        {
            //Console.Error.WriteLine($"[DEBUG] ExecuteBody START - env visible vars: {string.Join(", ", env.GetAllVisible().Keys)}");

            // Create a restricted environment for where clause evaluation
            // It can only see: function parameters (not return variables or body assignments)
            var whereEnv = new Environment();

            // Copy parameters into whereEnv
            var allVars = env.GetAllVisible();
            foreach (var (name, value) in allVars)
            {
                whereEnv.Set(name, value);
            }

            // Set up where-clause bindings in the where environment
            // These can reference parameters and other where bindings, but NOT return variables
            if (body.WhereBindings != null)
            {
                //Console.Error.WriteLine($"[DEBUG] Where-bindings count: {body.WhereBindings.Count}");
                foreach (var b in body.WhereBindings)
                {
                    //Console.Error.WriteLine($"[DEBUG] Setting up where-binding with targets: {string.Join(", ", b.Names)}");

                    // prepare LazyBinding -- will be used 
                    // as binding for single target,
                    // or as a shared binding for multi-target cases
                    var groupBinding = new LazyBinding(() => 
                    {
                        //Console.Error.WriteLine($"[DEBUG] Evaluating binding for RHS: {string.Join(", ", b.Names)}");
                        //Console.Error.WriteLine($"[DEBUG] whereEnv visible vars: {string.Join(", ", whereEnv.GetAllVisible().Keys)}");
                        var result = Eval(b.Value, whereEnv);
                        return result;
                    });

                    if (b.Names.Count == 1)
                    {
                        // Single-target binding: standard lazy evaluation
                        whereEnv.Set(b.Names[0], new LazyValue(groupBinding));
                    }
                    else
                    {
                        // Multi-target binding: need 
                        // * one binding per target to capture the target index, plus
                        // * the group binding to take care of evaluation and caching
                        for (int i = 0; i < b.Names.Count; i++)
                        {
                            int idx = i;  // Closure capture to prevent loop variable capture
                            var targetName = b.Names[i];

                            whereEnv.Set(targetName, new LazyValue(new LazyBinding(() =>
                            {
                                // Access the shared cached result and extract position
                                var obj = groupBinding.Value;  // Triggers single evaluation
                                if (obj is ObjectValue ov)
                                    return ov.Fields.Values.ToList()[idx];

                                throw new ExecutionException($"Multi-target binding: cannot extract value at index {idx}");
                            })));
                        }
                    }
                }
            }

            // Copy where bindings into the main environment for body statement use
            // Keep as lazy values - they will be resolved on first access via ResolveVar
            foreach (var wb in (body.WhereBindings ?? new List<LetBinding>()))
            {
                foreach (var name in wb.Names)
                {
                    var whereBinding = whereEnv.Get(name);
                    if (whereBinding != null)
                        env.Set(name, whereBinding);
                    else
                        throw new ExecutionException($"'{name}' NOT FOUND in whereEnv (internal error)");
                }
            }

            var outputFields = new Dictionary<string, DslValue>();
            DslValue? lastResult = null;
            var assignedReturnVars = new HashSet<string>();
            var returnVarSet = new HashSet<string>(returnVarNames);

            // Execute statements - return variables become accessible after assignment
            foreach (var stmt in body.Statements)
            {
                if (stmt is AssignmentStmt asgn)
                {
                    var value = Eval(asgn.Value, env);
                    if (asgn.Targets.Count == 0)
                        lastResult = value;
                    else if (asgn.Targets.Count == 1)
                    {
                        var target = asgn.Targets[0];
                        env.Set(target, value);
                        outputFields[target] = value;
                        assignedReturnVars.Add(target);
                        lastResult = value;
                    }
                    else
                    {
                        if (value is ObjectValue ov)
                        {
                            // Multi-target assignment: extract values in key order from SortedDictionary
                            // (keys sorted alphabetically, matching the alphabetically-sorted return values in TCL)
                            if (returnVarSet.Count == asgn.Targets.Count)
                            {
                                var values = ov.Fields.Values.ToList();
                                if (values.Count != asgn.Targets.Count)
                                    throw new ExecutionException($"Multi-target assignment: expected {asgn.Targets.Count} values, got {values.Count}");

                                for (int i = 0; i < asgn.Targets.Count; i++)
                                {
                                    var targetName = asgn.Targets[i];
                                    var fv = values[i];
                                    env.Set(targetName, fv);
                                    outputFields[targetName] = fv;
                                    assignedReturnVars.Add(targetName);
                                }
                                lastResult = value;
                            }
                            else
                                throw new ExecutionException($"Multi-target assignment count mismatch: expected {asgn.Targets.Count} returns, but function signature declares {returnVarSet.Count}");
                        }
                        else
                            throw new ExecutionException("Multi-target assignment requires object result");
                    }
                }
                else if (stmt is ConditionalChainStmt ccs)
                {
                    // Handle conditional chain (expr if cond | expr if cond | expr)
                    DslValue result = EvalConditionalChain(ccs, env);
                    lastResult = result;
                }
            }

            // Validate and process return variables
            if (returnVarSet.Count > 0)
            {
                // Special case: single return variable with no explicit assignment uses lastResult
                if (returnVarSet.Count == 1 && assignedReturnVars.Count == 0)
                {
                    if (lastResult != null)
                        return lastResult;  // Return the value directly, NOT wrapped
                    else
                        throw new ExecutionException($"Return variable '{returnVarSet.First()}' was not assigned");
                }
                // Multiple return variables with no explicit assignment: check if lastResult matches
                else if (returnVarSet.Count > 1 && assignedReturnVars.Count == 0)
                {
                    if (lastResult is ObjectValue resultObj)
                    {
                        // Extract string keys from result
                        var resultKeyStrings = new HashSet<string>();
                        foreach (var k in resultObj.Fields.Keys)
                        {
                            if (k is string str)
                                resultKeyStrings.Add(str);
                        }

                        // If result has exactly the right keys (as strings), return it as-is
                        if (returnVarSet.SetEquals(resultKeyStrings))
                        {
                            return resultObj;  // Return the conditional result directly
                        }
                        else
                        {
                            // Doesn't match - this is an error
                            throw new ExecutionException(
                                $"Return variables not assigned: expected {{{string.Join(", ", returnVarSet)}}}, " +
                                $"but conditional returned keys {{{string.Join(", ", resultKeyStrings)}}}");
                        }
                    }
                    else
                    {
                        // lastResult is null or not an object - error
                        throw new ExecutionException(
                            $"Return variables not assigned: expected {{{string.Join(", ", returnVarSet)}}}, " +
                            $"but conditional returned {lastResult?.GetDslType().TypeName ?? "null"}");
                    }
                }
                else
                {
                    // Multiple returns or explicit assignment: all must be assigned
                    var unassigned = returnVarSet.Except(assignedReturnVars).ToList();
                    if (unassigned.Count > 0)
                        throw new ExecutionException($"Return variable(s) not assigned: {string.Join(", ", unassigned)}");

                    // Return output fields as an object (only for explicit multi-return assignments)
                    if (outputFields.Count > 0)
                    {
                        var r = new ObjectValue();
                        foreach (var (k, v) in outputFields) r.Fields[k] = v;
                        return r;
                    }
                }
            }
            else if (outputFields.Count > 0)
            {
                // No return signature, but assignments were made - return them
                var r = new ObjectValue();
                foreach (var (k, v) in outputFields) r.Fields[k] = v;
                return r;
            }

            // No explicit return variables - return lastResult if available
            return lastResult ?? new ObjectValue();
        }

        private DslValue EvalConditionalChain(ConditionalChainStmt ccs, Environment env)
        {
            foreach (var (condition, value) in ccs.Branches)
            {
                if (IsTruthy(Eval(condition, env)))
                    return Eval(value, env);
            }
            if (ccs.DefaultValue != null)
                return Eval(ccs.DefaultValue, env);
            throw new ExecutionException("Conditional chain: no branch matched and no default provided");
        }

        private DslValue Eval(Expression expr, Environment env)
        {
            try
            {
                return expr switch
                {
                    IntLiteral il => new NumericValue(il.Value, "int"),
                    RealLiteral rl => new NumericValue(rl.Value, "real"),
                    StringLiteral sl => new StringValue(sl.Value),
                    BoolLiteral bl => new BoolValue(bl.Value),
                    IdentifierExpr id => ResolveVar(id.Name, env),
                    UnaryOpExpr uo => EvalUnary(uo, env),
                    BinaryOpExpr bo => EvalBinary(bo, env),
                    FunctionCallExpr fc => EvalFunctionCall(fc, env),
                    IndexExpr idx => EvalIndex(idx, env),
                    ObjectLiteral ol => EvalObjectLiteral(ol, env),
                    ConditionalExpr ce => EvalConditional(ce, env),
                    WhereExpr we => EvalWhere(we, env),
                    RangeExpr re => EvalRange(re, env),
                    _ => throw new ExecutionException($"Unknown expression: {expr.GetType().Name}")
                };
            }
            catch (ExecutionException ex) when (ex.Line == 0)
            {
                // Re-throw with location info if not already present
                throw new ExecutionException(ex.Message.Replace(" at line 0, col 0", ""), expr.Line, expr.Column);
            }
        }

        private DslValue ResolveVar(string name, Environment env)
        {
            var v = env.Get(name);
            if (v is LazyValue lv)
            {
                var r = lv.Binding.Value;
                env.Set(name, r);
                return r;
            }
            if (v != null) return v;
            throw new ExecutionException($"Undefined variable: '{name}'");
        }

        private DslValue EvalUnary(UnaryOpExpr op, Environment env)
        {
            if (op.Operator == "?") return CheckKeyExistence(op.Operand, env);
            var operand = Eval(op.Operand, env);
            return op.Operator switch
            {
                "-" => Negate(operand),
                "+" => UnaryPlus(operand),
                "#" => Cardinality(operand),
                _ => throw new ExecutionException($"Unknown unary operator: {op.Operator}")
            };
        }

        private DslValue Negate(DslValue v)
        {
            if (v is NumericValue nv) return new NumericValue(-nv.Value, nv.NumericType);
            if (v is BoolValue bv) return new NumericValue(bv.Value ? -1 : 0, "int");
            if (v is ObjectValue ov)
            {
                var r = new ObjectValue();
                foreach (var (k, val) in ov.Fields) r.Fields[k] = Negate(val);
                return r;
            }
            throw new ExecutionException("Cannot negate non-numeric value");
        }

        private DslValue UnaryPlus(DslValue v)
        {
            if (v is NumericValue || v is BoolValue) return v;
            if (v is ObjectValue ov)
            {
                var r = new ObjectValue();
                foreach (var (k, val) in ov.Fields) r.Fields[k] = UnaryPlus(val);
                return r;
            }
            return v;
        }

        private DslValue Cardinality(DslValue v)
        {
            if (v is ObjectValue ov) return new NumericValue(ov.Fields.Count, "int");
            throw new ExecutionException("# only applies to objects");
        }

        private DslValue CheckKeyExistence(Expression operand, Environment env)
        {
            if (operand is IndexExpr idx)
            {
                var obj = Eval(idx.Object, env);
                var index = Eval(idx.Index, env);
                if (obj is ObjectValue ov)
                    return new BoolValue(ov.Fields.ContainsKey(DslValueToKey(index)));
                return new BoolValue(false);
            }
            try
            {
                var val = Eval(operand, env);
                return new BoolValue(val is not ObjectValue ov2 || ov2.Fields.Count > 0);
            }
            catch
            {
                return new BoolValue(false);
            }
        }

        private DslValue EvalBinary(BinaryOpExpr op, Environment env)
        {
            var left = Eval(op.Left, env);
            var right = Eval(op.Right, env);

            if (left is NumericValue ln && right is NumericValue rn)
                return op.Operator switch
                {
                    "+" => new NumericValue(ln.Value + rn.Value, CombineType(ln, rn)),
                    "-" => new NumericValue(ln.Value - rn.Value, CombineType(ln, rn)),
                    "*" => new NumericValue(ln.Value * rn.Value, CombineType(ln, rn)),
                    "/" => rn.Value == 0
                        ? throw new ExecutionException("Division by zero")
                        : new NumericValue(ln.Value / rn.Value, "real"),
                    "<" => new BoolValue(ln.Value < rn.Value),
                    ">" => new BoolValue(ln.Value > rn.Value),
                    "<=" => new BoolValue(ln.Value <= rn.Value),
                    ">=" => new BoolValue(ln.Value >= rn.Value),
                    "=" => new BoolValue(ln.Value == rn.Value),
                    _ => throw new ExecutionException($"Unknown operator: {op.Operator}")
                };

            if (left is BoolValue lb && right is BoolValue rb)
                return op.Operator switch
                {
                    "=" => new BoolValue(lb.Value == rb.Value),
                    _ => throw new ExecutionException($"Cannot apply '{op.Operator}' to bools")
                };

            if (left is ObjectValue lo && right is ObjectValue ro)
                return op.Operator switch
                {
                    "<" => new BoolValue(ObjectKeyComparer.CompareValues(lo, ro) < 0),
                    ">" => new BoolValue(ObjectKeyComparer.CompareValues(lo, ro) > 0),
                    "<=" => new BoolValue(ObjectKeyComparer.CompareValues(lo, ro) <= 0),
                    ">=" => new BoolValue(ObjectKeyComparer.CompareValues(lo, ro) >= 0),
                    "=" => new BoolValue(ObjectKeyComparer.CompareValues(lo, ro) == 0),
                    _ => throw new ExecutionException($"Cannot apply '{op.Operator}' to objects")
                };

            throw new ExecutionException(
                $"Cannot apply '{op.Operator}' to {left.GetDslType().TypeName} and {right.GetDslType().TypeName}");
        }

        private static string CombineType(NumericValue a, NumericValue b)
        {
            if (a.NumericType == "real" || b.NumericType == "real") return "real";
            if (a.NumericType == "int" || b.NumericType == "int") return "int";
            return "num";
        }

        private DslValue EvalFunctionCall(FunctionCallExpr fc, Environment env)
        {
            switch (fc.Name)
            {
                case "map": return EvalMap(fc.Arguments, env);
                case "filter": return EvalFilter(fc.Arguments, env);
                case "keys": return EvalKeys(fc.Arguments, env);
                case "first": return EvalFirst(fc.Arguments, env);
                case "count": return EvalCount(fc.Arguments, env);
                case "sum": return EvalSum(fc.Arguments, env);
                case "merge": return EvalMerge(fc.Arguments, env);
                case "concat": return EvalConcat(fc.Arguments, env);
                case "replace": return EvalReplace(fc.Arguments, env);
                case "arg_min": return EvalArgMin(fc.Arguments, env);
            }

            if (_functions.TryGetValue(fc.Name, out var func))
            {
                var plain = new List<DslValue>();
                foreach (var a in fc.Arguments)
                {
                    if (a.Iterator != null)
                        throw new ExecutionException(
                            $"Iterator not supported in user function call '{fc.Name}'");
                    plain.Add(Eval(a.Value, env));
                }
                var na = new Dictionary<string, DslValue>();
                for (int i = 0; i < func.ImplParams.Count && i < plain.Count; i++)
                    na[func.ImplParams[i]] = plain[i];
                return CallFunction(func, na);
            }
            throw new ExecutionException($"Unknown function: '{fc.Name}'");
        }

        private DslValue EvalMap(List<CallArg> args, Environment env)
        {
            var it = args.FirstOrDefault(a => a.Iterator != null);
            if (it == null) throw new ExecutionException("map() requires an iterator clause");
            var coll = Eval(it.Iterator!.Collection, env);
            if (coll is not ObjectValue co)
                throw new ExecutionException("map() collection must be an object");
            var vars = it.Iterator.Variables;
            var ea = args.Where(a => a.Iterator == null).ToList();
            if (ea.Count != 1)
                throw new ExecutionException("map() expects one expression argument");
            var body = ea[0].Value;
            var result = new ObjectValue();
            foreach (var (k, v) in co.Fields)
            {
                env.PushScope();
                BindIter(env, vars, k, v);
                result.Fields[k] = Eval(body, env);
                env.PopScope();
            }
            return result;
        }

        private DslValue EvalFilter(List<CallArg> args, Environment env)
        {
            var it = args.FirstOrDefault(a => a.Iterator != null);
            if (it == null) throw new ExecutionException("filter() requires an iterator clause");
            var coll = Eval(it.Iterator!.Collection, env);
            if (coll is not ObjectValue co)
                throw new ExecutionException("filter() collection must be an object");
            var vars = it.Iterator.Variables;
            var ea = args.Where(a => a.Iterator == null).ToList();
            if (ea.Count != 1)
                throw new ExecutionException("filter() expects one predicate argument");
            var pred = ea[0].Value;
            var result = new ObjectValue();
            foreach (var (k, v) in co.Fields)
            {
                env.PushScope();
                BindIter(env, vars, k, v);
                if (IsTruthy(Eval(pred, env))) result.Fields[k] = v;
                env.PopScope();
            }
            return result;
        }

        private DslValue EvalKeys(List<CallArg> args, Environment env)
        {
            if (args.Count != 1) throw new ExecutionException("keys() expects one argument");
            var c = Eval(args[0].Value, env);
            if (c is not ObjectValue ov) throw new ExecutionException("keys() requires an object");
            var r = new ObjectValue();
            foreach (var k in ov.Fields.Keys) r.Fields[k] = KeyToDsl(k);
            return r;
        }

        private DslValue EvalFirst(List<CallArg> args, Environment env)
        {
            if (args.Count != 1) throw new ExecutionException("first() expects one argument");
            var c = Eval(args[0].Value, env);
            if (c is not ObjectValue ov) throw new ExecutionException("first() requires an object");
            if (ov.Fields.Count == 0) throw new ExecutionException("first() on empty object");
            return ov.Fields.First().Value;
        }

        private DslValue EvalCount(List<CallArg> args, Environment env)
        {
            var it = args.FirstOrDefault(a => a.Iterator != null);
            if (it != null)
            {
                var coll = Eval(it.Iterator!.Collection, env);
                if (coll is not ObjectValue co)
                    throw new ExecutionException("count() collection must be object");
                var vars = it.Iterator.Variables;
                var ea = args.Where(a => a.Iterator == null).ToList();
                int cnt = 0;
                foreach (var (k, v) in co.Fields)
                {
                    env.PushScope();
                    BindIter(env, vars, k, v);
                    if (ea.Count > 0) { if (IsTruthy(Eval(ea[0].Value, env))) cnt++; }
                    else { if (IsTruthy(v)) cnt++; }
                    env.PopScope();
                }
                return new NumericValue(cnt, "int");
            }
            if (args.Count != 1) throw new ExecutionException("count() expects one argument");
            var c = Eval(args[0].Value, env);
            if (c is ObjectValue ov)
            {
                int cnt2 = 0;
                foreach (var v in ov.Fields.Values) if (IsTruthy(v)) cnt2++;
                return new NumericValue(cnt2, "int");
            }
            throw new ExecutionException("count() requires an object");
        }

        private DslValue EvalSum(List<CallArg> args, Environment env)
        {
            var it = args.FirstOrDefault(a => a.Iterator != null);
            if (it != null)
            {
                var coll = Eval(it.Iterator!.Collection, env);
                if (coll is not ObjectValue co)
                    throw new ExecutionException("sum() collection must be object");
                var vars = it.Iterator.Variables;
                var ea = args.Where(a => a.Iterator == null).ToList();
                decimal tot = 0;
                foreach (var (k, v) in co.Fields)
                {
                    env.PushScope();
                    BindIter(env, vars, k, v);
                    var val = ea.Count > 0 ? Eval(ea[0].Value, env) : v;
                    if (val is NumericValue nv) tot += nv.Value;
                    else throw new ExecutionException("sum() requires numeric values");
                    env.PopScope();
                }
                return new NumericValue(tot, "real");
            }
            if (args.Count != 1) throw new ExecutionException("sum() expects one argument");
            var c = Eval(args[0].Value, env);
            if (c is not ObjectValue ov) throw new ExecutionException("sum() requires an object");
            decimal t = 0;
            foreach (var v in ov.Fields.Values)
            {
                if (v is NumericValue nv) t += nv.Value;
                else throw new ExecutionException("sum() requires numeric values");
            }
            return new NumericValue(t, "real");
        }

        private DslValue EvalMerge(List<CallArg> args, Environment env) =>
            EvalAggregation(args, env, MergeInto);

        private void MergeInto(ObjectValue target, DslValue source)
        {
            if (source is not ObjectValue s)
                throw new ExecutionException("merge() needs objects");
            foreach (var (k, v) in s.Fields)
            {
                if (target.Fields.TryGetValue(k, out var ex))
                {
                    if (ex is ObjectValue eo && v is ObjectValue vo)
                        MergeInto(eo, vo);
                    else
                        throw new ExecutionException($"merge() conflict on key: {k}");
                }
                else target.Fields[k] = v;
            }
        }

        private DslValue EvalConcat(List<CallArg> args, Environment env) =>
            EvalAggregation(args, env, ConcatInto);

        private void ConcatInto(ObjectValue target, DslValue source)
        {
            if (source is not ObjectValue s)
                throw new ExecutionException("concat() needs objects");
            foreach (var (k, v) in s.Fields)
            {
                if (target.Fields.ContainsKey(k))
                    throw new ExecutionException($"concat() key conflict: {k}");
                target.Fields[k] = v;
            }
        }

        private DslValue EvalReplace(List<CallArg> args, Environment env) =>
            EvalAggregation(args, env, ReplaceInto);

        private void ReplaceInto(ObjectValue target, DslValue source)
        {
            if (source is not ObjectValue s)
                throw new ExecutionException("replace() needs objects");
            foreach (var (k, v) in s.Fields) target.Fields[k] = v;
        }

        private DslValue EvalAggregation(List<CallArg> args, Environment env,
            Action<ObjectValue, DslValue> combine)
        {
            var it = args.FirstOrDefault(a => a.Iterator != null);
            if (it != null)
            {
                var coll = Eval(it.Iterator!.Collection, env);
                if (coll is not ObjectValue co)
                    throw new ExecutionException("Aggregation collection must be object");
                var vars = it.Iterator.Variables;
                var ea = args.Where(a => a.Iterator == null).ToList();
                var result = new ObjectValue();
                foreach (var (k, v) in co.Fields)
                {
                    env.PushScope();
                    BindIter(env, vars, k, v);
                    combine(result, ea.Count > 0 ? Eval(ea[0].Value, env) : v);
                    env.PopScope();
                }
                return result;
            }
            var r = new ObjectValue();
            foreach (var a in args) combine(r, Eval(a.Value, env));
            return r;
        }

        private DslValue EvalArgMin(List<CallArg> args, Environment env)
        {
            if (args.Count != 1) throw new ExecutionException("arg_min() expects one argument");
            var c = Eval(args[0].Value, env);
            if (c is not ObjectValue ov) throw new ExecutionException("arg_min() requires an object");
            if (ov.Fields.Count == 0) return new ObjectValue();
            DslValue? min = null;
            foreach (var v in ov.Fields.Values)
                if (min == null || ObjectKeyComparer.CompareValues(v, min) < 0)
                    min = v;
            var r = new ObjectValue();
            foreach (var (k, v) in ov.Fields)
                if (ObjectKeyComparer.CompareValues(v, min!) == 0)
                    r.Fields[k] = KeyToDsl(k);
            return r;
        }

        private DslValue EvalIndex(IndexExpr idx, Environment env)
        {
            var obj = Eval(idx.Object, env);
            var index = Eval(idx.Index, env);
            if (obj is not ObjectValue ov)
                throw new ExecutionException(
                    $"Cannot index non-object (got {obj.GetDslType().TypeName})");
            var key = DslValueToKey(index);
            if (ov.Fields.TryGetValue(key, out var val)) return val;
            throw new ExecutionException($"Key '{key}' not found in object");
        }

        private DslValue EvalObjectLiteral(ObjectLiteral ol, Environment env)
        {
            var r = new ObjectValue();
            foreach (var (ke, ve) in ol.Fields)
            {
                var k = DslValueToKey(Eval(ke, env));
                r.Fields[k] = Eval(ve, env);
            }
            return r;
        }

        private DslValue EvalConditional(ConditionalExpr ce, Environment env) =>
            IsTruthy(Eval(ce.Condition, env))
                ? Eval(ce.ThenExpr, env)
                : Eval(ce.ElseExpr, env);

        private DslValue EvalWhere(WhereExpr we, Environment env)
        {
            env.PushScope();

            // Set up inline where bindings
            foreach (var b in we.Bindings)
            {
                if (b.Names.Count == 1)
                {
                    // Single-target binding: standard lazy evaluation
                    env.Set(b.Names[0], new LazyValue(new LazyBinding(() => Eval(b.Value, env))));
                }
                else
                {
                    // Multi-target binding: trigger-all-on-first-access
                    var evaluated = new bool[1] { false };  // Shared state: has group been evaluated?
                    var values = new DslValue[b.Names.Count];  // Shared state: extracted values

                    // Create lazy binding for each target
                    for (int i = 0; i < b.Names.Count; i++)
                    {
                        int idx = i;  // Closure capture to prevent loop variable capture
                        var targetName = b.Names[i];

                        env.Set(targetName, new LazyValue(new LazyBinding(() =>
                        {
                            // If not yet evaluated, evaluate RHS once and extract all values
                            if (!evaluated[0])
                            {
                                var result = Eval(b.Value, env);
                                if (result is ObjectValue ov)
                                {
                                    var extractedValues = ov.Fields.Values.ToList();
                                    for (int j = 0; j < b.Names.Count && j < extractedValues.Count; j++)
                                    {
                                        values[j] = extractedValues[j];
                                    }
                                }
                                evaluated[0] = true;
                            }

                            // Return value for this target
                            if (idx < values.Length)
                                return values[idx];

                            throw new ExecutionException($"Multi-target binding: cannot extract value at index {idx}");
                        })));
                    }
                }
            }
            var r = Eval(we.MainExpr, env);
            env.PopScope();
            return r;
        }

        private DslValue EvalRange(RangeExpr re, Environment env)
        {
            var s = Eval(re.Start, env);
            var e = Eval(re.End, env);
            if (s is not NumericValue sn || e is not NumericValue en)
                throw new ExecutionException("Range requires numeric bounds");
            var r = new ObjectValue();
            for (long i = (long)sn.Value; i <= (long)en.Value; i++)
                r.Fields[i] = new NumericValue(i, "int");
            return r;
        }

        private void BindIter(Environment env, List<string> vars,
            object key, DslValue value)
        {
            if (vars.Count == 1) env.Set(vars[0], value);
            else if (vars.Count >= 2)
            {
                env.Set(vars[0], KeyToDsl(key));
                env.Set(vars[1], value);
            }
        }

        private static object DslValueToKey(DslValue v) => v switch
        {
            NumericValue nv => nv.Value % 1 == 0
                ? (object)(long)nv.Value : (object)nv.Value,
            StringValue sv => sv.Value,
            BoolValue bv => bv.Value ? 1L : 0L,
            ObjectValue ov => ov,
            _ => throw new ExecutionException(
                $"Cannot use as key: {v.GetDslType().TypeName}")
        };

        private static DslValue KeyToDsl(object k) => k switch
        {
            long l => new NumericValue(l, "int"),
            int i => new NumericValue(i, "int"),
            decimal d => new NumericValue(d, "real"),
            string s => new StringValue(s),
            ObjectValue ov => ov,
            _ => throw new ExecutionException(
                $"Unexpected key type: {k.GetType().Name}")
        };

        private static bool IsTruthy(DslValue v) => v switch
        {
            BoolValue bv => bv.Value,
            NumericValue nv => nv.Value != 0,
            ObjectValue ov => ov.Fields.Count > 0,
            StringValue sv => !string.IsNullOrEmpty(sv.Value),
            _ => true
        };
    }
}
