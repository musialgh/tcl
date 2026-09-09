# ERM to EF Core Skeleton Converter

* The source application is a management system whose backend shall use EF Core 10 in C#, Npgsql, and PostgreSQL for data storage.
* For the database, an initial ER model has been drawn in Excalidraw.
* This model consists of several submodels, each of which addresses a logical subdomain.
* In this project, a Python script shall be developed by Codex that converts the set of all submodels
  into a skeleton in C# defining the model classes to be used by EF Core 10.

## Requirements

### Scope and generated code

- Deliver a standalone Python converter usable by other developers without AI
  agents. Parsing, reconciliation, geometric assignment, error checking, and C#
  generation must be executable script capabilities, not manual session steps.
- Generate class skeletons in `Implementation/model.cs` when an output path is
  explicitly supplied; omitting `--output` performs validation and diagnostics
  only. Most attributes have not yet been modeled. Use PascalCase for classes
  and members.
- Diagnostics are always printed to standard output. The CLI intentionally has
  no separate verbosity flag: normal output is limited to actionable diagnostics
  and one generation/status line, while `-h` documents all options.
- Each explicitly modeled entity implemented as an actual entity shall have a
  `Guid Id` primary key.
- Collection navigations shall use `ICollection<T>`; single-valued navigations
  and foreign keys shall reflect cardinality and optionality.
- Prefer bidirectional skip navigations (`ICollection<T>` on both sides) for
  binary many-to-many relationships without relationship attributes. Let EF Core
  manage the join entity without a dedicated C# association class. Use explicit
  association classes for relationship attributes and ternary relationships.
  Fluent API configuration does not itself require an association class.
- Include ERM cardinality constraints in the skeleton wherever possible; identify
  constraints requiring additional configuration or application/database checks.
- Continuous regeneration after manual development is not required. After
  attributes are added, the EF classes become the primary source.

### Drawing interpretation and reconciliation

- Submodels are distinguished by semantic color name, matching the first word
  of export filenames. Use the PNG table as the reference. Exact RGB matching
  between Confluence and Excalidraw is unnecessary because their palettes differ.
- External entities have their owning submodel's color and a dashed outline.
  Entities may be repeated within or across submodels for readability without
  creating duplicate semantic entities.
- All model connectors are headless Excalidraw arrows, used for endpoint
  connectivity. Start/end order has no semantic direction. Triangular line
  shapes are derivation notation, not relationship connectors.
- Conflicting definitions of the same entity name are input errors. Report them
  for the user to correct rather than resolving them automatically.
- Relationship diamonds may be unnamed. If a relationship is named, use that
  name for its respective cross table when one is needed.
- Merge repeated occurrences of the same relationship across submodels using
  canonical participating entities plus relationship names and endpoint roles,
  where present. Preserve distinct relationships between the same entity pair,
  including self-references; `pred_sp` and `succ_sp` remain separate.
- Compare roles and cardinalities independently of arrow order. Report conflicts
  or ambiguous identity for user correction; a missing optional name is not
  itself an error.
- When a self-relationship has no explicit endpoint roles, generate a directed
  source/target relationship with a deterministic assignment of ends. No domain
  distinction between visually upper/lower ends is implied. Do not make the
  relationship symmetric or automatically insert its reverse.
- Recognize cardinalities `1`, `0..1`, `1..*`, and `*`. Jointly associate
  all incident relationship endpoints around an entity with all plausible
  cardinality texts using the best geometric combination. Do not use independent
  nearest-label guesses or reuse labels across competing endpoints.

### Virtual bases

Support three representations:

1. Separate derived entities with a polymorphic link expressed by one target ID
   and an enum identifying the target entity type; no separate base entity.
2. Separate derived entities with one nullable foreign-key field per derived
   type; no separate base entity.
3. Only the base is implemented as an actual entity, with derived types
   distinguished by a type enum; no separate derived entities. This supports
   `organization` as one actual entity with its derived kinds as enum values.

Expose one repeatable `-b/--virtual-base` option. One bare strategy sets the
global default; any number of `<logical-name>=<strategy>` values override named
bases. If no bare strategy is supplied, the global default is `nullable-fks`.
Supported strategy values
are `id-and-kind`, `nullable-fks`, and `base-and-kind`. Reject unknown base names,
unknown strategies, and malformed settings. The current model uses
`nullable-fks` as its default, with `organization=base-and-kind`:

`-b nullable-fks -b organization=base-and-kind`

Define integrity constraints and mappings of relationships to derived types
for each representation before generating it.

### Remaining choices and document maintenance

Step 4's current mapping policy and outstanding choices are specified below.

Keep this file as current prompt context. Integrate accepted decisions directly
into the relevant requirements and update affected plan items and findings.
Remove superseded statements and resolved issues. Do not retain development
history or duplicate decision records.

### C# and EF Core mapping policy

Naming and output:

- Put entities, generated enums, and a `TasModelConfiguration.Configure(ModelBuilder)`
  helper in `Implementation/model.cs`. The host calls this helper from its own
  `OnModelCreating`; do not generate a production DbContext, connection string,
  migrations, or dependency-injection setup. The compilation test can supply a
  minimal test context.
- Expose `--namespace`, defaulting to `Tas.Model`. Enable nullable reference types
  in the generated file and use public partial entity classes with public
  properties. Keep generated code independent of host implicit-usings settings.
- Apply the logical-name rule first, then PascalCase underscore/whitespace
  separated words. Preserve abbreviations: `startno_stack` becomes `StartnoStack`,
  not a guessed `StartNumberStack`. Report collisions after normalization.
- Use singular type names and plural collection names, with deterministic
  inflection (`Person` / `People`, `Country` / `Countries`) and explicit naming
  overrides for collisions. A supplied relationship name prefixes a navigation
  when needed to distinguish relations to the same type, such as the two
  timeslot–syncpoint relationships. Preserve original names in source comments.
- For an unnamed join, join sorted canonical participant type names; the current
  ternary join becomes `AccountRoleScope`. Require role/name overrides when this
  cannot distinguish two associations. Do not invent domain roles from arrows.
- For an unlabeled-role self-relationship diamond, sort its two incident entity
  connector IDs within the source file to assign technical Source and Target
  slots. Arrow point reversal leaves those IDs unchanged. For a direct self-loop,
  use a canonical ordering of the two endpoint geometries and retain any required
  tie-break in source metadata. This orientation is a storage convention only;
  ambiguous duplicate self-relationship occurrences still require reconciliation.
- Generate `Organization.PartOfTargets` and `Organization.PartOfSources` as skip
  collections, and a `PartOf` join with `SourceOrganizationId` and
  `TargetOrganizationId`. Do not assert parent/child meaning for either end.
- Map concrete entity tables to their class names; use PascalCase relationship
  names for named join tables and the rule above for unnamed joins. Keep names
  explicit in Fluent API so collection-property changes do not rename tables.

Navigation, foreign-key, and key rules:

| ER relationship | C# representation | EF mapping |
| --- | --- | --- |
| Binary one-to-many | Reference and FK on the many side; inverse collection on the one side | `HasOne` / `WithMany` with explicit FK and requiredness |
| Binary one-to-one | Two references; one FK on the selected dependent | `HasOne` / `WithOne`, explicit dependent and unique FK |
| Binary many-to-many, no payload | Two skip collections | `HasMany` / `WithMany` / named `UsingEntity`; composite join key |
| Binary relationship with payload | Explicit association entity with participant references/FKs and payload properties | Individual foreign keys plus uniqueness constraints preserving the original cardinalities |
| Ternary relationship | Explicit association entity with three participant references/FKs | Three foreign keys; preserve triple identity and any uniqueness implied by multiplicities |

- Initialize collections as `new List<T>()`. Required references use `T` with
  `= null!;`; optional references use `T?`. A required reference may still be
  uninitialized before EF loads it; annotations do not perform loading or validation.
- Use explicit `Guid` / `Guid?` FK properties named `<NavigationName>Id`.
  Nullability reflects whether the related single-valued endpoint is `1` or
  `0..1`. A many-side collection minimum is a separate constraint.
- Model entities and explicit association classes receive `Guid Id`. Pure
  EF-managed joins use the participant FK tuple as their composite primary key
  and do not need an additional ID. Explicit associations additionally require
  unique participant tuples, preserving ER relationship-set semantics.
- For an explicit binary association with a single-valued endpoint, add uniqueness
  on the opposite participant's FK. For ternary upper-bound-one endpoints, the
  other two participant FKs must uniquely determine that endpoint. Do not confuse
  required participation of an existing association row with required existence
  of an association row for every entity.
- The only current one-to-one is person–account (`0..1` / `0..1`). Use
  `Account.PersonId : Guid?`, `Account.Person : Person?`, and
  `Person.Account : Account?`, with a unique FK. This is a storage choice;
  both sides remain independently optional. Future ambiguous one-to-one mappings
  require an explicit dependent selection rather than source-arrow direction.
- Configure each relationship once in Fluent API, pairing navigations explicitly.
  Use `DeleteBehavior.Restrict` initially because lifecycle/cascade semantics are
  absent from the ERM. Deletion policy may be refined by the host application.

Explicit association classes required by the current source:

| Class | Reason |
| --- | --- |
| `ContributesEv` | contribution_type payload |
| `MarkingEligible` | round_type payload |
| `ParticipantDefinition` | conjunction_no payload |
| `ParticipantMember` | person–participant–club triple plus role / seq_no payload |
| `CompetitorMember` | role payload |
| `CompStartnoStack` | purpose / number range payload |
| `CompetitionDance` | seq_no payload |
| `ContributesCo` | contribution_type payload |
| `MapsTo` | external_id payload and virtual target |
| `AccountRoleScope` | account–role–scope triple and virtual target |

Expose collections of these association entities so callers can set their payload
or third participant. Additional skip navigations alongside binary association
classes are supported by EF but are not required for the initial skeleton.
This table does not account for extra association storage that may be required
when lowering a polymorphic target; validate that after selecting a strategy.

Virtual-base lowering and integrity:

- `id-and-kind`: store a target ID plus target-kind enum for each polymorphic
  reference. A single ID cannot have an ordinary FK to several different tables;
  do not generate a fictitious EF navigation to a missing base type. Use scalar
  properties and explicitly report target-existence/type validation as an
  application/trigger responsibility. Optional links require both values null
  or both present; required links require both present and a valid kind.
- `nullable-fks`: expand each polymorphic reference into one nullable FK and typed
  reference per allowed concrete target. Generate real FKs and a check constraint
  requiring exactly one target for a required reference, or zero/one for an
  optional reference. Collections of polymorphic associations may require an
  association row carrying this group of alternative targets.
- `base-and-kind`: emit only the base entity with a kind enum and redirect child
  references to that table, preserving the intended child kind as a constraint.
  This is one C# entity, not EF inheritance with derived CLR classes. A FK to the
  base ID alone cannot enforce the child's kind. Use an alternate `(Id, Kind)`
  key and a composite FK with a fixed-kind check on the referencing row for a
  child-specific reference; a general base reference uses the base ID alone.
  An optional child-specific reference must have both FK components null or
  both present. Preserve `Id` as the entity primary key.
- Represent discriminator enums as C# enums stored as integers with explicit
  values and validity checks initially; PostgreSQL native enum registration is
  not required. Never accept default zero as a valid kind unless explicitly defined.
- Expand nested target groups to their actual retained entity types according to
  the selected strategies. Detect cycles and overlapping collapse groups. Do not
  deduplicate separate relationship roles merely because they map to the same
  retained table.
- Composite uniqueness involving alternative nullable FKs needs strategy-specific
  indexes (for example, filtered unique indexes per target branch), not a naive
  unique tuple containing NULLs. Keep surrogate association IDs in those cases.
- Collection minima (`1..*`), principal-side existence requirements, and target
  existence in `id-and-kind` mode are not enforced by navigation declarations.
  Emit explicit diagnostics/TODOs for constraints outside the generated mapping.

Attribute and polymorphic-association handling:

- Preserve untyped attributes as source-linked TODO comments on their owning
  entity/association class and emit non-blocking `ATTRIBUTE_TYPE_PENDING`
  diagnostics. Do not generate guessed typed properties. Retain multiline groups
  intact where attribute boundaries are unknown, and report
  `ATTRIBUTE_GROUPING_PENDING`. Their presence still requires the association
  class even though payload properties are deferred.
- No scalar types are specified for the 13 attribute groups in the current
  drawings. For example, do not assume role is a string, seq_no is an integer,
  or external_id is a Guid. Type/grouping overrides may supply this information
  later; absent overrides do not block a structural skeleton.
- Each `MapsTo` row has a required `IdspaceId` plus exactly one of its seven
  nullable target FKs. Add one partial unique index per target branch, on
  `(IdspaceId, <TargetId>)` with `<TargetId> IS NOT NULL`.
- Each `AccountRoleScope` row has required `AccountId` and `RoleId` plus exactly
  one of four nullable scope FKs. Add one partial unique index per branch, on
  `(AccountId, RoleId, <ScopeTargetId>)` with `<ScopeTargetId> IS NOT NULL`.
- Each Result's required context, phase, and owner references expands separately
  into an exactly-one-of-two FK group. Evaluation's owner reference similarly
  expands into an exactly-one-of-two group. These many-to-one groups do not need
  per-target uniqueness indexes. Keep distinct navigations and FK names for each
  group, including inverse collections.
- For another selected strategy, recalculate association keys and uniqueness
  from semantic target identity: `(kind, id)` for id-and-kind, or retained base
  ID for base-and-kind. Validate generated PostgreSQL checks and indexes in
  step 6; do not regard C# compilation alone as constraint verification.

Current virtual-base mapping:

| Base | Strategy | Result |
| --- | --- | --- |
| organization | base-and-kind | One `Organization` entity; `Club` and `Federation` are `OrganizationKind` values, not separate entity classes |
| result_phase | nullable-fks | Alternatives Round / RoundDance |
| result_context | nullable-fks | Alternatives Competition / CompetitionCall |
| result_owner | nullable-fks | Alternatives Competitor / Participant |
| with_external | nullable-fks | Alternatives Competition / CompetitionCall / Country / Event / Organization / Participant / Person |
| scope | nullable-fks | Alternatives Competition / Event / Floor / Organization |

The other five base types have no entity classes. A reference to federation or
club maps to an Organization reference constrained to that kind; a general
organization reference accepts either. Emit stable explicit `OrganizationKind`
values (`Club = 1`, `Federation = 2`) and reject other values with a check.
Do not add a third generic Organization kind without a domain requirement.
The selected representation retains 41 of the 48 named semantic entity types:
five virtual bases and two organization-derived types are omitted as classes.
The ten listed association classes are additional, giving 51 entity classes for
the current strategy selection. Untyped payloads do not change this count.

Technical references: [EF Core many-to-many mapping](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many),
[one-to-one mapping](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-one),
[foreign and principal keys](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/foreign-and-principal-keys),
and [value conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions).
See also [PostgreSQL partial indexes](https://www.postgresql.org/docs/current/indexes-partial.html).

### Intermediate model contract

The converter shall keep source extraction, semantic reconciliation, validation,
and C# emission separate. The intermediate model is independent of EF storage
strategy and contains the following records (collections are deterministically
ordered by semantic key, then source reference):

| Record | Fields and purpose |
| --- | --- |
| `SourceRef` | Submodel key, relative source file, element ID. Element IDs are scoped to their file, never assumed globally unique. |
| `DrawingElement` | Source reference, Excalidraw type, raw geometry/style, group IDs, raw bindings, and text where present. Deleted elements are excluded; the raw input remains available for diagnostics. |
| `Label` | Source reference, original text, container reference, logical name when applicable, and resolution evidence. Attribute text remains unsplit until interpretation. |
| `EntityOccurrence` | Rectangle reference, label reference, logical name, owning submodel inferred from the color aliases, local/external status, and canonical entity key. |
| `Entity` | Logical name as canonical key, owning submodel, definition references, occurrence references, attribute references, and derivation participation. No C# name, foreign key, or storage strategy is assigned here. |
| `Connector` | Arrow reference, complete point sequence, and two `Endpoint` records. Endpoint indices preserve source access only, not semantic direction. |
| `Endpoint` | Source endpoint index, raw bound target, resolved shape or null, resolution status (`bound`, `container`, `geometric`, `unresolved`), candidate references, and evidence. |
| `RelationshipOccurrence` | Diamond reference or direct connector reference, optional label/name, connector references, participant slots, attribute references, and resolution status. |
| `ParticipantSlot` | Entity occurrence/key, optional role, incident endpoint references, and multiplicity assignment or null. Keep slots distinct even when they refer to the same entity. |
| `Relationship` | Canonical identity, occurrence references, optional name, participant slots, attributes, and reconciliation status. Unresolved candidates retain source identities and are not prematurely merged. |
| `AttributeGroup` | Ellipse/label references, raw text, owner candidates or resolved entity/relationship, and optional interpreted attributes/types. Unknown types remain null. |
| `Derivation` | Triangle reference, base occurrence/key, child occurrence/keys, connector evidence, and resolution status. Base and child roles may remain unresolved until step 3. |
| `CardinalityLabel` | Text reference, geometry, raw token, parsed lower/upper bounds, and assignment status. An unbounded upper bound is distinct from an unknown assignment. |
| `MultiplicityAssignment` | Participant slot, cardinality-label reference, bounds, assignment method, score/ambiguity evidence. Populated in step 3. |
| `Diagnostic` | Stable code, severity, message, all relevant source references, candidate evidence, and affected stage. Errors prevent downstream emission, but do not discard recoverable source data. |

Label resolution and identity:

- Prefer a live text element's `containerId` for shape labels. Cross-check
  container/back-reference information, but never derive connectivity solely from
  `boundElements`. Multiple conflicting labels require a diagnostic.
- Entity and relationship logical names remove layout line breaks and surrounding
  whitespace while preserving case and other characters. For example,
  `competitor_status\n_event` becomes `competitor_status_event`. This is a source
  identity rule, not PascalCase generation. Preserve the raw label and report
  normalization collisions rather than silently conflating different names.
- Match external occurrences to the unique local definition by logical name,
  validating the owner color. Repetition alone is allowed; disagreement in owner
  or modeled meaning is an error. Relationships present in only one submodel
  supplement the entity and are not conflicting entity definitions.

Endpoint and geometry resolution:

- Prefer each arrow's live `startBinding`/`endBinding` targets. When a target is
  text with a valid shape container, retain the raw target and resolve through
  that container. A free cardinality text is not an entity endpoint.
- For missing, deleted, or semantically unsuitable targets, keep the endpoint
  unresolved with geometric candidates for step 3. Do not invent an attachment
  or interpret a stale back-reference as an additional connection.
- Retain local connector points, position, angle, dimensions, rounding, and group
  membership. Compute world-space geometry with the applicable element transform;
  use rotated shape boundaries rather than only axis-aligned boxes. Group IDs
  provide context, not parent transforms or semantic identity.
- Preserve bends and repeated/zero-length points in source data. Geometric
  scoring uses the nearest non-zero segment when a local endpoint tangent is
  needed. Geometry fallback must record candidate scores and ambiguity.

Relationship reconciliation is staged. Step 2 groups potential repeated
representations by participating canonical entities and available names/roles;
step 3 completes topology and multiplicity validation before a merge is final.
Use an order-independent multiset of participant identities/roles, retaining
self-reference slots. Distinct nonempty names/roles remain distinct. A named and
unnamed occurrence are not automatically equivalent. Compatible anonymous
occurrences may merge only when their identity is unambiguous; conflicting
multiplicities are errors, not a reason to silently create separate relations.
Do not assume every diamond is binary or every rectangle-to-rectangle arrow is
a relationship: derivation classification precedes final relationship creation.

Virtual bases remain ordinary named semantic entities with separate derivation
records. Do not collapse children, synthesize foreign keys/enums, or select an
EF representation in this model. This preserves all three required strategies.

### Connectivity and cardinality algorithm

- Classify derivation connectors before cardinality matching. A triangle's
  location and orientation identify its base attachment region; connectors
  leaving that region toward child rectangles are derivation edges. Require
  unambiguous base/child geometry, retaining the triangle and arrow provenance.
  Never classify a connector solely because its entity is a virtual base.
- An ellipse connector attaches attribute text to its entity or relationship
  owner and receives no entity multiplicity. For each diamond, gather all
  incident entity slots separately from its attribute attachments. Preserve
  ternary relationships; do not decompose them into independent binary edges.
- Cardinalities at an entity endpoint describe how many instances of that entity
  may participate for the related instance (for a ternary relationship, a fixed
  combination of the other participants). Thus competition `*` / event `1`
  means each competition has one event and an event may have many competitions.
- Construct a bipartite graph of relationship entity-endpoints and candidate
  cardinality texts. Solve minimum-cost one-to-one assignment jointly across
  each connected candidate component, including overlapping entity neighborhoods.
  Solving the entire submodel is equivalent and prevents label reuse globally.
- Use endpoint-to-label geometry and local connector alignment, with explicit
  unmatched slots. Keep geometric feasibility limits so an absent nearby label
  cannot be filled by an unrelated distant label. Report unused labels too.
- Estimate ambiguity by forbidding each chosen assignment in turn and solving
  again. A small alternative-cost gap is a diagnostic, not license for arbitrary
  tie-breaking. Missing cardinalities remain unknown, distinct from `*`.
- Initial audit parameters for these unrotated exports: candidates within 100
  drawing units of the text bounding box; cost = endpoint-to-text-center distance
  plus twice any backward displacement beyond 10 units along the outgoing local
  connector segment. Unmatched slots cost 10,000; forbidden pairs cost 100,000.
  Review gaps below 10 or chosen costs above 110. These are audit parameters,
  not universal confidence guarantees: expose/tune geometric tolerances in the
  converter and validate scale/rotation behavior with fixtures.
- Preserve source file, arrow endpoint, and selected text IDs in executable
  diagnostics/reports. Any source-specific override must explicitly identify the
  endpoint and value/label; validate it against changed inputs. No override is
  used in the current audit.
- Reconcile repeated relationship occurrences only after comparing multiplicities
  by canonical participant slot. Keep different names and ternary structures.

# References
* `00-legend-2026-09-08-1616.excalidraw` contains the drawings' legend.
* `00-legend-2026-09-08-1616.png` contains both the submodel table with its background colors and the drawings' legend.

## Development Plan

Steps 1–4 are complete. Step 5 is complete as the initial standalone converter
implementation. Steps 6–7 have not started; generated C# mapping and full EF
validation remain subject to the verification work in those steps.

### 1. Establish the input inventory and drawing conventions

Status: complete. Current inventory, conventions, and remaining input issues
are recorded below.

- Inventory the seven `.excalidraw` files in `Model/`; use the root legend as
  reference data, not as a submodel to convert.
- Inspect representative drawings against the legend to identify entity boxes,
  external references, relationship diamonds, attributes, cardinality labels,
  virtual bases, and the connectors used for derivation.
- Use the confirmed submodel-to-color mapping below, taken from the table in
  `00-legend-2026-09-08-1616.png`. Match each semantic color name to the first
  word of its submodel export filename. Excalidraw and Confluence use different
  palettes, so do not require exact RGB equality between them.
- Map drawing colors to these semantic submodel identities for resolving external
  entities; diagnose unknown or ambiguous drawing colors.
- Define explicit input selection so that future revisions of a drawing are not
  accidentally processed alongside older versions of the same submodel.

### 2. Define a normalized intermediate model

Status: complete. The contract is defined under Requirements; current source
reconciliation results are recorded below.

- Separate Excalidraw parsing, semantic interpretation, validation, and C# emission.
- Preserve source file and element IDs for entities, relationships, attributes,
  connector endpoints, and cardinality assignments so diagnostics can be traced
  back to the drawings.
- Treat all model connectors as Excalidraw `arrow` elements without arrowheads.
  Arrows are used because they provide endpoint connectivity; `line` elements do
  not represent model connections. Do not infer semantic direction from arrow
  start/end order.
- Ignore deleted elements. Resolve labels and connectors from bindings where
  available, with geometric fallback for unbound elements. Account for local
  connector points, rotations, grouped shapes, and multiline labels.
- Maintain separate identities for visual occurrences and canonical entities.
  Reconcile dashed external occurrences with their owning submodel's definition;
  diagnose missing definitions and naming/color conflicts instead of inventing
  additional entities.
- Preserve relationship names and roles. Merge repeated representations of the
  same relationship without collapsing distinct relationships between the same
  entity pair, including self-references. Apply the relationship-merging
  requirements; report conflicting definitions or ambiguous identity for correction.

### 3. Resolve connectivity and cardinalities

Status: complete. Topology and all endpoint cardinalities are resolved.
Current results and validation evidence are recorded below.

- Reconstruct direct connections, connections through relationship diamonds,
  attribute attachments, and virtual-base derivation structures. Keep these
  semantic roles distinct before interpreting multiplicities; derive them from
  the connected shapes and drawing notation, not arrow start/end order.
- Parse `1`, `0..1`, `1..*`, and `*` into explicit lower and upper bounds.
- For each entity occurrence, consider all incident relationship endpoints and
  all plausible nearby cardinality labels together. Solve a minimum-cost joint
  assignment using distance to the endpoint/entity boundary and the local
  geometry of the connector near that endpoint; do not use independent
  nearest-label guesses. Any geometric alignment score must be independent of
  arrow start/end order and must not imply relationship direction, foreign-key
  ownership, or base/child roles.
- Prevent reuse of a cardinality label across competing endpoints. Where
  candidate neighborhoods overlap, reconcile assignments across those neighborhoods.
- Report missing, conflicting, or ambiguous assignments with source references
  and scoring evidence. Provide a source-keyed override mechanism for cases that
  cannot be resolved reliably from geometry, and retain overrides as explicit
  decisions rather than hidden heuristics.
- Validate endpoint semantics on hand-checked examples before applying the
  inferred multiplicities to C# navigation and foreign-key generation.

### 4. Agree on the C# and EF Core mapping policy

Status: complete. Relationship representation, naming/keys, per-base strategies,
attribute TODO handling, and directed self-relationship mapping are specified in
Requirements. Implementation and executable validation remain in steps 5–6.

- Resolve the remaining naming, nullability, and namespace details listed in
  Requirements, applying the established PascalCase and output-file rules.
- Give every explicitly modeled concrete entity a `Guid Id` primary key.
  Generate collection navigations as initialized `ICollection<T>` properties and
  single-valued navigations with requiredness derived from cardinality.
- Specify foreign-key ownership and nullability for one-to-many and one-to-one
  relations, including multiple relations between the same types. Diagnose cases
  where the drawing does not determine a one-to-one dependent unambiguously.
- Decide how to represent named relationships, many-to-many relationships, and
  relationships with attributes: direct navigations versus explicit association
  classes. Define key policy for any generated association classes separately
  from the required IDs of explicitly modeled entities.
- Inventory the few modeled attributes. Generate scalar properties only when
  their types are supported by the source or an accepted decision; record unknown
  types instead of guessing domain data.
- Define mappings for all three virtual-base strategies in the requirements,
  including a single base entity with an enum and no separate derived entities.
  Document target validity, exclusivity, requiredness, and the treatment of
  relationships targeting derived types for each strategy.
- Determine whether annotations or minimal Fluent API configuration are needed
  for an unambiguous EF model. Keep all generated model classes in one file under
  `Implementation`; agree on the scope/location of any supporting configuration.
- Record constraints that skeletons alone cannot enforce, such as a collection's
  `1..*` minimum, rather than treating them as fully implemented constraints.

### 5. Implement the converter and diagnostics

Status: complete as an initial executable implementation in `erm_converter.py`.
It accepts explicit export paths, optional `--output`, `--namespace`, and the
repeatable `-b/--virtual-base` configuration option. It parses all seven current exports,
validates global entity ownership and duplicate definitions, resolves live arrow
bindings, performs one-to-one cardinality matching with explicit unmatched slots,
and writes output atomically only when no error diagnostics exist. Warnings are
reported and do not prevent output. The generated file is `Implementation/model.cs`.

Diagnostics-only invocation from `ERM`:

```text
python3 erm_converter.py
```

The implementation always prints diagnostics to standard output and preserves
source file/element IDs in them. It treats
stale `boundElements` back-references as metadata, never arrow direction, and
rejects unknown external colors, missing entity definitions, duplicate local
definitions, malformed input, and unresolved required input selection. It is a
usable CLI baseline; step 6 must expand its relationship classification,
virtual-base lowering, and EF configuration verification against fixtures.

- Implement a Python command-line entry point with explicit input, output, and
  override/configuration paths; document invocation and dependencies.
- Validate the complete intermediate model before emitting code. Give actionable
  diagnostics for unresolved references, unsupported shapes, ambiguous semantics,
  duplicate identifiers, and contradictory relationship definitions.
- Generate deterministic output with stable ordering, source traceability, and a
  generated-file notice. Avoid timestamps or ordering dependent on drawing layout.
- Write the single C# model file under `Implementation` only after validation
  succeeds; a failed conversion must not replace a valid previous output.

### 6. Verify against fixtures and the complete model

- Create small, hand-checked fixtures for repeated external entities, rotated or
  unbound connectors, crowded endpoints, ambiguous cardinalities, self-relations,
  multiple named relationships, relationship attributes, and virtual bases.
- Verify that reversing an arrow's point order and swapping its endpoint bindings
  leaves interpreted connectivity, cardinalities, and generated output unchanged.
- Test joint cardinality assignment with a case where greedy nearest-neighbor
  matching gives the wrong result. Test that ambiguity requires a diagnostic or
  explicit override, and that labels cannot be assigned twice.
- Review a canonical entity/relationship inventory against all seven drawings;
  confirm entity counts according to the selected virtual-base strategy and that each modeled relationship
  is represented with the intended multiplicity and roles.
- Compile the generated file in a minimal compatible .NET/EF Core 10 project and
  validate model construction with the corresponding Npgsql provider. Resolve
  mapping ambiguities and unintended shadow relationships. Use a PostgreSQL
  integration check only if database behavior is part of the accepted scope.
- Verify reproducibility and failure behavior: identical inputs produce identical
  output, and invalid input yields diagnostics without replacing generated code.

### 7. Complete delivery and preserve context

- Deliver the Python converter, necessary configuration/overrides, focused tests,
  usage instructions, and the generated C# file under `Implementation`.
- Completion requires all seven submodels to be accounted for, unresolved
  interpretation issues to be explicitly recorded, and the accepted mapping to
  pass the compilation and EF model checks above. Blocking ambiguities must be
  resolved before calling the generated model complete.
- Maintain current progress and useful findings. Integrate accepted decisions
  directly into Requirements and update affected plan items. Remove resolved
  issues and superseded statements; do not retain a development history or
  duplicate decision records.

### Current findings

- `Model/` contains seven Excalidraw files, named by color:
  black, blue, gray, green, orange, purple, and red. `Implementation/` is empty.
- The legend identifies rectangles as entities, dashed rectangles as external
  entities, diamonds as optionally named relationships, and ellipses as attributes. It also
  illustrates a virtual base with a derivation connector and two concrete children.
- The drawings include direct entity connections, relationship diamonds,
  attributes, and repeated entity labels; interpretation must support these
  rather than assume every connector is a simple binary association.
- The Markdown export omits table background colors. Use the table in
  `00-legend-2026-09-08-1616.png` for the following mapping:

| Submodel | Export filename prefix |
| --- | --- |
| Übergreifende Struktur | `black` |
| Logischer Turnierablauf und Startnummern | `green` |
| Zeitplan, Flächen, Säle | `purple` |
| Menschen, Accounts, Rollen, Berechtigungen | `red` |
| Regelungen für Wettbewerbe | `blue` |
| Logs, Traceability und Turnierprotokoll | `gray` |
| Externe IDs | `orange` |

### Current input inventory

All seven selected exports parse as Excalidraw JSON. Counts below exclude deleted
elements and count visual occurrences, not canonical entities or generated classes.
Solid rectangles include virtual-base candidates.

| Selected input (relative to ERM) | Solid rectangles | Dashed rectangles | Arrows |
| --- | ---: | ---: | ---: |
| `Model/black-2026-09-08-1557.excalidraw` | 4 | 10 | 19 |
| `Model/blue-2026-09-08-1558.excalidraw` | 11 | 5 | 25 |
| `Model/gray-2026-09-08-1558.excalidraw` | 4 | 7 | 10 |
| `Model/green-2026-09-07-1652.excalidraw` | 16 | 12 | 58 |
| `Model/orange-2026-09-08-1558.excalidraw` | 2 | 8 | 11 |
| `Model/purple-2026-09-08-1557.excalidraw` | 4 | 4 | 12 |
| `Model/red-2026-09-08-1557.excalidraw` | 7 | 5 | 12 |

Input-selection policy: the table above is the explicit current input set. The
future CLI must accept explicit paths and require exactly one selected export per
submodel for a complete conversion. Reject duplicate submodel selections and
missing files; do not silently choose by modification time or combine revisions.
Replacing an export requires explicitly updating the selection. Exclude the root
legend and PNG from conversion input. No converter or CLI is implemented in step 1.

The observed Excalidraw stroke-color mapping is:

| Semantic submodel color | Observed stroke colors |
| --- | --- |
| black | `#1e1e1e` |
| blue | `#1971c2` |
| gray | `#868e96` |
| green | `#2f9e44` |
| orange | `#f08c00` |
| purple | `#be4bdb`, `#9c36b5` |
| red | `#e03131` |

Use these explicit aliases for the current exports. Both purple shades belong to
the same submodel: local purple rectangles use `#be4bdb`, while external purple
rectangles in black/red use `#9c36b5`. All observed active element stroke colors
are covered. Future unknown shades require a diagnostic and reviewed mapping;
do not assign ownership through an arbitrary nearest-RGB guess.

Drawing conventions verified against the legend and export structures:

- Solid and dashed rectangles distinguish local definitions and external
  occurrences. Repeated external boxes are present, for example three `person`
  occurrences and two `club` occurrences in green.
- Diamonds represent relationships; ellipses carry attribute text. Text may span
  lines, including multiple words or apparent multiple attributes in one ellipse
  (`purpose` / `number range`, `role` / `seq_no`). Preserve original text until
  semantic interpretation; line breaks alone do not define attribute boundaries.
- All 147 arrows bind at both endpoints to live rectangle, diamond, or ellipse
  shapes (294 references); none binds to a text element.
- Six closed, rounded triangular `line` elements occur in black (one), green
  (three), orange (one), and red (one). Their geometry matches the legend's
  derivation marker. They are notation shapes, not relationship connectors:
  retain them for virtual-base recognition instead of discarding all `line`
  elements. The three green base rectangles are labeled `result_phase`,
  `result_context`, and `result_owner` (with line breaks in the source labels).
- Cardinality texts use the documented tokens. Some texts contain `boundElements`
  references, including multiple texts referencing the same arrow; these are not
  reliable endpoint assignments. Retain the planned joint geometric assignment.
- `pred_sp` and `succ_sp` in purple demonstrate distinct named relationships
  between the same entities. Preserve both during later merging.

- The gray submodel uses a blue dashed `dance` occurrence (`#1971c2`), matching
  its blue definition. Its status-event entity is `competitor_status_event`
  (`competitor_status` / `_event` in the source label), with an external green
  `competitor` occurrence matching the green definition. Status events concern
  competitors following the domain split of participants and competitors.

### Intermediate-model source audit

The 99 rectangle occurrences resolve by logical name to 48 semantic entities:
48 local definitions and 51 external occurrences. Each has exactly one bound
label, each external occurrence has a local definition, and all owner colors
agree using the documented aliases. There are no duplicate local definitions.
The count includes virtual bases and is not a generated-class count.

| Owner | Canonical logical names |
| --- | --- |
| black | organization, federation, club, event |
| blue | contest_property, mark_property, judging_property, contest_value, mark_value, judging_value, mark_type, marking_type, dance, competition_type, dsl_script |
| gray | result_log, competitor_status_event, competition_report_event, change_log |
| green | competitor, participant, mark, evaluation, result, result_phase, result_context, result_owner, judge_employment, startno, startno_stack, competition, competition_call, round, round_dance, teamup |
| orange | with_external, idspace |
| purple | timeslot, syncpoint, hall, floor |
| red | person, country, license, account, role, scope, permission |

The input also contains 18 diamonds, 13 attribute ellipses, six derivation
markers, and 321 text elements. Arrow endpoint targets comprise 234 rectangles,
47 diamonds, and 13 ellipses. This is a source-structure inventory;
these counts do not imply semantic relationship counts.

Some arrow entries in element `boundElements` lists are not reciprocated by the
referenced arrow's current endpoints (including missing live arrows). Treat these as stale metadata, not additional graph edges or
errors requiring manual cleanup when valid forward bindings suffice.

### Connectivity and cardinality results

All 147 arrows are classified: 19 derivation connectors, 13 attribute
attachments, and 115 relationship connectors. The latter represent 77 direct
relationships and 18 diamond relationships, totaling 95 occurrences. Two diamonds
are ternary: green `participant_member` (person, participant, club), and the
unnamed red relationship (role, scope, account). The remaining 16 are binary.

Four repeated direct relationships have matching multiplicities across their
submodels and merge: competition–event, event–startno_stack, mark–mark_type,
and judge_employment–judging_value. This yields 91 canonical relationship records
with all endpoint multiplicities assigned.

| Submodel | Relationship occurrences | Entity-endpoint slots | Cardinality labels assigned |
| --- | ---: | ---: | ---: |
| black | 13 | 26 | 26 |
| blue | 18 | 36 | 36 |
| gray | 10 | 20 | 20 |
| green | 36 | 73 | 73 |
| orange | 2 | 4 | 4 |
| purple | 10 | 20 | 20 |
| red | 6 | 13 | 13 |

All 192 cardinality texts are assigned to the 192 relationship entity-endpoints
without reuse, leftovers, missing assignments, or ambiguity flags under the
audit parameters.

| Virtual base | Derived types |
| --- | --- |
| organization | club, federation |
| result_phase | round, round_dance |
| result_context | competition, competition_call |
| result_owner | competitor, participant |
| with_external | competition, competition_call, country, event, organization, participant, person |
| scope | competition, event, floor, organization |

The `with_external` drawing directly lists organization, not federation; the
organization derivation remains a separate structure. Preserve this nesting.
Attribute attachments are:

| Submodel | Connected owner and raw attribute-group text (line breaks shown as /) |
| --- | --- |
| black | contribution_type — contributes_ev |
| blue | participant_definition — conjunction_no |
| blue | dsl_script — purposeround_type_set |
| blue | marking_eligible — round_type |
| green | participant_member — roleseq_no |
| green | comp_startno_stack — purposenumber range |
| green | seq_no — competition_dance |
| green | seq_no — round_dance |
| green | heat_no — teamup |
| green | mark — heat_no |
| green | contribution_type — contributes_co |
| green | role — competitor_member |
| orange | maps_to — external_id |

Attribute-group labels above identify attachments only; concatenated multiline
text is not an assertion that it is a single scalar attribute. Original ellipse
text remains authoritative for step 4.

Validation performed with a temporary Python audit prototype: the assignment
solver matched exhaustive search on 80 small matrices and passed a greedy-failure
example. Reversing every arrow's points and swapping endpoint bindings preserved
all assignments. Alignment penalty weights 0, 1, 2, and 3 gave identical results
on all seven exports. Assigned pairs passed the stated cost/margin review limits.
Source-coordinate spot checks covered competition–event in black, pred_sp in
purple, and the crowded participant edge in green. All current elements have
zero rotation; rotated and scaled inputs still require converter fixture tests.
These checks validate this audit, not a delivered production converter.

### Relationship inventory

Each `entity [value]` gives the multiplicity at that entity's endpoint.
Repeated occurrences are consolidated; diamond names are shown when supplied.

| Submodels | Relationship name | Entity endpoints |
| --- | --- | --- |
| black, green | (unnamed) | `competition [*]` — `event [1]` |
| black, green | (unnamed) | `startno_stack [*]` — `event [1]` |
| black | (unnamed) | `dance [*]` — `event [1]` |
| black | (unnamed) | `event [1]` — `hall [*]` |
| black | (unnamed) | `event [1]` — `syncpoint [*]` |
| black | (unnamed) | `event [1]` — `person [*]` |
| black | (unnamed) | `event [1]` — `permission [*]` |
| black | (unnamed) | `event [1]` — `role [*]` |
| black | (unnamed) | `event [1]` — `country [*]` |
| black | (unnamed) | `event [1]` — `account [*]` |
| black | contributes_ev | `organization [*]` — `event [*]` |
| black | client | `organization [*]` — `event [1]` |
| black | part_of | `organization [*]` — `organization [*]` |
| blue | (unnamed) | `contest_value [*]` — `contest_property [1]` |
| blue | (unnamed) | `contest_property [*]` — `federation [1]` |
| blue | (unnamed) | `competition [*]` — `competition_type [*]` |
| blue | (unnamed) | `competition_type [*]` — `federation [1]` |
| blue | (unnamed) | `dsl_script [*]` — `competition_type [*]` |
| blue | (unnamed) | `mark_property [*]` — `federation [1]` |
| blue | (unnamed) | `mark_value [*]` — `mark_property [1]` |
| blue | (unnamed) | `judging_value [*]` — `judging_property [1]` |
| blue | (unnamed) | `mark_type [*]` — `mark_value [1..*]` |
| blue | (unnamed) | `federation [1]` — `mark_type [*]` |
| blue, green | (unnamed) | `mark [*]` — `mark_type [1]` |
| blue | (unnamed) | `mark_type [1..*]` — `marking_type [*]` |
| blue | (unnamed) | `federation [1]` — `judging_property [*]` |
| blue, green | (unnamed) | `judge_employment [*]` — `judging_value [*]` |
| blue | competition_definition | `competition_type [*]` — `contest_value [1..*]` |
| blue | marking_eligible | `marking_type [*]` — `competition_type [*]` |
| blue | participant_definition | `contest_value [1..*]` — `participant [*]` |
| blue | dance_definition | `dance [*]` — `contest_value [1..*]` |
| gray | (unnamed) | `result_log [*]` — `account [1]` |
| gray | (unnamed) | `result [*]` — `result_log [1]` |
| gray | (unnamed) | `evaluation [*]` — `result_log [1]` |
| gray | (unnamed) | `competitor_status_event [*]` — `competitor [1]` |
| gray | (unnamed) | `competitor_status_event [*]` — `account [1]` |
| gray | (unnamed) | `competition_report_event [*]` — `account [1]` |
| gray | (unnamed) | `change_log [*]` — `account [1]` |
| gray | (unnamed) | `competition [0..1]` — `competition_report_event [*]` |
| gray | (unnamed) | `round [0..1]` — `competition_report_event [*]` |
| gray | (unnamed) | `dance [0..1]` — `competition_report_event [*]` |
| green | (unnamed) | `startno [*]` — `startno_stack [1]` |
| green | (unnamed) | `competition [1]` — `competition_call [1..*]` |
| green | (unnamed) | `competition_call [1]` — `competitor [*]` |
| green | (unnamed) | `competition [1]` — `round [*]` |
| green | (unnamed) | `round [1]` — `round_dance [*]` |
| green | (unnamed) | `round_dance [*]` — `dance [1]` |
| green | (unnamed) | `judge_employment [1]` — `mark [*]` |
| green | (unnamed) | `teamup [*]` — `round_dance [1]` |
| green | (unnamed) | `mark [*]` — `participant [1]` |
| green | (unnamed) | `participant [*]` — `competitor [1]` |
| green | (unnamed) | `result_owner [1]` — `result [*]` |
| green | (unnamed) | `evaluation [*]` — `round [1]` |
| green | (unnamed) | `result [*]` — `result_context [1]` |
| green | (unnamed) | `round [*]` — `competitor [*]` |
| green | (unnamed) | `competition_type [1]` — `competition_call [*]` |
| green | (unnamed) | `startno [0..1]` — `competitor [*]` |
| green | (unnamed) | `round [1]` — `judge_employment [*]` |
| green | (unnamed) | `mark [*]` — `round_dance [1]` |
| green | (unnamed) | `person [1]` — `judge_employment [*]` |
| green | (unnamed) | `dance [*]` — `federation [1]` |
| green | (unnamed) | `result [*]` — `result_phase [1]` |
| green | (unnamed) | `participant [*]` — `teamup [*]` |
| green | (unnamed) | `evaluation [*]` — `result_owner [1]` |
| green | (unnamed) | `competitor [*]` — `club [1]` |
| green | (unnamed) | `participant [*]` — `startno [0..1]` |
| green | participant_member | `person [*]` — `participant [*]` — `club [*]` |
| green | competitor_member | `competitor [*]` — `person [*]` |
| green | on_stage | `teamup [*]` — `person [*]` |
| green | comp_startno_stack | `startno_stack [*]` — `competition [*]` |
| green | competition_dance | `competition [*]` — `dance [*]` |
| green | contributes_co | `organization [*]` — `competition [*]` |
| green | governs | `competition [*]` — `federation [1]` |
| orange | (unnamed) | `idspace [*]` — `federation [1]` |
| orange | maps_to | `idspace [*]` — `with_external [*]` |
| purple | (unnamed) | `timeslot [*]` — `floor [*]` |
| purple | (unnamed) | `floor [1]` — `teamup [*]` |
| purple | (unnamed) | `round [*]` — `floor [*]` |
| purple | (unnamed) | `competition [*]` — `floor [*]` |
| purple | (unnamed) | `floor [1]` — `judge_employment [*]` |
| purple | (unnamed) | `floor [*]` — `hall [1]` |
| purple | (unnamed) | `timeslot [*]` — `competition [0..1]` |
| purple | (unnamed) | `timeslot [*]` — `round [0..1]` |
| purple | pred_sp | `syncpoint [1]` — `timeslot [*]` |
| purple | succ_sp | `syncpoint [1]` — `timeslot [*]` |
| red | (unnamed) | `country [1]` — `person [*]` |
| red | (unnamed) | `person [0..1]` — `account [0..1]` |
| red | (unnamed) | `permission [*]` — `role [*]` |
| red | (unnamed) | `person [1]` — `license [*]` |
| red | (unnamed) | `license [*]` — `federation [1]` |
| red | (unnamed) | `role [*]` — `scope [*]` — `account [*]` |
