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
#   4: FUNCTION bar(x, y = 0):
#   5:     bar(
DOC = (
    "SEL\n"
    "FUNCTION foo():\n"
    "    FOR x IN marks:\n"
    "    xyz\n"
    "FUNCTION bar(x, y = 0):\n"
    "    bar("
)

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
# id=6: go-to-definition on "bar" identifier in the call on line 5, col 4
inp += rpc({"jsonrpc": "2.0", "id": 6, "method": "textDocument/definition",
            "params": {"textDocument": {"uri": "file:///test.tql"},
                       "position": {"line": 5, "character": 4}}})
# id=7: go-to-definition on unknown word "xyz" (line 3, col 4) → null
inp += rpc({"jsonrpc": "2.0", "id": 7, "method": "textDocument/definition",
            "params": {"textDocument": {"uri": "file:///test.tql"},
                       "position": {"line": 3, "character": 4}}})
# id=8: signatureHelp inside bar( at end of line 5 (col 8, just after the open paren)
inp += rpc({"jsonrpc": "2.0", "id": 8, "method": "textDocument/signatureHelp",
            "params": {"textDocument": {"uri": "file:///test.tql"},
                       "position": {"line": 5, "character": 8}}})
# id=9: shutdown
inp += rpc({"jsonrpc": "2.0", "id": 9, "method": "shutdown", "params": {}})
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
check("initialize: definitionProvider == true",
      caps.get("definitionProvider") is True)
check("initialize: signatureHelpProvider declared",
      "signatureHelpProvider" in caps)

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
           "SUM", "COUNT", "AVG", "MEDIAN",
           "GLOBAL", "ABORT"]:
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

# go-to-definition — known function "bar" (line 4, FUNCTION bar(...))
r6 = responses.get(6, {}).get("result")
check("definition('bar'): result is non-null", r6 is not None)
if r6 is not None:
    check("definition('bar'): points to line 4",
          r6.get("range", {}).get("start", {}).get("line") == 4,
          f"got: {r6}")
    check("definition('bar'): uri is the test document",
          r6.get("uri") == "file:///test.tql", f"got: {r6}")

# go-to-definition — unknown word "xyz"
r7 = responses.get(7, {}).get("result")
check("definition('xyz'): result is null", r7 is None)

# signatureHelp — inside bar( at col 8
r8 = responses.get(8, {}).get("result")
check("signatureHelp('bar('): result is non-null", r8 is not None)
if r8 is not None:
    sigs = r8.get("signatures", [])
    check("signatureHelp('bar('): one signature returned", len(sigs) == 1,
          f"got: {sigs}")
    if sigs:
        label = sigs[0].get("label", "")
        check("signatureHelp('bar('): label contains 'bar'",
              label.startswith("bar("), f"got: {label!r}")
        check("signatureHelp('bar('): label contains parameter 'x'",
              "x" in label, f"got: {label!r}")
        check("signatureHelp('bar('): label contains 'y = 0'",
              "y = 0" in label, f"got: {label!r}")
    check("signatureHelp('bar('): activeSignature == 0",
          r8.get("activeSignature") == 0)
    check("signatureHelp('bar('): activeParameter == 0",
          r8.get("activeParameter") == 0)

# shutdown
check("shutdown: result is null",
      responses.get(9, {}).get("result") is None)

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
