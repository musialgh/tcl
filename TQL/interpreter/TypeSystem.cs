using System.Globalization;

namespace TqlInterpreter;

public enum ColumnType { Decimal, Text, Guid }

// ── runtime values ────────────────────────────────────────────────────────────

public abstract class TqlValue
{
    public static readonly TqlValue Null  = new NullVal();
    public static readonly TqlValue True  = new DecimalVal(1m);
    public static readonly TqlValue False = new DecimalVal(0m);

    public bool IsNull => this is NullVal;

    // truthiness: 0/NULL/empty-string = false
    public bool IsTrue => this switch
    {
        NullVal    => false,
        DecimalVal d => d.V != 0m,
        TextVal t    => t.V.Length > 0,
        GuidVal    => true,
        TableVal   => true,
        RowVal     => true,
        _ => false
    };

    public decimal AsDecimal(int line = 0, int col = 0) => this switch
    {
        DecimalVal d => d.V,
        NullVal      => throw new TqlRuntimeException("NULL used in arithmetic", line, col),
        _            => throw new TqlRuntimeException($"{GetType().Name} cannot be used as a number", line, col)
    };

    public string AsText() => this switch
    {
        TextVal t    => t.V,
        DecimalVal d => d.V.ToString(CultureInfo.InvariantCulture),
        NullVal      => "",
        GuidVal g    => g.V.ToString(),
        _            => ToString() ?? ""
    };

    public static TqlValue FromDecimal(decimal v) => new DecimalVal(v);
    public static TqlValue FromText(string v)     => new TextVal(v);
    public static TqlValue FromGuid(Guid v)        => new GuidVal(v);
    public static TqlValue FromBool(bool v)        => v ? True : False;
    public static TqlValue FromTable(Table t)      => new TableVal(t);
}

public sealed class NullVal    : TqlValue { public override string ToString() => "NULL"; }

public sealed class DecimalVal : TqlValue
{
    public decimal V { get; }
    public DecimalVal(decimal v) { V = v; }
    public override string ToString() => V.ToString(CultureInfo.InvariantCulture);
}

public sealed class TextVal : TqlValue
{
    public string V { get; }
    public TextVal(string v) { V = v; }
    public override string ToString() => V;
}

public sealed class GuidVal : TqlValue
{
    public Guid V { get; }
    public GuidVal(Guid v) { V = v; }
    public override string ToString() => V.ToString();
}

public sealed class TableVal : TqlValue
{
    public Table T { get; }
    public TableVal(Table t) { T = t; }
    public override string ToString() => $"[table {T.ColumnCount}×{T.RowCount}]";
}

// produced by table[i] and FOR x IN table iteration
public sealed class RowVal : TqlValue
{
    public IReadOnlyDictionary<string, TqlValue> Fields { get; }
    public RowVal(Dictionary<string, TqlValue> f) { Fields = f; }

    public TqlValue Get(string col)
        => Fields.TryGetValue(col, out var v) ? v : TqlValue.Null;

    // coerce single-field row to scalar
    public TqlValue Scalar => Fields.Count == 1
        ? Fields.Values.First()
        : throw new TqlRuntimeException("Multi-column row cannot be used as scalar");

    public override string ToString() => $"[row ({string.Join(", ", Fields.Keys)})]";
}

// ── total ordering used for indices ──────────────────────────────────────────

public sealed class TqlValueComparer : IComparer<TqlValue>, IEqualityComparer<TqlValue>
{
    public static readonly TqlValueComparer Instance = new();

    public int Compare(TqlValue? a, TqlValue? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        return (a, b) switch
        {
            (NullVal,    NullVal)    => 0,
            (NullVal,    _)          => -1,
            (_,          NullVal)    => 1,
            (DecimalVal x, DecimalVal y) => x.V.CompareTo(y.V),
            (TextVal x,    TextVal y)    => string.Compare(x.V, y.V, StringComparison.Ordinal),
            (GuidVal x,    GuidVal y)    => x.V.CompareTo(y.V),
            // cross-type: Decimal < Text < Guid < other
            (DecimalVal, _) => -1, (_, DecimalVal) => 1,
            (TextVal,    _) => -1, (_, TextVal)    => 1,
            _               => 0
        };
    }

    public bool Equals(TqlValue? x, TqlValue? y) => Compare(x, y) == 0;

    public int GetHashCode(TqlValue v) => v switch
    {
        NullVal      => 0,
        DecimalVal d => d.V.GetHashCode(),
        TextVal t    => t.V.GetHashCode(StringComparison.Ordinal),
        GuidVal g    => g.V.GetHashCode(),
        _            => v.GetHashCode()
    };
}

// ── Table ─────────────────────────────────────────────────────────────────────

public class Table
{
    // name → column index (0-based)
    public Dictionary<string, int> ColumnList { get; } = new(StringComparer.Ordinal);

    // per-column: value → set of physical row IDs
    private readonly List<Dictionary<TqlValue, SortedSet<int>>> _indices = [];

    // logical view: list of physical row IDs in display order
    private List<int> _sortOrder = [];

    // physical storage: rowId → cell array
    private readonly Dictionary<int, TqlValue[]> _rows = [];

    private readonly List<TqlValue>    _defaults  = [];
    private readonly List<ColumnType>  _colTypes  = [];

    private int _nextRowId;

    // ── schema ──────────────────────────────────────────────────────────────

    public int ColumnCount => ColumnList.Count;
    public int RowCount    => _rows.Count;

    private string[] _colNames = [];
    public IReadOnlyList<string> ColumnNames => _colNames;

    /// <summary>Append a column; updates existing rows with the default value.</summary>
    public void AddColumn(string name, ColumnType type, TqlValue? def = null, bool ifNotExists = false)
    {
        if (ColumnList.ContainsKey(name))
        {
            if (ifNotExists) return;
            throw new TqlRuntimeException($"Column '{name}' already exists");
        }
        int idx = _colTypes.Count;
        ColumnList[name] = idx;
        _colTypes.Add(type);
        var d = def ?? TqlValue.Null;
        _defaults.Add(d);
        _indices.Add(new Dictionary<TqlValue, SortedSet<int>>(TqlValueComparer.Instance));

        // extend existing rows
        foreach (var rid in _rows.Keys.ToList())
        {
            var arr = _rows[rid];
            Array.Resize(ref arr, idx + 1);
            arr[idx] = d;
            _rows[rid] = arr;
            IdxAdd(idx, d, rid);
        }
        RebuildNames();
    }

    private void RebuildNames()
    {
        _colNames = new string[ColumnList.Count];
        foreach (var (n, i) in ColumnList) _colNames[i] = n;
    }

    // ── row operations ───────────────────────────────────────────────────────

    /// <summary>Insert a row aligned to ColumnList; missing/extra cells use defaults.</summary>
    public int InsertRow(TqlValue[] values)
    {
        int rid = _nextRowId++;
        var cells = new TqlValue[ColumnCount];
        for (int i = 0; i < ColumnCount; i++)
            cells[i] = i < values.Length && values[i] is not null ? values[i] : _defaults[i];
        _rows[rid] = cells;
        _sortOrder.Add(rid);
        for (int i = 0; i < ColumnCount; i++) IdxAdd(i, cells[i], rid);
        return rid;
    }

    /// <summary>Update one cell; keeps index consistent.</summary>
    public void UpdateCell(int rowId, int colIdx, TqlValue newVal)
    {
        var cells = _rows[rowId];
        IdxRemove(colIdx, cells[colIdx], rowId);
        cells[colIdx] = newVal;
        IdxAdd(colIdx, newVal, rowId);
    }

    /// <summary>Remove rows by physical ID.</summary>
    public void DeleteRows(IEnumerable<int> ids)
    {
        foreach (int rid in ids.ToList())
        {
            if (!_rows.TryGetValue(rid, out var cells)) continue;
            for (int i = 0; i < cells.Length; i++) IdxRemove(i, cells[i], rid);
            _rows.Remove(rid);
        }
        _sortOrder.RemoveAll(rid => !_rows.ContainsKey(rid));
    }

    // ── sort order ───────────────────────────────────────────────────────────

    /// <summary>Replace sort order; passed IDs must all be valid physical IDs.</summary>
    public void SetSortOrder(IEnumerable<int> ids) => _sortOrder = ids.ToList();

    /// <summary>Reset to insertion order.</summary>
    public void ResetSortOrder() => _sortOrder = [.. _rows.Keys.OrderBy(x => x)];

    // ── row access ───────────────────────────────────────────────────────────

    /// <summary>Enumerate (rowId, cells) in sort order.</summary>
    public IEnumerable<(int RowId, TqlValue[] Cells)> GetRowsInOrder()
    {
        foreach (int rid in _sortOrder)
            if (_rows.TryGetValue(rid, out var c))
                yield return (rid, c);
    }

    public TqlValue[]? GetRow(int rowId) =>
        _rows.TryGetValue(rowId, out var c) ? c : null;

    public IEnumerable<int> AllRowIds() => _rows.Keys;

    // ── column helpers ───────────────────────────────────────────────────────

    public int ColIndex(string name, int line = 0, int col = 0)
    {
        if (!ColumnList.TryGetValue(name, out int idx))
            throw new TqlRuntimeException($"Unknown column '{name}'", line, col);
        return idx;
    }

    public bool HasColumn(string name) => ColumnList.ContainsKey(name);

    public TqlValue GetCell(int rowId, int colIdx) => _rows[rowId][colIdx];
    public TqlValue GetCell(int rowId, string name) => GetCell(rowId, ColIndex(name));

    public ColumnType GetColType(int idx) => _colTypes[idx];
    public TqlValue   GetDefault(int idx) => _defaults[idx];

    /// <summary>Return the physical IDs where column colIdx equals val (fast index lookup).</summary>
    public SortedSet<int> LookupByValue(int colIdx, TqlValue val)
    {
        if (_indices[colIdx].TryGetValue(val, out var s)) return s;
        return [];
    }

    // ── column extraction ────────────────────────────────────────────────────

    /// <summary>Return a new single-column table with values from the named column.</summary>
    public Table SelectColumn(string name)
    {
        int ci = ColIndex(name);
        var t = new Table();
        t.AddColumn(name, _colTypes[ci], _defaults[ci]);
        foreach (var (_, cells) in GetRowsInOrder())
            t.InsertRow([cells[ci]]);
        return t;
    }

    // ── RowVal construction ──────────────────────────────────────────────────

    /// <summary>Wrap a row as a RowVal with column names as keys.</summary>
    public RowVal RowValAt(int rowId)
    {
        var cells = _rows[rowId];
        var d = new Dictionary<string, TqlValue>(StringComparer.Ordinal);
        for (int i = 0; i < _colNames.Length; i++)
            d[_colNames[i]] = i < cells.Length ? cells[i] : TqlValue.Null;
        return new RowVal(d);
    }

    // ── clone ────────────────────────────────────────────────────────────────

    public Table Clone()
    {
        var t = new Table();
        for (int i = 0; i < _colTypes.Count; i++)
            t.AddColumn(_colNames[i], _colTypes[i], _defaults[i]);
        foreach (var (_, cells) in GetRowsInOrder())
            t.InsertRow((TqlValue[])cells.Clone());
        return t;
    }

    // ── index helpers ────────────────────────────────────────────────────────

    private void IdxAdd(int ci, TqlValue v, int rid)
    {
        var d = _indices[ci];
        if (!d.TryGetValue(v, out var s))
            d[v] = s = [];
        s.Add(rid);
    }

    private void IdxRemove(int ci, TqlValue v, int rid)
    {
        if (_indices[ci].TryGetValue(v, out var s)) s.Remove(rid);
    }
}
