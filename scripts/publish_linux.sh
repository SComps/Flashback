#!/bin/bash
# Flashback Suite - Linux Publish Script (Native AOT / Multi-Arch)
# NOTE: Flashback.LicenseGenerator is EXCLUDED from this script.
set -e

# Detect Architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64)  RID="linux-x64" ;;
    aarch64) RID="linux-arm64" ;;
    armv7l)  RID="linux-arm" ;;
    *)       echo "Unknown architecture: $ARCH. Defaulting to x64."; RID="linux-x64" ;;
esac

PUBLISH_DIR="../publish/linux"
mkdir -p "$PUBLISH_DIR"

echo "Cleaning up old binaries (preserving config and licenses)..."
find "$PUBLISH_DIR" -maxdepth 1 -type f ! -name "*.dat" ! -name "*.lic" -delete

echo "Publishing Flashback Suite for $ARCH ($RID)..."

# Engine
echo "-> Publishing Flashback.Engine..."
dotnet publish ../Flashback.Engine/Flashback.Engine.vbproj -c Release -r $RID --self-contained true /p:PublishAot=true /p:PublishDir="$PUBLISH_DIR"

# Console Config
echo "-> Publishing Flashback.Config.Console..."
dotnet publish ../Flashback.Config.Console/Flashback.Config.Console.vbproj -c Release -r $RID --self-contained true /p:PublishAot=true /p:PublishDir="$PUBLISH_DIR"

# 3270 Config
echo "-> Publishing Flashback.Config.3270..."
dotnet publish ../Flashback.Config.3270/Flashback.Config.3270.vbproj -c Release -r $RID --self-contained true /p:PublishAot=true /p:PublishDir="$PUBLISH_DIR"

echo -e "\nPublish complete! Files located in: $PUBLISH_DIR"
