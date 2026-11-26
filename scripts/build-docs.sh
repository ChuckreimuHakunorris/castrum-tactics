#!/usr/bin/env bash
set -euo pipefail

dotnet tool restore
dotnet docfx metadata Documentation/docfx.json --warningsAsErrors
dotnet docfx build Documentation/docfx.json --warningsAsErrors
dotnet run --project scripts/DocumentationLinks -- Documentation/.docfx/site
