#!/usr/bin/env python3
"""Convert DBML to Azimutt AML v2 format with enum types and namespace grouping."""

import re
import sys

# Namespace grouping: table -> namespace
GROUPS = {
    'organizations':                  'org',
    'organization_external_ids':      'org',
    'federations':                    'org',

    'competition_types':              'catalog',
    'competition_disciplines':        'catalog',
    'competition_forms':              'catalog',
    'competition_orders':             'catalog',
    'participant_types':              'catalog',
    'age_groups':                     'catalog',
    'start_classes':                  'catalog',
    'leagues':                        'catalog',
    'dances':                         'catalog',
    'flooring_types':                 'catalog',

    'persons':                        'people',
    'participants':                   'people',
    'participant_external_ids':       'people',
    'participant_members':            'people',

    'tournaments':                    'venue',
    'tournament_external_ids':        'venue',
    'halls':                          'venue',
    'floors':                         'venue',

    'competitions':                   'competition',
    'competition_external_ids':       'competition',
    'competition_dances':             'competition',
    'competition_judges':             'competition',
    'competition_rounds':             'competition',
    'competition_round_judges':       'competition',

    'competition_entries':            'participation',
    'competition_round_results':      'participation',
    'competition_round_heats':        'participation',
    'competition_round_heat_entries': 'participation',
    'competition_round_heat_results': 'participation',
    'competition_results':            'participation',
}


def qualified(table):
    ns = GROUPS.get(table)
    return f'{ns}.{table}' if ns else table


def parse_dbml(path):
    with open(path) as f:
        src = f.read()

    enums = {}   # name -> [values]
    tables = {}  # name -> [(col_name, col_type, is_pk)]
    refs = []    # [(from_table, from_col, to_table, to_col)]

    lines = src.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i].strip()

        # Enum definition
        m = re.match(r'^Enum\s+(\w+)\s*\{', line)
        if m:
            enum_name = m.group(1)
            values = []
            i += 1
            while i < len(lines) and '}' not in lines[i]:
                v = lines[i].strip()
                if v and not v.startswith('//'):
                    values.append(v)
                i += 1
            enums[enum_name] = values
            i += 1
            continue

        # Table definition
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
                        col_type = cm.group(2)
                        attrs = (cm.group(3) or '').lower()
                        is_pk = 'primary key' in attrs
                        columns.append((col_name, col_type, is_pk))
                depth += opens - closes
                i += 1
            tables[table_name] = columns
            continue

        # Ref definition
        m = re.match(r'^Ref:\s+(\w+)\.(\w+)\s*>\s*(\w+)\.(\w+)', line)
        if m:
            refs.append((m.group(1), m.group(2), m.group(3), m.group(4)))

        i += 1

    return enums, tables, refs


def to_aml(enums, tables, refs):
    fk_map = {(t, c): (tt, tc) for t, c, tt, tc in refs}
    out = []

    # Enum types
    if enums:
        for enum_name, values in enums.items():
            out.append(f'type {enum_name} ({", ".join(values)})')
        out.append('')

    # Tables grouped by namespace
    current_ns = None
    for table_name, columns in tables.items():
        ns = GROUPS.get(table_name)
        if ns != current_ns:
            current_ns = ns
            out.append('')

        out.append(qualified(table_name))
        for col_name, col_type, is_pk in columns:
            parts = [f'  {col_name}', col_type]
            if is_pk:
                parts.append('pk')
            if (table_name, col_name) in fk_map:
                to_table, to_col = fk_map[(table_name, col_name)]
                parts.append(f'-> {qualified(to_table)}({to_col})')
            out.append(' '.join(parts))
        out.append('')

    return '\n'.join(out).lstrip('\n')


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(f'usage: {sys.argv[0]} schema.dbml [output.aml]', file=sys.stderr)
        sys.exit(1)
    dbml_path = sys.argv[1]
    out_path = sys.argv[2] if len(sys.argv) > 2 else dbml_path.replace('.dbml', '.aml')
    enums, tables, refs = parse_dbml(dbml_path)
    with open(out_path, 'w') as f:
        f.write(to_aml(enums, tables, refs) + '\n')
    print(f'written: {out_path} ({len(enums)} enums, {len(tables)} tables, {len(refs)} refs)')
