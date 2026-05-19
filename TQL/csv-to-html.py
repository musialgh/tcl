#!/usr/bin/env python3
"""
Convert a TQL CSV report to a self-contained HTML table.

CSV layout (generic, no assumed column names):
  col[0]       — row key  (becomes row header)
  col[1..N-3]  — path components  (define column hierarchy)
  col[N-2]     — cell value
  col[N-1]     — tooltip text shown on hover

Path levels that are empty for all rows are automatically suppressed.
Columns are arranged in first-occurrence order; all columns sharing a
path prefix are adjacent.  Shared prefixes produce colspan'd header cells.
Short paths get rowspan to fill the header height — no blank cells needed.

Usage:
  python3 csv-to-html.py report.csv -o report.html -t "Finale report"
"""

import argparse
import csv
import html
import sys
from collections import OrderedDict
from pathlib import Path


# ── CSV input ──────────────────────────────────────────────────────────────────

def read_csv(src: str):
    """Return (header_row, data_rows) from a ;-delimited CSV."""
    opener = (lambda: open(src, newline='', encoding='utf-8')) if src != '-' \
             else (lambda: sys.stdin)
    with opener() as f:
        rows = list(csv.reader(f, delimiter=';'))
    rows = [r for r in rows if any(r)]   # drop blank lines
    return (rows[0], rows[1:]) if rows else ([], [])


# ── column path logic ──────────────────────────────────────────────────────────

def row_path(row: list, n: int) -> tuple:
    """Build column path from a CSV row: filter empty values from cols 1..N-3."""
    return tuple(v for v in row[1:n - 2] if v)


def column_paths(rows: list, n: int) -> list:
    """All unique column paths in first-occurrence order."""
    seen = OrderedDict()
    for row in rows:
        p = row_path(row, n)
        seen.setdefault(p, None)
    return list(seen)


# ── table data ─────────────────────────────────────────────────────────────────

def build_data(rows: list, n: int) -> OrderedDict:
    """Return {row_key: {path: (value, tooltip)}} in first-occurrence key order."""
    data: OrderedDict = OrderedDict()
    for row in rows:
        key   = row[0]
        path  = row_path(row, n)
        value = row[n - 2] if n >= 2 else ''
        tip   = row[n - 1] if n >= 1 else ''
        data.setdefault(key, {})[path] = (value, tip)
    return data


# ── header construction ────────────────────────────────────────────────────────

def build_header(paths: list) -> list[list[tuple]]:
    """
    Return one list per header row, each entry being (label, colspan, rowspan).

    When a path ends at level L its cell gets rowspan = max_depth - L so it
    spans the remaining (otherwise empty) header rows.  No blank placeholder
    cells are emitted.
    """
    if not paths:
        return []
    max_depth = max(len(p) for p in paths)
    header_rows = []
    covered: set[int] = set()          # column indices already given a rowspan

    for level in range(max_depth):
        row = []
        i = 0
        while i < len(paths):
            if i in covered:
                i += 1
                continue

            path   = paths[i]
            prefix = path[:level + 1]

            # find the run of consecutive uncovered columns sharing this prefix
            j = i + 1
            while j < len(paths) and j not in covered \
                    and paths[j][:level + 1] == prefix:
                j += 1

            colspan = j - i
            label   = path[level] if level < len(path) else ''
            is_leaf = (level == len(path) - 1)

            if is_leaf:
                rowspan = max_depth - level
                for k in range(i, j):
                    covered.add(k)
            else:
                rowspan = 1

            row.append((label, colspan, rowspan))
            i = j

        header_rows.append(row)

    return header_rows


# ── HTML generation ────────────────────────────────────────────────────────────

_CSS = """
  body  { font-family: sans-serif; font-size: 13px; padding: 1em; }
  h2    { margin-bottom: .4em; }
  table { border-collapse: collapse; }
  th, td { border: 1px solid #aaa; padding: 3px 7px; white-space: nowrap; }
  th    { background: #e4e8ee; text-align: center; }
  td.rk { font-weight: bold; }
  td.v  { text-align: right; cursor: default; }
  td.v:hover { background: #fffacc; }
  td.v:empty { background: #f7f7f7; }
"""


def generate_html(title: str, row_key_label: str,
                  header_rows: list, paths: list, data: OrderedDict) -> str:
    depth = len(header_rows)
    e = html.escape                        # shorthand

    out = [
        '<!DOCTYPE html>',
        '<html><head>',
        f'  <meta charset="utf-8">',
        f'  <title>{e(title)}</title>',
        '  <style>', _CSS, '  </style>',
        '</head><body>',
        f'<h2>{e(title)}</h2>',
        '<table><thead>',
    ]

    for level, row in enumerate(header_rows):
        out.append('<tr>')
        if level == 0:
            out.append(f'  <th rowspan="{depth}">{e(row_key_label)}</th>')
        for label, colspan, rowspan in row:
            attrs  = f' colspan="{colspan}"' if colspan > 1 else ''
            attrs += f' rowspan="{rowspan}"' if rowspan > 1 else ''
            out.append(f'  <th{attrs}>{e(label)}</th>')
        out.append('</tr>')

    out.append('</thead><tbody>')

    for row_key, cells in data.items():
        out.append('<tr>')
        out.append(f'  <td class="rk">{e(row_key)}</td>')
        for path in paths:
            if path in cells:
                value, tip = cells[path]
                tip_attr = f' title="{e(tip)}"' if tip else ''
                out.append(f'  <td class="v"{tip_attr}>{e(value)}</td>')
            else:
                out.append('  <td class="v"></td>')
        out.append('</tr>')

    out.append('</tbody></table>')
    out.append('</body></html>')
    return '\n'.join(out)


# ── main ───────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('input', nargs='?', default='-',
                    help='Input CSV file (default: stdin)')
    ap.add_argument('-o', '--output', default=None,
                    help='Output HTML file (default: stdout)')
    ap.add_argument('-t', '--title', default=None,
                    help='Page/table title (default: input filename stem)')
    args = ap.parse_args()

    title = args.title or (Path(args.input).stem if args.input != '-' else 'TQL Report')

    headers, rows = read_csv(args.input)
    if len(headers) < 3:
        sys.exit('error: CSV must have at least 3 columns (key, …, value, tooltip)')

    n = len(headers)
    paths       = column_paths(rows, n)
    data        = build_data(rows, n)
    header_rows = build_header(paths)

    out = generate_html(title, headers[0], header_rows, paths, data)

    if args.output:
        Path(args.output).write_text(out, encoding='utf-8')
    else:
        print(out)


if __name__ == '__main__':
    main()
