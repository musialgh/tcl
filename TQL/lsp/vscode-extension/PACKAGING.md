# Packaging the TQL VS Code Extension

The extension is distributed as a self-contained `.vsix` file.
No .NET SDK and no `node_modules` are required on the recipient's machine.

## What's inside the VSIX

| File | Description |
|------|-------------|
| `extension.bundle.js` | Bundled extension + `vscode-languageclient` (single JS file) |
| `server/tql-lsp` | Self-contained linux-x64 LSP server binary (no .NET needed) |
| `syntaxes/tql.tmLanguage.json` | TextMate grammar for syntax highlighting |
| `language-configuration.json` | Bracket/indent/comment rules |
| `package.json` | Extension manifest |

## Installing on the recipient's machine

```bash
code --install-extension tql-lsp-0.0.1.vsix
```

Or via VS Code: **Extensions → ⋮ menu → Install from VSIX…**

## Rebuilding the VSIX

Run these commands from `TQL/lsp/vscode-extension/`:

```bash
# 1. Publish a fresh self-contained linux-x64 server binary
dotnet publish ../tql-lsp.csproj \
  -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -o server/

# 2. Pack everything into the VSIX (bundles extension.js automatically)
python3 build_vsix.py
```

The resulting `tql-lsp-<version>.vsix` is ready to share.

## Notes

- `server/`, `extension.bundle.js`, and `*.vsix` are in `.gitignore` — regenerate them before packaging.
- To target a different OS, change `-r linux-x64` in step 1 (e.g. `win-x64`, `osx-arm64`) and update `extension.js` if the binary name differs (Windows adds `.exe`).
- `build_vsix.py` reads the version from `package.json`; bump it there before releasing.
