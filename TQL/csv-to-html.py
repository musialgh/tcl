#!/usr/bin/env python3
"""
Convert a TQL CSV report to a self-contained HTML table.

CSV layout (generic, no assumed column names):
  col[0]       — row key  (becomes row header)
  col[1..N-3]  — path components  (define column hierarchy)
  col[N-2]     — cell value
  col[N-1]     — tooltip text shown on hover

The last path column (col N-3, the "key") is always non-empty by convention,
so every column path has at least one component.

Path levels that are empty for all rows are automatically suppressed.
Columns sharing a path prefix are kept adjacent; within each prefix group
the next component determines order:
  - integer keys  → sorted numerically ascending
  - textual keys  → sorted by first occurrence in the CSV within their group
Short paths receive a rowspan spanning the remaining header rows; their text
is bottom-aligned to sit at the same visual level as neighbouring leaf labels.

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
    if src == '-':
        rows = list(csv.reader(sys.stdin, delimiter=';'))
    else:
        with open(src, newline='', encoding='utf-8') as f:
            rows = list(csv.reader(f, delimiter=';'))
    rows = [r for r in rows if any(r)]          # drop blank / trailing lines
    return (rows[0], rows[1:]) if rows else ([], [])


# ── column paths ───────────────────────────────────────────────────────────────

def row_path(row: list, n: int) -> tuple:
    """Column path for one CSV row: non-empty values from cols 1 .. N-3."""
    return tuple(v for v in row[1:n - 2] if v)


def collect_paths(rows: list, n: int) -> list:
    """Unique column paths in first-occurrence order (used as sorting baseline)."""
    seen: OrderedDict = OrderedDict()
    for row in rows:
        seen.setdefault(row_path(row, n), None)
    return list(seen)


def sort_paths(paths: list, level: int = 0) -> list:
    """
    Recursively sort paths so that within every prefix group at level L the
    next component is ordered:
      - integer strings  → numerically ascending
      - other strings    → by first occurrence within this group (from the input
                           order of `paths`, which reflects CSV first-occurrence)
    All paths in one call share a common prefix of length `level`.
    """
    if len(paths) <= 1:
        return list(paths)

    # Occurrence index of each component at this level, within this group.
    occ: dict = {}
    for path in paths:
        c = path[level] if level < len(path) else ''
        if c not in occ:
            occ[c] = len(occ)

    # Group paths by their component at this level.
    groups: OrderedDict = OrderedDict()
    for path in paths:
        c = path[level] if level < len(path) else ''
        groups.setdefault(c, []).append(path)

    def comp_key(c: str):
        try:
            return (0, int(c))          # numeric: sort by value
        except ValueError:
            return (1, occ.get(c, 0))   # textual: sort by first-occurrence index

    result = []
    for c in sorted(groups, key=comp_key):
        result.extend(sort_paths(groups[c], level + 1))
    return result


# ── table data ─────────────────────────────────────────────────────────────────

def build_data(rows: list, n: int) -> OrderedDict:
    """Return {row_key: {path: (value, tooltip)}} in first-occurrence row order."""
    data: OrderedDict = OrderedDict()
    for row in rows:
        key   = row[0]
        path  = row_path(row, n)
        value = row[n - 2] if n >= 2 else ''
        tip   = row[n - 1] if n >= 1 else ''
        data.setdefault(key, {})[path] = (value, tip)
    return data


# ── header construction ────────────────────────────────────────────────────────

def build_header(paths: list) -> list:
    """
    Return one list per header row, each entry (label, colspan, rowspan).

    A column whose path ends at level L gets rowspan = max_depth - L, covering
    the remaining header rows without emitting any blank placeholder cells.
    """
    if not paths:
        return []
    max_depth = max(len(p) for p in paths)
    header_rows = []
    covered: set = set()        # column indices already given a rowspan

    for level in range(max_depth):
        row = []
        i = 0
        while i < len(paths):
            if i in covered:
                i += 1
                continue

            path   = paths[i]
            prefix = path[:level + 1]

            # extent of consecutive uncovered columns sharing this prefix
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

_CSS = """\
  body  { font-family: sans-serif; font-size: 13px; padding: 1em; }
  h2    { margin-bottom: .4em; }
  table { border-collapse: collapse; }
  th, td { border: 1px solid #aaa; padding: 3px 7px; white-space: nowrap; }
  th    { background: #e4e8ee; text-align: center; vertical-align: bottom; }
  td.rk { font-weight: bold; }
  td.v  { text-align: right; cursor: default; }
  td.v:hover { background: #fffacc; }
  td.v:empty { background: #f7f7f7; }"""


def generate_html(title: str, row_key_label: str,
                  header_rows: list, paths: list, data: OrderedDict) -> str:
    depth = len(header_rows)
    e = html.escape

    out = [
        '<!DOCTYPE html>',
        '<html><head>',
        f'  <meta charset="utf-8">',
        f'  <title>{e(title)}</title>',
        '  <style>',
        _CSS,
        '  </style>',
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

    raw_paths   = collect_paths(rows, n)    # first-occurrence order (sort baseline)
    paths       = sort_paths(raw_paths)     # numeric asc / textual by occurrence
    data        = build_data(rows, n)
    header_rows = build_header(paths)

    out = generate_html(title, headers[0], header_rows, paths, data)

    if args.output:
        Path(args.output).write_text(out, encoding='utf-8')
    else:
        print(out)


if __name__ == '__main__':
    main()
