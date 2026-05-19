# Packaging the TQL VS Code Extension

The extension is distributed as a self-contained `.vsix` file.
No .NET SDK and no `node_modules` are required on the recipient's machine.

## What's inside the VSIX

| File | Description |
|------|-------------|
| `extension.bundle.js` | Bundled extension + `vscode-languageclient` (single JS file) |
| `server/tql-lsp` | Self-contained linux-x64 LSP server binary |
| `server/tql-lsp.exe` | Self-contained win-x64 LSP server binary |
| `syntaxes/tql.tmLanguage.json` | TextMate grammar for syntax highlighting |
| `language-configuration.json` | Bracket/indent/comment rules |
| `package.json` | Extension manifest |

Each VSIX contains only the binary for its target platform.

## Building

From `TQL/lsp/`:

```bash
python3 build.py              # both platforms
python3 build.py linux-x64   # Linux only
python3 build.py win-x64     # Windows only
```

This runs `dotnet publish` for each target, bundles `extension.js`, and
produces `vscode-extension/tql-lsp-<version>-<platform>.vsix`.

## Installing on the recipient's machine

```bash
code --install-extension tql-lsp-0.0.1-linux-x64.vsix
```

Or via VS Code: **Extensions → ⋮ menu → Install from VSIX…**

## Notes

- `server/`, `extension.bundle.js`, and `*.vsix` are in `.gitignore` — regenerate before packaging.
- `extension.js` detects the OS at runtime (`process.platform`) and appends `.exe` on Windows automatically.
- Bump the version in `package.json` before releasing.
