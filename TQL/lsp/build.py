#!/usr/bin/env python3
"""
Integrated build: dotnet publish + VSIX packaging for Linux and Windows.
Run from any directory — all paths are resolved relative to this script.

Usage:
    python3 build.py              # build both platforms
    python3 build.py linux-x64   # build one platform only
    python3 build.py win-x64
"""
import json
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

LSP_DIR = Path(__file__).parent                   # TQL/lsp/
EXT_DIR = LSP_DIR / "vscode-extension"
CSPROJ  = LSP_DIR / "tql-lsp.csproj"

PLATFORMS = [
    {"rid": "linux-x64", "binary": "tql-lsp",     "suffix": "linux-x64"},
    {"rid": "win-x64",   "binary": "tql-lsp.exe", "suffix": "win-x64"},
]

# ── helpers ───────────────────────────────────────────────────────────────────

def run(*cmd, cwd=None):
    print("  $", " ".join(str(c) for c in cmd))
    subprocess.run(cmd, check=True, cwd=cwd)

def bundle_extension():
    """esbuild: bundle extension.js + vscode-languageclient → extension.bundle.js"""
    out = EXT_DIR / "extension.bundle.js"
    run("npx", "esbuild", "extension.js",
        "--bundle", "--platform=node", "--target=node18",
        "--external:vscode", f"--outfile={out}",
        cwd=EXT_DIR)
    return out

def publish_server(rid, binary_name):
    """dotnet publish → self-contained single-file binary; returns Path to the binary."""
    tmp = tempfile.mkdtemp(prefix=f"tql-lsp-{rid}-")
    run("dotnet", "publish", str(CSPROJ),
        "-c", "Release",
        "-r", rid,
        "--self-contained",
        "-p:PublishSingleFile=true",
        "-o", tmp)
    binary = Path(tmp) / binary_name
    if not binary.exists():
        sys.exit(f"ERROR: expected binary not produced: {binary}")
    return binary

def build_vsix(pkg, binary, suffix):
    name      = pkg["name"]
    version   = pkg["version"]
    publisher = pkg["publisher"]
    vsix_name = f"{name}-{version}-{suffix}.vsix"

    ct_xml = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">\n'
        '  <Default Extension=".vsixmanifest" ContentType="text/xml" />\n'
        '  <Default Extension=".json"  ContentType="application/json" />\n'
        '  <Default Extension=".js"    ContentType="application/javascript" />\n'
        '  <Default Extension=".xml"   ContentType="application/xml" />\n'
        '  <Default Extension=".md"    ContentType="text/markdown" />\n'
        '  <Default Extension=".tql"   ContentType="text/plain" />\n'
        '  <Default Extension=""       ContentType="application/octet-stream" />\n'
        '</Types>\n'
    )

    manifest_xml = f"""\
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0"
    xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011"
    xmlns:d="http://schemas.microsoft.com/developer/vsx-schema-design/2011">
  <Metadata>
    <Identity Language="en-US"
              Id="{name}"
              Version="{version}"
              Publisher="{publisher}" />
    <DisplayName>{pkg.get("displayName", name)}</DisplayName>
    <Description xml:space="preserve">{pkg.get("description", "")}</Description>
    <Tags></Tags>
    <Categories></Categories>
    <GalleryFlags>Public</GalleryFlags>
    <Properties>
      <Property Id="Microsoft.VisualStudio.Code.Engine"
                Value="{pkg.get('engines', {}).get('vscode', '*')}" />
      <Property Id="Microsoft.VisualStudio.Code.ExtensionDependencies" Value="" />
    </Properties>
  </Metadata>
  <Installation>
    <InstallationTarget Id="Microsoft.VisualStudio.Code"/>
  </Installation>
  <Dependencies/>
  <Assets>
    <Asset Type="Microsoft.VisualStudio.Code.Manifest"
           Path="extension/package.json" Addressable="true" />
  </Assets>
</PackageManifest>
"""

    static = [
        "package.json",
        "README.md",
        "extension.bundle.js",
        "language-configuration.json",
        "syntaxes/tql.tmLanguage.json",
    ]

    out = EXT_DIR / vsix_name
    with zipfile.ZipFile(out, "w", compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml", ct_xml)
        z.writestr("extension.vsixmanifest", manifest_xml)
        for rel in static:
            src = EXT_DIR / rel
            z.write(src, f"extension/{rel}")
            print(f"  + extension/{rel}  ({src.stat().st_size:,} bytes)")
        arcname = f"extension/server/{binary.name}"
        z.write(binary, arcname)
        print(f"  + {arcname}  ({binary.stat().st_size:,} bytes)")

    print(f"  → {vsix_name}  ({out.stat().st_size:,} bytes)")
    return out

# ── main ──────────────────────────────────────────────────────────────────────

wanted = sys.argv[1:] or [p["rid"] for p in PLATFORMS]
targets = [p for p in PLATFORMS if p["rid"] in wanted]
if not targets:
    sys.exit(f"ERROR: unknown platform(s) {wanted}. Choose from: {[p['rid'] for p in PLATFORMS]}")

pkg = json.loads((EXT_DIR / "package.json").read_text())

print("=== Bundling extension.js ===")
bundle_extension()

for plat in targets:
    print(f"\n=== Publishing server ({plat['rid']}) ===")
    binary = publish_server(plat["rid"], plat["binary"])

    print(f"\n=== Packaging VSIX ({plat['suffix']}) ===")
    build_vsix(pkg, binary, plat["suffix"])

print("\nDone.")
