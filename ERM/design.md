# ERM to EF Core Skeleton Converter

* Our web application TAS is a management SW for performaning dancing competitions.
* TAS' backend shall use EF Core 10 in C#, npgsql, and PostgreSQL for data storage.
* For the database, an initial ER model has been drawn in Excalidraw.
* This model consists of several submodels, each of which addresses a logical subdomain.
* In this project, a Python script shall be developed by Codex that converts the set of all submodels
  into a skeleton in C# defining the model classes to be used by EF Core 10.

## Requirements

### Scope and generated code

- Generate class skeletons in `Implementation/model.cs`; most attributes have
  not yet been modeled. Use PascalCase for classes and members.
- Each explicitly modeled entity implemented as an actual entity shall have a
  `Guid Id` primary key.
- Collection navigations shall use `ICollection<T>`; single-valued navigations
  and foreign keys shall reflect cardinality and optionality.
- Where possible, leave implementation of association classes to EF Core.
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

Expose the strategy as a CLI argument, initially global; per-case configuration
may be added later. Define integrity constraints and mappings of relationships
to derived types for each representation before generating it.

### Remaining choices and document maintenance

Details still to determine in their respective plan steps: namespace, multiline
identifier normalization and collision handling, unnamed cross-table naming,
unknown attribute types, one-to-one foreign-key ownership, keys for generated
association classes, nullable-reference initialization, supporting EF configuration
scope/location, cardinality ambiguity thresholds, and whether overrides are needed.

Keep this file as current prompt context. Integrate accepted decisions directly
into the relevant requirements and update affected plan items and findings.
Remove superseded statements and resolved issues. Do not retain development
history or duplicate decision records.

# References
* `00-legend-2026-09-08-1616.excalidraw` contains the drawings' legend.
* `00-legend-2026-09-08-1616.png` contains both the submodel table with its background colors and the drawings' legend.
* `ER-Modellierung.md` contains the list of submodels with a color map

## Development Plan

Step 1 is complete; steps 2–7 have not been started.

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
- All 147 arrows have bindings at both endpoints (294 bound endpoints).
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

No outstanding issues remain from the step 1 inventory checks. The selected
files and shape counts above remain current.

Canonical reconciliation and cardinality interpretation have not been performed.
