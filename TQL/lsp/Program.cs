using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

TqlLsp.Run();

static class TqlLsp
{
    static readonly Stream In  = Console.OpenStandardInput();
    static readonly Stream Out = Console.OpenStandardOutput();
    static readonly Dictionary<string, string[]> Docs = new();
    static bool _shutdown;

    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
                        hoverProvider      = true
                    },
                    serverInfo = new { name = "tql-lsp", version = "0.1.0" }
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
                Docs[S(p?["textDocument"]?["uri"])] =
                    SplitLines(S(p?["textDocument"]?["text"]));
                break;

            case "textDocument/didChange":
            {
                var changes = p?["contentChanges"]?.AsArray();
                if (changes?.Count > 0)
                    Docs[S(p?["textDocument"]?["uri"])] =
                        SplitLines(S(changes[^1]?["text"]));
                break;
            }

            case "textDocument/didClose":
                Docs.Remove(S(p?["textDocument"]?["uri"]));
                break;

            case "textDocument/completion":
                OnCompletion(id, p);
                break;

            case "textDocument/hover":
                OnHover(id, p);
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
