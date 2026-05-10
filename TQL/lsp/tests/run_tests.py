#!/usr/bin/env python3
"""
Integration tests for the TQL LSP server.
Runs one server process, exchanges a fixed sequence of JSON-RPC messages,
and asserts specific fields in the responses.
"""
import json
import subprocess
import sys
from pathlib import Path

PROJ = Path(__file__).parent.parent / "tql-lsp.csproj"
SERVER = ["dotnet", "run", "--project", str(PROJ), "-c", "Release", "--no-build"]

# ---- helpers -----------------------------------------------------------------

PASS = FAIL = 0

def check(name: str, cond: bool, detail: str = ""):
    global PASS, FAIL
    if cond:
        print(f"  PASS  {name}")
        PASS += 1
    else:
        print(f"  FAIL  {name}" + (f"\n        {detail}" if detail else ""))
        FAIL += 1

def rpc(obj: dict) -> bytes:
    body = json.dumps(obj).encode()
    return b"Content-Length: " + str(len(body)).encode() + b"\r\n\r\n" + body

def parse_responses(raw: bytes) -> dict:
    """Parse all Content-Length-framed responses; return dict keyed by id."""
    out = {}
    while raw:
        hend = raw.index(b"\r\n\r\n")
        cl = int(next(
            l for l in raw[:hend].decode().split("\r\n")
            if l.lower().startswith("content-length:")
        ).split(":", 1)[1].strip())
        r = json.loads(raw[hend + 4 : hend + 4 + cl])
        if "id" in r:
            out[r["id"]] = r
        raw = raw[hend + 4 + cl:]
    return out

# ---- build server input -------------------------------------------------------

# Document text (split into lines for reference):
#   0: SEL
#   1: FUNCTION foo():
#   2:     FOR x IN marks:
#   3:     xyz
DOC = "SEL\nFUNCTION foo():\n    FOR x IN marks:\n    xyz\n"

inp = b""
inp += rpc({"jsonrpc": "2.0", "id": 1, "method": "initialize",
            "params": {"capabilities": {}}})
inp += rpc({"jsonrpc": "2.0", "method": "initialized", "params": {}})
inp += rpc({"jsonrpc": "2.0", "method": "textDocument/didOpen", "params": {
    "textDocument": {"uri": "file:///test.tql", "languageId": "tql",
                     "version": 1, "text": DOC}
}})
# id=2: completion at end of "SEL" (line 0, col 3)
inp += rpc({"jsonrpc": "2.0", "id": 2, "method": "textDocument/completion",
            "params": {"textDocument": {"uri": "file:///test.tql"},
                       "position": {"line": 0, "character": 3}}})
# id=3: completion at start of line (empty prefix → all keywords)
inp += rpc({"jsonrpc": "2.0", "id": 3, "method": "textDocument/completion",
            "params": {"textDocument": {"uri": "file:///test.tql"},
                       "position": {"line": 0, "character": 0}}})
# id=4: hover on "FUNCTION" (line 1, col 4 = inside the word)
inp += rpc({"jsonrpc": "2.0", "id": 4, "method": "textDocument/hover",
            "params": {"textDocument": {"uri": "file:///test.tql"},
                       "position": {"line": 1, "character": 4}}})
# id=5: hover on unknown word "xyz" (line 3, col 4)
inp += rpc({"jsonrpc": "2.0", "id": 5, "method": "textDocument/hover",
            "params": {"textDocument": {"uri": "file:///test.tql"},
                       "position": {"line": 3, "character": 4}}})
# id=6: shutdown
inp += rpc({"jsonrpc": "2.0", "id": 6, "method": "shutdown", "params": {}})
inp += rpc({"jsonrpc": "2.0", "method": "exit", "params": {}})

# ---- run server --------------------------------------------------------------

proc = subprocess.run(SERVER, input=inp, capture_output=True)
responses = parse_responses(proc.stdout)

# ---- assertions --------------------------------------------------------------

print()
print("==> TQL LSP server tests")
print()

# initialize
caps = responses.get(1, {}).get("result", {}).get("capabilities", {})
check("initialize: textDocumentSync.change == 1",
      caps.get("textDocumentSync", {}).get("change") == 1)
check("initialize: completionProvider declared",
      "completionProvider" in caps)
check("initialize: hoverProvider == true",
      caps.get("hoverProvider") is True)

# completion — specific prefix
items2 = [i["label"] for i in responses.get(2, {}).get("result", {}).get("items", [])]
check("completion(prefix='SEL'): SELECT is returned",
      "SELECT" in items2, f"got: {items2}")
check("completion(prefix='SEL'): no other keywords returned",
      all(k.upper().startswith("SEL") for k in items2), f"got: {items2}")

# completion — empty prefix (all keywords)
r3     = responses.get(3, {}).get("result", {})
items3 = [i["label"] for i in r3.get("items", [])]
check("completion(empty prefix): isIncomplete == false",
      r3.get("isIncomplete") is False)
for kw in ["SELECT", "FUNCTION", "FOR", "WHILE", "IF", "LET",
           "UPDATE", "INSERT", "CREATE", "DROP", "ADD", "IMPORT",
           "PLACE_MIN", "PLACE_MAX", "SUM", "COUNT", "AVG", "MEDIAN"]:
    check(f"completion(empty prefix): includes {kw}", kw in items3)

# hover — known keyword
r4  = responses.get(4, {}).get("result")
check("hover('FUNCTION'): result is non-null", r4 is not None)
if r4 is not None:
    val = r4.get("contents", {}).get("value", "")
    check("hover('FUNCTION'): kind is markdown",
          r4.get("contents", {}).get("kind") == "markdown")
    check("hover('FUNCTION'): content starts with **FUNCTION**",
          val.strip().startswith("**FUNCTION**"), f"got: {val[:80]!r}")

# hover — unknown word
r5 = responses.get(5, {}).get("result")
check("hover('xyz'): result is null", r5 is None)

# shutdown
check("shutdown: result is null",
      responses.get(6, {}).get("result") is None)

# exit code
check("exit: server exited with code 0",
      proc.returncode == 0, f"exit code: {proc.returncode}")

# ---- summary -----------------------------------------------------------------

total = PASS + FAIL
print()
print("=" * 44)
print(f"  Results:  {PASS} passed,  {FAIL} failed  (of {total})")
print("=" * 44)

sys.exit(0 if FAIL == 0 else 1)
