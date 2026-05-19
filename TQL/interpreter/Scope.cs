namespace TqlInterpreter;

public class GlobalScope
{
    public Dictionary<string, TqlValue>    Variables { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, FunctionDef> Functions { get; } = new(StringComparer.Ordinal);
}

public class LocalScope
{
    private readonly GlobalScope              _global;
    private readonly Dictionary<string, TqlValue> _locals   = new(StringComparer.Ordinal);
    private readonly HashSet<string>              _imported = new(StringComparer.Ordinal);

    /// <summary>When true, Bind/Resolve operate directly on the global scope (for top-level code).</summary>
    public bool IsGlobal { get; init; }

    public GlobalScope Global => _global;

    public LocalScope(GlobalScope global) => _global = global;

    /// <summary>
    /// Resolve a name: in global context all names come from global scope;
    /// in function context, locals first then imported globals.
    /// </summary>
    public TqlValue Resolve(string name, int line = 0, int col = 0)
    {
        if (IsGlobal)
        {
            if (_global.Variables.TryGetValue(name, out var gv)) return gv;
            throw new TqlRuntimeException($"Undefined variable '{name}'", line, col);
        }
        if (_locals.TryGetValue(name, out var v))   return v;
        if (_imported.Contains(name) &&
            _global.Variables.TryGetValue(name, out v)) return v;
        throw new TqlRuntimeException($"Undefined variable '{name}'", line, col);
    }

    public bool TryResolve(string name, out TqlValue val)
    {
        if (IsGlobal)
        {
            var ok = _global.Variables.TryGetValue(name, out val!);
            if (!ok) val = TqlValue.Null;
            return ok;
        }
        if (_locals.TryGetValue(name, out val!))    return true;
        if (_imported.Contains(name) &&
            _global.Variables.TryGetValue(name, out val!)) return true;
        val = TqlValue.Null;
        return false;
    }

    /// <summary>Bind: goes to local dict or (for global context) to global variables.</summary>
    public void Bind(string name, TqlValue val)
    {
        if (IsGlobal) _global.Variables[name] = val;
        else _locals[name] = val;
    }

    /// <summary>GLOBAL name — import existing global into local scope.</summary>
    public void ImportGlobal(string name, int line = 0, int col = 0)
    {
        if (IsGlobal) return; // already in global scope
        if (!_global.Variables.ContainsKey(name))
            throw new TqlRuntimeException($"Global variable '{name}' is not defined", line, col);
        _imported.Add(name);
    }

    /// <summary>GLOBAL name = expr — create/update global and make it locally visible.</summary>
    public void WriteGlobal(string name, TqlValue val)
    {
        _global.Variables[name] = val;
        if (!IsGlobal) _imported.Add(name);
    }
}
