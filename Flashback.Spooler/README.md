# Flashback.Spooler

A network print spooler service that receives print jobs on port 9100 (JetDirect compatible) and forwards them to Flashback.Engine for PDF generation.

## Overview

Flashback.Spooler acts as an intermediary between legacy port 9100 printing infrastructure and the Flashback.Engine PDF generation system. It provides:

- **Port 9100 Listener**: Accepts raw print data from network printers and applications
- **Job Spooling**: Temporary storage of print jobs with automatic cleanup
- **Queue Management**: FIFO queue with retry logic and state tracking
- **Engine Integration**: Forwards jobs to Flashback.Engine for processing

## Architecture

```
[Port 9100 Devices] → [Spooler Port 9100] → [Temp Files] → [Spooler Engine Port] ← [Flashback.Engine]
```

## Installation

### Windows

#### As a Windows Service

```powershell
# Install service
sc.exe create FlashbackSpooler binPath="C:\Path\To\Flashback.Spooler.exe" start=auto

# Start service
sc.exe start FlashbackSpooler

# Stop service
sc.exe stop FlashbackSpooler

# Uninstall service
sc.exe delete FlashbackSpooler
```

#### Manual Execution

```powershell
# Run in foreground
.\Flashback.Spooler.exe

# Show help
.\Flashback.Spooler.exe --help

# Show version
.\Flashback.Spooler.exe --version
```

### Linux

#### As a Systemd Service

Create `/etc/systemd/system/flashback-spooler.service`:

```ini
[Unit]
Description=Flashback Spooler Service
After=network.target

[Service]
Type=notify
ExecStart=/usr/local/bin/Flashback.Spooler
Restart=always
RestartSec=10
User=flashback
WorkingDirectory=/var/lib/flashback/spooler

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable flashback-spooler
sudo systemctl start flashback-spooler
sudo systemctl status flashback-spooler
```

#### Manual Execution

```bash
# Run in foreground
./Flashback.Spooler

# Run in background (daemon mode)
./Flashback.Spooler --daemon

# Show help
./Flashback.Spooler --help
```

## Configuration

Configuration is stored in `spooler.conf` (INI format). If the file doesn't exist, a default configuration will be created automatically.

### Configuration File Location

- **Windows**: Same directory as executable
- **Linux**: Current working directory or `/etc/flashback/spooler.conf`

### Configuration Options

```ini
[Listener]
# Enable port 9100 listener (JetDirect compatible)
Port9100Enabled=true

# Port for Flashback.Engine to connect to
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
```

## Flashback.Engine Configuration

To connect Flashback.Engine to the Spooler, add a device entry to `devices.dat`:

```
Spooler||Print Spooler Gateway||0||0||127.0.0.1:9001||0||False||True||0||Output||0||1||True
```

**Field Breakdown:**
- **DevName**: `Spooler`
- **DevDescription**: `Print Spooler Gateway`
- **DevType**: `0` (standard)
- **ConnType**: `0` (client connection - Engine connects TO Spooler)
- **DevDest**: `127.0.0.1:9001` (Spooler's Engine listener port)
- **OS**: `0` (Generic - raw data passthrough)
- **PDF**: `True`
- **Enabled**: `True`

## Network Ports

- **Port 9100**: Receives print jobs (JetDirect compatible) - **FIXED**
- **Port 9001**: Flashback.Engine connects here - **CONFIGURABLE**

### Firewall Configuration

#### Windows

```powershell
# Allow port 9100 (incoming print jobs)
New-NetFirewallRule -DisplayName "Flashback Spooler - Port 9100" -Direction Inbound -LocalPort 9100 -Protocol TCP -Action Allow

# Allow port 9001 (Engine connection) - localhost only recommended
New-NetFirewallRule -DisplayName "Flashback Spooler - Engine Port" -Direction Inbound -LocalPort 9001 -Protocol TCP -Action Allow -RemoteAddress 127.0.0.1
```

#### Linux (ufw)

```bash
# Allow port 9100 from network
sudo ufw allow 9100/tcp

# Allow port 9001 from localhost only
sudo ufw allow from 127.0.0.1 to any port 9001 proto tcp
```

## Usage

### Sending Print Jobs

Any application or device that supports port 9100 printing can send jobs to the Spooler:

#### From Windows

```powershell
# Add network printer
Add-Printer -Name "Flashback Spooler" -PortName "IP_192.168.1.100:9100" -DriverName "Generic / Text Only"
```

#### From Linux (CUPS)

```bash
# Add printer
lpadmin -p flashback-spooler -v socket://192.168.1.100:9100 -E
```

#### Raw TCP/IP

```bash
# Send file directly
cat document.txt | nc 192.168.1.100 9100
```

### Monitoring

#### View Logs

**Windows:**
```powershell
Get-Content -Path ".\logs\spooler.log" -Tail 50 -Wait
```

**Linux:**
```bash
tail -f ./logs/spooler.log
```

#### Check Service Status

**Windows:**
```powershell
sc.exe query FlashbackSpooler
```

**Linux:**
```bash
systemctl status flashback-spooler
```

## Troubleshooting

### Port 9100 Connection Refused

- Check if Spooler service is running
- Verify firewall allows port 9100
- Check `Port9100Enabled=true` in config

### Engine Not Connecting

- Verify Flashback.Engine is running
- Check Engine's `devices.dat` configuration
- Verify `EnginePort` in spooler.conf matches Engine config
- Check firewall allows Engine port (default 9001)

### Jobs Not Processing

- Check Engine connection status in logs
- Verify spool directory has write permissions
- Check disk space availability
- Review `MaxJobSizeMB` setting

### Spool Directory Full

- Reduce `MaxSpoolAge` to clean up old files sooner
- Reduce `MaxSpoolFiles` to limit total file count
- Manually clean spool directory if needed
- Check that jobs are being transmitted successfully

## Performance

### Scalability

- **Concurrent Jobs**: Supports multiple simultaneous port 9100 connections
- **Queue Size**: Limited only by available disk space
- **Throughput**: 100+ jobs per hour typical
- **Latency**: < 1 second from job completion to Engine transmission

### Resource Usage

- **Memory**: ~50-100 MB typical
- **CPU**: Minimal (mostly I/O operations)
- **Disk**: Depends on job size and retention settings

## Security Considerations

### Network Exposure

- Port 9100 is exposed to the network (standard for printers)
- Engine port should be localhost-only in production
- Consider firewall rules to restrict access

### File System

- Spool directory should have restricted permissions
- Prevent unauthorized access to temporary files
- Implement file size limits to prevent disk exhaustion

### Resource Limits

- Maximum job size enforcement (`MaxJobSizeMB`)
- Maximum queue depth (`MaxSpoolFiles`)
- Automatic cleanup of old spool files

## Command Line Options

```
Usage: Flashback.Spooler [options]

Options:
  -h, --help              Show help message
  -v, --version           Show version information
  -d, --daemon            Run in background (Linux only)
  -c, --config <path>     Specify configuration file path

Examples:
  Flashback.Spooler
  Flashback.Spooler -c /etc/flashback/spooler.conf
  Flashback.Spooler --daemon
```

## Technical Details

### Data Flow

1. Client connects to port 9100
2. Spooler receives raw print data
3. Data is streamed to temporary spool file
4. Job completion detected (connection close or timeout)
5. Job added to transmission queue
6. When Engine is connected, job is transmitted
7. On successful transmission, spool file is deleted

### Job States

- **Receiving**: Data being received on port 9100
- **Spooled**: Complete job stored in temporary file
- **Queued**: Job waiting for Engine connection
- **Transmitting**: Job being sent to Engine
- **Completed**: Job successfully transmitted
- **Failed**: Job transmission failed (will retry)
- **Expired**: Job exceeded retry limit or age limit

### File Naming

Spool files use the format: `job_YYYYMMDD_HHMMSS_NNNNNN.dat`

Example: `job_20260529_143022_000001.dat`

## Building from Source

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022 or VS Code

### Build Commands

**Debug Build:**
```bash
dotnet build Flashback.Spooler.vbproj
```

**Release Build (AOT):**
```bash
# Windows
dotnet publish -c Release -r win-x64 -f net10.0-windows --self-contained true /p:PublishAot=true

# Linux
dotnet publish -c Release -r linux-x64 -f net10.0 --self-contained true /p:PublishAot=true
```

## License

Part of the Flashback Print Server Suite.
Copyright (c) 2024-2026

## Support

For issues, questions, or contributions, please refer to the main Flashback repository.

## Version History

### 1.0.0 (2026-05-29)
- Initial release
- Port 9100 listener (JetDirect compatible)
- Engine connection and job forwarding
- Job spooling with automatic cleanup
- Retry logic and error handling
- Windows Service and Linux Systemd support
- AOT compilation for optimal performance