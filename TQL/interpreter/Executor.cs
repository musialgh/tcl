using System.Globalization;

namespace TqlInterpreter;

/// <summary>Tree-walking interpreter for TQL.</summary>
public class Executor
{
    private readonly GlobalScope      _global;
    private readonly string           _scriptDir;
    private readonly HashSet<string>  _importedFiles = new(StringComparer.OrdinalIgnoreCase);

    public Executor(GlobalScope global, string scriptDir)
    {
        _global    = global;
        _scriptDir = scriptDir;
    }

    // ── file execution ────────────────────────────────────────────────────────

    /// <summary>Load, parse and execute a TQL file in global context.</summary>
    public void ExecuteFile(string path)
    {
        path = Path.IsPathRooted(path) ? path : Path.Combine(_scriptDir, path);
        path = Path.GetFullPath(path);
        if (!_importedFiles.Add(path)) return; // prevent re-import

        string src = File.ReadAllText(path);
        var tokens  = new Lexer(src, path).Tokenize();
        var dirs    = new Parser(tokens).ParseProgram();
        var scope   = new LocalScope(_global) { IsGlobal = true };
        ExecBlock(dirs, scope);
    }

    // ── block / directive execution ───────────────────────────────────────────

    public void ExecBlock(List<DirectiveNode> directives, LocalScope scope)
    {
        foreach (var d in directives) ExecDirective(d, scope);
    }

    private void ExecDirective(DirectiveNode d, LocalScope scope)
    {
        switch (d)
        {
            case ImportDirective id:
                var importPath = Path.Combine(_scriptDir, id.Path);
                ExecuteFile(importPath);
                break;

            case FunctionDef fd:
                _global.Functions[fd.Name] = fd;
                break;

            case GlobalDeclDirective gdd:
                foreach (var name in gdd.Names)
                    scope.ImportGlobal(name, gdd.Line, gdd.Col);
                break;

            case GlobalAssignDirective gad:
                scope.WriteGlobal(gad.Name, Eval(gad.Value, scope));
                break;

            case LetDirective ld:
                scope.Bind(ld.Name, Eval(ld.Value, scope));
                break;

            case CallDirective cd:
                InvokeFunction(cd.FuncName, cd.Args, scope, cd.Line, cd.Col);
                break;

            case ReturnDirective rd:
                throw new ReturnSignal(rd.Value is null ? TqlValue.Null : Eval(rd.Value, scope));

            case AbortDirective ad:
                throw new TqlAbortException(Eval(ad.Message, scope).AsText());

            case ExprDirective ed:
                Eval(ed.Expr, scope); // result discarded
                break;

            case ForRangeDirective frd:
                ExecForRange(frd, scope);
                break;

            case ForInDirective fid:
                ExecForIn(fid, scope);
                break;

            case ForListDirective fld:
                ExecForList(fld, scope);
                break;

            case WhileDirective wd:
                ExecWhile(wd, scope);
                break;

            case BreakDirective:
                throw new BreakSignal();

            case ContinueDirective:
                throw new ContinueSignal();

            case IfDirective ifd:
                ExecIf(ifd, scope);
                break;

            case InsertIntoDirective iid:
                DoInsertInto(iid.Target, iid.Sources, iid.Using, scope, iid.Line, iid.Col);
                break;

            case UpdateFromDirective ufd:
                DoUpdateFrom(ufd.Target, Eval(ufd.Source, scope), ufd.Using, ufd.Line, ufd.Col);
                break;

            case ConcatToDirective ctd:
                DoConcatTo(ctd.Target, Eval(ctd.Source, scope), ctd.Using, ctd.Line, ctd.Col);
                break;

            case AddColumnDirective acd:
                DoAddColumn(acd, scope);
                break;

            case SaveTableDirective std:
                DoSaveTable(std, scope);
                break;

            case LoadTableIntoDirective ltd:
                DoLoadTableInto(ltd, scope);
                break;

            default:
                throw new TqlRuntimeException($"Unknown directive type {d.GetType().Name}", d.Line, d.Col);
        }
    }

    // ── control flow ──────────────────────────────────────────────────────────

    private void ExecForRange(ForRangeDirective d, LocalScope scope)
    {
        decimal from = Eval(d.From, scope).AsDecimal(d.Line, d.Col);
        decimal to   = Eval(d.To,   scope).AsDecimal(d.Line, d.Col);
        if (d.Desc)
            for (decimal i = from; i >= to; i--)
            { scope.Bind(d.Var, TqlValue.FromDecimal(i)); RunLoop(d.Body, scope); }
        else
            for (decimal i = from; i <= to; i++)
            { scope.Bind(d.Var, TqlValue.FromDecimal(i)); RunLoop(d.Body, scope); }
    }

    private void ExecForIn(ForInDirective d, LocalScope scope)
    {
        var srcVal = Eval(d.Source, scope);
        if (srcVal is not TableVal tv)
            throw new TqlRuntimeException("FOR IN requires a table", d.Line, d.Col);
        // single-column tables or explicit TableColumn source → scalar iteration
        bool scalar = d.Source is TableColumn || tv.T.ColumnCount == 1;
        foreach (var (rid, cells) in tv.T.GetRowsInOrder().ToList())
        {
            scope.Bind(d.Var, scalar ? cells[0] : tv.T.RowValAt(rid));
            RunLoop(d.Body, scope);
        }
    }

    private void ExecForList(ForListDirective d, LocalScope scope)
    {
        foreach (var expr in d.Values)
        {
            scope.Bind(d.Var, Eval(expr, scope));
            RunLoop(d.Body, scope);
        }
    }

    private void RunLoop(List<DirectiveNode> body, LocalScope scope)
    {
        try { ExecBlock(body, scope); }
        catch (ContinueSignal) { }
        catch (BreakSignal)    { throw; } // propagate break to loop
    }

    private void ExecWhile(WhileDirective d, LocalScope scope)
    {
        while (true)
        {
            var cond = Eval(d.Cond, scope);
            if (!AutoScalar(cond).IsTrue) break;
            try { ExecBlock(d.Body, scope); }
            catch (ContinueSignal) { continue; }
            catch (BreakSignal)    { break; }
        }
    }

    private void ExecIf(IfDirective d, LocalScope scope)
    {
        foreach (var branch in d.Branches)
        {
            var cond = Eval(branch.Cond, scope);
            if (AutoScalar(cond).IsTrue)
            { ExecBlock(branch.Body, scope); return; }
        }
        if (d.ElseBody is not null) ExecBlock(d.ElseBody, scope);
    }

    // ── expression evaluation ─────────────────────────────────────────────────

    /// <summary>
    /// Evaluate an expression. srcTable/srcRowId provide a row context used in
    /// WHERE clauses so bare column names resolve to the current row's values.
    /// srcAlias is the optional table alias from the FROM clause.
    /// </summary>
    public TqlValue Eval(ExprNode e, LocalScope scope,
                          Table? srcTable = null, int srcRowId = -1,
                          string? srcAlias = null)
    {
        switch (e)
        {
            case NullLit:    return TqlValue.Null;
            case BoolLit bl: return TqlValue.FromBool(bl.V);
            case NumberLit nl: return TqlValue.FromDecimal(nl.V);
            case StringLit sl: return TqlValue.FromText(sl.V);

            case StringPattern sp:
                return TqlValue.FromText(EvalPattern(sp, scope, srcTable, srcRowId, srcAlias));

            case IdentExpr ie:
                // row context: column name takes priority over variable
                if (srcTable is not null && srcRowId >= 0 && srcTable.HasColumn(ie.Name))
                    return srcTable.GetCell(srcRowId, ie.Name);
                if (scope.TryResolve(ie.Name, out var sv)) return sv;
                throw new TqlRuntimeException($"Undefined '{ie.Name}'", ie.Line, ie.Col);

            case BracketedIdent bi:
                if (srcTable is not null && srcRowId >= 0 && srcTable.HasColumn(bi.Name))
                    return srcTable.GetCell(srcRowId, bi.Name);
                if (scope.TryResolve(bi.Name, out var bv)) return bv;
                throw new TqlRuntimeException($"Undefined '{bi.Name}'", bi.Line, bi.Col);

            case ParenExpr pe:
                return Eval(pe.Inner, scope, srcTable, srcRowId, srcAlias);

            case UnaryOp uo:
                return EvalUnary(uo, scope, srcTable, srcRowId, srcAlias);

            case BinaryOp bo:
                return EvalBinary(bo, scope, srcTable, srcRowId, srcAlias);

            case IsNullExpr ine:
            {
                var v = Eval(ine.Operand, scope, srcTable, srcRowId, srcAlias);
                bool isNull = v.IsNull;
                return TqlValue.FromBool(ine.Negated ? !isNull : isNull);
            }

            case InExpr inEx:
                return EvalIn(inEx, scope, srcTable, srcRowId, srcAlias);

            case ValuesMatchExpr vme:
                return EvalValuesMatch(vme, scope, srcTable, srcRowId, srcAlias);

            case FuncCallExpr fc:
                return EvalFuncCall(fc, scope, srcTable, srcRowId, srcAlias);

            case SelectExpr se:
                return TqlValue.FromTable(EvalSelect(se.S, scope));

            case RowExpr re:
                return TqlValue.FromTable(EvalRow(re, scope, srcTable, srcRowId, srcAlias));

            case TableColumn tc:
                return EvalTableColumn(tc, scope, srcTable, srcRowId, srcAlias);

            case TableIndex ti:
                return EvalTableIndex(ti, scope, srcTable, srcRowId, srcAlias);

            case CreateTableExpr:
                return TqlValue.FromTable(new Table());

            case LoadTableExpr lt:
            {
                string path = Eval(lt.Path, scope).AsText();
                if (!Path.IsPathRooted(path)) path = Path.Combine(_scriptDir, path);
                using var reader = new StreamReader(path);
                var loaded = CsvIO.ReadTable(reader)
                    ?? throw new TqlRuntimeException("LOAD: file is empty", lt.Line, lt.Col);
                return TqlValue.FromTable(loaded);
            }

            case InsertExpr ie_:
                return DoInsertExpr(ie_, scope);

            case UpdateSetExpr use_:
                return DoUpdateSetExpr(use_, scope);

            default:
                throw new TqlRuntimeException($"Unknown expression type {e.GetType().Name}", e.Line, e.Col);
        }
    }

    // ── operators ─────────────────────────────────────────────────────────────

    private TqlValue EvalUnary(UnaryOp uo, LocalScope scope,
                                Table? src, int rid, string? alias)
    {
        var v = Eval(uo.Operand, scope, src, rid, alias);
        return uo.Op switch
        {
            "-"   => TqlValue.FromDecimal(-AutoScalar(v).AsDecimal(uo.Line, uo.Col)),
            "+"   => v,
            "NOT" => TqlValue.FromBool(!AutoScalar(v).IsTrue),
            _     => throw new TqlRuntimeException($"Unknown unary op '{uo.Op}'", uo.Line, uo.Col)
        };
    }

    private TqlValue EvalBinary(BinaryOp bo, LocalScope scope,
                                 Table? src, int rid, string? alias)
    {
        // short-circuit for AND/OR
        if (bo.Op == "AND")
        {
            var l = Eval(bo.L, scope, src, rid, alias);
            if (!AutoScalar(l).IsTrue) return TqlValue.False;
            return TqlValue.FromBool(AutoScalar(Eval(bo.R, scope, src, rid, alias)).IsTrue);
        }
        if (bo.Op == "OR")
        {
            var l = Eval(bo.L, scope, src, rid, alias);
            if (AutoScalar(l).IsTrue) return TqlValue.True;
            return TqlValue.FromBool(AutoScalar(Eval(bo.R, scope, src, rid, alias)).IsTrue);
        }
        if (bo.Op == "XOR")
        {
            bool l = AutoScalar(Eval(bo.L, scope, src, rid, alias)).IsTrue;
            bool r = AutoScalar(Eval(bo.R, scope, src, rid, alias)).IsTrue;
            return TqlValue.FromBool(l != r);
        }

        var lv = AutoScalar(Eval(bo.L, scope, src, rid, alias));
        var rv = AutoScalar(Eval(bo.R, scope, src, rid, alias));
        int cmp = TqlValueComparer.Instance.Compare(lv, rv);

        return bo.Op switch
        {
            "="  => TqlValue.FromBool(cmp == 0),
            "<>" => TqlValue.FromBool(cmp != 0),
            "<"  => TqlValue.FromBool(cmp <  0),
            "<=" => TqlValue.FromBool(cmp <= 0),
            ">"  => TqlValue.FromBool(cmp >  0),
            ">=" => TqlValue.FromBool(cmp >= 0),
            "+"  => NumOp(lv, rv, (a,b) => a+b, bo),
            "-"  => NumOp(lv, rv, (a,b) => a-b, bo),
            "*"  => NumOp(lv, rv, (a,b) => a*b, bo),
            "/"  => rv.IsNull || rv.AsDecimal() == 0m
                    ? TqlValue.Null
                    : NumOp(lv, rv, (a,b) => a/b, bo),
            "%"  => NumOp(lv, rv, (a,b) => a%b, bo),
            _    => throw new TqlRuntimeException($"Unknown operator '{bo.Op}'", bo.Line, bo.Col)
        };
    }

    private static TqlValue NumOp(TqlValue l, TqlValue r,
                                   Func<decimal,decimal,decimal> op, BinaryOp bo)
    {
        if (l.IsNull || r.IsNull) return TqlValue.Null;
        return TqlValue.FromDecimal(op(l.AsDecimal(bo.Line, bo.Col), r.AsDecimal(bo.Line, bo.Col)));
    }

    /// <summary>Apply a binary operator to two already-evaluated values (used by EvalAggExpr).</summary>
    private static TqlValue ApplyBinaryOp(string op, TqlValue lv, TqlValue rv, int line, int col)
    {
        int cmp = TqlValueComparer.Instance.Compare(lv, rv);
        return op switch
        {
            "AND" => TqlValue.FromBool(lv.IsTrue && rv.IsTrue),
            "OR"  => TqlValue.FromBool(lv.IsTrue || rv.IsTrue),
            "XOR" => TqlValue.FromBool(lv.IsTrue != rv.IsTrue),
            "="   => TqlValue.FromBool(cmp == 0),
            "<>"  => TqlValue.FromBool(cmp != 0),
            "<"   => TqlValue.FromBool(cmp <  0),
            "<="  => TqlValue.FromBool(cmp <= 0),
            ">"   => TqlValue.FromBool(cmp >  0),
            ">="  => TqlValue.FromBool(cmp >= 0),
            "+"   => lv.IsNull || rv.IsNull ? TqlValue.Null : TqlValue.FromDecimal(lv.AsDecimal(line, col) + rv.AsDecimal(line, col)),
            "-"   => lv.IsNull || rv.IsNull ? TqlValue.Null : TqlValue.FromDecimal(lv.AsDecimal(line, col) - rv.AsDecimal(line, col)),
            "*"   => lv.IsNull || rv.IsNull ? TqlValue.Null : TqlValue.FromDecimal(lv.AsDecimal(line, col) * rv.AsDecimal(line, col)),
            "/"   => lv.IsNull || rv.IsNull || rv.AsDecimal(line, col) == 0m ? TqlValue.Null
                     : TqlValue.FromDecimal(lv.AsDecimal(line, col) / rv.AsDecimal(line, col)),
            "%"   => lv.IsNull || rv.IsNull ? TqlValue.Null : TqlValue.FromDecimal(lv.AsDecimal(line, col) % rv.AsDecimal(line, col)),
            _     => throw new TqlRuntimeException($"Unknown operator '{op}'", line, col)
        };
    }

    private TqlValue EvalIn(InExpr ie, LocalScope scope,
                             Table? src, int rid, string? alias)
    {
        var lhs = AutoScalar(Eval(ie.Value, scope, src, rid, alias));
        var rhs = Eval(ie.ColExpr, scope); // evaluate without row context
        Table rhsTable = rhs is TableVal tv ? tv.T
            : throw new TqlRuntimeException("IN requires a table on the right side", ie.Line, ie.Col);
        bool found = rhsTable.GetRowsInOrder()
            .Any(r => TqlValueComparer.Instance.Equals(r.Cells[0], lhs));
        return TqlValue.FromBool(found != ie.Negated);
    }

    private TqlValue EvalValuesMatch(ValuesMatchExpr vme, LocalScope scope,
                                      Table? srcTable, int srcRowId, string? alias)
    {
        if (srcTable is null || srcRowId < 0)
            throw new TqlRuntimeException("VALUES MATCH used outside row context", vme.Line, vme.Col);
        var matchVal = Eval(vme.RowExpr, scope);
        IReadOnlyDictionary<string, TqlValue> fields = RowValFrom(matchVal, vme.Line, vme.Col).Fields;
        foreach (var (col, val) in fields)
        {
            if (!srcTable.HasColumn(col)) return TqlValue.False;
            if (!TqlValueComparer.Instance.Equals(srcTable.GetCell(srcRowId, col), val))
                return TqlValue.False;
        }
        return TqlValue.True;
    }

    // ── function calls ────────────────────────────────────────────────────────

    private TqlValue EvalFuncCall(FuncCallExpr fc, LocalScope scope,
                                   Table? src, int rid, string? alias)
    {
        string name = fc.Name.ToUpperInvariant();
        switch (name)
        {
            case "IF":
            {
                if (fc.Args.Count < 3)
                    throw new TqlRuntimeException("IF() needs 3 arguments", fc.Line, fc.Col);
                var cond   = Eval(fc.Args[0].Value, scope, src, rid, alias);
                var thenV  = Eval(fc.Args[1].Value, scope, src, rid, alias);
                var elseV  = Eval(fc.Args[2].Value, scope, src, rid, alias);
                return Builtins.If(AutoScalar(cond), thenV, elseV);
            }
            case "ROWS":
            {
                var arg = Eval(fc.Args[0].Value, scope, src, rid, alias);
                return Builtins.Rows(arg, fc.Line, fc.Col);
            }
            case "GUID":
                return Builtins.Guid();

            // aggregate functions used as scalar (outside SELECT) — operate on an argument
            case "COUNT":
            {
                if (fc.Args.Count == 0) return TqlValue.FromDecimal(0);
                bool star = fc.Args[0].Value is IdentExpr { Name: "*" };
                if (star) return TqlValue.FromDecimal(1); // outside aggregation context: count 1 row
                var v = Eval(fc.Args[0].Value, scope, src, rid, alias);
                return v.IsNull ? TqlValue.False : TqlValue.True;
            }

            default:
                return InvokeFunction(fc.Name, fc.Args, scope, fc.Line, fc.Col);
        }
    }

    /// <summary>Call a user-defined function by name; return its return value.</summary>
    private TqlValue InvokeFunction(string name, List<Arg> args, LocalScope callerScope,
                                     int line, int col)
    {
        if (!_global.Functions.TryGetValue(name, out var fd))
            throw new TqlRuntimeException($"Undefined function '{name}'", line, col);

        var funcScope = new LocalScope(_global);
        BindParams(fd.Params, args, callerScope, funcScope, line, col);
        try { ExecBlock(fd.Body, funcScope); return TqlValue.Null; }
        catch (ReturnSignal rs) { return rs.Value; }
    }

    private void BindParams(List<Param> parms, List<Arg> args,
                             LocalScope callerScope, LocalScope funcScope, int line, int col)
    {
        var keyArgs = args
            .Where(a => a.AsName is not null)
            .ToDictionary(a => a.AsName!, a => Eval(a.Value, callerScope), StringComparer.Ordinal);
        var posArgs = args.Where(a => a.AsName is null).ToList();
        int posIdx = 0;
        foreach (var param in parms)
        {
            if (keyArgs.TryGetValue(param.Name, out var kv))
                funcScope.Bind(param.Name, kv);
            else if (posIdx < posArgs.Count)
                funcScope.Bind(param.Name, Eval(posArgs[posIdx++].Value, callerScope));
            else if (param.Default is not null)
                funcScope.Bind(param.Name, Eval(param.Default, funcScope));
            else
                throw new TqlRuntimeException($"Missing argument '{param.Name}'", line, col);
        }
    }

    // ── table column / index access ───────────────────────────────────────────

    private TqlValue EvalTableColumn(TableColumn tc, LocalScope scope,
                                      Table? srcTable, int srcRowId, string? alias)
    {
        var tableVal = Eval(tc.TableExpr, scope); // no row context for the table itself
        if (tableVal is RowVal rv)
            return rv.Get(tc.ColName);
        if (tableVal is TableVal tv)
        {
            // if this table IS the current source table, return cell value from current row
            if (srcTable is not null && srcRowId >= 0 && ReferenceEquals(tv.T, srcTable))
                return srcTable.HasColumn(tc.ColName)
                    ? srcTable.GetCell(srcRowId, tc.ColName)
                    : TqlValue.Null;
            // otherwise, return a single-column projection
            return TqlValue.FromTable(tv.T.SelectColumn(tc.ColName));
        }
        throw new TqlRuntimeException($"Cannot access column '{tc.ColName}' on a non-table value", tc.Line, tc.Col);
    }

    private TqlValue EvalTableIndex(TableIndex ti, LocalScope scope,
                                     Table? srcTable, int srcRowId, string? alias)
    {
        var tableVal = Eval(ti.TableExpr, scope, srcTable, srcRowId, alias);
        var idxVal   = AutoScalar(Eval(ti.Index, scope, srcTable, srcRowId, alias));
        if (tableVal is not TableVal tv)
            throw new TqlRuntimeException("Table index requires a table", ti.Line, ti.Col);
        int n = (int)idxVal.AsDecimal(ti.Line, ti.Col);
        if (n == 0)
        {
            // column names as RowVal
            var d = new Dictionary<string, TqlValue>(StringComparer.Ordinal);
            foreach (var col in tv.T.ColumnNames)
                d[col] = TqlValue.FromText(col);
            return new RowVal(d);
        }
        int rowNum = 0;
        foreach (var (rid, cells) in tv.T.GetRowsInOrder())
        {
            if (++rowNum == n) return tv.T.RowValAt(rid);
        }
        return TqlValue.Null;
    }

    // ── ROW expression ────────────────────────────────────────────────────────

    private Table EvalRow(RowExpr re, LocalScope scope,
                           Table? srcTable, int srcRowId, string? alias)
    {
        var t = new Table();
        var cells = new TqlValue[re.Fields.Count];
        for (int i = 0; i < re.Fields.Count; i++)
        {
            var f = re.Fields[i];
            var v = Eval(f.Value, scope, srcTable, srcRowId, alias);
            string colName = f.Name ?? $"col_{i}";
            var colType = v switch
            {
                DecimalVal => ColumnType.Decimal,
                GuidVal    => ColumnType.Guid,
                _          => ColumnType.Text
            };
            t.AddColumn(colName, colType);
            cells[i] = v;
        }
        t.InsertRow(cells);
        return t;
    }

    // ── string interpolation ──────────────────────────────────────────────────

    private string EvalPattern(StringPattern sp, LocalScope scope,
                                Table? src, int rid, string? alias)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var part in sp.Parts)
        {
            if (part.Text is not null) sb.Append(part.Text);
            else sb.Append(AutoScalar(Eval(part.Expr!, scope, src, rid, alias)).AsText());
        }
        return sb.ToString();
    }

    // ── SELECT ────────────────────────────────────────────────────────────────

    /// <summary>Full SELECT evaluation pipeline.</summary>
    private Table EvalSelect(SelectSpec spec, LocalScope scope)
    {
        // 1. evaluate FROM
        var fromVal = Eval(spec.From, scope);
        if (fromVal is not TableVal fromTv)
            throw new TqlRuntimeException("SELECT FROM requires a table", 0, 0);
        Table src = fromTv.T;
        string? alias = spec.FromAlias;

        // 2. filter with WHERE
        var filteredIds = new List<int>();
        foreach (var (rid, cells) in src.GetRowsInOrder())
        {
            if (spec.Where is null || AutoScalar(Eval(spec.Where, scope, src, rid, alias)).IsTrue)
                filteredIds.Add(rid);
        }

        // 3. detect aggregates
        bool hasAgg   = spec.Cols.Any(c => c.Expr is not IdentExpr { Name: "*" } && ContainsAgg(c.Expr));
        bool hasGroup = spec.GroupBy.Count > 0;

        // 4. build result rows
        Table result = new Table();
        bool schemaDone = false;

        void AddResultRow(TqlValue[] cells)
        {
            if (!schemaDone)
            {
                BuildResultSchema(result, spec.Cols, src, cells);
                schemaDone = true;
            }
            result.InsertRow(cells);
        }

        if (hasGroup)
        {
            var groups = GroupRows(src, filteredIds, spec.GroupBy);
            foreach (var (_, groupIds) in groups)
                AddResultRow(EvalGroupRow(spec.Cols, src, groupIds, scope, alias));
        }
        else if (hasAgg)
        {
            AddResultRow(EvalGroupRow(spec.Cols, src, filteredIds, scope, alias));
        }
        else
        {
            // handle SELECT *
            bool isStar = spec.Cols.Count == 1 && spec.Cols[0].Expr is IdentExpr { Name: "*" };
            foreach (int rid in filteredIds)
            {
                TqlValue[] cells;
                if (isStar)
                {
                    cells = src.GetRow(rid)!.ToArray();
                    if (!schemaDone) { CopySchema(result, src); schemaDone = true; }
                    result.InsertRow(cells);
                }
                else
                {
                    cells = EvalProjectRow(spec.Cols, src, rid, scope, alias);
                    AddResultRow(cells);
                }
            }
        }

        // 5. ORDER BY
        if (spec.OrderBy.Count > 0) ApplyOrderBy(result, spec.OrderBy, scope);

        // 6. LIMIT
        if (spec.Limit is not null) ApplyLimit(result, spec.Limit, scope);

        return result;
    }

    private static void CopySchema(Table target, Table src)
    {
        for (int i = 0; i < src.ColumnCount; i++)
            target.AddColumn(src.ColumnNames[i], src.GetColType(i), src.GetDefault(i));
    }

    private static void BuildResultSchema(Table result, List<SelectCol> cols,
                                           Table src, TqlValue[] cells)
    {
        for (int i = 0; i < cols.Count; i++)
        {
            string name = ColName(cols[i], i);
            var ct = cells[i] is DecimalVal ? ColumnType.Decimal
                   : cells[i] is GuidVal    ? ColumnType.Guid
                   : ColumnType.Text;
            result.AddColumn(name, ct);
        }
    }

    private static string ColName(SelectCol sc, int idx)
    {
        if (sc.Alias is not null) return sc.Alias;
        if (sc.Expr is IdentExpr id)   return id.Name;
        if (sc.Expr is BracketedIdent bi) return bi.Name;
        if (sc.Expr is TableColumn tc)  return tc.ColName;
        return $"col_{idx}";
    }

    // project a single row without aggregates
    private TqlValue[] EvalProjectRow(List<SelectCol> cols, Table src, int rid,
                                       LocalScope scope, string? alias)
    {
        var cells = new TqlValue[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            cells[i] = AutoScalar(Eval(cols[i].Expr, scope, src, rid, alias));
        return cells;
    }

    // evaluate a group of rows (aggregate or first-row for non-agg columns)
    private TqlValue[] EvalGroupRow(List<SelectCol> cols, Table src, List<int> rowIds,
                                     LocalScope scope, string? alias)
    {
        var cells = new TqlValue[cols.Count];
        int firstRid = rowIds.Count > 0 ? rowIds[0] : -1;
        for (int i = 0; i < cols.Count; i++)
            cells[i] = ContainsAgg(cols[i].Expr)
                ? EvalAggExpr(cols[i].Expr, scope, src, rowIds, alias)
                : firstRid >= 0
                    ? AutoScalar(Eval(cols[i].Expr, scope, src, firstRid, alias))
                    : TqlValue.Null;
        return cells;
    }

    // ── aggregate expression evaluation ──────────────────────────────────────

    private static bool ContainsAgg(ExprNode e) => e switch
    {
        FuncCallExpr fc => Builtins.IsAggregate(fc.Name) || fc.Args.Any(a => ContainsAgg(a.Value)),
        BinaryOp bo     => ContainsAgg(bo.L) || ContainsAgg(bo.R),
        UnaryOp  uo     => ContainsAgg(uo.Operand),
        ParenExpr pe    => ContainsAgg(pe.Inner),
        IsNullExpr ie   => ContainsAgg(ie.Operand),
        _               => false
    };

    private TqlValue EvalAggExpr(ExprNode e, LocalScope scope, Table src, List<int> rowIds,
                                  string? alias)
    {
        if (e is FuncCallExpr fc && Builtins.IsAggregate(fc.Name))
            return ComputeAgg(fc, scope, src, rowIds, alias);

        // recursively handle expressions that mix aggregates with scalars
        if (e is BinaryOp bo)
        {
            var l = ContainsAgg(bo.L)
                ? EvalAggExpr(bo.L, scope, src, rowIds, alias)
                : rowIds.Count > 0 ? AutoScalar(Eval(bo.L, scope, src, rowIds[0], alias)) : TqlValue.Null;
            var r = ContainsAgg(bo.R)
                ? EvalAggExpr(bo.R, scope, src, rowIds, alias)
                : rowIds.Count > 0 ? AutoScalar(Eval(bo.R, scope, src, rowIds[0], alias)) : TqlValue.Null;
            return ApplyBinaryOp(bo.Op, l, r, bo.Line, bo.Col);
        }

        // fall back: evaluate against first row
        return rowIds.Count > 0 ? AutoScalar(Eval(e, scope, src, rowIds[0], alias)) : TqlValue.Null;
    }

    private TqlValue ComputeAgg(FuncCallExpr fc, LocalScope scope, Table src, List<int> rowIds,
                                 string? alias)
    {
        string name = fc.Name.ToUpperInvariant();
        bool star = fc.Args.Count > 0 && fc.Args[0].Value is IdentExpr { Name: "*" };

        IEnumerable<TqlValue> Vals() =>
            rowIds.Select(rid => Eval(fc.Args[0].Value, scope, src, rid, alias));

        return name switch
        {
            "COUNT"  => star
                ? TqlValue.FromDecimal(rowIds.Count)
                : Builtins.Count(Vals()),
            "SUM"    => Builtins.Sum(Vals()),
            "MIN"    => Builtins.Min(Vals()),
            "MAX"    => Builtins.Max(Vals()),
            "AVG"    => Builtins.Avg(Vals()),
            "MEDIAN" => Builtins.Median(Vals()),
            "FIRST"  => Builtins.First(Vals()),
            _        => TqlValue.Null
        };
    }

    // ── GROUP BY ──────────────────────────────────────────────────────────────

    private static List<(TqlValue[] Key, List<int> RowIds)> GroupRows(
        Table src, List<int> rowIds, List<string> groupCols)
    {
        var groups = new List<(TqlValue[] Key, List<int> Rows)>();
        var keyMap  = new List<(TqlValue[], List<int>)>(); // ordered by first occurrence

        foreach (int rid in rowIds)
        {
            var key = groupCols.Select(c => src.HasColumn(c) ? src.GetCell(rid, c) : TqlValue.Null)
                               .ToArray();
            // find matching group
            int gi = keyMap.FindIndex(g => KeysEqual(g.Item1, key));
            if (gi < 0) { var list = new List<int> { rid }; keyMap.Add((key, list)); }
            else keyMap[gi].Item2.Add(rid);
        }
        return keyMap.Select(g => (g.Item1, g.Item2)).ToList();
    }

    private static bool KeysEqual(TqlValue[] a, TqlValue[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!TqlValueComparer.Instance.Equals(a[i], b[i])) return false;
        return true;
    }

    // ── ORDER BY ─────────────────────────────────────────────────────────────

    private void ApplyOrderBy(Table result, List<OrderByCol> orderBy, LocalScope scope)
    {
        var rows = result.GetRowsInOrder().ToList();
        // Evaluate all sort keys up front so exceptions surface cleanly (not wrapped by Sort).
        var keys = rows.Select(r =>
            orderBy.Select(ob => AutoScalar(Eval(ob.Expr, scope, result, r.RowId))).ToArray()
        ).ToList();
        var indexed = rows.Select((r, i) => (r, i)).ToList();
        indexed.Sort((a, b) =>
        {
            for (int k = 0; k < orderBy.Count; k++)
            {
                int c = TqlValueComparer.Instance.Compare(keys[a.i][k], keys[b.i][k]);
                if (c != 0) return orderBy[k].Desc ? -c : c;
            }
            return 0;
        });
        result.SetSortOrder(indexed.Select(x => x.r.RowId));
    }

    // ── LIMIT ─────────────────────────────────────────────────────────────────

    private void ApplyLimit(Table result, LimitClause limit, LocalScope scope)
    {
        int n = (int)Eval(limit.Count, scope).AsDecimal();

        if (limit.OnDistinct is null)
        {
            var keep = result.GetRowsInOrder().Take(n).Select(r => r.RowId).ToList();
            result.SetSortOrder(keep);
        }
        else
        {
            // keep rows from the first n distinct value-tuples
            int groups = 0;
            TqlValue[]? lastKey = null;
            var keep = new List<int>();
            foreach (var (rid, cells) in result.GetRowsInOrder())
            {
                var key = limit.OnDistinct.Select(c => result.GetCell(rid, c)).ToArray();
                if (lastKey is null || !KeysEqual(lastKey, key))
                {
                    if (++groups > n) break;
                    lastKey = key;
                }
                keep.Add(rid);
            }
            result.SetSortOrder(keep);
        }
    }

    // ── table mutation directives ─────────────────────────────────────────────

    private void DoInsertInto(string targetName, List<ExprNode> sources,
                               List<UsingCol>? using_, LocalScope scope, int line, int col)
    {
        var tgt = ResolveTable(targetName, scope, line, col);
        foreach (var src in sources)
        {
            var val = Eval(src, scope);
            InsertRows(tgt, val, using_, line, col);
        }
    }

    private TqlValue DoInsertExpr(InsertExpr ie, LocalScope scope)
    {
        var tgt = ResolveTable(ie.Target, scope, ie.Line, ie.Col);
        int before = tgt.RowCount;
        foreach (var src in ie.Sources)
            InsertRows(tgt, Eval(src, scope), ie.Using, ie.Line, ie.Col);
        // return first column of newly inserted rows
        return BuildFirstColTable(tgt, before);
    }

    private static void InsertRows(Table tgt, TqlValue srcVal,
                                    List<UsingCol>? using_, int line, int col)
    {
        if (srcVal is not TableVal tv)
            throw new TqlRuntimeException("INSERT VALUES requires a table source", line, col);
        Table src = tv.T;
        foreach (var (_, srcCells) in src.GetRowsInOrder())
        {
            var cells = new TqlValue[tgt.ColumnCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = tgt.GetDefault(i);

            if (using_ is not null)
            {
                foreach (var u in using_)
                {
                    string tgtCol = u.Tgt ?? u.Src;
                    if (!src.HasColumn(u.Src) || !tgt.HasColumn(tgtCol)) continue;
                    cells[tgt.ColIndex(tgtCol)] = srcCells[src.ColIndex(u.Src)];
                }
            }
            else
            {
                // match by column name
                foreach (var colName in src.ColumnNames)
                    if (tgt.HasColumn(colName))
                        cells[tgt.ColIndex(colName)] = srcCells[src.ColIndex(colName)];
            }
            tgt.InsertRow(cells);
        }
    }

    private void DoUpdateFrom(string targetName, TqlValue srcVal,
                               List<UsingCol>? using_, int line, int col)
    {
        if (srcVal is not TableVal tv)
            throw new TqlRuntimeException("UPDATE FROM requires a table source", line, col);
        var tgt = ResolveTable(targetName, null!, line, col);
        Table src = tv.T;
        if (src.ColumnCount == 0 || tgt.ColumnCount == 0) return;

        string tgtJoinCol = tgt.ColumnNames[0];
        string srcJoinCol = src.ColumnNames[0];

        foreach (var (srid, scells) in src.GetRowsInOrder())
        {
            var joinVal = scells[0];
            var matchIds = tgt.LookupByValue(tgt.ColIndex(tgtJoinCol), joinVal).ToList();
            foreach (int trid in matchIds)
            {
                if (using_ is not null)
                {
                    foreach (var u in using_)
                    {
                        string tgtCol = u.Tgt ?? u.Src;
                        if (!src.HasColumn(u.Src) || !tgt.HasColumn(tgtCol)) continue;
                        tgt.UpdateCell(trid, tgt.ColIndex(tgtCol), scells[src.ColIndex(u.Src)]);
                    }
                }
                else
                {
                    // update matching columns (skip join column)
                    for (int si = 1; si < src.ColumnCount; si++)
                    {
                        string colName = src.ColumnNames[si];
                        if (tgt.HasColumn(colName))
                            tgt.UpdateCell(trid, tgt.ColIndex(colName), scells[si]);
                    }
                }
            }
        }
    }

    private TqlValue DoUpdateSetExpr(UpdateSetExpr use_, LocalScope scope)
    {
        var tgt = ResolveTable(use_.Target, scope, use_.Line, use_.Col);
        var rowVal = Eval(use_.SourceRow, scope);
        RowVal row = RowValFrom(rowVal, use_.Line, use_.Col);

        var modifiedIds = new List<int>();
        foreach (var (rid, cells) in tgt.GetRowsInOrder().ToList())
        {
            if (use_.Where is not null &&
                !AutoScalar(Eval(use_.Where, scope, tgt, rid)).IsTrue) continue;

            if (use_.Using is not null)
            {
                foreach (var u in use_.Using)
                {
                    string tgtCol = u.Tgt ?? u.Src;
                    if (!row.Fields.ContainsKey(u.Src) || !tgt.HasColumn(tgtCol)) continue;
                    tgt.UpdateCell(rid, tgt.ColIndex(tgtCol), row.Fields[u.Src]);
                }
            }
            else
            {
                foreach (var (col, val) in row.Fields)
                    if (tgt.HasColumn(col))
                        tgt.UpdateCell(rid, tgt.ColIndex(col), val);
            }
            modifiedIds.Add(rid);
        }
        return BuildFirstColSubset(tgt, modifiedIds);
    }

    private void DoConcatTo(string targetName, TqlValue srcVal,
                             List<UsingCol>? using_, int line, int col)
    {
        if (srcVal is not TableVal tv)
            throw new TqlRuntimeException("CONCAT TO requires a table source", line, col);
        var tgt = ResolveTable(targetName, null!, line, col);
        Table src = tv.T;
        if (src.ColumnCount == 0 || tgt.ColumnCount == 0) return;

        // identify new columns to add
        var newCols = new List<(string srcName, string tgtName, int srcIdx)>();
        if (using_ is not null)
        {
            foreach (var u in using_)
            {
                string tgtCol = u.Tgt ?? u.Src;
                if (!tgt.HasColumn(tgtCol) && src.HasColumn(u.Src))
                {
                    int si = src.ColIndex(u.Src);
                    tgt.AddColumn(tgtCol, src.GetColType(si), src.GetDefault(si));
                    newCols.Add((u.Src, tgtCol, si));
                }
            }
        }
        else
        {
            for (int si = 1; si < src.ColumnCount; si++) // skip join column
            {
                string name = src.ColumnNames[si];
                if (!tgt.HasColumn(name))
                {
                    tgt.AddColumn(name, src.GetColType(si), src.GetDefault(si));
                    newCols.Add((name, name, si));
                }
            }
        }

        // fill new column values for matched rows
        string tgtJoinCol = tgt.ColumnNames[0];
        foreach (var (srid, scells) in src.GetRowsInOrder())
        {
            var joinVal = scells[0];
            var matchIds = tgt.LookupByValue(tgt.ColIndex(tgtJoinCol), joinVal).ToList();
            foreach (int trid in matchIds)
                foreach (var (_, tgtCol, si) in newCols)
                    tgt.UpdateCell(trid, tgt.ColIndex(tgtCol), scells[si]);
        }
    }

    private void DoAddColumn(AddColumnDirective acd, LocalScope scope)
    {
        var tbl = ResolveTable(acd.TableName, scope, acd.Line, acd.Col);
        TqlValue? def = acd.Default is not null ? Eval(acd.Default, scope) : null;
        tbl.AddColumn(acd.ColName, acd.ColType, def, acd.IfNotExists);
    }

    private void DoSaveTable(SaveTableDirective std_, LocalScope scope)
    {
        var tbl = ResolveTable(std_.TableName, scope, std_.Line, std_.Col);
        string path = Eval(std_.Path, scope).AsText();
        if (!Path.IsPathRooted(path)) path = Path.Combine(_scriptDir, path);
        using var writer = new StreamWriter(path);
        CsvIO.WriteTable(tbl, writer);
    }

    /// <summary>LOAD TABLE FROM path INTO target: replace all rows of an existing table from CSV.</summary>
    private void DoLoadTableInto(LoadTableIntoDirective ltd, LocalScope scope)
    {
        string path = Eval(ltd.Path, scope).AsText();
        if (!Path.IsPathRooted(path)) path = Path.Combine(_scriptDir, path);

        var target = ResolveTable(ltd.Target, scope, ltd.Line, ltd.Col);

        using var reader = new StreamReader(path);
        var csv = CsvIO.ReadTable(reader)
            ?? throw new TqlRuntimeException($"LOAD INTO: '{path}' is empty", ltd.Line, ltd.Col);

        // validate: every CSV column must exist in target with a compatible type
        foreach (var csvCol in csv.ColumnNames)
        {
            if (!target.HasColumn(csvCol))
                throw new TqlRuntimeException(
                    $"LOAD INTO: column '{csvCol}' from CSV does not exist in '{ltd.Target}'",
                    ltd.Line, ltd.Col);
            int ti = target.ColIndex(csvCol);
            int ci = csv.ColIndex(csvCol);
            if (target.GetColType(ti) == ColumnType.Decimal && csv.GetColType(ci) == ColumnType.Text)
                throw new TqlRuntimeException(
                    $"LOAD INTO: column '{csvCol}' is Decimal in '{ltd.Target}' but has text values in CSV",
                    ltd.Line, ltd.Col);
        }

        // discard existing rows
        target.DeleteRows(target.AllRowIds().ToList());

        // insert CSV rows; columns absent in CSV receive their declared DEFAULT
        foreach (var (_, csvCells) in csv.GetRowsInOrder())
        {
            var cells = new TqlValue[target.ColumnCount];
            for (int i = 0; i < target.ColumnCount; i++)
            {
                string col = target.ColumnNames[i];
                cells[i] = csv.HasColumn(col)
                    ? csvCells[csv.ColIndex(col)]
                    : target.GetDefault(i);
            }
            target.InsertRow(cells);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Resolve a table variable by name.</summary>
    private Table ResolveTable(string name, LocalScope? scope, int line, int col)
    {
        TqlValue val;
        if (scope is not null && scope.TryResolve(name, out val) && val is TableVal tv)
            return tv.T;
        if (_global.Variables.TryGetValue(name, out val!) && val is TableVal gv)
            return gv.T;
        throw new TqlRuntimeException($"'{name}' is not a table", line, col);
    }

    /// <summary>Coerce a 1×1 TableVal to its scalar; return other values unchanged.</summary>
    private static TqlValue AutoScalar(TqlValue v)
    {
        if (v is TableVal tv && tv.T.RowCount == 1 && tv.T.ColumnCount == 1)
            return tv.T.GetRowsInOrder().First().Cells[0];
        if (v is RowVal rv && rv.Fields.Count == 1) return rv.Fields.Values.First();
        return v;
    }

    /// <summary>Extract a RowVal from a TableVal (first row) or RowVal directly.</summary>
    private static RowVal RowValFrom(TqlValue val, int line, int col)
    {
        if (val is RowVal rv)    return rv;
        if (val is TableVal tv && tv.T.RowCount > 0)
            return tv.T.RowValAt(tv.T.GetRowsInOrder().First().RowId);
        throw new TqlRuntimeException("Expected a row value", line, col);
    }

    private static TqlValue BuildFirstColTable(Table tgt, int fromRowCount)
    {
        var t = new Table();
        if (tgt.ColumnCount == 0) return TqlValue.FromTable(t);
        t.AddColumn(tgt.ColumnNames[0], tgt.GetColType(0));
        var rows = tgt.GetRowsInOrder().Skip(fromRowCount).ToList();
        foreach (var (_, cells) in rows) t.InsertRow([cells[0]]);
        return TqlValue.FromTable(t);
    }

    private static TqlValue BuildFirstColSubset(Table tgt, List<int> rowIds)
    {
        var t = new Table();
        if (tgt.ColumnCount == 0) return TqlValue.FromTable(t);
        t.AddColumn(tgt.ColumnNames[0], tgt.GetColType(0));
        foreach (int rid in rowIds) t.InsertRow([tgt.GetCell(rid, 0)]);
        return TqlValue.FromTable(t);
    }
}
