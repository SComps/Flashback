# Flashback.PrintSpooler - Technical Design Document

## Overview

Flashback.PrintSpooler is a new application in the Flashback suite that acts as an intermediary between Port 9100 print devices and the Flashback.Engine. It receives raw print data on port 9100, stores it temporarily, and forwards it to Flashback.Engine for processing.

## Architecture

### High-Level Design

```
[Port 9100 Devices] 
       ↓
[PrintSpooler Port 9100 Listener]
       ↓
[Temporary File Storage]
       ↓
[PrintSpooler Engine Listener] ← [Flashback.Engine connects as client]
       ↓
[Flashback.Engine processes and creates PDF]
```

### Component Breakdown

#### 1. Port 9100 Listener (JetDirect Compatible)
- **Purpose**: Accept incoming print jobs from network printers and applications
- **Protocol**: Raw TCP/IP on port 9100 (industry standard for network printing)
- **Behavior**: 
  - Listens continuously for incoming connections
  - Accepts multiple sequential connections
  - Receives raw print data without modification
  - Detects job completion via connection close or timeout
  - Stores complete job to temporary file

#### 2. Engine Connection Listener
- **Purpose**: Provide a connection point for Flashback.Engine
- **Protocol**: Raw TCP/IP on user-configurable port (default: 9001)
- **Behavior**:
  - Listens for Flashback.Engine to connect as a client
  - Maintains persistent connection
  - Queues jobs when Engine is not connected
  - Sends complete job data when Engine is ready
  - Handles reconnection scenarios

#### 3. Temporary File Storage
- **Purpose**: Buffer print jobs between receipt and transmission
- **Location**: Configurable directory (default: `./spool`)
- **File Naming**: `job_<timestamp>_<sequence>.dat`
- **Cleanup**: Files deleted after successful transmission
- **Retention**: Failed jobs retained for troubleshooting

#### 4. Job Queue Manager
- **Purpose**: Coordinate job flow between listeners
- **Behavior**:
  - FIFO queue for job processing
  - Monitors Engine connection status
  - Automatically sends queued jobs when Engine connects
  - Handles job retry logic

## Configuration

### Configuration File: `printspooler.conf`

```ini
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
LogFile=./logs/printspooler.log

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

## Data Flow

### Sequence Diagram

```
Port 9100 Device          PrintSpooler              Flashback.Engine
      |                        |                           |
      |--[Connect]------------>|                           |
      |                        |                           |
      |--[Send Print Data]---->|                           |
      |                        |--[Write to temp file]     |
      |                        |                           |
      |--[Close Connection]--->|                           |
      |                        |--[Job Complete]           |
      |                        |                           |
      |                        |<--[Connect]---------------|
      |                        |                           |
      |                        |--[Send Job Data]--------->|
      |                        |                           |
      |                        |<--[Acknowledge]-----------|
      |                        |                           |
      |                        |--[Delete temp file]       |
      |                        |                           |
```

### Job Processing States

1. **RECEIVING**: Data being received on port 9100
2. **SPOOLED**: Complete job stored in temporary file
3. **QUEUED**: Job waiting for Engine connection
4. **TRANSMITTING**: Job being sent to Engine
5. **COMPLETED**: Job successfully transmitted and acknowledged
6. **FAILED**: Job transmission failed (will retry)
7. **EXPIRED**: Job exceeded retry limit or age limit

## Technical Implementation

### Project Structure

```
Flashback.PrintSpooler/
├── Flashback.PrintSpooler.vbproj
├── Program.vb                    # Entry point, service setup
├── PrintSpoolerWorker.vb         # Main background service
├── Port9100Listener.vb           # Handles port 9100 connections
├── EngineListener.vb             # Handles Engine connections
├── JobQueue.vb                   # Job queue management
├── SpoolManager.vb               # Temporary file operations
├── ConfigManager.vb              # Configuration file handling
└── Models/
    ├── PrintJob.vb               # Print job data model
    └── SpoolerConfig.vb          # Configuration data model
```

### Key Classes

#### PrintSpoolerWorker
- Inherits from `BackgroundService`
- Coordinates all components
- Manages service lifecycle
- Handles graceful shutdown

#### Port9100Listener
- Accepts incoming TCP connections on port 9100
- Reads raw data stream
- Detects job completion (connection close or timeout)
- Creates `PrintJob` objects
- Adds jobs to queue

#### EngineListener
- Listens on configurable port for Engine connection
- Maintains single persistent connection
- Monitors connection health
- Signals queue when ready to receive jobs

#### JobQueue
- Thread-safe FIFO queue
- Monitors Engine connection status
- Automatically transmits jobs when Engine is connected
- Implements retry logic for failed transmissions
- Raises events for job state changes

#### SpoolManager
- Creates temporary files for incoming jobs
- Manages spool directory
- Implements cleanup policies (age, count limits)
- Provides job persistence and recovery

### Dependencies

- **Flashback.Core**: For logging utilities and common types
- **Microsoft.Extensions.Hosting**: For background service infrastructure
- **Microsoft.Extensions.Logging**: For structured logging
- **System.Net.Sockets**: For TCP/IP networking

### Error Handling

1. **Port 9100 Connection Errors**
   - Log connection failures
   - Continue listening for new connections
   - Do not crash service

2. **Engine Connection Errors**
   - Queue jobs when Engine is disconnected
   - Attempt reconnection on Engine listener
   - Log connection state changes

3. **File System Errors**
   - Log spool file creation/deletion failures
   - Attempt alternate spool locations if configured
   - Prevent service crash

4. **Data Transmission Errors**
   - Retry failed transmissions (configurable)
   - Move to failed queue after max retries
   - Alert via logging

### Logging Strategy

- **Info Level**: Job received, job transmitted, connection events
- **Warning Level**: Retry attempts, queue full warnings
- **Error Level**: Failed transmissions, file system errors, configuration errors
- **Debug Level**: Detailed data flow, buffer operations
- **Trace Level**: Raw data inspection (disabled by default)

## Integration with Flashback.Engine

### Engine Configuration

In Flashback.Engine's `devices.dat`, users will configure a device entry:

```
PrintSpooler||Print Spooler Gateway||0||0||127.0.0.1:9001||0||False||True||0||Output||0||1||True
```

**Field Breakdown**:
- DevName: `PrintSpooler`
- DevDescription: `Print Spooler Gateway`
- DevType: `0` (standard)
- ConnType: `0` (client connection - Engine connects TO PrintSpooler)
- DevDest: `127.0.0.1:9001` (PrintSpooler's Engine listener port)
- OS: `0` (Generic - raw data passthrough)
- PDF: `True`
- Enabled: `True`

### Data Format

- **No modification**: Data received on port 9100 is transmitted byte-for-byte to Engine
- **No headers**: Raw data stream without protocol wrappers
- **No buffering**: Complete job sent as single transmission
- **No compression**: Data sent uncompressed

## Service Installation

### Windows Service

```powershell
# Install as Windows Service
sc.exe create FlashbackPrintSpooler binPath="C:\Path\To\Flashback.PrintSpooler.exe" start=auto

# Start service
sc.exe start FlashbackPrintSpooler

# Stop service
sc.exe stop FlashbackPrintSpooler

# Uninstall service
sc.exe delete FlashbackPrintSpooler
```

### Linux Systemd Service

```ini
[Unit]
Description=Flashback Print Spooler Service
After=network.target

[Service]
Type=notify
ExecStart=/usr/local/bin/Flashback.PrintSpooler
Restart=always
RestartSec=10
User=flashback
WorkingDirectory=/var/lib/flashback/printspooler

[Install]
WantedBy=multi-user.target
```

## Testing Strategy

### Unit Tests
- Job queue operations (enqueue, dequeue, retry)
- Configuration parsing
- Spool file management
- State machine transitions

### Integration Tests
- Port 9100 listener with test client
- Engine listener with mock Engine
- End-to-end job flow
- Reconnection scenarios

### Manual Testing
- Use `Flashback.TestTool` to send test jobs to port 9100
- Configure Engine to connect to PrintSpooler
- Verify PDF generation
- Test disconnection/reconnection
- Verify spool cleanup

## Performance Considerations

### Scalability
- **Concurrent Jobs**: Support multiple simultaneous port 9100 connections
- **Queue Size**: Configurable maximum queue depth
- **Memory Usage**: Stream large jobs to disk, don't buffer in memory
- **CPU Usage**: Minimal processing, mostly I/O operations

### Throughput
- **Target**: 100+ jobs per hour
- **Latency**: < 1 second from job completion to Engine transmission
- **Network**: Minimal overhead, raw data passthrough

## Security Considerations

1. **Network Exposure**
   - Port 9100 exposed to network (standard for printers)
   - Engine port should be localhost-only by default
   - Consider firewall rules for production

2. **File System**
   - Spool directory should have restricted permissions
   - Prevent unauthorized access to temporary files
   - Implement file size limits to prevent disk exhaustion

3. **Resource Limits**
   - Maximum job size enforcement
   - Maximum queue depth
   - Automatic cleanup of old spool files

## Future Enhancements

1. **Multiple Engine Connections**: Support load balancing across multiple Engine instances
2. **Job Prioritization**: Priority queue for urgent jobs
3. **Web Interface**: Status dashboard and configuration UI
4. **Email Notifications**: Alert on job failures or queue issues
5. **Metrics/Monitoring**: Prometheus/Grafana integration
6. **Job Filtering**: Route jobs to different Engines based on content
7. **Compression**: Optional compression for large jobs
8. **Encryption**: TLS support for Engine connection

## Conclusion

Flashback.PrintSpooler provides a robust, reliable bridge between legacy Port 9100 printing infrastructure and the modern Flashback.Engine PDF generation system. Its simple architecture, comprehensive error handling, and flexible configuration make it an essential component of the Flashback suite.