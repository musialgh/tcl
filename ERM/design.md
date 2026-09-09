# ERM to EF Core Skeleton Converter

## Requirements

### Purpose and scope

- `erm_converter.py` is a standalone, dependency-free Python converter from a
  selected set of Excalidraw ER submodel exports to a C# EF Core model skeleton.
  Parsing, validation, reconciliation, cardinality assignment, and generation
  are script capabilities; developers do not need an AI agent to run it.
- The converter produces a skeleton only. It does not generate a `DbContext`,
  connection string, migrations, dependency-injection setup, scalar properties
  for untyped attributes, or database data.
- The general CLI contract is documented in `README.md`. Diagnostics always go
  to standard output. Omitting `--output` performs validation only.

### Input conventions and identity

- Inputs are Excalidraw exports. By default the converter selects
  `model/*.excalidraw`; explicit paths replace that selection. Each selected
  export is identified by the semantic color name at the start of its filename.
- Solid rectangles are local entity definitions and dashed rectangles are
  external occurrences. An external occurrence must resolve to exactly one
  local definition with the same logical name and owner color. Repeated external
  occurrences do not create entities.
- The current exports use the following source-color aliases:

  | Color name | Excalidraw stroke color |
  | --- | --- |
  | black | `#1e1e1e` |
  | blue | `#1971c2` |
  | gray | `#868e96` |
  | green | `#2f9e44` |
  | orange | `#f08c00` |
  | purple | `#be4bdb`, `#9c36b5` |
  | red | `#e03131` |

  The `purple-alt` alias is an external-occurrence color for the purple
  submodel. Unknown colors and owner-color mismatches are errors.
- Logical entity and relationship names remove whitespace and source line
  breaks. Thus `competitor_status` followed by `_event` in an entity label is
  `competitor_status_event`. Attribute labels use a different rule below.
- All model connectors are headless Excalidraw `arrow` elements. Arrow end order
  provides connectivity only and has no semantic direction. Closed triangular
  `line` elements denote derivation, not relationships.
- Diamonds denote relationships and ellipses denote attributes. A connector to
  an ellipse is an attribute attachment, never an entity relationship.

### Relationships and cardinality

- A diamond may be unnamed. A supplied relationship name identifies the
  relationship/join table; an unnamed join uses sorted canonical participant
  type names. Distinct named relationships must remain distinct.
- Merge repeated relationship occurrences only when their names, canonical
  participant identities, roles, and cardinalities are compatible. A named and
  unnamed occurrence are not assumed to be the same relationship. Conflicts are
  errors.
- Cardinality tokens are `1`, `0..1`, `1..*`, and `*`. Their labels are assigned
  jointly to nearby relationship endpoints, without reuse. Missing or unused
  labels are errors.
- A self-relationship with no source roles is directed technically as Source and
  Target using stable source evidence. The direction expresses storage only; it
  does not imply a domain meaning such as parent/child. The current `part_of`
  relationship maps to `PartOfTargets` and `PartOfSources` with
  `SourceOrganizationId` and `TargetOrganizationId`.

### Attribute handling

- Preserve each untyped source attribute as one `// Attribute: <name>` comment
  on the owning entity or explicit association class, and emit a non-blocking
  `ATTRIBUTE_TYPE_PENDING` diagnostic. Do not guess typed properties.
- Place attribute comments after all generated properties in their owning class.
- Each nonempty attribute-label line starts an attribute unless it starts with
  `_`. An underscore-prefixed line is appended to the preceding attribute name;
  for example, `contribution` followed by `_type` becomes `contribution_type`.
- Attribute presence requires an explicit association class even while its
  property types remain pending. No scalar types are available in the current
  drawings.

### C# output and naming

- Generate only when `--output FILE` is supplied. Write atomically after all
  error diagnostics pass, so a failed conversion cannot replace a valid output.
- Begin generated files with an auto-generated notice, local timestamp including
  UTC offset, and the shell-quoted command line. The timestamp intentionally
  changes output bytes; the model body must otherwise be semantically stable.
- The generated namespace defaults to `GeneratedModel`. Classes and members use
  PascalCase after normalizing logical underscore/whitespace-separated names.
  Use deterministic plural collections, including `Person` / `People` and
  `Country` / `Countries`; resolve C# member collisions deterministically.
- Generate nullable-enabled, public partial classes independent of host implicit
  usings. Every explicitly generated class inherits `Guid Id` from the common
  base named by `-e/--entity_base` (default `EntityBase`). This base is reserved
  for future common behavior such as soft deletion.
- Initialize collection navigations as `ICollection<T> ... { get; } = [];`.
  Required references use `T` and `= null!;`; optional references use `T?` and
  nullable `Guid?` foreign keys.
- Generate a `ModelConfiguration.Configure(ModelBuilder)` helper. The host calls
  it from `OnModelCreating`. The generated Fluent API uses `DeleteBehavior.Restrict`
  until lifecycle rules are modeled.

### EF Core mapping rules

| ER relationship | C# representation and mapping |
| --- | --- |
| One-to-many | Reference/FK on the many side, inverse collection, `HasOne` / `WithMany`, requiredness from cardinality |
| One-to-one | Two references, selected dependent FK, `HasOne` / `WithOne`, unique FK index |
| Binary many-to-many without payload | Two skip collections and an EF-managed named join with explicit FK names and composite key |
| Binary relationship with attributes | Explicit association entity with references/FKs, payload comments, and participant uniqueness |
| Ternary or higher relationship | Explicit association entity with each participant reference/FK and cardinality-derived uniqueness |

- Explicit association entities have their own `Id`; EF-managed skip-navigation
  joins use their participant FK composite key. Explicit association rows are
  unique by their full participant tuple. If an endpoint has upper bound one,
  the other participant identities are also unique.
- Generate individual FK indexes and explicit unique indexes. For a nullable
  polymorphic target group, generate one filtered unique index per target branch
  instead of a naive nullable composite unique index.
- Generate Fluent configuration for navigations, foreign keys, alternate keys,
  many-to-many joins, indexes, and checks. PostgreSQL-oriented check constraints
  enforce enum ranges, fixed derived kinds, and exactly-one nullable target
  choices.
- `1..*` collection minima and principal-side existence requirements cannot be
  guaranteed by navigation declarations. Emit generated TODO comments; enforce
  them through application or database rules when the domain policy is known.
- The person/account one-to-one maps `Account` as dependent. `Account.PersonId`
  is nullable and unique, and both references are optional.

### Virtual bases

Each derivation base supports these strategies through repeatable
`-b/--virtual-base` settings:

1. `id-and-kind`: retain concrete entities, store target ID plus kind enum, and
   leave target existence/type validation to application or database logic.
2. `nullable-fks`: retain concrete entities and create one nullable typed FK per
   target, with an exactly-one check for each required polymorphic reference.
3. `base-and-kind`: retain only the base entity with a kind enum. References to
   a derived type use the base ID/kind alternate key and a fixed-kind check.
   The referencing CLR class exposes only the meaningful ID and navigation; the
   fixed kind is an EF Core shadow property with a default, check, and composite
   foreign key.

One bare strategy sets the default; `NAME=STRATEGY` overrides individual bases.
The default is `nullable-fks`. Reject unknown bases, invalid strategies, and any
`base-and-kind` combination that would collapse one concrete entity into more
than one base.

The current mapping is:

| Base | Strategy | Retained representation |
| --- | --- | --- |
| organization | base-and-kind | `Organization` plus `OrganizationKind.Club` / `.Federation` |
| result_phase | nullable-fks | Round / RoundDance target FKs |
| result_context | nullable-fks | Competition / CompetitionCall target FKs |
| result_owner | nullable-fks | Competitor / Participant target FKs |
| with_external | nullable-fks | Concrete external-target FKs |
| scope | nullable-fks | Competition / Event / Floor / Organization target FKs |

Nested virtual targets expand to their retained concrete representation. For
`nullable-fks`, an association row has exactly one target FK in its group; for
`id-and-kind`, validation remains a documented TODO because one ID cannot be a
normal FK to several tables.

### Current explicit associations

The current mapping generates explicit association classes for payload,
ternary, and polymorphic relationships:

`ContributesEv`, `MarkingEligible`, `ParticipantDefinition`,
`ParticipantMember`, `CompetitorMember`, `CompStartnoStack`,
`CompetitionDance`, `ContributesCo`, `MapsTo`, `AccountRoleScope`,
`ResultResultOwner`, `ResultResultContext`, `ResultResultPhase`, and
`EvaluationResultOwner`.

With the selected mapping, this produces 55 explicit CLR entity classes. Pure
binary many-to-many relationships are additional EF-managed shared join types.

### Validation and maintenance

- Diagnostics must contain source file and element IDs where applicable. Errors
  prevent output; warnings, including untyped attributes, do not.
- The converter must not use stale `boundElements` back-references as graph
  edges. Live arrow endpoint bindings are authoritative.
- Keep this file compact and current. Add durable requirements, mappings, and
  unresolved limitations here. Put developer usage in `README.md`; do not keep
  completed plans, resolved issues, or chronology.
- The generated model has been compile-checked and EF-model-built for the
  selected mapping, global `nullable-fks`, and global `id-and-kind`. A global
  `base-and-kind` mapping is correctly rejected for overlapping derivations.
  Focused fixture coverage and live PostgreSQL verification remain future work.

## References

- `00-legend-2026-09-08-1616.excalidraw` and
  `00-legend-2026-09-08-1616.png` describe the current drawing legend.
- [EF Core relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/)
- [EF Core many-to-many](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many)
- [PostgreSQL partial indexes](https://www.postgresql.org/docs/current/indexes-partial.html)
