#!/usr/bin/env python3
"""Add {constraint: foreign_key} to D2 sql_table columns based on DBML Ref lines."""

import re
import sys


def parse_fks(dbml_path):
    fks = set()
    with open(dbml_path) as f:
        for line in f:
            m = re.match(r'^Ref:\s+(\w+)\.(\w+)\s*>', line)
            if m:
                fks.add((m.group(1), m.group(2)))
    return fks


def add_fks(d2_src, fks):
    lines = d2_src.splitlines(keepends=True)
    out = []
    current_table = None
    depth = 0

    for line in lines:
        net = line.count('{') - line.count('}')

        if depth == 0 and net > 0:
            m = re.match(r'^(\w+):\s*\{', line)
            if m:
                current_table = m.group(1)

        if current_table and depth == 1 and net == 0:
            m = re.match(r'^(\s+)(\w+):\s+\S+', line)
            if m and m.group(2) != 'shape' and (current_table, m.group(2)) in fks:
                line = line.rstrip('\n') + ' {constraint: foreign_key}\n'

        out.append(line)
        depth += net
        if depth == 0:
            current_table = None

    return ''.join(out)


if __name__ == '__main__':
    if len(sys.argv) < 3:
        print(f'usage: {sys.argv[0]} schema.dbml input.d2 [output.d2]', file=sys.stderr)
        sys.exit(1)
    dbml_path, d2_path = sys.argv[1], sys.argv[2]
    out_path = sys.argv[3] if len(sys.argv) > 3 else d2_path
    fks = parse_fks(dbml_path)
    with open(d2_path) as f:
        src = f.read()
    with open(out_path, 'w') as f:
        f.write(add_fks(src, fks))
    print(f'tagged {len(fks)} FK columns → {out_path}')
