#!/bin/bash
# Flashback Spooler - Linux Systemd Service Installation Script

set -e

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root (use sudo)"
    exit 1
fi

echo ""
echo "Flashback Spooler Service Installation"
echo "======================================"
echo ""

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$(dirname "$SCRIPT_DIR")"

# Prompt for installation directory
echo "Default installation directory: /usr/local/bin"
read -p "Installation directory [Enter for default]: " INSTALL_DIR

if [ -z "$INSTALL_DIR" ]; then
    INSTALL_DIR="/usr/local/bin"
fi

# Check if Flashback.Spooler exists in publish directory
SPOOLER_EXE="$PUBLISH_DIR/Flashback.Spooler"

if [ ! -f "$SPOOLER_EXE" ]; then
    echo "ERROR: Flashback.Spooler not found in $PUBLISH_DIR"
    echo "Please run publish_linux.sh first to build the application"
    exit 1
fi

# Copy executable
echo ""
echo "Copying Flashback.Spooler to $INSTALL_DIR..."
cp "$SPOOLER_EXE" "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/Flashback.Spooler"

# Create working directory
WORK_DIR="/var/lib/flashback/spooler"
echo "Creating working directory: $WORK_DIR"
mkdir -p "$WORK_DIR"
mkdir -p "$WORK_DIR/logs"
mkdir -p "$WORK_DIR/spool"

# Create flashback user if it doesn't exist
if ! id -u flashback > /dev/null 2>&1; then
    echo "Creating flashback user..."
    useradd -r -s /bin/false -d /var/lib/flashback flashback
fi

# Set ownership
chown -R flashback:flashback "$WORK_DIR"

# Create default configuration file
CONFIG_FILE="$WORK_DIR/spooler.conf"
if [ ! -f "$CONFIG_FILE" ]; then
    echo "Creating default configuration file..."
    cat > "$CONFIG_FILE" << 'EOF'
# Flashback Spooler Configuration File
# Lines starting with # or ; are comments

[Listener]
# Port 9100 is fixed for JetDirect compatibility
Port9100Enabled=true

# Engine connection port (configurable)
EnginePort=9001

[Storage]
# Temporary spool directory
SpoolDirectory=./spool

# Maximum spool file age in hours before cleanup
MaxSpoolAge=24

# Maximum number of spool files to retain
MaxSpoolFiles=1000

[Logging]
# Log level: Trace, Debug, Info, Warning, Error
LogLevel=Info

# Log file location
LogFile=./logs/spooler.log

[Behavior]
# Timeout in seconds for detecting job completion on port 9100
JobCompletionTimeout=5

# Maximum job size in MB (0 = unlimited)
MaxJobSizeMB=100

# Enable automatic retry for failed transmissions
EnableRetry=true

# Retry attempts before giving up
MaxRetries=3

# Retry delay in seconds
RetryDelaySeconds=30
EOF
    chown flashback:flashback "$CONFIG_FILE"
    echo "Configuration file created: $CONFIG_FILE"
fi

# Create systemd service file
SERVICE_FILE="/etc/systemd/system/flashback-spooler.service"
echo ""
echo "Creating systemd service file..."

cat > "$SERVICE_FILE" << EOF
[Unit]
Description=Flashback Spooler Service
After=network.target

[Service]
Type=notify
ExecStart=$INSTALL_DIR/Flashback.Spooler
Restart=always
RestartSec=10
User=flashback
WorkingDirectory=$WORK_DIR

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$WORK_DIR

[Install]
WantedBy=multi-user.target
EOF

echo "Service file created: $SERVICE_FILE"

# Reload systemd
echo ""
echo "Reloading systemd daemon..."
systemctl daemon-reload

# Enable and start service
echo "Enabling flashback-spooler service..."
systemctl enable flashback-spooler

echo "Starting flashback-spooler service..."
systemctl start flashback-spooler

# Wait a moment for service to start
sleep 2

# Check service status
if systemctl is-active --quiet flashback-spooler; then
    echo ""
    echo "Installation completed successfully!"
    echo ""
    echo "Service Name: flashback-spooler"
    echo "Status: Running"
    echo "Installation Directory: $INSTALL_DIR"
    echo "Working Directory: $WORK_DIR"
    echo "Configuration File: $CONFIG_FILE"
    echo "Log Directory: $WORK_DIR/logs"
    echo "Spool Directory: $WORK_DIR/spool"
    echo ""
    echo "Network Ports:"
    echo "  Port 9100: Receives print jobs (JetDirect compatible)"
    echo "  Port 9001: Flashback.Engine connects here"
    echo ""
    echo "Service Management Commands:"
    echo "  Start:   sudo systemctl start flashback-spooler"
    echo "  Stop:    sudo systemctl stop flashback-spooler"
    echo "  Restart: sudo systemctl restart flashback-spooler"
    echo "  Status:  sudo systemctl status flashback-spooler"
    echo "  Logs:    sudo journalctl -u flashback-spooler -f"
    echo ""
    echo "View Application Logs:"
    echo "  tail -f $WORK_DIR/logs/spooler.log"
    echo ""
else
    echo ""
    echo "WARNING: Service installed but not running"
    echo "Check status with: sudo systemctl status flashback-spooler"
    echo "Check logs with: sudo journalctl -u flashback-spooler -n 50"
fi

# Made with Bob
