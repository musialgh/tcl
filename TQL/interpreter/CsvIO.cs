using System.Globalization;
using System.Text;

namespace TqlInterpreter;

public static class CsvIO
{
    // ── read ─────────────────────────────────────────────────────────────────

    /// <summary>Read one table from reader; stops at blank line or EOF. Returns null at EOF.</summary>
    public static Table? ReadTable(TextReader reader)
    {
        // skip leading blank lines, read header
        string? header = null;
        while ((header = reader.ReadLine()) != null && header.Trim() == "") {}
        if (header is null) return null;

        var colNames = ParseRow(header);

        // read data rows until blank line or EOF
        var rawRows = new List<List<string>>();
        while (true)
        {
            string? line = reader.ReadLine();
            if (line is null || line.Trim() == "") break;
            rawRows.Add(ParseRow(line));
        }

        // infer column types
        var types = new ColumnType[colNames.Count];
        for (int c = 0; c < colNames.Count; c++)
        {
            types[c] = ColumnType.Decimal;
            foreach (var row in rawRows)
            {
                string cell = c < row.Count ? row[c] : "";
                if (cell == "" || cell.Equals("NULL", StringComparison.OrdinalIgnoreCase)) continue;
                if (!decimal.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                { types[c] = ColumnType.Text; break; }
            }
        }

        var t = new Table();
        for (int c = 0; c < colNames.Count; c++) t.AddColumn(colNames[c], types[c]);
        foreach (var row in rawRows)
        {
            var cells = new TqlValue[colNames.Count];
            for (int c = 0; c < colNames.Count; c++)
                cells[c] = ParseCell(c < row.Count ? row[c] : "", types[c]);
            t.InsertRow(cells);
        }
        return t;  // Table? — non-null here
    }

    private static TqlValue ParseCell(string raw, ColumnType type)
    {
        if (raw == "" || raw.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return TqlValue.Null;
        if (type == ColumnType.Decimal &&
            decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d))
            return TqlValue.FromDecimal(d);
        return TqlValue.FromText(raw);
    }

    // ── write ────────────────────────────────────────────────────────────────

    /// <summary>Write table to writer with trailing blank line.</summary>
    public static void WriteTable(Table t, TextWriter writer)
    {
        writer.WriteLine(string.Join(";", t.ColumnNames.Select(Q)));
        foreach (var (_, cells) in t.GetRowsInOrder())
            writer.WriteLine(string.Join(";", cells.Select(v => Q(v.IsNull ? "" : v.AsText()))));
        writer.WriteLine();
    }

    private static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // ── CSV row parser ────────────────────────────────────────────────────────

    private static List<string> ParseRow(string line)
    {
        var result = new List<string>();
        int i = 0;
        while (i <= line.Length)
        {
            if (i == line.Length) { result.Add(""); break; }
            if (line[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '\\' && i + 1 < line.Length)
                    { sb.Append(line[i + 1]); i += 2; }
                    else if (line[i] == '"')
                    { i++; break; }
                    else sb.Append(line[i++]);
                }
                result.Add(sb.ToString());
            }
            else
            {
                int j = line.IndexOf(';', i);
                if (j < 0) j = line.Length;
                result.Add(line[i..j].Trim());
                i = j;
            }
            if (i < line.Length && line[i] == ';') i++;
            else break;
        }
        return result;
    }
}
