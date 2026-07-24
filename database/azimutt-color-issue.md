## Summary

The AML `{color: …}` entity property documented in [AML › Properties](https://azimutt.app/docs/aml/properties) ("color, to define the entity default color for Azimutt layouts") has no effect. Entities defined with an explicit color always render with the auto-computed name-based color instead — including in a brand-new project/layout. The color is parsed correctly but discarded during the AML→schema conversion before it ever reaches the frontend.

## Environment

- Self-hosted `ghcr.io/azimuttapp/azimutt:main` (Docker)
- Also reproduces from the AML editor generally, since the import path is shared.

## Reproduction

1. Create a new project from an AML source containing:

   ```aml
   organization {color: blue}
     id uuid pk

   event {color: green}
     id uuid pk
   ```

2. Open the resulting layout (fresh — no manual color changes).

**Expected:** `organization` is blue, `event` is green.

**Actual:** both get the auto color derived from the table name (`computeColor`); the `{color}` values are ignored.

## Root cause

The parser reads the color correctly into `entity.extra.color` (`entityExtraKeys` includes `'color'` — [libs/models/src/database.ts#L212](https://github.com/azimuttapp/azimutt/blob/main/libs/models/src/database.ts#L212)).

But the AML source import goes `parseAml(...).map(databaseToLegacy)` ([frontend/ts-src/index.ts#L461](https://github.com/azimuttapp/azimutt/blob/main/frontend/ts-src/index.ts#L461)), and `tableToLegacy` does **not** carry `extra.color` (or `extra.tags`) into the `LegacyTable` — only `comment`/`doc`, stats, keys, etc. survive ([libs/models/src/legacy/legacyDatabase.ts#L224-L240](https://github.com/azimuttapp/azimutt/blob/main/libs/models/src/legacy/legacyDatabase.ts#L224-L240)):

```ts
function tableToLegacy(e: Entity): LegacyTable {
    return removeUndefined({
        schema: e.schema || '', table: e.name,
        columns: (e.attrs || []).map(...),
        view: ..., definition: e.def, primaryKey: ..., uniques: ..., indexes: ..., checks: ...,
        comment: e.doc,          // survives
        stats: ...,
        // extra.color / extra.tags are lost here
    })
}
```

Because the color never reaches project metadata, the layout color resolver falls back to the name hash ([ErdTableProps.elm#L43](https://github.com/azimuttapp/azimutt/blob/main/frontend/src/PagesComponents/Organization_/Project_/Models/ErdTableProps.elm#L43)):

```elm
color = meta |> Maybe.andThen .color |> Maybe.withDefault (computeColor table.id)
```

`meta.color` is always `Nothing` for AML-imported entities, so `computeColor` (first-word hash) always wins.

## Suggested fix

Propagate `entity.extra.color` (and `extra.tags`) from the parsed `Database` into the project **metadata** on AML import, so `ErdTableProps.init` finds `meta.color`. This likely means carrying it through the `databaseToLegacy` / `gotAmlSchema` path into the `TableMeta` / `Metadata` the frontend builds, rather than only through the `LegacyTable` schema (which has no color field today).

## Note

The docs promise this works, so this is a doc/impl mismatch — either the import path should honor `{color}`, or the docs should note the limitation.
