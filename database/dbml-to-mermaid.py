#!/usr/bin/env python3
"""Convert DBML to Mermaid erDiagram."""

import re
import sys


def parse_dbml(path):
    with open(path) as f:
        src = f.read()

    tables = {}  # name -> [(col_name, col_type, is_pk)]
    refs = []    # [(from_table, from_col, to_table, to_col)]

    lines = src.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i].strip()

        m = re.match(r'^Table\s+(\w+)\s*\{', line)
        if m:
            table_name = m.group(1)
            columns = []
            i += 1
            depth = 1
            while i < len(lines) and depth > 0:
                tline = lines[i].strip()
                opens = tline.count('{')
                closes = tline.count('}')
                if depth == 1 and opens == 0 and tline and not tline.startswith('//') and not tline.startswith('Note'):
                    cm = re.match(r'^(\w+)\s+(\S+)\s*(?:\[([^\]]*)\])?', tline)
                    if cm:
                        col_name = cm.group(1)
                        raw_type = re.sub(r'\(.*?\)', '', cm.group(2))
                        col_type = re.sub(r'[^a-zA-Z0-9_]', '_', raw_type).strip('_')
                        is_pk = 'primary key' in (cm.group(3) or '').lower()
                        columns.append((col_name, col_type, is_pk))
                depth += opens - closes
                i += 1
            tables[table_name] = columns
            continue

        m = re.match(r'^Ref:\s+(\w+)\.(\w+)\s*>\s*(\w+)\.(\w+)', line)
        if m:
            refs.append((m.group(1), m.group(2), m.group(3), m.group(4)))

        i += 1

    return tables, refs


def to_mermaid(tables, refs):
    fk_cols = {(t, c) for t, c, _, _ in refs}

    out = ['erDiagram']

    for table_name, columns in tables.items():
        out.append(f'    {table_name} {{')
        for col_name, col_type, is_pk in columns:
            marker = ' PK' if is_pk else (' FK' if (table_name, col_name) in fk_cols else '')
            out.append(f'        {col_type} {col_name}{marker}')
        out.append('    }')
        out.append('')

    seen = set()
    for from_table, _, to_table, _ in refs:
        key = (to_table, from_table)
        if key not in seen:
            seen.add(key)
            out.append(f'    {to_table} ||--o{{ {from_table} : ""')

    return '\n'.join(out)


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(f'usage: {sys.argv[0]} schema.dbml [output.mmd]', file=sys.stderr)
        sys.exit(1)
    dbml_path = sys.argv[1]
    out_path = sys.argv[2] if len(sys.argv) > 2 else dbml_path.replace('.dbml', '.mmd')
    tables, refs = parse_dbml(dbml_path)
    with open(out_path, 'w') as f:
        f.write(to_mermaid(tables, refs) + '\n')
    print(f'written: {out_path} ({len(tables)} tables, {len(refs)} refs)')
