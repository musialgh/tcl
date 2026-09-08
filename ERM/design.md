# ERM to EF Core Skeleton Converter

* Our web application TAS is a management SW for performaning dancing competitions.
* TAS' backend shall use EF Core 10 in C#, npgsql, and PostgreSQL for data storage.
* For the database, an initial ER model has been drawn in Excalidraw.
* This model consists of several submodels, each of which addresses a logical subdomain.
* In this project, a Python script shall be developed by Codex that converts the set of all submodels
  into a skeleton in C# defining the model classes to be used by EF Core 10.

## Requirements

* Navigation items shall be modeled as `ICollection<>`.
* All entities modeled explicitly in the ERM shall carry an id field of type `guid` as its primary key.
* For now, only class skeletons shall be generated, for most attributes have not been modeled in the ERM.
* The submodels are distinguished by color.
  * External entities have their submodel's color and a dashed outline.
  * External entities may appear multiple times in a different submodel (to avoid line crossings).
  * Cardinalities need to be assigned to the closest line ends, for they are not otherwise linked
    to the lines in the excalidraw drawings. To maximize success, geometric relation between *all*
    line ends connected to an entity box shall be associated to *all* candidate cardinality texts
    and best combination of associations used.
  * Virtual base entities shall not yield separate classs, but be
    implemented in one of the following ways:
    * a single foreign key and an enum idientiying the target entity, or
    * a foreign key field, NULL permitted, for each of the children
* All model classes shall be implemented in a single C# file,
  to be placed in directory `Implementation`

# References
* `00-legend-2026-09-08-1616.excalidraw` contains the drawings' legend.
* `00-legend-2026-09-08-1616.png` contains both the submodel table with its background colors and the drawings' legend.
* `ER-Modellierung.md` contains the list of submodels with a color map

## Development Plan

The scope of this phase is planning only. The steps below describe subsequent
implementation work; they do not authorize implementation as part of this edit.
This document will remain the shared context for requirements, accepted design
decisions, significant findings, and implementation progress.

### 1. Establish the input inventory and drawing conventions

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
  entity pair, including self-references. Apply the accepted relationship-merging
  rule below; report conflicting definitions or ambiguous identity for correction.

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

- Define namespace, class/property naming, multiline-name normalization, collision
  handling, nullable-reference conventions, and the output filename.
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
- Choose and record a strategy for each virtual base: one target ID plus an enum,
  or nullable foreign keys to its concrete children. Generate no virtual-base
  class. Document target validity, exclusivity, and requiredness checks, and the
  EF/database mapping consequences of the chosen representation.
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
  confirm that each concrete entity occurs once and each modeled relationship
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
- Update this section's progress and the records below as work proceeds. Record
  accepted decisions with rationale and consequences; retain superseded decisions
  with a pointer to their replacement. Record findings with source evidence and
  their impact on implementation. Keep proposals distinct from accepted decisions.

### Confirmed findings

- Initial inspection: `Model/` contains seven Excalidraw files, named by color:
  black, blue, gray, green, orange, purple, and red. `Implementation/` is empty.
- The legend identifies rectangles as entities, dashed rectangles as external
  entities, diamonds as named relationships, and ellipses as attributes. It also
  illustrates a virtual base with a derivation connector and two concrete children.
- The drawings include direct entity connections, relationship diamonds,
  attributes, and repeated entity labels; interpretation must support these
  rather than assume every connector is a simple binary association.
- The Markdown export loses the Confluence table's background colors. The updated
  `00-legend-2026-09-08-1616.png` preserves the table and legend, resolving the
  previously noted missing-color information. Its mapping is:

| Submodel | Export filename prefix |
| --- | --- |
| Übergreifende Struktur | `black` |
| Logischer Turnierablauf und Startnummern | `green` |
| Zeitplan, Flächen, Säle | `purple` |
| Menschen, Accounts, Rollen, Berechtigungen | `red` |
| Regelungen für Wettbewerbe | `blue` |
| Logs, Traceability und Turnierprotokoll | `gray` |
| Externe IDs | `orange` |

### Design decisions and open questions

Accepted clarification (2026-09-08): table colors map by semantic color name to
export filename prefixes. Exact RGB matching across Confluence and Excalidraw is
not required because their palettes differ. Use the PNG table as the color
reference; the Markdown table remains useful for submodel names and links.

Accepted clarification (2026-09-08): all model connectors are Excalidraw arrows
without arrowheads, chosen for their endpoint connectivity, which lines do not
provide. They carry no semantic direction through their start/end ordering.
Relationship and derivation semantics come from the connected shapes, labels,
and notation.

Resolve and record the remaining decisions during the corresponding steps above:

- Canonical entity identities and any conflicting definitions.
  - conflicts resulting from multiple occurrences of the same entity name shall not
    be resolved, for they would constitute an error in the input model. 
    Such error shall be reported and fixed by me.
  - Accepted relationship-merging rule: merge repeated occurrences of the same
    relationship across all submodels. Identify relationships by their canonical
    participating entities together with their relationship names and endpoint
    roles, where present; the entity pair alone is not sufficient.
    Preserve distinct relationships between the same entities, including
    self-references. For example, `pred_sp` and `succ_sp` between `timeslot` and
    `syncpoint` remain two separate relationships.
    Compare endpoint roles and cardinalities independently of arrow start/end
    order. Report conflicting definitions of the same relationship or ambiguous
    relationship identity for correction by me; do not silently merge them.
    The same entity or relationship may be repeated in multiple submodels for
    human readability without creating duplicate semantic entities or relations.
- Naming/namespace conventions and generated filename.
  - generated file shall be `model.cs`
  - naming convention is PascalCase for classes and members
- Association-class, attribute-type, and one-to-one foreign-key policies.
  - where possible, implementation of association classes shall be left to EF Core
- Virtual-base strategy for each relevant base and its integrity constraints.
  - shall be a CLI argument, a global one for starters, later maybe configurable per case
- Required EF configuration and the boundary between skeleton generation and
  database-enforced constraints.
  - cardinality constraints specified in ERM should be included in the skeleton 
    wherever possible
- Ambiguity thresholds and the format of reviewed source-specific overrides.
  - the converter need not support continuous loop development. The EF classes will serve
    as the primary source after attributes have been added.
