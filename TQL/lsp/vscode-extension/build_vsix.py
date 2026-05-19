#!/usr/bin/env python3
"""Build a .vsix from the extension directory without requiring vsce."""
import json
import os
import zipfile
from pathlib import Path

ROOT = Path(__file__).parent

pkg = json.loads((ROOT / "package.json").read_text())
name      = pkg["name"]
version   = pkg["version"]
publisher = pkg["publisher"]
vsix_name = f"{name}-{version}.vsix"

# Files to include (relative to ROOT), mapped to their path inside the VSIX.
# Extension content lives under "extension/" in the ZIP.
INCLUDE = [
    "package.json",
    "extension.bundle.js",
    "language-configuration.json",
    "syntaxes/tql.tmLanguage.json",
    "server/tql-lsp",
]

# Gather all content-type extensions present
ext_types = {
    ".json":  "application/json",
    ".js":    "application/javascript",
    ".xml":   "application/xml",
    ".md":    "text/markdown",
    ".tql":   "text/plain",
    "":       "application/octet-stream",
}

content_types_xml = '<?xml version="1.0" encoding="utf-8"?>\n'
content_types_xml += '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">\n'
content_types_xml += '  <Default Extension=".vsixmanifest" ContentType="text/xml" />\n'
for ext, ct in ext_types.items():
    if ext:
        content_types_xml += f'  <Default Extension="{ext}" ContentType="{ct}" />\n'
    else:
        content_types_xml += f'  <Default Extension="" ContentType="{ct}" />\n'
content_types_xml += '</Types>\n'

manifest_xml = f'''<?xml version="1.0" encoding="utf-8"?>
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
                Value="{pkg.get("engines", {}).get("vscode", "*")}" />
      <Property Id="Microsoft.VisualStudio.Code.ExtensionDependencies"
                Value="" />
    </Properties>
  </Metadata>
  <Installation>
    <InstallationTarget Id="Microsoft.VisualStudio.Code"/>
  </Installation>
  <Dependencies/>
  <Assets>
    <Asset Type="Microsoft.VisualStudio.Code.Manifest"
           Path="extension/package.json"
           Addressable="true" />
  </Assets>
</PackageManifest>
'''

out = ROOT / vsix_name
with zipfile.ZipFile(out, "w", compression=zipfile.ZIP_DEFLATED) as z:
    z.writestr("[Content_Types].xml", content_types_xml)
    z.writestr("extension.vsixmanifest", manifest_xml)
    for rel in INCLUDE:
        src = ROOT / rel
        if not src.exists():
            print(f"  MISSING  {rel}")
            continue
        arcname = f"extension/{rel}"
        z.write(src, arcname)
        size = src.stat().st_size
        print(f"  + {arcname}  ({size:,} bytes)")

vsix_size = out.stat().st_size
print(f"\nWrote {vsix_name}  ({vsix_size:,} bytes)")
