using TqlInterpreter;

// ── helpers ───────────────────────────────────────────────────────────────────

static void Die(string msg) { Console.Error.WriteLine($"tql: {msg}"); Environment.Exit(1); }

/// <summary>Resolve a command-line path against the caller's working directory.</summary>
static string ResolveCliPath(string path, string invocationDirectory) =>
    Path.GetFullPath(path, invocationDirectory);

/// <summary>Drain all blank-line-separated tables from a reader until EOF.</summary>
static List<Table> ReadAllTables(TextReader rdr)
{
    var result = new List<Table>();
    Table? t;
    while ((t = CsvIO.ReadTable(rdr)) is not null) result.Add(t);
    return result;
}

// ── argument parsing ──────────────────────────────────────────────────────────

const string Help = """
usage: tql <script> {[-r <file>] -i <table>} {[-w <file>] -o <table>} [-b]

  <script>            TQL script file to execute (.tql extension optional)

  Input (may be repeated):
    -r <file>         Read next input table(s) from <file> instead of stdin
                      ('-' resets to stdin)
    -i <table>        Assign the next read to global table <table>
                      Multiple blank-line-separated tables in one file are
                      assigned to consecutive -i names.

  Output (may be repeated):
    -w <file>         Write next output table to <file> instead of stdout
                      ('-' resets to stdout)
    -o <table>        Write global table <table> to the current output target

  -b, --browse        Open each -w output file with xdg-open after writing
  -h, --help          Show this help and exit

  Relative command-line paths are resolved against the caller's current
  working directory. Paths used inside a TQL script are script-relative.

Examples:
  tql script.tql -r input.csv -i marks -w report.csv -o report
  tql script.tql -i marks -o report                    # stdin / stdout
  tql script.tql -r a.csv -i t1 -r b.csv -i t2 -o out # two input files
""";

if (args.Length == 0)
{
    Console.Error.WriteLine(Help);
    Environment.Exit(1);
}

string invocationDirectory = Environment.CurrentDirectory;
string scriptArgument = args[0];

if (scriptArgument is "-h" or "--help")
{
    Console.WriteLine(Help);
    Environment.Exit(0);
}

var inputs  = new List<InputSpec>();
var outputs = new List<OutputSpec>();
bool browse = false;

string? pendingRead  = null;   // last -r value awaiting -i
string? pendingWrite = null;   // last -w value awaiting -o

for (int a = 1; a < args.Length; a++)
{
    switch (args[a])
    {
        case "-h": case "--help":
            Console.WriteLine(Help);
            Environment.Exit(0);
            break;

        case "-r":
            if (++a >= args.Length) Die("-r requires a file argument");
            pendingRead = args[a] == "-" ? null : ResolveCliPath(args[a], invocationDirectory);
            break;

        case "-i":
            if (++a >= args.Length) Die("-i requires a table name");
            inputs.Add(new InputSpec(args[a], pendingRead));
            pendingRead = null;
            break;

        case "-w":
            if (++a >= args.Length) Die("-w requires a file argument");
            pendingWrite = args[a] == "-" ? null : ResolveCliPath(args[a], invocationDirectory);
            break;

        case "-o":
            if (++a >= args.Length) Die("-o requires a table name");
            outputs.Add(new OutputSpec(args[a], pendingWrite));
            pendingWrite = null;
            break;

        case "-b": case "--browse":
            browse = true;
            break;

        default:
            Die($"unknown argument '{args[a]}'");
            break;
    }
}

// ── resolve script path ───────────────────────────────────────────────────────

string scriptPath = ResolveCliPath(scriptArgument, invocationDirectory);
if (!File.Exists(scriptPath))
{
    var withExt = scriptPath.EndsWith(".tql", StringComparison.OrdinalIgnoreCase)
        ? scriptPath : scriptPath + ".tql";
    if (File.Exists(withExt)) scriptPath = withExt;
    else Die($"script not found: {scriptArgument}");
}

string scriptDir = Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? ".";

// ── read input tables ─────────────────────────────────────────────────────────

// Group input specs by source file so multiple tables from the same file
// are read sequentially from one stream (blank-line separated).
var stdinNames    = new List<string>();                                         // inputs from stdin
var fileNames     = new Dictionary<string, List<string>>(StringComparer.Ordinal); // inputs from file
foreach (var inp in inputs)
{
    if (inp.FilePath is null)
        stdinNames.Add(inp.TableName);
    else
    {
        if (!fileNames.TryGetValue(inp.FilePath, out var names))
            fileNames[inp.FilePath] = names = new List<string>();
        names.Add(inp.TableName);
    }
}

var openReaders = new Dictionary<string, StreamReader>(StringComparer.Ordinal);
TextReader? stdinReader = null;
TextReader GetStdin() => stdinReader ??= new StreamReader(Console.OpenStandardInput());

var tableMap = new Dictionary<string, Table>(StringComparer.Ordinal);

if (stdinNames.Count > 0)
{
    var tables = ReadAllTables(GetStdin());
    for (int i = 0; i < stdinNames.Count; i++)
    {
        if (i >= tables.Count)
            Die($"expected table '{stdinNames[i]}' but stdin contained only {tables.Count} table(s)");
        tableMap[stdinNames[i]] = tables[i];
    }
}

foreach (var (filePath, names) in fileNames)
{
    TextReader rdr = openReaders.TryGetValue(filePath, out var existing)
        ? existing
        : openReaders[filePath] = new StreamReader(filePath);

    var tables = ReadAllTables(rdr);

    for (int i = 0; i < names.Count; i++)
    {
        if (i >= tables.Count)
            Die($"expected table '{names[i]}' but file '{filePath}' contained only {tables.Count} table(s)");
        tableMap[names[i]] = tables[i];
    }
}

foreach (var (_, rdr) in openReaders) rdr.Dispose();

// ── set up executor and load input tables into global scope ───────────────────

var global   = new GlobalScope();
var executor = new Executor(global, scriptDir);

foreach (var (name, table) in tableMap)
    global.Variables[name] = new TableVal(table);

// ── execute the script ────────────────────────────────────────────────────────

try
{
    executor.ExecuteFile(scriptPath);
}
catch (TqlAbortException ex)
{
    Console.Error.WriteLine($"ABORT: {ex.Message}");
    Environment.Exit(2);
}
catch (TqlRuntimeException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(3);
}
catch (TqlParseException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(4);
}

// ── write output tables ───────────────────────────────────────────────────────

var openWriters = new Dictionary<string, StreamWriter>(StringComparer.Ordinal);
TextWriter? stdoutWriter = null;
TextWriter GetStdout() => stdoutWriter ??= new StreamWriter(Console.OpenStandardOutput());

foreach (var spec in outputs)
{
    if (!global.Variables.TryGetValue(spec.TableName, out var val))
        Die($"output table '{spec.TableName}' is not defined after script execution");

    if (val is not TableVal tv)
    {
        Die($"'{spec.TableName}' is not a table (got {val!.GetType().Name})");
        continue;
    }

    TextWriter wtr = spec.FilePath is null
        ? GetStdout()
        : openWriters.TryGetValue(spec.FilePath, out var existing)
            ? existing
            : openWriters[spec.FilePath] = new StreamWriter(spec.FilePath);

    CsvIO.WriteTable(tv.T, wtr);
}

stdoutWriter?.Flush();
foreach (var (_, wtr) in openWriters) { wtr.Flush(); wtr.Dispose(); }

if (browse)
{
    foreach (var path in openWriters.Keys)
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", path) { UseShellExecute = false });
}

// ── support types ─────────────────────────────────────────────────────────────

/// <summary>One scheduled table read: table name → source file path (null = stdin).</summary>
record InputSpec (string TableName, string? FilePath);

/// <summary>One scheduled table write: table name → destination file path (null = stdout).</summary>
record OutputSpec(string TableName, string? FilePath);
