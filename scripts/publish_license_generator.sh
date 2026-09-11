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

if [ "$OS" = "FreeBSD" ]; then
    echo "Publishing Flashback.LicenseGenerator.Console for FreeBSD $ARCH..."
    echo "(Note: FreeBSD uses system dotnet runtime; publishing framework-dependent binaries)"
    dotnet publish "$REPO_ROOT/Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj" \
        -c Release \
        -f net9.0 \
        --no-self-contained \
        /p:PublishAot=false \
        /p:PublishDir="$PUBLISH_DIR"

    # Create launcher
    cat << EOF > "$PUBLISH_DIR/Flashback.LicenseGenerator.Console"
#!/bin/sh
APP_DIR="\$(cd "\$(dirname "\$0")" && pwd)"
exec dotnet "\$APP_DIR/Flashback.LicenseGenerator.Console.dll" "\$@"
EOF
    chmod +x "$PUBLISH_DIR/Flashback.LicenseGenerator.Console"
else
    case $ARCH in
        x86_64)  RID="linux-x64" ;;
        aarch64) RID="linux-arm64" ;;
        armv7l)  RID="linux-arm" ;;
        *)       echo "Unknown architecture: $ARCH. Defaulting to x64."; RID="linux-x64" ;;
    esac

    echo "Publishing Flashback.LicenseGenerator.Console for Linux $ARCH ($RID)..."
    dotnet publish "$REPO_ROOT/Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj" \
        -c Release \
        -r $RID \
        -f net9.0 \
        --self-contained true \
        /p:PublishAot=true \
        /p:PublishDir="$PUBLISH_DIR"
fi

echo -e "\nPublish complete! Files located in: $PUBLISH_DIR"

