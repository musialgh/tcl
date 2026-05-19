namespace TqlInterpreter;

/// <summary>Scalar and aggregate built-in functions.</summary>
public static class Builtins
{
    public static readonly HashSet<string> SqlAggNames = new(StringComparer.OrdinalIgnoreCase)
        { "SUM", "COUNT", "MIN", "MAX", "AVG", "MEDIAN", "FIRST" };

    /// <summary>True if name is a SQL aggregate (evaluated over a group of rows).</summary>
    public static bool IsAggregate(string name) => SqlAggNames.Contains(name);

    // ── scalar builtins ───────────────────────────────────────────────────────

    /// <summary>IF(cond, thenVal, elseVal)</summary>
    public static TqlValue If(TqlValue cond, TqlValue thenVal, TqlValue elseVal)
        => cond.IsTrue ? thenVal : elseVal;

    /// <summary>ROWS(table) → count of data rows</summary>
    public static TqlValue Rows(TqlValue arg, int line, int col)
    {
        if (arg is TableVal tv) return TqlValue.FromDecimal(tv.T.RowCount);
        throw new TqlRuntimeException("ROWS() requires a table argument", line, col);
    }

    /// <summary>GUID() → new random GUID</summary>
    public static TqlValue Guid() => TqlValue.FromGuid(System.Guid.NewGuid());

    // ── aggregate builtins (operate over a list of values) ───────────────────

    /// <summary>SUM of non-NULL decimal values; NULL if none.</summary>
    public static TqlValue Sum(IEnumerable<TqlValue> vals)
    {
        decimal? sum = null;
        foreach (var v in vals)
            if (!v.IsNull) { sum = (sum ?? 0m) + v.AsDecimal(); }
        return sum.HasValue ? TqlValue.FromDecimal(sum.Value) : TqlValue.Null;
    }

    /// <summary>COUNT(*) — all rows; COUNT(expr) — non-NULL/non-zero/non-empty rows.</summary>
    public static TqlValue Count(IEnumerable<TqlValue> vals, bool countAll = false)
    {
        long n = 0;
        foreach (var v in vals)
            if (countAll || (!v.IsNull && v.IsTrue)) n++;
        return TqlValue.FromDecimal(n);
    }

    public static TqlValue Min(IEnumerable<TqlValue> vals)
    {
        TqlValue? min = null;
        foreach (var v in vals)
            if (!v.IsNull && (min is null || TqlValueComparer.Instance.Compare(v, min) < 0)) min = v;
        return min ?? TqlValue.Null;
    }

    public static TqlValue Max(IEnumerable<TqlValue> vals)
    {
        TqlValue? max = null;
        foreach (var v in vals)
            if (!v.IsNull && (max is null || TqlValueComparer.Instance.Compare(v, max) > 0)) max = v;
        return max ?? TqlValue.Null;
    }

    public static TqlValue Avg(IEnumerable<TqlValue> vals)
    {
        decimal sum = 0; int n = 0;
        foreach (var v in vals)
            if (!v.IsNull) { sum += v.AsDecimal(); n++; }
        return n == 0 ? TqlValue.Null : TqlValue.FromDecimal(sum / n);
    }

    public static TqlValue Median(IEnumerable<TqlValue> vals)
    {
        var sorted = vals.Where(v => !v.IsNull).OrderBy(v => v, TqlValueComparer.Instance).ToList();
        if (sorted.Count == 0) return TqlValue.Null;
        int mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1) return sorted[mid];
        // even count: average of two middle values
        decimal a = sorted[mid - 1].AsDecimal(), b = sorted[mid].AsDecimal();
        return TqlValue.FromDecimal((a + b) / 2);
    }

    public static TqlValue First(IEnumerable<TqlValue> vals)
    {
        foreach (var v in vals) return v;
        return TqlValue.Null;
    }
}
