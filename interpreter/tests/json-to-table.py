#!/usr/bin/env python3
"""
Convert nested JSON objects to HTML tables.
- Top-level keys become row titles
- Leaf keys become column titles  
- Intermediate keys become column group titles
"""
import json
import sys
from pathlib import Path
from typing import Any, Dict, List, Tuple


def get_leaf_keys(obj: Any, path: List[str] = None) -> Dict[Tuple, Any]:
    """Recursively extract all leaf keys with their path and value."""
    if path is None:
        path = []
    leaves = {}
    
    if isinstance(obj, dict):
        for key, val in obj.items():
            new_path = path + [key]
            if isinstance(val, dict) and val:  # non-empty dict
                leaves.update(get_leaf_keys(val, new_path))
            else:
                leaves[tuple(new_path)] = val
    return leaves


def get_all_column_paths(data: Dict[str, Any]) -> List[Tuple]:
    """Extract all unique column paths across all rows."""
    all_paths = set()
    for row_obj in data.values():
        if isinstance(row_obj, dict):
            all_paths.update(get_leaf_keys(row_obj).keys())
    return sorted(all_paths)


def get_value(data: Dict[str, Any], row_key: str, col_path: Tuple) -> str:
    """Retrieve value from nested structure following the column path."""
    try:
        current = data[row_key]
        for key in col_path:
            current = current[key]
        return str(current)
    except (KeyError, TypeError):
        return ''


def generate_html_table(data: Dict[str, Any], title: str = None) -> str:
    """
    Generate HTML table from nested JSON object.
    Structure: { row_key: { [intermediate_key: ]* leaf_key: value } }
    - row_key becomes row title
    - intermediate keys become recursive column group headers with colspan
    - leaf_key becomes column title under each group
    - Repeated keys at the same level are only shown once; repetitions left blank with no border
    """
    col_paths = get_all_column_paths(data)
    row_keys = sorted(data.keys())
    
    if not col_paths or not row_keys:
        return '<p>No data to display</p>'
    
    # Determine nesting depth
    max_depth = max(len(p) for p in col_paths)
    group_depth = max_depth - 1  # intermediate levels before leaf
    
    html = ['<table border="1" cellpadding="8" cellspacing="0" style="border-collapse:collapse">']
    
    if title:
        html.append(f'<caption><strong>{title}</strong></caption>')
    
    html.append('<thead>')
    
    # Generate header rows for each grouping level
    for level in range(group_depth):
        html.append('<tr>')
        if level == 0:
            html.append(f'<th rowspan="{group_depth + 1}">Item</th>')
        
        # Track which group labels we've already shown at this level
        seen_labels = set()
        
        # Group columns by their path up to this level
        current_group_key = None
        group_start_idx = 0
        
        for idx, path in enumerate(col_paths):
            # Get the group identifier at this level (tuple of all keys up to this level)
            group_key = path[:level + 1]
            
            # Check if we've moved to a new group
            if group_key != current_group_key:
                # Output colspan for previous group
                if current_group_key is not None:
                    colspan = idx - group_start_idx
                    group_label = current_group_key[level]
                    
                    # Check if this label has been shown before at this level
                    if group_label in seen_labels:
                        # This is a repetition - leave blank with no border
                        html.append(f'<th colspan="{colspan}" style="border-bottom:none"></th>')
                    else:
                        # First occurrence - show the label
                        html.append(f'<th colspan="{colspan}">{group_label}</th>')
                        seen_labels.add(group_label)
                
                # Start new group
                current_group_key = group_key
                group_start_idx = idx
        
        # Don't forget the last group
        if current_group_key is not None:
            colspan = len(col_paths) - group_start_idx
            group_label = current_group_key[level]
            
            # Check if this label has been shown before at this level
            if group_label in seen_labels:
                # This is a repetition - leave blank with no border
                html.append(f'<th colspan="{colspan}" style="border-bottom:none"></th>')
            else:
                # First occurrence - show the label
                html.append(f'<th colspan="{colspan}">{group_label}</th>')
                seen_labels.add(group_label)
        
        html.append('</tr>')
    
    # Column leaf header row
    html.append('<tr>')
    if group_depth == 0:
        html.append('<th>Item</th>')
    for path in col_paths:
        leaf_key = path[-1]
        html.append(f'<th>{leaf_key}</th>')
    html.append('</tr>')
    
    html.append('</thead>')
    
    # Body rows
    html.append('<tbody>')
    for row_key in row_keys:
        html.append('<tr>')
        html.append(f'<td><strong>{row_key}</strong></td>')
        for col_path in col_paths:
            val = get_value(data, row_key, col_path)
            html.append(f'<td style="text-align:right">{val}</td>')
        html.append('</tr>')
    html.append('</tbody>')
    html.append('</table>')
    
    return '\n'.join(html)


def main():
    import argparse
    parser = argparse.ArgumentParser(
        description='Convert nested JSON to HTML table'
    )
    parser.add_argument(
        'input', nargs='?', default='-',
        help='Input JSON file (default: stdin)'
    )
    parser.add_argument(
        '-o', '--output', default=None,
        help='Output HTML file (default: stdout)'
    )
    parser.add_argument(
        '-t', '--title', default=None,
        help='Table title'
    )
    parser.add_argument(
        '-k', '--key', default=None,
        help='Restrict to a single top-level JSON key'
    )
    
    args = parser.parse_args()
    
    # Read JSON
    if args.input == '-':
        data = json.load(sys.stdin)
    else:
        with open(args.input) as f:
            data = json.load(f)
    
    # Extract specific key if requested
    if args.key:
        if args.key not in data:
            print(f"Error: Key '{args.key}' not found in JSON", file=sys.stderr)
            sys.exit(1)
        data = data[args.key]
    
    # Generate HTML
    html = generate_html_table(data, title=args.title)
    
    # Write output
    if args.output:
        with open(args.output, 'w') as f:
            f.write(html)
    else:
        print(html)


if __name__ == '__main__':
    main()
