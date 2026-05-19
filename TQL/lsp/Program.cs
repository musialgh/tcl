using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

TqlLsp.Run();

static class TqlLsp
{
    static readonly Stream In  = Console.OpenStandardInput();
    static readonly Stream Out = Console.OpenStandardOutput();
    static readonly Dictionary<string, string[]> Docs = new();
    static readonly Dictionary<string, FuncDef>  Funcs = new(StringComparer.OrdinalIgnoreCase);
    static bool _shutdown;

    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Function definition index
    record ParameterInfo(string Name, string? Default);
    record FuncDef(string Uri, int Line, int Col, ParameterInfo[] Params);

    public static void Run()
    {
        while (true)
        {
            int len = ReadHeaders();
            if (len < 0) break;   // EOF
            if (len == 0) continue;

            var buf = new byte[len];
            for (int r = 0; r < len;)
            {
                int n = In.Read(buf, r, len - r);
                if (n == 0) return;
                r += n;
            }

            JsonNode? msg;
            try { msg = JsonNode.Parse(buf); } catch { continue; }
            if (msg == null) continue;

            Dispatch(msg);
        }
    }

    static void Dispatch(JsonNode msg)
    {
        var method = msg["method"]?.GetValue<string>() ?? "";
        var id = msg["id"];
        var p  = msg["params"];

        switch (method)
        {
            case "initialize":
                Respond(id, new
                {
                    capabilities = new
                    {
                        textDocumentSync   = new { openClose = true, change = 1 },
                        completionProvider = new { },
                        hoverProvider      = true,
                        definitionProvider = true,
                        signatureHelpProvider = new { triggerCharacters = new[] { "(", "," } }
                    },
                    serverInfo = new { name = "tql-lsp", version = "0.2.0" }
                });
                break;

            case "initialized": break;

            case "shutdown":
                _shutdown = true;
                Respond(id, (object?)null);
                break;

            case "exit":
                Environment.Exit(_shutdown ? 0 : 1);
                break;

            case "textDocument/didOpen":
            {
                var lines = SplitLines(S(p?["textDocument"]?["text"]));
                var uri   = S(p?["textDocument"]?["uri"]);
                Docs[uri] = lines;
                ReparseDoc(uri, lines);
                break;
            }

            case "textDocument/didChange":
            {
                var changes = p?["contentChanges"]?.AsArray();
                if (changes?.Count > 0)
                {
                    var lines = SplitLines(S(changes[^1]?["text"]));
                    var uri   = S(p?["textDocument"]?["uri"]);
                    Docs[uri] = lines;
                    ReparseDoc(uri, lines);
                }
                break;
            }

            case "textDocument/didClose":
            {
                var uri = S(p?["textDocument"]?["uri"]);
                Docs.Remove(uri);
                RemoveFuncsForUri(uri);
                break;
            }

            case "textDocument/completion":
                OnCompletion(id, p);
                break;

            case "textDocument/hover":
                OnHover(id, p);
                break;

            case "textDocument/definition":
                OnDefinition(id, p);
                break;

            case "textDocument/signatureHelp":
                OnSignatureHelp(id, p);
                break;

            default:
                if (id != null)
                    RespondError(id, -32601, "Method not found");
                break;
        }
    }

    // ---- feature handlers ----

    static void OnCompletion(JsonNode? id, JsonNode? p)
    {
        var uri  = S(p?["textDocument"]?["uri"]);
        int line = p?["position"]?["line"]?.GetValue<int>()      ?? 0;
        int ch   = p?["position"]?["character"]?.GetValue<int>() ?? 0;

        var prefix = WordPrefix(uri, line, ch);

        var items = Keywords.All
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key)
            .Select(kv => new
            {
                label         = kv.Key,
                kind          = 14,  // Keyword
                documentation = new { kind = "markdown", value = kv.Value }
            })
            .ToArray();

        Respond(id, new { isIncomplete = false, items });
    }

    static void OnHover(JsonNode? id, JsonNode? p)
    {
        var uri  = S(p?["textDocument"]?["uri"]);
        int line = p?["position"]?["line"]?.GetValue<int>()      ?? 0;
        int ch   = p?["position"]?["character"]?.GetValue<int>() ?? 0;

        var word = WordAt(uri, line, ch)?.ToUpperInvariant();
        if (word != null && Keywords.All.TryGetValue(word, out var doc))
            Respond(id, new { contents = new { kind = "markdown", value = doc } });
        else
            Respond(id, (object?)null);
    }

    static void OnDefinition(JsonNode? id, JsonNode? p)
    {
        var uri  = S(p?["textDocument"]?["uri"]);
        int line = p?["position"]?["line"]?.GetValue<int>()      ?? 0;
        int ch   = p?["position"]?["character"]?.GetValue<int>() ?? 0;

        var word = WordAt(uri, line, ch);
        if (word != null && Funcs.TryGetValue(word, out var def))
        {
            Respond(id, new
            {
                uri   = def.Uri,
                range = new
                {
                    start = new { line = def.Line, character = def.Col },
                    end   = new { line = def.Line, character = def.Col + word.Length }
                }
            });
        }
        else
        {
            Respond(id, (object?)null);
        }
    }

    static void OnSignatureHelp(JsonNode? id, JsonNode? p)
    {
        var uri  = S(p?["textDocument"]?["uri"]);
        int line = p?["position"]?["line"]?.GetValue<int>()      ?? 0;
        int ch   = p?["position"]?["character"]?.GetValue<int>() ?? 0;

        var (funcName, activeParam) = FindFunctionCall(uri, line, ch);
        if (funcName == null || !Funcs.TryGetValue(funcName, out var def))
        {
            Respond(id, (object?)null);
            return;
        }

        // Build label and per-parameter [start,end] offsets within the label string
        var paramStrings = def.Params
            .Select(pr => pr.Default != null ? $"{pr.Name} = {pr.Default}" : pr.Name)
            .ToArray();
        var label = $"{funcName}({string.Join(", ", paramStrings)})";

        var paramLabels = new List<int[]>();
        int offset = funcName.Length + 1; // skip "name("
        for (int i = 0; i < paramStrings.Length; i++)
        {
            paramLabels.Add(new[] { offset, offset + paramStrings[i].Length });
            offset += paramStrings[i].Length + 2; // ", "
        }

        Respond(id, new
        {
            signatures = new[]
            {
                new
                {
                    label      = label,
                    parameters = paramLabels.Select(r => new { label = r }).ToArray()
                }
            },
            activeSignature = 0,
            activeParameter = def.Params.Length == 0
                ? 0
                : Math.Min(activeParam, def.Params.Length - 1)
        });
    }

    // ---- function-call context (for signature help) ----

    // Scans backwards from (line, ch) to find the nearest unclosed '(' and the
    // function name before it. Returns (null,0) if not inside a call.
    static (string? Name, int ActiveParam) FindFunctionCall(string uri, int line, int ch)
    {
        if (!Docs.TryGetValue(uri, out var lines) || line >= lines.Length)
            return (null, 0);

        var s      = lines[line][..Math.Min(ch, lines[line].Length)];
        int depth  = 0;
        int commas = 0;
        bool inStr = false;

        for (int i = s.Length - 1; i >= 0; i--)
        {
            char c = s[i];
            // Naively skip string literals (TQL uses " and ')
            if (c == '"' || c == '\'') { inStr = !inStr; continue; }
            if (inStr) continue;

            if (c == ')') { depth++; continue; }
            if (c == ',') { if (depth == 0) commas++; continue; }
            if (c != '(') continue;

            if (depth > 0) { depth--; continue; }

            // Found the matching '(' — extract function name before it
            int nameEnd = i;
            while (nameEnd > 0 && s[nameEnd - 1] == ' ') nameEnd--;
            int nameStart = nameEnd;
            while (nameStart > 0 && IsId(s[nameStart - 1])) nameStart--;
            if (nameStart < nameEnd)
                return (s[nameStart..nameEnd], commas);
            return (null, 0);
        }

        return (null, 0);
    }

    // ---- function-definition parsing ----

    static readonly Regex FuncRx = new(
        @"^\s*FUNCTION\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static void ReparseDoc(string uri, string[] lines)
    {
        RemoveFuncsForUri(uri);

        for (int i = 0; i < lines.Length; i++)
        {
            var m = FuncRx.Match(lines[i]);
            if (!m.Success) continue;

            var name    = m.Groups[1].Value;
            var nameCol = m.Groups[1].Index;

            // Collect parameter text between '(' and matching ')'.
            // The signature may span multiple continuation lines; strip # comments first.
            var sb    = new StringBuilder();
            var first = StripComment(lines[i])[(m.Index + m.Length)..]; // after '('
            sb.Append(first);
            int li = i + 1;
            while (!sb.ToString().Contains(')') && li < lines.Length)
            {
                sb.Append(' ');
                sb.Append(StripComment(lines[li]));
                li++;
            }

            var raw      = sb.ToString();
            var closeIdx = raw.IndexOf(')');
            var paramTxt = closeIdx >= 0 ? raw[..closeIdx] : raw;

            Funcs[name] = new FuncDef(uri, i, nameCol, ParseParams(paramTxt));
        }
    }

    static void RemoveFuncsForUri(string uri)
    {
        var keys = Funcs.Where(kv => kv.Value.Uri == uri).Select(kv => kv.Key).ToList();
        foreach (var k in keys) Funcs.Remove(k);
    }

    static ParameterInfo[] ParseParams(string paramText)
    {
        if (string.IsNullOrWhiteSpace(paramText))
            return Array.Empty<ParameterInfo>();

        return paramText
            .Split(',')
            .Select(part =>
            {
                var halves = part.Split('=', 2);
                var name   = halves[0].Trim();
                var def    = halves.Length > 1 ? halves[1].Trim() : null;
                return new ParameterInfo(name, string.IsNullOrEmpty(def) ? null : def);
            })
            .Where(pi => !string.IsNullOrEmpty(pi.Name))
            .ToArray();
    }

    // Strip everything from the first '#' to end-of-line (outside strings is enough for signatures)
    static string StripComment(string line)
    {
        bool inStr = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"' || c == '\'') inStr = !inStr;
            else if (c == '#' && !inStr) return line[..i];
        }
        return line;
    }

    // ---- document helpers ----

    static string WordPrefix(string uri, int line, int ch)
    {
        if (!Docs.TryGetValue(uri, out var lines) || line >= lines.Length) return "";
        var s = lines[line];
        int end = Math.Min(ch, s.Length), start = end;
        while (start > 0 && IsId(s[start - 1])) start--;
        return s[start..end];
    }

    static string? WordAt(string uri, int line, int ch)
    {
        if (!Docs.TryGetValue(uri, out var lines) || line >= lines.Length) return null;
        var s = lines[line];
        if (ch >= s.Length || !IsId(s[ch])) return null;
        int start = ch, end = ch;
        while (start > 0 && IsId(s[start - 1])) start--;
        while (end < s.Length && IsId(s[end])) end++;
        return s[start..end];
    }

    static bool IsId(char c) => char.IsLetterOrDigit(c) || c == '_';

    // ---- JSON-RPC transport ----

    static int ReadHeaders()
    {
        int len = 0;
        var sb = new StringBuilder();
        while (true)
        {
            int b = In.ReadByte();
            if (b < 0) return -1;
            if (b == '\n')
            {
                var line = sb.ToString().TrimEnd('\r');
                sb.Clear();
                if (line.Length == 0) return len;
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line["Content-Length:".Length..].Trim(), out len);
            }
            else sb.Append((char)b);
        }
    }

    static void Respond(JsonNode? id, object? result)
    {
        var idStr  = id?.ToJsonString() ?? "null";
        var resStr = result == null ? "null" : JsonSerializer.Serialize(result, Opts);
        Send($"{{\"jsonrpc\":\"2.0\",\"id\":{idStr},\"result\":{resStr}}}");
    }

    static void RespondError(JsonNode? id, int code, string message)
    {
        var idStr  = id?.ToJsonString() ?? "null";
        var msgStr = JsonSerializer.Serialize(message);
        Send($"{{\"jsonrpc\":\"2.0\",\"id\":{idStr},\"error\":{{\"code\":{code},\"message\":{msgStr}}}}}");
    }

    static void Send(string json)
    {
        var body   = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        lock (Out) { Out.Write(header); Out.Write(body); Out.Flush(); }
    }

    static string   S(JsonNode? n) => n?.GetValue<string>() ?? "";
    static string[] SplitLines(string t) => t.Split('\n');
}
