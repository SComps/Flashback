#!/bin/sh
# Flashback Suite - FreeBSD Publish Script (Single-File / Multi-Arch)
# NOTE: Flashback.LicenseGenerator is EXCLUDED from this script to prevent shipping to end users.
set -e

# Resolve script and repository directories
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Detect Architecture
ARCH=$(uname -m)
case "$ARCH" in
    amd64|x86_64)  RID="freebsd-x64" ;;
    arm64|aarch64) RID="freebsd-arm64" ;;
    *)             echo "Unknown architecture: $ARCH. Defaulting to freebsd-x64."; RID="freebsd-x64" ;;
esac

# Define default path (outside the git tree) and prompt user
DEFAULT_PUBLISH_DIR="$HOME/flashback-publish"

echo ""
printf "\033[1;37mWhere should the publish output be located?\033[0m\n"
printf "\033[0;37mDefault: %s\033[0m\n" "$DEFAULT_PUBLISH_DIR"
printf "Path [Enter for default]: "
read INPUT_PATH

if [ -z "$INPUT_PATH" ]; then
    PUBLISH_DIR="$DEFAULT_PUBLISH_DIR"
else
    case "$INPUT_PATH" in
        "~"*) PUBLISH_DIR="${HOME}${INPUT_PATH#\~}" ;;
        /*)   PUBLISH_DIR="$INPUT_PATH" ;;
        *)    PUBLISH_DIR="$(pwd)/$INPUT_PATH" ;;
    esac
fi
mkdir -p "$PUBLISH_DIR"

echo "Cleaning up old binaries (preserving config and licenses)..."
find "$PUBLISH_DIR" -maxdepth 1 -type f ! -name "*.dat" ! -name "*.lic" -delete

echo "Publishing Flashback Suite for FreeBSD $ARCH ($RID)..."
echo "(Note: Native AOT is unsupported on FreeBSD; publishing self-contained single-file binaries)"
echo "(Note: UI components like WPF, WinUI, and Tray are Windows-only and excluded)"

# Common publish arguments:
# Single-file self-contained publish with PublishAot disabled.
PUB_ARGS="-c Release -r $RID -f net9.0 --self-contained true /p:PublishSingleFile=true /p:PublishAot=false /p:PublishDir=$PUBLISH_DIR"

# 1. Engine (Service/Daemon)
echo "-> Publishing Flashback.Engine..."
dotnet publish "$REPO_ROOT/Flashback.Engine/Flashback.Engine.vbproj" $PUB_ARGS

# 2. Console Configuration Tool
echo "-> Publishing Flashback.Config.Console..."
dotnet publish "$REPO_ROOT/Flashback.Config.Console/Flashback.Config.Console.vbproj" $PUB_ARGS

# 3. 3270 Terminal Configuration Tool
echo "-> Publishing Flashback.Config.3270..."
dotnet publish "$REPO_ROOT/Flashback.Config.3270/Flashback.Config.3270.vbproj" $PUB_ARGS

# 4. Spooler Service
echo "-> Publishing Flashback.Spooler..."
dotnet publish "$REPO_ROOT/Flashback.Spooler/Flashback.Spooler.vbproj" $PUB_ARGS

printf "\n\033[1;32mPublish complete! Files located in: %s\033[0m\n" "$PUBLISH_DIR"
