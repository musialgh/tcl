# Excalidraw ERM to EF Core Skeleton Converter

`erm_converter.py` validates a set of Excalidraw ER-model exports and can
generate a C# EF Core model skeleton. It requires Python 3 and has no third-party
Python dependencies.

## Quick start

Run validation only:

```bash
python3 erm_converter.py
```

Generate a model file:

```bash
python3 erm_converter.py --output implementation/model.cs
```

The default input selection is `model/*.excalidraw`. Supply paths to replace it
when the complete configured set of color-named exports is elsewhere:

```bash
python3 erm_converter.py path/to/exports/*.excalidraw
```

## Options

| Option | Meaning |
| --- | --- |
| `-o`, `--output FILE` | Generate C# into `FILE`. If omitted, the converter performs diagnostics only. |
| `-n`, `--namespace NAME` | Namespace for generated types. Default: `GeneratedModel`. |
| `-e`, `--entity_base NAME` | Name for the generated common entity base class. Default: `EntityBase`. |
| `-b`, `--virtual-base STRATEGY\|NAME=STRATEGY` | Repeatable virtual-base configuration. |
| `-h`, `--help` | Show complete CLI help. |

`-b` accepts these strategies:

- `id-and-kind`: one target ID plus a target-kind enum. The target cannot be an
  ordinary foreign key, so target existence/type validation remains a TODO.
- `nullable-fks`: one nullable typed FK per possible target, with an
  exactly-one-target check. This is the default.
- `base-and-kind`: one retained base entity plus a type enum; derived references
  use a base ID/kind constraint.

Set the global default with one bare strategy, then override individual bases:

```bash
python3 erm_converter.py \
  -b nullable-fks \
  -b some_base=base-and-kind \
  --output implementation/model.cs
```

Unknown strategies or bases, duplicate defaults/overrides, malformed settings,
and unsafe overlapping `base-and-kind` collapses are errors.

## Diagnostics and output

Diagnostics are always written to standard output.

- Errors exit with status 2 and prevent generation or replacement of an output
  file.
- Warnings do not prevent generation. For example, an untyped Excalidraw
  attribute produces `ATTRIBUTE_TYPE_PENDING` and becomes a comment in the C#
  skeleton.
- Successful diagnostics-only runs exit with status 0 and do not write a model.

Generated files begin with a timestamp and the command line used to create them.
This means the file header changes on every generation. The remainder is ordered
deterministically from the interpreted model.

## Generated skeleton

The generated file contains nullable-enabled partial entity classes, a common
base class with `Guid Id`, navigation and foreign-key properties, explicit
association classes where EF cannot represent the relationship as a simple skip
navigation, and `ModelConfiguration.Configure(ModelBuilder)` for Fluent mapping.

For a fixed derived-type reference represented through `base-and-kind`, the
generated CLR entity exposes only the meaningful ID and navigation. EF retains
the fixed discriminator as a hidden shadow property so the database can enforce
the derived kind through a check constraint and composite foreign key.

The configuration includes relationship cardinality where EF can represent it,
foreign keys, indexes, unique constraints, relational checks, and filtered
indexes for nullable polymorphic targets. It deliberately leaves these items as
comments or TODOs until the source model provides enough information:

- Attribute scalar property types.
- Required minimum collection sizes such as `1..*`.
- Existence/type validation for `id-and-kind` polymorphic references.

## Input interpretation

The converter reads headless Excalidraw arrows as connectivity only; arrow
direction has no domain meaning. Rectangles denote entities, diamonds
relationships, ellipses attributes, and triangular line shapes derivation.

Entity and relationship labels ignore source line breaks. Attribute labels differ:
each nonempty line begins a new attribute, except a line beginning with `_`,
which continues the preceding attribute. Thus `contribution` followed by `_type`
is emitted as one `contribution_type` attribute comment. Attribute comments are
placed after generated members in their class.
