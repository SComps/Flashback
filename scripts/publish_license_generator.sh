#!/bin/bash
# Flashback License Generator Console - Linux Publish Script (Native AOT)
# Publishes to the same directory as other Flashback components
set -e

# Resolve script and repository directories
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Detect OS and Architecture
OS=$(uname -s)
ARCH=$(uname -m)

if [ "$OS" = "FreeBSD" ]; then
    case $ARCH in
        amd64|x86_64)  RID="freebsd-x64" ;;
        arm64|aarch64) RID="freebsd-arm64" ;;
        *)             echo "Unknown architecture: $ARCH. Defaulting to freebsd-x64."; RID="freebsd-x64" ;;
    esac
    PUB_EXTRA_FLAGS="/p:PublishSingleFile=true /p:PublishAot=false"
else
    case $ARCH in
        x86_64)  RID="linux-x64" ;;
        aarch64) RID="linux-arm64" ;;
        armv7l)  RID="linux-arm" ;;
        *)       echo "Unknown architecture: $ARCH. Defaulting to x64."; RID="linux-x64" ;;
    esac
    PUB_EXTRA_FLAGS="/p:PublishAot=true"
fi

# Define default path (outside the git tree) and prompt user
DEFAULT_PUBLISH_DIR="$HOME/flashback-publish"

echo ""
echo -e "\033[1;37mWhere should the publish output be located?\033[0m"
echo -e "\033[0;37mDefault: $DEFAULT_PUBLISH_DIR\033[0m"
read -p "Path [Enter for default]: " INPUT_PATH

if [ -z "$INPUT_PATH" ]; then
    PUBLISH_DIR="$DEFAULT_PUBLISH_DIR"
else
    # Safely expand ~ if present
    if [[ "$INPUT_PATH" == "~"* ]]; then
        PUBLISH_DIR="${HOME}${INPUT_PATH:1}"
    else
        PUBLISH_DIR="$INPUT_PATH"
    fi

    # Convert to absolute path if relative
    if [[ "$PUBLISH_DIR" != /* ]]; then
        PUBLISH_DIR="$(pwd)/$PUBLISH_DIR"
    fi
fi
mkdir -p "$PUBLISH_DIR"

echo "Publishing Flashback.LicenseGenerator.Console for $OS $ARCH ($RID)..."

# Publish License Generator Console
echo "-> Publishing Flashback.LicenseGenerator.Console..."
dotnet publish "$REPO_ROOT/Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj" \
    -c Release \
    -r $RID \
    -f net9.0 \
    --self-contained true \
    $PUB_EXTRA_FLAGS \
    /p:PublishDir="$PUBLISH_DIR"

echo -e "\nPublish complete! Files located in: $PUBLISH_DIR"

