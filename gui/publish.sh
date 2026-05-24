#!/usr/bin/env bash
# Publishes a self-contained, single-file build of the Avalonia GUI for a given runtime.
# Usage: ./publish.sh [runtime-identifier]
#   e.g. ./publish.sh win-x64
#        ./publish.sh linux-x64
#        ./publish.sh osx-arm64
set -euo pipefail

RID="${1:-linux-x64}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="$SCRIPT_DIR/bin/Release/publish/$RID"

rm -rf "$OUT_DIR"
dotnet publish "$SCRIPT_DIR/gui.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o "$OUT_DIR"

echo "Published $RID build to: $OUT_DIR"
