#!/usr/bin/env python3
"""
Integration tests for the TQL interpreter.
Builds the interpreter once, runs each test case, and compares output
line-by-line against the stored expected CSV.

Usage:
    python3 run_tests.py          # run all tests
    python3 run_tests.py --build  # force rebuild before running
    python3 run_tests.py <name>   # run only tests whose name contains <name>
"""

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).parent
PROJ = ROOT / "interpreter" / "TqlInterpreter.csproj"
TQL  = ["dotnet", "run", "--project", str(PROJ), "-c", "Release", "--no-build", "--"]

TESTS = [
    {
        "name":     "finale",
        "script":   "run-cms-finale.tql",
        "input":    "test-data/cms-finale-marks.csv",
        "table":    "marks",
        "output":   "report",
        "expected": "test-data/cms-finale-report.expected.csv",
    },
    {
        "name":     "elimination",
        "script":   "run-cms-elimination.tql",
        "input":    "test-data/cms-elimination-marks.csv",
        "table":    "marks",
        "output":   "report",
        "expected": "test-data/cms-elimination-report.expected.csv",
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
        ["dotnet", "build", str(PROJ), "-c", "Release", "--nologo", "-v", "quiet"],
        capture_output=True, text=True
    )
    if r.returncode != 0:
        print(r.stdout, r.stderr)
        sys.exit(f"Build failed (exit {r.returncode})")
    print("Build OK\n")

def run_test(t: dict):
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

    actual   = result.stdout.splitlines()
    expected = (ROOT / t["expected"]).read_text().splitlines()

    check(f"{name}: row count ({len(actual)} rows)",
          len(actual) == len(expected),
          f"expected {len(expected)}, got {len(actual)}")

    mismatches = [
        (i + 1, exp, act)
        for i, (exp, act) in enumerate(zip(expected, actual))
        if exp != act
    ]
    check(f"{name}: output matches expected",
          len(mismatches) == 0,
          "\n        ".join(
              f"line {ln}: expected {e!r}\n                  got     {a!r}"
              for ln, e, a in mismatches[:5]
          ))

# ── main ──────────────────────────────────────────────────────────────────────

def main():
    force_build = "--build" in sys.argv
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
        run_test(t)

    print()
    print("=" * 44)
    total = PASS + FAIL
    print(f"  Results:  {PASS} passed,  {FAIL} failed  (of {total})")
    print("=" * 44)
    sys.exit(0 if FAIL == 0 else 1)

if __name__ == "__main__":
    main()
