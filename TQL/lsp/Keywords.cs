static class Keywords
{
    // Keys are uppercase; looked up case-insensitively (word under cursor is uppercased before lookup).
    // Values are Markdown documentation shown in hover and completion tooltips.
    public static readonly Dictionary<string, string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ADD"] = """
            **ADD COLUMN**
            ```
            ADD COLUMN <name> {DECIMAL|TEXT|GUID} [DEFAULT <value>] TO TABLE <table> [IF NOT EXISTS]
            ```
            Adds a column to a table.
            The **first** column is always the primary key — it must not have a `DEFAULT` and must not be NULL.
            """,

        ["AND"] = "**AND** — logical conjunction: `<expr> AND <expr>`",

        ["AS"] = "**AS** — renames a column or expression in SELECT: `<expr> AS <name>`",

        ["ASC"] = "**ASC** — ascending order (default). Used in `ORDER BY`, `GROUP BY`, and `FOR … IN range`.",

        ["AVG"] = "**AVG(`<expr>`)** — aggregate: arithmetic mean of non-NULL values.",

        ["BREAK"] = "**BREAK** — exits the enclosing `FOR` or `WHILE` loop immediately.",

        ["BY"] = "**BY** — part of `ORDER BY` and `GROUP BY`.",

        ["COLUMN"] = "**COLUMN** — part of `ADD COLUMN`.",

        ["CONCAT"] = """
            **CONCAT TO**
            ```
            CONCAT TO <target> FROM <source> [USING {<src-col> [AS <tgt-col>][,]}+]
            ```
            Adds **new columns** to existing rows matched by primary key.
            Equivalent to a column-adding JOIN.
            """,

        ["CONTINUE"] = "**CONTINUE** — skips to the next iteration of the enclosing `FOR` or `WHILE` loop.",

        ["COUNT"] = """
            **COUNT(`{* | <expr>}`)** — aggregate.
            - `COUNT(*)` — total row count selected by WHERE.
            - `COUNT(<expr>)` — rows where expr is non-zero, non-empty, and non-NULL.
            """,

        ["CREATE"] = """
            **CREATE TABLE**
            ```
            CREATE TABLE <name> [IF NOT EXISTS]
            ```
            Creates a new empty table. Add columns with `ADD COLUMN`.
            """,

        ["DECIMAL"] = "**DECIMAL** — arbitrary-precision numeric type. Used in `ADD COLUMN` and as a parameter type.",

        ["DEFAULT"] = "**DEFAULT `<value>`** — default column value in `ADD COLUMN`. Not allowed on the primary key.",

        ["DEFINE"] = """
            **DEFINE**
            ```
            DEFINE <identifier>(<param-list>) AS <macro-body>
            ```
            Defines a textual macro (substituted at parse time).
            """,

        ["DESC"] = "**DESC** — descending order. Used in `ORDER BY`, `GROUP BY`, and `FOR … IN range`.",

        ["DISTINCT"] = "**DISTINCT** — part of `LIMIT … ON DISTINCT <cols>`. Counts blocks of consecutive equal values instead of individual rows.",

        ["DROP"] = """
            **DROP TABLE**
            ```
            DROP TABLE <name> [IF NOT EXISTS]
            ```
            Drops (deletes) a table.
            """,

        ["ELSE"] = """
            **ELSE** / **ELSE IF**
            ```
            IF <cond>:
                …
            ELSE IF <cond>:
                …
            ELSE:
                …
            ```
            Optional fallback branch of an `IF` block.
            """,

        ["FIRST"] = "**FIRST(`<expr>`)** — aggregate: first value in the group.",

        ["FOR"] = """
            **FOR**
            ```
            FOR <var> IN <low>..<high> [ASC|DESC]:
            FOR <var> IN <table>:
            FOR <var> IN <table>.<column>:
            FOR <var> IN (<value-list>):
            ```
            Iterates over a numeric range, table rows, a column, or a list.
            Body is an indented block. Supports `BREAK` and `CONTINUE`.
            """,

        ["FROM"] = "**FROM** — source table clause in `SELECT … FROM`, `UPDATE … FROM`, `CONCAT TO … FROM`.",

        ["FUNCTION"] = """
            **FUNCTION**
            ```
            FUNCTION <name>[(<param-list>)]:
                <body>
            ```
            Declares a function. Body is an indented block.
            - `DECIMAL`, `STRING`, `NULL` → passed **by value**
            - Table parameters → passed **by reference** (use `LET` to copy)
            """,

        ["GROUP"] = "**GROUP BY `<col-list> [ASC|DESC]`** — groups result rows for aggregate functions.",

        ["GUID"] = "**GUID** — globally-unique identifier column type. On `INSERT INTO`, keys are auto-generated when the source provides no key data.",

        ["IF"] = """
            **IF** (directive)
            ```
            IF <condition>:
                <body>
            ```
            Conditional block with optional `ELSE IF` / `ELSE`. Body is an indented block.

            **IF** (built-in expression function)
            ```
            IF(<condition>, <expr-when-true>, <expr-when-false>)
            ```
            """,

        ["IMPORT"] = """
            **IMPORT**
            ```
            IMPORT <file-path>
            ```
            Includes another TQL file (file header directive).
            """,

        ["IN"] = """
            **IN** — two uses:
            1. Loop variable binding: `FOR <var> IN …`
            2. WHERE membership test: `<column> IN <column-expr>`
               where `<column-expr>` is a single-column table expression.
            """,

        ["INSERT"] = """
            **INSERT INTO**
            ```
            INSERT INTO <target> VALUES <source> [USING {<src-col> [AS <tgt-col>][,]}+]
            ```
            Inserts **new rows** only; duplicate keys cause an error.
            If the target key is `GUID` and the source has no key data, keys are auto-generated.
            Returns a single-column table of all new keys. Can also be used as a directive.
            """,

        ["INTO"] = "**INTO** — part of `INSERT INTO`.",

        ["IS"] = "**IS [NOT] NULL** — null test: `<expr> IS NULL` / `<expr> IS NOT NULL`.",

        ["LAST"] = "**LAST(`<expr>`)** — aggregate: last value in the group.",

        ["LET"] = """
            **LET**
            ```
            LET <identifier> = <expression>
            ```
            Binds a name to a value. Use `LET` to copy a table parameter before mutating it.
            """,

        ["LIMIT"] = """
            **LIMIT**
            ```
            LIMIT <n>
            LIMIT <n> ON DISTINCT <col-list>
            ```
            Restricts result rows. `ON DISTINCT` counts **blocks** of consecutive equal values (use with `ORDER BY`).
            """,

        ["MAX"] = "**MAX(`<expr>`)** — aggregate: maximum value.",

        ["MEDIAN"] = "**MEDIAN(`<expr>`)** — aggregate: median value.",

        ["MIN"] = "**MIN(`<expr>`)** — aggregate: minimum value.",

        ["NOT"] = "**NOT** — logical negation: `NOT <expr>` or `<expr> IS NOT NULL`.",

        ["NULL"] = "**NULL** — null value literal. Test with `IS NULL` / `IS NOT NULL`.",

        ["ON"] = "**ON** — part of `LIMIT … ON DISTINCT`.",

        ["OR"] = "**OR** — logical disjunction: `<expr> OR <expr>`.",

        ["ORDER"] = "**ORDER BY `<col-list> [ASC|DESC]`** — sorts the SELECT result.",

        ["PLACE_MAX"] = """
            **PLACE_MAX(`<expr> [, {ASC|DESC}]`)**
            Maximum of the placement range when values tie.
            If _m_ rows share rank _k_, each gets [k, k+m−1]; PLACE_MAX returns k+m−1.
            `ASC` (default) = smaller value → better rank.
            """,

        ["PLACE_MIN"] = """
            **PLACE_MIN(`<expr> [, {ASC|DESC}]`)**
            Minimum of the placement range when values tie.
            If _m_ rows share rank _k_, each gets [k, k+m−1]; PLACE_MIN returns k.
            `ASC` (default) = smaller value → better rank.
            """,

        ["SELECT"] = """
            **SELECT**
            ```
            SELECT {* | <expr> [AS <col>] [,]}+
            FROM <table>
            [WHERE <condition>]
            [ORDER BY <cols> [ASC|DESC]]
            [GROUP BY <cols>]
            [LIMIT <n> [ON DISTINCT <cols>]]
            ```
            Returns a table expression.
            The source primary key is preserved as result key if it appears in the output (even if renamed).
            Otherwise the first listed column becomes the new key.
            """,

        ["SET"] = "**SET `<source-row>`** — new values in `UPDATE … SET`. The source must resolve to exactly one row.",

        ["STRING"] = "**STRING** — string parameter type in `FUNCTION` declarations (passed by value).",

        ["SUM"] = "**SUM(`<expr>`)** — aggregate: sum of values.",

        ["TABLE"] = "**TABLE** — part of `CREATE TABLE`, `DROP TABLE`, `ADD COLUMN … TO TABLE`.",

        ["TEXT"] = "**TEXT** — string column type in `ADD COLUMN`.",

        ["TO"] = "**TO** — part of `CONCAT TO` and `ADD COLUMN … TO TABLE`.",

        ["UPDATE"] = """
            **UPDATE** (directive)
            ```
            UPDATE <target> FROM <source> [USING {<src-col> [AS <tgt-col>][,]}+]
            ```
            Updates existing rows matched by primary key. Unmatched source rows cause an error.

            **UPDATE** (table expression / directive)
            ```
            UPDATE <target> [WHERE <condition>] SET <row> [USING …]
            ```
            Updates matched rows; returns a single-column table of matched keys.
            """,

        ["USING"] = """
            **USING `{<src-col> [AS <tgt-col>] [,]}+`**
            Maps source column names to target column names in
            `UPDATE … FROM`, `CONCAT TO … FROM`, `INSERT INTO … VALUES`, `UPDATE … SET`.
            """,

        ["VALUES"] = "**VALUES `<source-table>`** — source rows for `INSERT INTO … VALUES`.",

        ["WHERE"] = """
            **WHERE `<condition>`**
            Filters rows in `SELECT` and `UPDATE … SET`.
            Extra term: `<column> IN <column-expr>` (single-column membership test).
            """,

        ["WHILE"] = """
            **WHILE**
            ```
            WHILE <condition>:
                <body>
            ```
            Repeats the indented block while the condition is true. Supports `BREAK` and `CONTINUE`.
            """,

        ["XOR"] = "**XOR** — logical exclusive or: `<expr> XOR <expr>`.",
    };
}
