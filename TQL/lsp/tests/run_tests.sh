#!/usr/bin/env bash
# ============================================================================
#  Integration test runner for the TQL LSP server
#
#  Usage:   ./tcltests/run_tests.sh    (from repo root)
#        or ./run_tests.sh                      (from this directory)
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$SCRIPT_DIR/../tql-lsp.csproj"

echo "==> Building TQL LSP server..."
dotnet build "$PROJ" -c Release --nologo -v quiet
echo

exec python3 "$SCRIPT_DIR/run_tests.py"
