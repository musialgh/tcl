<div align="center">

# TQL Language Support

**A focused Visual Studio Code extension for the TQL scripting language**

Syntax highlighting · IntelliSense · Keyword documentation · Function navigation

</div>

---

TQL Language Support brings the essentials of a modern editing experience to
`.tql` files. It combines a TextMate grammar with a lightweight, bundled
language server, so the extension works without a separate .NET installation.

## Features

| Feature | What it provides |
| --- | --- |
| **Syntax highlighting** | Clear highlighting for TQL control flow, SQL-style queries, types, literals, built-in functions, strings, and comments |
| **Keyword completion** | Context-friendly suggestions for TQL keywords and built-ins while you type |
| **Hover documentation** | Concise syntax and usage help directly on documented keywords |
| **Signature help** | Parameter information for user-defined functions when typing `(` or `,` |
| **Go to Definition** | Navigation from a function call to its declaration |
| **Smart editing** | Automatic bracket and quote closing, comment support, and indentation after `:` |

## A quick look at TQL

```tql
# Rank every start by its total score
FUNCTION rank_starts(scores, limit = 3):
    LET totals = SELECT start, SUM(value) AS total
        FROM scores
        GROUP BY start
        ORDER BY total DESC
        LIMIT limit

    FOR result IN totals:
        CALL write_result(result.start, result.total)

    RETURN totals
```

Move the pointer over keywords such as `FUNCTION`, `SELECT`, `GROUP`, or
`RETURN` to see their documentation. Start typing a keyword to open completion,
or invoke suggestions manually with **Ctrl+Space**. Function calls support
signature help and **Go to Definition**.

## Installation

Choose the VSIX that matches the operating system on which VS Code runs:

- `tql-lsp-<version>-linux-x64.vsix`
- `tql-lsp-<version>-win-x64.vsix`

In Visual Studio Code, open the Extensions view, choose **… → Install from
VSIX…**, and select the package. Alternatively, install it from a terminal:

```bash
code --install-extension tql-lsp-<version>-linux-x64.vsix --force
```

Open or reload a file with the `.tql` extension. VS Code should select the
**TQL** language mode automatically.

> When using WSL, SSH, or a Dev Container, install the extension in the remote
> environment in which the workspace is open.

## Updating

Rebuilding a VSIX does not update an already installed extension automatically.
Install the newly built package again, then run **Developer: Reload Window**.
Increasing the extension version for every distributed build makes installed
versions easier to identify.

## Troubleshooting

If highlighting works but hover help or completion does not:

1. Confirm that the correct platform package is installed.
2. Reinstall the VSIX with `--force` and reload the VS Code window.
3. Check **View → Output → TQL Language Server** for activation or server errors.
4. For remote workspaces, check whether the extension is installed locally or
   in the remote environment.

## Building from source

From the `TQL/lsp` directory, run:

```bash
python3 build.py              # Linux and Windows packages
python3 build.py linux-x64    # Linux only
python3 build.py win-x64      # Windows only
```

The generated platform-specific packages are written to `vscode-extension/`.
The build bundles both the JavaScript language client and the self-contained TQL
language server.

## Current scope

The extension focuses on editing support. It does not currently execute TQL
scripts, format documents, or report parser diagnostics.
