#!/bin/bash
# Flashback License Generator Console - Linux Publish Script (Native AOT)
# Publishes to the same directory as other Flashback components
set -e

# Detect Architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64)  RID="linux-x64" ;;
    aarch64) RID="linux-arm64" ;;
    armv7l)  RID="linux-arm" ;;
    *)       echo "Unknown architecture: $ARCH. Defaulting to x64."; RID="linux-x64" ;;
esac

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

echo "Publishing Flashback.LicenseGenerator.Console for $ARCH ($RID)..."

# Publish License Generator Console
echo "-> Publishing Flashback.LicenseGenerator.Console..."
dotnet publish Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj \
    -c Release \
    -r $RID \
    -f net10.0 \
    --self-contained true \
    /p:PublishAot=true \
    /p:PublishDir="$PUBLISH_DIR"

echo -e "\nPublish complete! Files located in: $PUBLISH_DIR"

# Made with Bob
