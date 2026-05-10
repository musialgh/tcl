#!/usr/bin/env bash
# ============================================================================
#  Integration test runner for the TCL interpreter
#
#  Usage:   ./run_tests.sh                      (from repo root)
#
#  Each test case is a triple:
#      <function_name>  <input.json>  <expected_output.json>
#
#  Adding a test: just append a line to the TESTS array below and drop
#  the corresponding *_in.json / *_out.json files into tcltests/.
# ============================================================================

set -euo pipefail

# ---------- locate directories ------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/../.."
INTERPRETER_DIR="$REPO_ROOTtcl"
TEST_DIR="$INTERPRETER_DIR/tests"

# ---------- test definitions --------------------------------------------------
# Format:  "function_name|input_file|expected_output_file"

TESTS=(
  "evaluate_dance_elimination|test_elimination_dance_001_in.json|test_elimination_dance_001_out.json"
  "evaluate_dance_final|test_final_dance_001_in.json|test_final_dance_001_out.json"
  "wrap_round_final_skating|test_final_round_tso_P_skating_in.json|test_final_round_tso_P_skating_out.json"
)

# ---------- build once --------------------------------------------------------

echo "==> Building interpreter..."
dotnet build "$INTERPRETER_DIR/TclInterpreter.csproj" -c Release --nologo -v quiet 2>/dev/null
echo

# ---------- run tests ---------------------------------------------------------

PASS=0
FAIL=0
ERRORS=""

for entry in "${TESTS[@]}"; do
  IFS='|' read -r func infile outfile <<< "$entry"

  input_path="$TEST_DIR/$infile"
  expected_path="$TEST_DIR/$outfile"

  printf "  %-50s " "$func ($infile)"

  # Verify files exist
  if [[ ! -f "$input_path" ]]; then
    echo "SKIP (missing $infile)"
    continue
  fi
  if [[ ! -f "$expected_path" ]]; then
    echo "SKIP (missing $outfile)"
    continue
  fi

  # Run the interpreter
  actual=$(dotnet run --project "$INTERPRETER_DIR/TclInterpreter.csproj" \
    -c Release --no-build -- \
    "$func" \
    --input-file "$input_path" \
    --script "$REPO_ROOT/adjudication.tcl" \
    --script "$REPO_ROOT/cross-majority-skating.tcl" \
    --script "$REPO_ROOT/cms-round-wrapper.tcl" \
    ) || {
      echo "FAIL (non-zero exit)"
      FAIL=$((FAIL + 1))
      ERRORS+="  FAIL: $func ($infile) — interpreter returned non-zero exit code\n"
      continue
    }

  # Compare (normalised: strip trailing whitespace / ensure final newline)
  expected=$(cat "$expected_path")

  if diff -u --label "expected ($outfile)" --label "actual" \
       <(echo "$expected") <(echo "$actual") > /dev/null 2>&1; then
    echo "OK"
    PASS=$((PASS + 1))
  else
    echo "FAIL (output mismatch)"
    FAIL=$((FAIL + 1))
    ERRORS+="  FAIL: $func ($infile)\n"
    # Show diff for diagnostics
    diff -u --label "expected ($outfile)" --label "actual" \
         <(echo "$expected") <(echo "$actual") || true
    echo
  fi
done

# ---------- summary -----------------------------------------------------------

echo
echo "============================================"
echo "  Results:  $PASS passed,  $FAIL failed"
echo "============================================"

if [[ $FAIL -gt 0 ]]; then
  echo
  printf "$ERRORS"
  exit 1
fi
