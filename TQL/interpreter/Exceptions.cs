namespace TqlInterpreter;

public class TqlParseException(string msg, int line, int col)
    : Exception($"[{line}:{col}] Parse error: {msg}")
{
    public int Line { get; } = line;
    public int Col  { get; } = col;
}

public class TqlRuntimeException : Exception
{
    public int Line { get; }
    public int Col  { get; }
    public TqlRuntimeException(string msg) : base(msg) {}
    public TqlRuntimeException(string msg, int line, int col)
        : base($"[{line}:{col}] {msg}") { Line = line; Col = col; }
}

public class TqlAbortException(string msg) : Exception(msg);

// internal signals used for control flow — never escape the interpreter
internal sealed class BreakSignal()    : Exception("break");
internal sealed class ContinueSignal() : Exception("continue");
internal sealed class ReturnSignal(TqlValue value) : Exception("return")
{
    public TqlValue Value { get; } = value;
}
