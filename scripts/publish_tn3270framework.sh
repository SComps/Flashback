#!/bin/bash
# -----------------------------------------------------------------------------
# Build Flashback.TN3270Framework and copy the DLL (and deps) to an output folder.
#
# Usage:
#   ./scripts/publish_tn3270framework.sh
#   ./scripts/publish_tn3270framework.sh /path/to/output
# -----------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/Flashback.TN3270Framework/TN3270Framework.vbproj"
CONFIG="Release"
FRAMEWORK="net10.0"
DEFAULT_OUT="$HOME/tn3270framework-publish"

OUT_DIR="${1:-}"

if [[ -z "$OUT_DIR" ]]; then
    echo ""
    echo -e "\033[1;37mWhere should the TN3270Framework DLL be placed?\033[0m"
    echo -e "\033[0;37mDefault: $DEFAULT_OUT\033[0m"
    read -r -p "Path [Enter for default]: " INPUT_PATH
    if [[ -z "$INPUT_PATH" ]]; then
        OUT_DIR="$DEFAULT_OUT"
    else
        if [[ "$INPUT_PATH" == "~"* ]]; then
            OUT_DIR="${HOME}${INPUT_PATH:1}"
        else
            OUT_DIR="$INPUT_PATH"
        fi
        if [[ "$OUT_DIR" != /* ]]; then
            OUT_DIR="$(pwd)/$OUT_DIR"
        fi
    fi
fi

mkdir -p "$OUT_DIR"

echo ""
echo -e "\033[1;36m=== TN3270Framework build ===\033[0m"
echo "  Project : $PROJECT"
echo "  Config  : $CONFIG"
echo "  TFM     : $FRAMEWORK"
echo "  Output  : $OUT_DIR"
echo ""

if [[ ! -f "$PROJECT" ]]; then
    echo -e "\033[0;31mERROR: Project not found: $PROJECT\033[0m" >&2
    exit 1
fi

echo "-> Restoring..."
dotnet restore "$PROJECT"

echo "-> Building ($CONFIG / $FRAMEWORK)..."
dotnet build "$PROJECT" -c "$CONFIG" -f "$FRAMEWORK" --no-restore -o "$OUT_DIR"

echo ""
echo -e "\033[0;32mDone.\033[0m"
echo "  Output: $OUT_DIR"
ls -1 "$OUT_DIR"/*.dll 2>/dev/null || true
echo ""
