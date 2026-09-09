#!/usr/bin/env python3
"""Convert Excalidraw ER submodels to a C# EF Core model skeleton.

The converter is deliberately dependency-free.  It treats the arrow bindings in
the source as connectivity (never as direction), validates the drawing before
writing output, and emits diagnostics suitable for correcting a source drawing.
"""
from __future__ import annotations

import argparse
import json
import math
import re
import sys
import textwrap
from dataclasses import dataclass, field
from pathlib import Path

COLORS = {"black": "#1e1e1e", "blue": "#1971c2", "gray": "#868e96",
          "green": "#2f9e44", "orange": "#f08c00", "purple": "#be4bdb",
          "red": "#e03131", "purple-alt": "#9c36b5"}
CARD = {"1": (1, 1), "0..1": (0, 1), "1..*": (1, None), "*": (0, None)}
CARD_TOKENS = set(CARD)

class HelpFormatter(argparse.HelpFormatter):
    """Wrap option help at 120 columns while preserving intentional newlines."""
    def __init__(self, prog):
        super().__init__(prog, max_help_position=30, width=120)

    def _split_lines(self, text, width):
        return [line for paragraph in text.splitlines() or [""]
                for line in (textwrap.wrap(paragraph, width) or [""])]

@dataclass(frozen=True)
class Ref:
    file: str
    element: str
    def __str__(self): return f"{self.file}:{self.element}"

@dataclass
class Diagnostic:
    code: str
    message: str
    ref: Ref | None = None
    severity: str = "error"
    def line(self): return f"{self.severity.upper()} {self.code}: {self.message}" + (f" [{self.ref}]" if self.ref else "")

@dataclass
class Entity:
    name: str
    owner: str
    definition: Ref
    occurrences: list[Ref] = field(default_factory=list)

@dataclass
class Endpoint:
    arrow: Ref
    side: int
    target: dict
    entity: str | None = None
    card: str | None = None

@dataclass
class Relationship:
    name: str | None
    endpoints: list[Endpoint]
    source: Ref
    attributes: list[str] = field(default_factory=list)

def logical(text: str) -> str:
    return re.sub(r"\s+", "", text.replace("\r", "").replace("\n", ""))

def pascal(name: str) -> str:
    parts = re.split(r"[^A-Za-z0-9]+", name)
    out = "".join(p[:1].upper() + p[1:] for p in parts if p)
    return out or "Unnamed"

def load(path: Path, diagnostics: list[Diagnostic]):
    try: data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as e:
        diagnostics.append(Diagnostic("INPUT_READ", str(e), Ref(str(path), "file"))); return None
    return [e for e in data.get("elements", []) if not e.get("isDeleted")]

def center(e): return (e["x"] + e.get("width", 0)/2, e["y"] + e.get("height", 0)/2)
def endpoint_point(a, side):
    p = a.get("points") or []
    if not p: return (a.get("x", 0), a.get("y", 0))
    q = p[0 if side == 0 else -1]
    return (a["x"] + q[0], a["y"] + q[1])
def distance_point_box(q, b):
    return math.hypot(max(b["x"]-q[0], 0, q[0]-b["x"]-b.get("width", 0)), max(b["y"]-q[1], 0, q[1]-b["y"]-b.get("height", 0)))

def hungarian(cost):
    """Minimum assignment for rows to columns; rectangular matrices allowed."""
    n, m = len(cost), len(cost[0]) if cost else 0
    if not n: return [], 0
    if m < n: return [None] * n, float("inf")
    u, v, p, way = [0.]*(n+1), [0.]*(m+1), [0]*(m+1), [0]*(m+1)
    for i in range(1, n+1):
        p[0], j0 = i, 0; mins = [float("inf")]*(m+1); used = [False]*(m+1)
        while True:
            used[j0] = True; i0, delta, j1 = p[j0], float("inf"), 0
            for j in range(1, m+1):
                if not used[j]:
                    cur = cost[i0-1][j-1] - u[i0] - v[j]
                    if cur < mins[j]: mins[j], way[j] = cur, j0
                    if mins[j] < delta: delta, j1 = mins[j], j
            for j in range(m+1):
                if used[j]: u[p[j]] += delta; v[j] -= delta
                else: mins[j] -= delta
            j0 = j1
            if not p[j0]: break
        while True:
            j1 = way[j0]; p[j0] = p[j1]; j0 = j1
            if not j0: break
    result = [None]*n
    for j in range(1, m+1):
        if p[j]: result[p[j]-1] = j-1
    return result, sum(cost[i][j] for i,j in enumerate(result))

def parse(paths: list[Path], diagnostics: list[Diagnostic]):
    entities: dict[str, Entity] = {}; all_rels: list[Relationship] = []
    endpoint_groups = []
    selected = {p.name.split("-", 1)[0]: p for p in paths}
    expected = {"black", "blue", "gray", "green", "orange", "purple", "red"}
    for key in expected - selected.keys(): diagnostics.append(Diagnostic("INPUT_MISSING", f"missing {key} export"))
    if len(selected) != len(paths): diagnostics.append(Diagnostic("INPUT_DUPLICATE", "more than one export selected for a color"))
    loaded = {}
    for path in paths:
        elements = load(path, diagnostics)
        if elements is not None: loaded[path] = elements
        # Definitions are collected before external occurrences are validated;
        # input order must not affect diagnostics.
        if elements is not None:
            labels0 = {e["containerId"]: e.get("text", "") for e in elements if e.get("type") == "text" and e.get("containerId")}
            for e in elements:
                if e.get("type") == "rectangle" and e.get("strokeStyle") == "solid":
                    n = logical(labels0.get(e["id"], "")); owner_color = COLORS.get(path.name.split("-",1)[0])
                    if not n: continue
                    if n in entities:
                        diagnostics.append(Diagnostic("ENTITY_DUPLICATE", f"duplicate local definition of {n}", Ref(path.name, e["id"])))
                    else: entities[n] = Entity(n, path.name.split("-",1)[0], Ref(path.name, e["id"]))
    for path in paths:
        color = path.name.split("-", 1)[0]; elements = load(path, diagnostics)
        elements = loaded.get(path)
        if elements is None: continue
        ix = {e["id"]: e for e in elements}; labels = {}
        for e in elements:
            if e.get("type") == "text" and e.get("containerId"):
                labels.setdefault(e["containerId"], []).append(e.get("text", ""))
        def label(e): return logical("".join(labels.get(e["id"], [])))
        rects = [e for e in elements if e.get("type") == "rectangle"]
        for e in rects:
            n = label(e)
            if not n:
                diagnostics.append(Diagnostic("ENTITY_LABEL", "rectangle has no entity label", Ref(path.name, e["id"]))); continue
            owner_color = COLORS.get(color); observed = e.get("strokeColor", "").lower()
            if e.get("strokeStyle") == "solid":
                if observed != owner_color and not (color == "purple" and observed == COLORS["purple-alt"]):
                    diagnostics.append(Diagnostic("ENTITY_COLOR", f"local entity {n} has stroke {observed}, expected {owner_color}", Ref(path.name, e["id"])))
                # Definitions were collected in a first pass.
            else:
                if observed not in COLORS.values(): diagnostics.append(Diagnostic("COLOR_UNKNOWN", f"unknown external stroke {observed}", Ref(path.name, e["id"])))
                if n in entities: entities[n].occurrences.append(Ref(path.name, e["id"]))
                else: diagnostics.append(Diagnostic("ENTITY_MISSING", f"external entity {n} has no local definition", Ref(path.name, e["id"])))
        cards = [e for e in elements if e.get("type") == "text" and e.get("text", "").strip() in CARD_TOKENS]
        entity_endpoints = []
        for a in (e for e in elements if e.get("type") == "arrow"):
            ends = []
            for side, binding_key in enumerate(("startBinding", "endBinding")):
                binding = a.get(binding_key) or {}; target = ix.get(binding.get("elementId"))
                if target is None:
                    diagnostics.append(Diagnostic("ENDPOINT_UNBOUND", "arrow endpoint has no live binding", Ref(path.name, a["id"])))
                    continue
                if target.get("type") == "text" and target.get("containerId") in ix:
                    target = ix[target["containerId"]]
                ends.append((side, target))
            if len(ends) != 2: continue
            diamonds = [t for _,t in ends if t.get("type") == "diamond"]
            entities_on = [(side,t) for side,t in ends if t.get("type") == "rectangle" and label(t) in entities]
            if not entities_on: continue
            if diamonds:
                d = diamonds[0]; name = label(d) or None
                rel = next((r for r in all_rels if r.source == Ref(path.name, d["id"])), None)
                if rel is None: rel = Relationship(name, [], Ref(path.name, d["id"])); all_rels.append(rel)
                for side,t in entities_on: rel.endpoints.append(Endpoint(Ref(path.name,a["id"]), side, t, label(t)))
            elif len(entities_on) == 2:
                all_rels.append(Relationship(None, [Endpoint(Ref(path.name,a["id"]), s, t, label(t)) for s,t in entities_on], Ref(path.name,a["id"])))
            else:
                entity_endpoints.extend((a,side,t) for side,t in entities_on)
        # Cardinality assignment is local to each selected drawing and global over
        # all candidate endpoint neighborhoods, preventing label reuse.
        if entity_endpoints:
            cost = []
            for a,side,t in entity_endpoints:
                q = endpoint_point(a, side); row=[]
                for c in cards:
                    d = math.hypot(q[0]-(c["x"]+c["width"]/2), q[1]-(c["y"]+c["height"]/2))
                    row.append(d if d <= 100 else 10000.)
                cost.append(row + [10000.]*len(entity_endpoints))
            match,total = hungarian(cost)
            for i,(a,side,t) in enumerate(entity_endpoints):
                j = match[i]
                if j is None or j >= len(cards) or cost[i][j] >= 10000:
                    diagnostics.append(Diagnostic("CARDINALITY_MISSING", f"no cardinality assigned at {label(t)}", Ref(path.name,a["id"]), severity="warning"))
                else:
                    value = cards[j]["text"].strip(); entity_endpoints[i][0]["_card"] = value
    # Attach cards to all relationship endpoints by arrow provenance.
    for rel in all_rels:
        for ep in rel.endpoints:
            # A direct relation's endpoint may have been annotated on its arrow.
            source = next((e for p in paths if p.name == ep.arrow.file for e in json.loads(p.read_text())["elements"] if e.get("id") == ep.arrow.element), None)
            ep.card = source.get("_card") if source else None
    return entities, all_rels

def generate(entities, relationships, namespace, virtual_default, virtual_overrides):
    collapsed = {"organization": (virtual_overrides.get("organization") or "base-and-kind")}
    classes = sorted(entities)
    lines = ["// <auto-generated />", "#nullable enable", "using Microsoft.EntityFrameworkCore;", "using System;", "using System.Collections.Generic;", "", f"namespace {namespace};", ""]
    if collapsed.get("organization") == "base-and-kind":
        lines += ["public enum OrganizationKind { Club = 1, Federation = 2 }", ""]
        classes = [c for c in classes if c not in {"club", "federation"}]
    for name in classes:
        cn = pascal(name); lines += [f"public partial class {cn}", "{", "    public Guid Id { get; set; }", ""]
        for r in relationships:
            own = [e for e in r.endpoints if e.entity == name]
            if not own: continue
            other = [e for e in r.endpoints if e.entity != name]
            if len(r.endpoints) == 2 and other:
                target = pascal(other[0].entity); prop = pascal(r.name or target)
                lines += [f"    public ICollection<{target}> {prop}s {{ get; }} = new List<{target}>();"]
        lines += ["}", ""]
    lines += ["public static class ModelConfiguration", "{", "    public static void Configure(ModelBuilder modelBuilder)", "    {", "        // Relationship configuration is emitted here as topology is resolved.", "        // TODO: configure generated association classes and virtual-base checks.", "    }", "}", ""]
    return "\n".join(lines)

def main(argv=None):
    ap = argparse.ArgumentParser(
        description="Convert a selected set of Excalidraw ER submodels into a C# EF Core model skeleton.",
        epilog="Inputs default to Model/*.excalidraw. Diagnostics are always printed to stdout. "
               "The converter validates the input even when --output is omitted; omit --output "
               "to perform a diagnostics-only run.",
        formatter_class=HelpFormatter)
    ap.add_argument("inputs", nargs="*", type=Path,
                    help="Excalidraw export paths (one export per color; default: Model/*.excalidraw).")
    ap.add_argument("-o", "--output", type=Path, metavar="FILE",
                    help="write generated C# to FILE; if omitted, do not generate a model file.")
    ap.add_argument("-n", "--namespace", default="GeneratedModel", metavar="NAME",
                    help="C# namespace for generated types (default: GeneratedModel).")
    ap.add_argument("-b", "--virtual-base", action="append", default=[], metavar="STRATEGY|NAME=STRATEGY",
                    help="repeatable virtual-base configuration. Without NAME=, STRATEGY sets the global default\n"
                         "(nullable-fks when omitted). NAME=STRATEGY overrides one named virtual base.\n"
                         "Strategies:\n"
                         "  id-and-kind   one target ID plus a type enum\n"
                         "  nullable-fks  one nullable FK per target type\n"
                         "  base-and-kind one base entity plus a type enum")
    args = ap.parse_args(argv)
    paths = args.inputs or sorted(Path("Model").glob("*.excalidraw")); diagnostics=[]
    overrides={}
    virtual_base_strategy = "nullable-fks"
    default_seen = False
    for value in args.virtual_base:
        if "=" not in value:
            if value not in ("id-and-kind", "nullable-fks", "base-and-kind"):
                diagnostics.append(Diagnostic("OPTION", f"invalid strategy {value}"))
            elif default_seen:
                diagnostics.append(Diagnostic("OPTION", "global virtual-base strategy supplied more than once"))
            else:
                virtual_base_strategy = value
                default_seen = True
        else:
            n, strategy = value.split("=", 1)
            if not n or strategy not in ("id-and-kind", "nullable-fks", "base-and-kind"):
                diagnostics.append(Diagnostic("OPTION", f"invalid virtual-base setting {value}"))
            elif n in overrides:
                diagnostics.append(Diagnostic("OPTION", f"virtual-base override supplied more than once for {n}"))
            else:
                overrides[n] = strategy
    entities, relationships = parse(paths, diagnostics)
    errors = [d for d in diagnostics if d.severity == "error"]
    if errors:
        for d in diagnostics: print(d.line())
        return 2
    for d in diagnostics: print(d.line())
    if args.output is None:
        print("OK: diagnostics-only run; model generation suppressed")
        return 0
    text = generate(entities, relationships, args.namespace, virtual_base_strategy, overrides)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    tmp = args.output.with_suffix(args.output.suffix + ".tmp")
    tmp.write_text(text, encoding="utf-8"); tmp.replace(args.output)
    print(f"wrote {args.output} ({len(entities)} entities, {len(relationships)} relationship occurrences)")
    return 0

if __name__ == "__main__": raise SystemExit(main())
