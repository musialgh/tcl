#!/usr/bin/env python3
"""
Integration tests for the TQL interpreter.
Builds the interpreter once, runs each test case, and compares output
against compact expected CSVs containing only placement rows. Comparison uses
the data columns from each expected CSV and ignores row order and reason text.

Usage:
    python3 run-tests.py          # run all tests
    python3 run-tests.py --build  # force rebuild before running
    python3 run-tests.py --html   # retain HTML reports beside test inputs
    python3 run-tests.py <name>   # run only tests whose name contains <name>
"""

import csv
import io
import os
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).parent
PROJ = ROOT / "interpreter" / "TqlInterpreter.csproj"
TQL  = ["dotnet", "run", "--project", str(PROJ), "--no-build", "--"]
CSV_TO_HTML = ROOT / "csv-to-html.py"

TESTS = [
    {
        "name":     "finale-example-m",
        "script":   "run-cms-finale.tql",
        "input":    "test-data/cms-finale-example-m-marks.csv",
        "table":    "marks",
        "output":   "report",
        "expected": "test-data/cms-finale-example-m-report.expected.csv",
    },
    {
        "name":     "finale-example-p",
        "script":   "run-cms-finale.tql",
        "input":    "test-data/cms-finale-example-p-marks.csv",
        "table":    "marks",
        "output":   "report",
        "expected": "test-data/cms-finale-example-p-report.expected.csv",
    },
    {
        "name":     "elimination",
        "script":   "run-cms-elimination.tql",
        "input":    "test-data/cms-elimination-marks.csv",
        "table":    "marks",
        "output":   "report",
        "expected": "test-data/cms-elimination-report.expected.csv",
    },
    {
        "name":     "finale-example-n",
        "script":   "run-cms-finale.tql",
        "input":    "test-data/cms-finale-example-n-marks.csv",
        "table":    "marks",
        "output":   "report",
        "expected": "test-data/cms-finale-example-n-report.expected.csv",
    },
]

# ── helpers ────────────────────────────────────────────────────────────────────

PASS = FAIL = 0

def check(name: str, ok: bool, detail: str = ""):
    global PASS, FAIL
    if ok:
        print(f"  PASS  {name}")
        PASS += 1
    else:
        print(f"  FAIL  {name}" + (f"\n        {detail}" if detail else ""))
        FAIL += 1

def build():
    print("Building interpreter...")
    r = subprocess.run(
        ["dotnet", "build", str(PROJ), "--nologo", "-v", "quiet"],
        capture_output=True, text=True
    )
    if r.returncode != 0:
        print(r.stdout, r.stderr)
        sys.exit(f"Build failed (exit {r.returncode})")
    print("Build OK\n")

def html_report_path(t: dict) -> Path:
    input_path = ROOT / t["input"]
    stem = input_path.stem
    report_stem = f"{stem[:-len('-marks')]}-report" if stem.endswith("-marks") else f"{stem}-report"
    return input_path.with_name(f"{report_stem}.html")


def retain_html_report(t: dict, csv_output: str):
    output_path = html_report_path(t)
    result = subprocess.run(
        [sys.executable, str(CSV_TO_HTML), "-", "-o", str(output_path), "-t", t["name"]],
        input=csv_output, capture_output=True, text=True, cwd=ROOT
    )
    ok = result.returncode == 0 and output_path.is_file() and output_path.stat().st_size > 0
    check(f"{t['name']}: HTML report written", ok,
          result.stderr.strip() or f"expected non-empty output file: {output_path}")
    if ok:
        print(f"        {output_path}")


def run_test(t: dict, keep_html: bool = False):
    cmd = TQL + [
        t["script"],
        "-r", t["input"], "-i", t["table"],
        "-w", "-", "-o", t["output"],
    ]
    result = subprocess.run(cmd, capture_output=True, text=True, cwd=ROOT)

    name = t["name"]
    check(f"{name}: exit code 0", result.returncode == 0,
          f"stderr: {result.stderr.strip()}")
    if result.returncode != 0:
        return

    if keep_html:
        retain_html_report(t, result.stdout)

    actual_reader = csv.DictReader(io.StringIO(result.stdout), delimiter=";")
    expected_path = ROOT / t["expected"]
    with expected_path.open(newline="") as stream:
        expected_reader = csv.DictReader(stream, delimiter=";")
        columns = expected_reader.fieldnames or []
        expected_rows = list(expected_reader)

    missing_columns = [column for column in columns if column not in (actual_reader.fieldnames or [])]
    if missing_columns:
        check(f"{name}: placement data columns", False,
              f"missing output columns: {', '.join(missing_columns)}")
        return

    placement_keys = {row["key"] for row in expected_rows}
    expected = Counter(tuple(row[column] for column in columns) for row in expected_rows)
    actual = Counter(
        tuple(row[column] for column in columns)
        for row in actual_reader
        if row["key"] in placement_keys
    )

    missing = expected - actual
    extra = actual - expected

    def describe(rows: Counter) -> list[str]:
        descriptions = []
        for values, count in rows.items():
            data = ", ".join(f"{column}={value!r}" for column, value in zip(columns, values))
            descriptions.append(f"{count}x ({data})")
        return descriptions

    detail = "\n        ".join(
        [f"missing: {row}" for row in describe(missing)[:5]]
        + [f"extra:   {row}" for row in describe(extra)[:5]]
    )
    check(f"{name}: placement rows match expected data",
          not missing and not extra, detail)


def run_cli_path_test():
    """CLI paths must be relative to the caller's working directory."""
    with tempfile.TemporaryDirectory(prefix="tql-cli-paths-") as directory:
        cwd = Path(directory)
        script = os.path.relpath(ROOT / "run-cms-finale.tql", cwd)
        input_file = os.path.relpath(ROOT / "test-data/cms-finale-example-m-marks.csv", cwd)
        output_file = cwd / "report.csv"
        cmd = TQL + [
            script,
            "-r", input_file, "-i", "marks",
            "-w", output_file.name, "-o", "report",
        ]
        result = subprocess.run(cmd, capture_output=True, text=True, cwd=cwd)

        check("cli-relative-paths: exit code 0", result.returncode == 0,
              f"stderr: {result.stderr.strip()}")
        check("cli-relative-paths: output below caller cwd",
              output_file.is_file() and output_file.stat().st_size > 0,
              f"expected non-empty output file: {output_file}")

# ── main ──────────────────────────────────────────────────────────────────────

def main():
    force_build = "--build" in sys.argv
    keep_html = "--html" in sys.argv
    filter_args = [a for a in sys.argv[1:] if not a.startswith("--")]

    if force_build:
        build()
    else:
        # quick check: does the binary exist?
        dll = PROJ.parent / "bin" / "Release" / "net10.0" / "tql.dll"
        if not dll.exists():
            build()

    tests = TESTS
    if filter_args:
        tests = [t for t in TESTS if any(f in t["name"] for f in filter_args)]
        if not tests:
            sys.exit(f"No tests matched: {filter_args}")

    print("==> TQL interpreter tests\n")
    for t in tests:
        run_test(t, keep_html)
    if not filter_args:
        run_cli_path_test()

    print()
    print("=" * 44)
    total = PASS + FAIL
    print(f"  Results:  {PASS} passed,  {FAIL} failed  (of {total})")
    print("=" * 44)
    sys.exit(0 if FAIL == 0 else 1)

if __name__ == "__main__":
    main()
