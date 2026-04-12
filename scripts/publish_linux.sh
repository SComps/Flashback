#!/bin/bash
# Flashback Suite - Linux Publish Script (Native AOT)
# NOTE: Flashback.LicenseGenerator is EXCLUDED from this script to prevent shipping to end users.
set -e

PUBLISH_DIR="../publish/linux"
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

echo "Publishing Flashback Suite for Linux (x64)..."

# Engine (Console App - AOT Compatible)
echo "-> Publishing Flashback.Engine (Native AOT)..."
dotnet publish ../Flashback.Engine/Flashback.Engine.vbproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true /p:PublishDir="$PUBLISH_DIR"

# Console Config (Console App - AOT Compatible)
echo "-> Publishing Flashback.Config.Console (Native AOT)..."
dotnet publish ../Flashback.Config.Console/Flashback.Config.Console.vbproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true /p:PublishDir="$PUBLISH_DIR"

# 3270 Config (Console App - AOT Compatible)
echo "-> Publishing Flashback.Config.3270 (Native AOT)..."
dotnet publish ../Flashback.Config.3270/Flashback.Config.3270.vbproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true /p:PublishDir="$PUBLISH_DIR"

echo -e "\nPublish complete! Files located in: $PUBLISH_DIR"
