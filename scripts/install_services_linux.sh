#!/bin/bash
# Flashback Suite - Linux Systemd Installer
# Run as root to install the Engine and 3270 Config daemons.

if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root (sudo)." 
   exit 1
fi

INSTALL_DIR=$(pwd)
read -p "Enter the full path to the Flashback binaries (Default: $INSTALL_DIR/../publish/linux): " USER_PATH
if [ -z "$USER_PATH" ]; then
    USER_PATH="$INSTALL_DIR/../publish/linux"
fi
USER_PATH=$(readlink -f $USER_PATH)

function create_service {
    local NAME=$1
    local DESC=$2
    local EXE=$3
    local SERVICE_FILE="/etc/systemd/system/${NAME}.service"

    echo "Registering $NAME..."
    
    cat <<EOF > $SERVICE_FILE
[Unit]
Description=$DESC
After=network.target

[Service]
Type=notify
ExecStart=$USER_PATH/$EXE
WorkingDirectory=$USER_PATH
Restart=always
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable $NAME
    echo "$NAME registered and enabled."
}

# Engine
if [ -f "$USER_PATH/Flashback.Engine" ]; then
    create_service "flashback-engine" "Flashback Printer Engine" "Flashback.Engine"
else
    echo "Warning: Flashback.Engine not found in $USER_PATH"
fi

# 3270 Config
if [ -f "$USER_PATH/Flashback.Config.3270" ]; then
    create_service "flashback-config3270" "Flashback 3270 Config Server" "Flashback.Config.3270"
else
    echo "Warning: Flashback.Config.3270 not found in $USER_PATH"
fi

echo -e "\nSystemd installation complete. Use 'systemctl start' to launch the daemons."
