#!/usr/bin/env python3
"""Convert Excalidraw ER submodels to a C# EF Core model skeleton.

The converter is deliberately dependency-free.  It treats the arrow bindings in
the source as connectivity (never as direction), validates the drawing before
writing output, and emits diagnostics suitable for correcting a source drawing.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import json
import math
import re
import shlex
import sys
import textwrap
from dataclasses import dataclass, field
from pathlib import Path

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
    attributes: list[str] = field(default_factory=list)

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

def attribute_names(texts: list[str]) -> list[str]:
    """Split attribute labels, preserving underscore-prefixed continuations."""
    names = []
    for text in texts:
        for raw_line in text.splitlines():
            line = raw_line.strip()
            if not line:
                continue
            if line.startswith("_") and names:
                names[-1] += line
            else:
                names.append(line)
    return names

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
    derivations: dict[str, list[str]] = {}
    loaded = {}
    for path in paths:
        elements = load(path, diagnostics)
        if elements is not None:
            loaded[path] = elements

    owner_colors: dict[Path, str] = {}
    color_owners: dict[str, Path] = {}
    for path, elements in loaded.items():
        solid_colors = {
            e.get("strokeColor", "").lower() for e in elements
            if e.get("type") == "rectangle" and e.get("strokeStyle") == "solid"
        }
        if len(solid_colors) != 1:
            diagnostics.append(Diagnostic(
                "MODEL_COLOR", f"export has {len(solid_colors)} solid rectangle-border colors; expected exactly one",
                Ref(path.name, "file")))
            continue
        owner_color = solid_colors.pop()
        if owner_color in color_owners:
            diagnostics.append(Diagnostic(
                "MODEL_COLOR_DUPLICATE",
                f"solid rectangle-border color {owner_color} also owns {color_owners[owner_color].name}",
                Ref(path.name, "file")))
            continue
        owner_colors[path] = owner_color
        color_owners[owner_color] = path

    for path, elements in loaded.items():
        # Definitions are collected before external occurrences are validated;
        # input order must not affect diagnostics.
        if path in owner_colors:
            labels0 = {e["containerId"]: e.get("text", "") for e in elements if e.get("type") == "text" and e.get("containerId")}
            for e in elements:
                if e.get("type") == "rectangle" and e.get("strokeStyle") == "solid":
                    n = logical(labels0.get(e["id"], "")); owner_color = owner_colors[path]
                    if not n: continue
                    if n in entities:
                        diagnostics.append(Diagnostic("ENTITY_DUPLICATE", f"duplicate local definition of {n}", Ref(path.name, e["id"])))
                    else: entities[n] = Entity(n, owner_color, Ref(path.name, e["id"]))
    for path in paths:
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
            observed = e.get("strokeColor", "").lower()
            if e.get("strokeStyle") == "solid":
                # Definitions were collected in a first pass.
                pass
            else:
                if observed not in color_owners: diagnostics.append(Diagnostic("COLOR_UNKNOWN", f"external stroke {observed} has no solid-rectangle owner", Ref(path.name, e["id"])))
                if n in entities:
                    entities[n].occurrences.append(Ref(path.name, e["id"]))
                    if observed != entities[n].owner:
                        diagnostics.append(Diagnostic("ENTITY_OWNER", f"external entity {n} has owner color {observed}, expected {entities[n].owner}", Ref(path.name, e["id"])))
                else: diagnostics.append(Diagnostic("ENTITY_MISSING", f"external entity {n} has no local definition", Ref(path.name, e["id"])))
        cards = [e for e in elements if e.get("type") == "text" and e.get("text", "").strip() in CARD_TOKENS]
        arrows = [e for e in elements if e.get("type") == "arrow"]
        derivation_arrows = set()
        for triangle in (e for e in elements if e.get("type") == "line"):
            points = triangle.get("points") or []
            if len(points) < 3: continue
            vertices = [(triangle["x"]+q[0], triangle["y"]+q[1]) for q in points[:3]]
            tc = (sum(q[0] for q in vertices)/3, sum(q[1] for q in vertices)/3)
            base = min(rects, key=lambda r: distance_point_box(tc, r))
            apex = min(vertices, key=lambda q: math.dist(q, center(base)))
            children=[]
            for a in arrows:
                for side,key in enumerate(("startBinding","endBinding")):
                    if (a.get(key) or {}).get("elementId") == base["id"] and math.dist(endpoint_point(a,side),apex) < 45:
                        other_id=(a.get(("endBinding","startBinding")[side]) or {}).get("elementId"); other=ix.get(other_id)
                        if other and other.get("type") == "rectangle":
                            derivation_arrows.add(a["id"]); children.append(label(other))
            if children: derivations[label(base)] = sorted(set(children))
        relation_by_diamond={}; entity_endpoints=[]; attribute_by_owner={}
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
            if a["id"] in derivation_arrows: continue
            diamonds = [t for _,t in ends if t.get("type") == "diamond"]
            # An ellipse carries attributes for the shape at the other end.
            # Attribute attachments are not relationships and therefore have no
            # entity cardinality labels.
            if any(t.get("type") == "ellipse" for _, t in ends):
                ellipse = next(t for _,t in ends if t.get("type") == "ellipse")
                owner = next(t for _,t in ends if t.get("type") != "ellipse")
                attrs = attribute_names(labels.get(ellipse["id"], []))
                attribute_by_owner.setdefault(owner["id"], []).extend(attrs)
                for attr in attrs:
                    diagnostics.append(Diagnostic("ATTRIBUTE_TYPE_PENDING", f"attribute {attr} has no modeled type", Ref(path.name,ellipse["id"]), severity="warning"))
                continue
            entities_on = [(side,t) for side,t in ends if t.get("type") == "rectangle" and label(t) in entities]
            if not entities_on: continue
            if diamonds:
                d = diamonds[0]; name = label(d) or None
                rel = relation_by_diamond.get(d["id"])
                if rel is None:
                    rel = Relationship(name, [], Ref(path.name, d["id"])); relation_by_diamond[d["id"]]=rel; all_rels.append(rel)
                for side,t in entities_on:
                    ep=Endpoint(Ref(path.name,a["id"]), side, t, label(t)); rel.endpoints.append(ep); entity_endpoints.append((a,ep))
            elif len(entities_on) == 2:
                eps=[Endpoint(Ref(path.name,a["id"]), s, t, label(t)) for s,t in entities_on]
                all_rels.append(Relationship(None, eps, Ref(path.name,a["id"])))
                entity_endpoints.extend((a,ep) for ep in eps)
        # Cardinality assignment is local to each selected drawing and global over
        # all candidate endpoint neighborhoods, preventing label reuse.
        if entity_endpoints:
            cost = []
            for a,ep in entity_endpoints:
                q = endpoint_point(a, ep.side); row=[]
                for c in cards:
                    d = math.hypot(q[0]-(c["x"]+c["width"]/2), q[1]-(c["y"]+c["height"]/2))
                    row.append(d if d <= 100 else 10000.)
                cost.append(row + [10000.]*len(entity_endpoints))
            match,total = hungarian(cost)
            used=set()
            for i,(a,ep) in enumerate(entity_endpoints):
                j = match[i]
                if j is None or j >= len(cards) or cost[i][j] >= 10000:
                    diagnostics.append(Diagnostic("CARDINALITY_MISSING", f"no cardinality assigned at {ep.entity}", Ref(path.name,a["id"])))
                else:
                    ep.card = cards[j]["text"].strip(); used.add(j)
            for j,c in enumerate(cards):
                if j not in used: diagnostics.append(Diagnostic("CARDINALITY_UNUSED", f"unused cardinality {c['text']}", Ref(path.name,c["id"])))
        for owner_id,attrs in attribute_by_owner.items():
            if owner_id in relation_by_diamond: relation_by_diamond[owner_id].attributes.extend(attrs)
            elif owner_id in labels and label(ix[owner_id]) in entities: entities[label(ix[owner_id])].attributes.extend(attrs)
        # Direct entity attributes are keyed by rectangle ID and handled above.
        for owner_id,attrs in attribute_by_owner.items():
            owner=ix.get(owner_id)
            if owner and owner.get("type")=="rectangle" and label(owner) in entities:
                entities[label(owner)].attributes.extend(a for a in attrs if a not in entities[label(owner)].attributes)
    merged={}
    for rel in all_rels:
        key=(rel.name,tuple(sorted(ep.entity for ep in rel.endpoints)))
        prior=merged.get(key)
        if prior is None: merged[key]=rel
        elif sorted((e.entity,e.card) for e in prior.endpoints)!=sorted((e.entity,e.card) for e in rel.endpoints):
            diagnostics.append(Diagnostic("RELATIONSHIP_CONFLICT", f"conflicting repeated relationship {rel.name or key[1]}", rel.source))
    return entities, list(merged.values()), derivations

def plural(name):
    return {"Person":"People","Country":"Countries"}.get(name, name[:-1]+"ies" if name.endswith("y") else name+"s")

def generate(entities, relationships, derivations, namespace, entity_base, virtual_default, virtual_overrides, command):
    strategies = {base: virtual_overrides.get(base, virtual_default) for base in derivations}
    omitted, child_to_base = set(), {}
    for base, children in derivations.items():
        if strategies[base] == "base-and-kind":
            omitted.update(children)
            child_to_base.update((child, base) for child in children)
        else:
            omitted.add(base)
    classes = [name for name in sorted(entities) if name not in omitted]

    def mapped(name):
        return child_to_base.get(name, name)

    def type_name(name):
        return pascal(mapped(name))

    def retained_targets(name, trail=()):
        if name in trail:
            return []
        if name not in derivations or strategies[name] == "base-and-kind":
            return [name]
        result = []
        for child in derivations[name]:
            for target in retained_targets(child, trail + (name,)):
                if target not in result:
                    result.append(target)
        return result

    def sql_identifier(name):
        return '"' + name.replace('"', '""') + '"'

    def csharp_string(value):
        return json.dumps(value, ensure_ascii=False)

    def command_comment_lines(value, width=120):
        """Render a shell-equivalent command in a C-style comment at `width`."""
        prefix, first, continuation = " * ", "Command: ", "    "
        lines, current = [], prefix + first
        for token in shlex.split(value):
            rendered = shlex.join([token])
            separator = "" if current == prefix + first else " "
            if current != prefix + first and len(current) + len(separator) + len(rendered) > width:
                lines.append(current)
                current, separator = prefix + continuation, ""
            current += separator + rendered
        lines.append(current)
        return lines

    lines = ["/*", f" * Generated: {datetime.now().astimezone().isoformat(timespec='seconds')}",
             *command_comment_lines(command.replace('*/', '* /')), " */", "#nullable enable",
             "using Microsoft.EntityFrameworkCore;", "using System;", "using System.Collections.Generic;", "",
             f"namespace {namespace};", "", f"public abstract class {entity_base}", "{", "}", "",
             f"public abstract class {entity_base}WithId : {entity_base}", "{",
             "    public Guid Id { get; set; }", "}", ""]

    for base, children in sorted(derivations.items()):
        if strategies[base] in ("base-and-kind", "id-and-kind"):
            enum_name = pascal(base) + "Kind"
            lines += [f"public enum {enum_name}", "{"]
            enum_targets = children if strategies[base] == "base-and-kind" else retained_targets(base)
            for value, child in enumerate(sorted(enum_targets), 1):
                lines.append(f"    {pascal(child)} = {value},")
            lines += ["}", ""]

    props = {name: [] for name in classes}
    used = {name: set() for name in classes}
    configs, joins, todos = [], [], []

    def add_prop(entity, base, text):
        entity = mapped(entity)
        if entity not in props:
            return None
        member, suffix = base, 2
        while member in used[entity]:
            member = f"{base}{suffix}"
            suffix += 1
        used[entity].add(member)
        props[entity].append(text.replace("$", member))
        return member

    def child_kind(entity):
        base = child_to_base.get(entity)
        return (base, pascal(base) + "Kind", pascal(entity)) if base else None

    def index_key(fields, filter_sql=None):
        return (tuple(fields), filter_sql)

    for rel in relationships:
        eps = rel.endpoints
        binary_many_to_many = (
            len(eps) == 2
            and CARD.get(eps[0].card, (0, None))[1] is None
            and CARD.get(eps[1].card, (0, None))[1] is None
        )
        virtual_endpoint = any(
            ep.entity in derivations and strategies.get(ep.entity) != "base-and-kind"
            for ep in eps)
        explicit = len(eps) != 2 or bool(rel.attributes) or virtual_endpoint or binary_many_to_many
        if explicit:
            base_name = pascal(rel.name or "".join(sorted(pascal(ep.entity) for ep in eps)))
            join_name, suffix = base_name, 2
            while join_name in {join["name"] for join in joins}:
                join_name = f"{base_name}{suffix}"
                suffix += 1
            join = {"name": join_name, "fields": [], "fks": [], "shadows": [], "checks": [],
                    "indexes": set(), "key": []}
            counts, seen, identities = {}, {}, []
            for ep in eps:
                counts[ep.entity] = counts.get(ep.entity, 0) + 1

            def explicit_relationship_comment(position):
                cards = [eps[position].card]
                targets = []
                for other_position, other in enumerate(eps):
                    if other_position == position:
                        continue
                    cards.append(other.card)
                    if pascal(other.entity) not in targets:
                        targets.append(pascal(other.entity))
                return f"    // relationship {':'.join(cards)} to {', '.join(targets)}"

            def join_reference_comment(entity, cardinality):
                return f"    // {pascal(entity)} at cardinality {cardinality}"

            def inverse_collection_base(position, role):
                if len(eps) == 2 and eps[0].entity == eps[1].entity and rel.name:
                    return pascal(rel.name) + ("Targets" if role == "Source" else "Sources")
                return plural(join_name)

            for position, ep in enumerate(eps):
                seen[ep.entity] = seen.get(ep.entity, 0) + 1
                role = (("Source" if seen[ep.entity] == 1 else "Target")
                        if counts[ep.entity] > 1 else "")
                strategy = strategies.get(ep.entity)
                if strategy == "nullable-fks":
                    alternatives, present, matching = [], [], []
                    base_id = role + pascal(ep.entity) + "Id"
                    join["fields"] += [join_reference_comment(ep.entity, ep.card),
                                       f"    public Guid {base_id} {{ get; set; }}"]
                    join["key"].append(base_id)
                    for child in retained_targets(ep.entity):
                        prop_name = role + pascal(child)
                        target_type = type_name(child)
                        join["fields"] += [join_reference_comment(child, ep.card),
                                           f"    public Guid? {prop_name}Id {{ get; set; }}",
                                           f"    public {target_type}? {prop_name} {{ get; set; }}"]
                        inverse = add_prop(child, inverse_collection_base(position, role),
                                           explicit_relationship_comment(position) + "\n"
                                           f"    public ICollection<{join_name}> $ {{ get; }} = [];")
                        join["fks"].append((target_type, prop_name, (prop_name + "Id",), inverse, False, None, set()))
                        condition = f"{sql_identifier(prop_name + 'Id')} IS NOT NULL"
                        alternatives.append(((prop_name + "Id",), condition))
                        present.append(f"CASE WHEN {sql_identifier(prop_name + 'Id')} IS NULL THEN 0 ELSE 1 END")
                        matching.append(
                            f"({condition} AND {sql_identifier(base_id)} = {sql_identifier(prop_name + 'Id')})")
                    identities.append(alternatives)
                    join["checks"].append((f"CK_{join_name}_{pascal(ep.entity)}_ExactlyOne",
                                           f"({' + '.join(present)} = 1) AND ({' OR '.join(matching)})"))
                elif strategy == "id-and-kind":
                    prop_name = role + pascal(ep.entity) + "Target"
                    enum_name = pascal(ep.entity) + "Kind"
                    join["fields"] += [join_reference_comment(ep.entity, ep.card),
                                       f"    public Guid {prop_name}Id {{ get; set; }}",
                                       f"    public {enum_name} {prop_name}Kind {{ get; set; }}",
                                       f"    // TODO: Validate that {prop_name}Id identifies an entity of {prop_name}Kind."]
                    identities.append([((prop_name + "Kind", prop_name + "Id"), None)])
                    join["key"].extend((prop_name + "Kind", prop_name + "Id"))
                    allowed = ", ".join(str(i) for i in range(1, len(retained_targets(ep.entity)) + 1))
                    join["checks"].append((f"CK_{join_name}_{prop_name}Kind",
                                           f"{sql_identifier(prop_name + 'Kind')} IN ({allowed})"))
                else:
                    prop_name = role + pascal(ep.entity)
                    target_type = type_name(ep.entity)
                    kind = child_kind(ep.entity)
                    join["fields"].append(join_reference_comment(ep.entity, ep.card))
                    join["fields"].append(f"    public Guid {prop_name}Id {{ get; set; }}")
                    fk_fields = (prop_name + "Id",)
                    principal = None
                    if kind:
                        base, enum_name, enum_value = kind
                        kind_prop = prop_name + "Kind"
                        join["shadows"].append((kind_prop, enum_name, enum_value))
                        fk_fields = (prop_name + "Id", kind_prop)
                        principal = ("Id", "Kind")
                        join["checks"].append((f"CK_{join_name}_{kind_prop}",
                                               f"{sql_identifier(kind_prop)} = {sorted(derivations[base]).index(ep.entity) + 1}"))
                    join["fields"].append(f"    public {target_type} {prop_name} {{ get; set; }} = null!;")
                    inverse = add_prop(ep.entity, inverse_collection_base(position, role),
                                       explicit_relationship_comment(position) + "\n"
                                       f"    public ICollection<{join_name}> $ {{ get; }} = [];")
                    join["fks"].append((target_type, prop_name, fk_fields, inverse, True, principal,
                                        {field for field, _, _ in join["shadows"] if field in fk_fields}))
                    identities.append([(fk_fields, None)])
                    join["key"].extend(fk_fields)
            for attr in rel.attributes:
                join["fields"].append(f"    // Attribute: {attr}")

            # Every explicit association row is unique by its complete semantic
            # participant tuple. Nullable polymorphic targets need one filtered
            # unique index for each concrete branch.
            combinations = [([], [])]
            for alternatives in identities:
                combinations = [(fields + list(alt_fields), filters + ([flt] if flt else []))
                                for fields, filters in combinations
                                for alt_fields, flt in alternatives]
            for fields, filters in combinations:
                join["indexes"].add(index_key(fields, " AND ".join(filters) or None))

            # An upper bound of one on an endpoint means the identities of all
            # other participants must be unique.
            for position, ep in enumerate(eps):
                if CARD.get(ep.card, (0, None))[1] != 1:
                    continue
                combinations = [([], [])]
                for other, alternatives in enumerate(identities):
                    if other == position:
                        continue
                    combinations = [(fields + list(alt_fields), filters + ([flt] if flt else []))
                                    for fields, filters in combinations
                                    for alt_fields, flt in alternatives]
                for fields, filters in combinations:
                    if fields:
                        join["indexes"].add(index_key(fields, " AND ".join(filters) or None))
            join["indexes"].discard(index_key(join["key"]))
            joins.append(join)
            for ep in eps:
                if ep.card == "1..*":
                    todos.append(f"Cardinality 1..* for {join_name}.{pascal(ep.entity)} requires existence validation.")
            continue

        a, b = eps
        ac, bc = type_name(a.entity), type_name(b.entity)
        a_many = CARD.get(a.card, (0, None))[1] is None
        b_many = CARD.get(b.card, (0, None))[1] is None
        prefix = pascal(rel.name) if rel.name else ""
        if not (a_many and b_many):
            many, one = (a, b) if a_many else ((b, a) if b_many else (None, None))
            if many:
                mn, on = mapped(many.entity), mapped(one.entity)
                mc, oc = type_name(many.entity), type_name(one.entity)
                optional = one.card == "0..1"
                nullable, init = ("?", "") if optional else ("", " = null!;")
                ref_base = prefix + pascal(one.entity)
                kind = child_kind(one.entity)
                ref = add_prop(mn, ref_base,
                               f"    // relationship {many.card}:{one.card}\n"
                               f"    public {oc}{nullable} $ {{ get; set; }}{init}\n"
                               f"    public Guid{nullable} $Id {{ get; set; }}")
                fk_fields, principal = ((ref + "Id",), None) if ref else ((), None)
                if ref and kind:
                    base, enum_name, enum_value = kind
                    kind_prop = ref + "Kind"
                    fk_fields, principal = (ref + "Id", kind_prop), ("Id", "Kind")
                    configs.append(("shadow", mc, kind_prop, enum_name, enum_value))
                    configs.append(("check", mc, f"CK_{mc}_{kind_prop}",
                                    f"{sql_identifier(kind_prop)} = {sorted(derivations[base]).index(one.entity) + 1}"))
                col = add_prop(on, prefix + plural(pascal(many.entity)),
                               f"    // relationship {one.card}:{many.card}\n"
                               f"    public ICollection<{mc}> $ {{ get; }} = [];")
                if ref and col:
                    shadow_fields = {kind_prop} if ref and kind else set()
                    configs.append(("om", mc, ref, oc, col, not optional, fk_fields, principal, shadow_fields))
                if many.card == "1..*":
                    todos.append(f"Cardinality 1..* for {oc}.{col} requires existence validation.")
            else:
                # The current model explicitly selects Account as the dependent
                # in its independently optional Person/Account relationship.
                dep, pri = (b, a) if b.entity == "account" else sorted((a, b), key=lambda ep: ep.entity)
                dn, pn = mapped(dep.entity), mapped(pri.entity)
                dc, pc = type_name(dep.entity), type_name(pri.entity)
                optional = pri.card == "0..1"
                nullable, init = ("?", "") if optional else ("", " = null!;")
                ref = add_prop(dn, prefix + pascal(pri.entity),
                               f"    // relationship {dep.card}:{pri.card}\n"
                               f"    public {pc}{nullable} $ {{ get; set; }}{init}\n"
                               f"    public Guid{nullable} $Id {{ get; set; }}")
                inv = add_prop(pn, prefix + pascal(dep.entity),
                               f"    // relationship {pri.card}:{dep.card}\n"
                               f"    public {dc}? $ {{ get; set; }}")
                if ref and inv:
                    configs.append(("oo", dc, ref, pc, inv, not optional))

    lines += ["// Explicit entities", ""]
    for name in classes:
        class_name = pascal(name)
        lines += [f"public partial class {class_name} : {entity_base}WithId", "{"]
        if name in derivations and strategies[name] == "base-and-kind":
            lines.append(f"    public {class_name}Kind Kind {{ get; set; }}")
        lines += props[name] or ["    // No modeled members beyond Id."]
        for attr in entities[name].attributes:
            lines.append(f"    // Attribute: {attr}")
        lines += ["}", ""]
    lines += ["// Join entities", ""]
    for join in joins:
        lines += [f"public partial class {join['name']} : {entity_base}", "{"] + join["fields"] + ["}", ""]

    lines += ["public static class ModelConfiguration", "{",
              "    public static void Configure(ModelBuilder modelBuilder)", "    {"]
    for base, children in sorted(derivations.items()):
        if strategies[base] == "base-and-kind":
            class_name = pascal(base)
            allowed = ", ".join(str(i) for i in range(1, len(children) + 1))
            lines.append(f"        modelBuilder.Entity<{class_name}>().HasAlternateKey(e => new {{ e.Id, e.Kind }});")
            sql = f"{sql_identifier('Kind')} IN ({allowed})"
            lines.append(f"        modelBuilder.Entity<{class_name}>().ToTable(t => t.HasCheckConstraint(\"CK_{class_name}_Kind\", {csharp_string(sql)}));")
    for config in configs:
        if config[0] == "om":
            _, dependent, nav, principal_type, inverse, required, fk_fields, principal_fields, shadow_fields = config
            fk = (", ".join(csharp_string(field) for field in fk_fields) if shadow_fields else
                  (f"e => e.{fk_fields[0]}" if len(fk_fields) == 1
                   else "e => new { " + ", ".join("e." + field for field in fk_fields) + " }"))
            principal = ("" if not principal_fields else
                         ".HasPrincipalKey(e => new { " + ", ".join("e." + field for field in principal_fields) + " })")
            lines.append(f"        modelBuilder.Entity<{dependent}>().HasOne(e => e.{nav}).WithMany(e => e.{inverse}).HasForeignKey({fk}){principal}.IsRequired({str(required).lower()}).OnDelete(DeleteBehavior.Restrict);")
            lines.append(f"        modelBuilder.Entity<{dependent}>().HasIndex({fk});")
        elif config[0] == "shadow":
            _, entity, property_name, enum_name, enum_value = config
            lines.append(f"        modelBuilder.Entity<{entity}>().Property<{enum_name}>({csharp_string(property_name)}).HasDefaultValue({enum_name}.{enum_value});")
        elif config[0] == "oo":
            _, dependent, nav, principal_type, inverse, required = config
            lines.append(f"        modelBuilder.Entity<{dependent}>().HasOne(e => e.{nav}).WithOne(e => e.{inverse}).HasForeignKey<{dependent}>(e => e.{nav}Id).IsRequired({str(required).lower()}).OnDelete(DeleteBehavior.Restrict);")
            lines.append(f"        modelBuilder.Entity<{dependent}>().HasIndex(e => e.{nav}Id).IsUnique();")
        else:
            _, entity, name, sql = config
            lines.append(f"        modelBuilder.Entity<{entity}>().ToTable(t => t.HasCheckConstraint(\"{name}\", {csharp_string(sql)}));")

    for join in joins:
        name = join["name"]
        for property_name, enum_name, enum_value in join["shadows"]:
            lines.append(f"        modelBuilder.Entity<{name}>().Property<{enum_name}>({csharp_string(property_name)}).HasDefaultValue({enum_name}.{enum_value});")
        key_shadow_fields = {field for field, _, _ in join["shadows"]}
        key = (", ".join(csharp_string(field) for field in join["key"])
               if any(field in key_shadow_fields for field in join["key"]) else
               (f"e => e.{join['key'][0]}" if len(join["key"]) == 1
                else "e => new { " + ", ".join("e." + field for field in join["key"]) + " }"))
        lines.append(f"        modelBuilder.Entity<{name}>().HasKey({key});")
        for target, nav, fk_fields, inverse, required, principal_fields, shadow_fields in join["fks"]:
            fk = (", ".join(csharp_string(field) for field in fk_fields) if shadow_fields else
                  (f"e => e.{fk_fields[0]}" if len(fk_fields) == 1
                   else "e => new { " + ", ".join("e." + field for field in fk_fields) + " }"))
            principal = ("" if not principal_fields else
                         ".HasPrincipalKey(e => new { " + ", ".join("e." + field for field in principal_fields) + " })")
            inverse_call = f"e => e.{inverse}" if inverse else ""
            lines.append(f"        modelBuilder.Entity<{name}>().HasOne(e => e.{nav}).WithMany({inverse_call}).HasForeignKey({fk}){principal}.IsRequired({str(required).lower()}).OnDelete(DeleteBehavior.Restrict);")
            lines.append(f"        modelBuilder.Entity<{name}>().HasIndex({fk});")
        for check_name, sql in join["checks"]:
            lines.append(f"        modelBuilder.Entity<{name}>().ToTable(t => t.HasCheckConstraint(\"{check_name}\", {csharp_string(sql)}));")
        for fields, filter_sql in sorted(join["indexes"], key=lambda item: (item[0], item[1] or "")):
            shadow_fields = {field for field, _, _ in join["shadows"]}
            expression = (", ".join(csharp_string(field) for field in fields)
                          if any(field in shadow_fields for field in fields) else
                          (f"e => e.{fields[0]}" if len(fields) == 1
                           else "e => new { " + ", ".join("e." + field for field in fields) + " }"))
            suffix = ".IsUnique()"
            if filter_sql:
                suffix += f".HasFilter({csharp_string(filter_sql)})"
            lines.append(f"        modelBuilder.Entity<{name}>().HasIndex({expression}){suffix};")
    for todo in sorted(set(todos)):
        lines.append(f"        // TODO: {todo}")
    lines += ["    }", "}", ""]
    return "\n".join(lines)

def main(argv=None):
    ap = argparse.ArgumentParser(
        description="Convert a selected set of Excalidraw ER submodels into a C# EF Core model skeleton.",
        epilog="Inputs default to model/*.excalidraw. Diagnostics are always printed to stdout. "
               "The converter validates the input even when --output is omitted; omit --output "
               "to perform a diagnostics-only run.",
        formatter_class=HelpFormatter)
    ap.add_argument("inputs", nargs="*", type=Path,
                    help="Excalidraw export paths (one export per color; default: model/*.excalidraw).")
    ap.add_argument("-o", "--output", type=Path, metavar="FILE",
                    help="write generated C# to FILE; if omitted, do not generate a model file.")
    ap.add_argument("-n", "--namespace", default="GeneratedModel", metavar="NAME",
                    help="C# namespace for generated types (default: GeneratedModel).")
    ap.add_argument("-e", "--entity_base", default="EntityBase", metavar="NAME",
                    help="base class name for generated entities, containing the Id property (default: EntityBase).")
    ap.add_argument("-b", "--virtual-base", action="append", default=[], metavar="STRATEGY|NAME=STRATEGY",
                    help="repeatable virtual-base configuration. Without NAME=, STRATEGY sets the global default\n"
                         "(nullable-fks when omitted). NAME=STRATEGY overrides one named virtual base.\n"
                         "Strategies:\n"
                         "  id-and-kind   one target ID plus a type enum\n"
                         "  nullable-fks  one nullable FK per target type\n"
                         "  base-and-kind one base entity plus a type enum")
    args = ap.parse_args(argv)
    paths = args.inputs or sorted(Path("model").glob("*.excalidraw")); diagnostics=[]
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
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", args.entity_base):
        diagnostics.append(Diagnostic("OPTION", f"invalid C# entity base name {args.entity_base}"))
    entities, relationships, derivations = parse(paths, diagnostics)
    for name in sorted(set(overrides) - set(derivations)):
        diagnostics.append(Diagnostic("OPTION", f"unknown virtual base {name}"))
    collapse_owner = {}
    for base, children in derivations.items():
        strategy = overrides.get(base, virtual_base_strategy)
        if strategy != "base-and-kind":
            continue
        for child in children:
            previous = collapse_owner.setdefault(child, base)
            if previous != base:
                diagnostics.append(Diagnostic(
                    "OPTION", f"base-and-kind would collapse {child} into both {previous} and {base}"))
    generated_names = {pascal(name) for name in entities}
    if args.entity_base in generated_names:
        diagnostics.append(Diagnostic("OPTION", f"entity base name {args.entity_base} conflicts with a generated entity"))
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*", args.namespace):
        diagnostics.append(Diagnostic("OPTION", f"invalid C# namespace {args.namespace}"))
    errors = [d for d in diagnostics if d.severity == "error"]
    if errors:
        for d in diagnostics: print(d.line())
        return 2
    for d in diagnostics: print(d.line())
    if args.output is None:
        print("OK: diagnostics-only run; model generation suppressed")
        return 0
    raw_argv = list(sys.orig_argv) if argv is None else [sys.executable, ap.prog, *argv]
    command = shlex.join(raw_argv)
    text = generate(entities, relationships, derivations, args.namespace, args.entity_base,
                    virtual_base_strategy, overrides, command)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    tmp = args.output.with_suffix(args.output.suffix + ".tmp")
    tmp.write_text(text, encoding="utf-8"); tmp.replace(args.output)
    print(f"wrote {args.output} ({len(entities)} entities, {len(relationships)} relationship occurrences)")
    return 0

if __name__ == "__main__": raise SystemExit(main())
