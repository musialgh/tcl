#!/usr/bin/env python3
"""Strip column definitions and simplify edges in D2 sql_table blocks for a compact overview."""

import re
import sys


def strip_columns(src):
    lines = src.splitlines(keepends=True)
    out = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if re.match(r'^\S.*\{\s*$', line):
            block = [line]
            depth = line.count('{') - line.count('}')
            i += 1
            while i < len(lines) and depth > 0:
                block.append(lines[i])
                depth += lines[i].count('{') - lines[i].count('}')
                i += 1
            if any('shape: sql_table' in l for l in block):
                shape_line = next(l for l in block if 'shape: sql_table' in l)
                out.extend([block[0], shape_line, block[-1]])
            else:
                out.extend(block)
        else:
            out.append(line)
            i += 1
    return ''.join(out)


def simplify_edges(src):
    lines = src.splitlines(keepends=True)
    out = []
    seen = set()
    for line in lines:
        m = re.match(r'^(\w+)\.\w+\s*->\s*(\w+)\.\w+\s*$', line)
        if m:
            edge = f'{m.group(1)} -> {m.group(2)}\n'
            if edge not in seen:
                seen.add(edge)
                out.append(edge)
        else:
            out.append(line)
    return ''.join(out)


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(f'usage: {sys.argv[0]} input.d2 [output.d2]', file=sys.stderr)
        sys.exit(1)
    infile = sys.argv[1]
    outfile = sys.argv[2] if len(sys.argv) > 2 else infile.replace('.d2', '-overview.d2')
    with open(infile) as f:
        src = f.read()
    result = simplify_edges(strip_columns(src))
    with open(outfile, 'w') as f:
        f.write(result)
    print(f'written: {outfile}')
