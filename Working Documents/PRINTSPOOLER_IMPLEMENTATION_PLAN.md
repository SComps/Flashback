# Flashback.PrintSpooler - Implementation Plan

## Overview

This document outlines the step-by-step implementation plan for creating the Flashback.PrintSpooler application. The implementation will follow the design specified in [`PRINTSPOOLER_DESIGN.md`](PRINTSPOOLER_DESIGN.md).

## Critical Requirements

### .NET 10 AOT Compatibility
**MANDATORY**: This application MUST compile on Linux with .NET 10 SDK using self-contained AOT builds.

#### AOT Compatibility Constraints
- ✅ **Allowed**: Basic networking (TcpListener, Socket, NetworkStream)
- ✅ **Allowed**: File I/O (FileStream, StreamReader, StreamWriter)
- ✅ **Allowed**: Threading (Task, CancellationToken, async/await)
- ✅ **Allowed**: Collections (List, Queue, ConcurrentQueue, Dictionary)
- ✅ **Allowed**: Microsoft.Extensions.Hosting (BackgroundService)
- ✅ **Allowed**: Microsoft.Extensions.Logging
- ❌ **Avoid**: Reflection-heavy operations
- ❌ **Avoid**: Dynamic code generation
- ❌ **Avoid**: Complex serialization (use simple text formats)

#### Project Configuration
```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <RootNamespace>Flashback.PrintSpooler</RootNamespace>
  <TargetFrameworks>net10.0;net10.0-windows</TargetFrameworks>
  <Deterministic>false</Deterministic>
  <AssemblyVersion>1.0.*</AssemblyVersion>
</PropertyGroup>
```

#### Build Commands
**Linux**:
```bash
dotnet publish Flashback.PrintSpooler.vbproj -c Release -r linux-x64 -f net10.0 --self-contained true /p:PublishAot=true
```

**Windows**:
```powershell
dotnet publish Flashback.PrintSpooler.vbproj -c Release -r win-x64 -f net10.0-windows --self-contained true /p:PublishAot=true
```

---

## Implementation Phases

### Phase 1: Project Setup and Infrastructure

#### 1.1 Create Project Structure
- [ ] Create `Flashback.PrintSpooler` directory
- [ ] Create `Flashback.PrintSpooler.vbproj` file with AOT-compatible settings
- [ ] Add project reference to `Flashback.Core`
- [ ] Configure NuGet packages:
  - Microsoft.Extensions.Hosting (10.0.5)
  - Microsoft.Extensions.Hosting.WindowsServices (10.0.5)
  - Microsoft.Extensions.Hosting.Systemd (10.0.5)
- [ ] Update `Flashback.slnx` to include new project

#### 1.2 Create Base Files
- [ ] Create `Program.vb` (entry point)
- [ ] Create `PrintSpoolerWorker.vb` (main service)
- [ ] Create `Models` directory
- [ ] Create basic logging configuration

**Estimated Time**: 1-2 hours

---

### Phase 2: Configuration Management

#### 2.1 Configuration Models
- [ ] Create `Models/SpoolerConfig.vb`
  - Simple property classes (AOT-friendly)
  - No complex serialization attributes
  - ListenerConfig class (Port9100Enabled, EnginePort)
  - StorageConfig class (SpoolDirectory, MaxSpoolAge, MaxSpoolFiles)
  - LoggingConfig class (LogLevel, LogFile)
  - BehaviorConfig class (JobCompletionTimeout, MaxJobSizeMB, EnableRetry, etc.)

#### 2.2 Configuration Manager
- [ ] Create `ConfigManager.vb`
  - Use simple text file parsing (INI-style format)
  - **NO JSON/XML serialization** (not AOT-friendly)
  - Manual parsing with String.Split
  - Provide default values
  - Validate configuration
  - Support configuration reload

#### 2.3 Default Configuration File
- [ ] Create default `printspooler.conf` template (INI format)
- [ ] Document all configuration options

**Estimated Time**: 2-3 hours

---

### Phase 3: Data Models and Queue Management

#### 3.1 Print Job Model
- [ ] Create `Models/PrintJob.vb`
  - Simple class with properties (no attributes)
  - JobId (GUID)
  - ReceivedTime (DateTime)
  - SpoolFilePath (String)
  - State (Enum: Receiving, Spooled, Queued, Transmitting, Completed, Failed, Expired)
  - RetryCount (Integer)
  - LastAttemptTime (DateTime)
  - FileSize (Long)
  - SourceEndpoint (String)

#### 3.2 Job Queue Manager
- [ ] Create `JobQueue.vb`
  - Thread-safe queue implementation (ConcurrentQueue - AOT compatible)
  - Enqueue method
  - Dequeue method
  - Peek method
  - Count property
  - Events: JobAdded, JobCompleted, JobFailed
  - Retry logic implementation
  - State management

**Estimated Time**: 3-4 hours

---

### Phase 4: Spool File Management

#### 4.1 Spool Manager
- [ ] Create `SpoolManager.vb`
  - Initialize spool directory
  - Create temporary file for incoming job
  - Write data to spool file (streaming with FileStream)
  - Read data from spool file (streaming with FileStream)
  - Delete spool file after successful transmission
  - Cleanup old/expired files
  - Enforce size limits
  - Handle file system errors gracefully

#### 4.2 File Naming Convention
- [ ] Implement naming: `job_<timestamp>_<sequence>.dat`
- [ ] Ensure unique file names
- [ ] Support recovery from existing spool files on startup

**Estimated Time**: 2-3 hours

---

### Phase 5: Port 9100 Listener

#### 5.1 Basic Listener Implementation
- [ ] Create `Port9100Listener.vb`
- [ ] Implement TCP listener on port 9100 (TcpListener - AOT compatible)
- [ ] Accept incoming connections
- [ ] Handle multiple concurrent connections
- [ ] Log connection events

#### 5.2 Data Reception
- [ ] Implement async data reading (NetworkStream.ReadAsync)
- [ ] Stream data to temporary file (via SpoolManager)
- [ ] Detect job completion:
  - Connection close
  - Inactivity timeout (configurable)
- [ ] Handle partial jobs
- [ ] Implement buffer management (byte arrays)

#### 5.3 Job Creation
- [ ] Create PrintJob object when job is complete
- [ ] Add job to JobQueue
- [ ] Log job receipt
- [ ] Handle errors gracefully

#### 5.4 Connection Management
- [ ] Implement keep-alive detection
- [ ] Handle client disconnections
- [ ] Clean up resources properly (using statements)
- [ ] Support rapid reconnections

**Estimated Time**: 4-5 hours

---

### Phase 6: Engine Connection Listener

#### 6.1 Basic Listener Implementation
- [ ] Create `EngineListener.vb`
- [ ] Implement TCP listener on configurable port (TcpListener)
- [ ] Accept single Engine connection
- [ ] Reject additional connections while Engine is connected
- [ ] Log connection state changes

#### 6.2 Connection Management
- [ ] Monitor connection health
- [ ] Detect disconnections
- [ ] Signal JobQueue when ready
- [ ] Handle reconnection scenarios
- [ ] Implement graceful shutdown

#### 6.3 Data Transmission
- [ ] Implement async data sending (NetworkStream.WriteAsync)
- [ ] Stream data from spool file to Engine
- [ ] Handle transmission errors
- [ ] Implement retry logic
- [ ] Confirm successful transmission
- [ ] Notify SpoolManager to delete file

#### 6.4 Queue Integration
- [ ] Subscribe to JobQueue events
- [ ] Automatically transmit queued jobs when connected
- [ ] Handle queue empty state
- [ ] Implement flow control

**Estimated Time**: 4-5 hours

---

### Phase 7: Main Service Worker

#### 7.1 Service Lifecycle
- [ ] Implement `PrintSpoolerWorker.vb` (inherits BackgroundService)
- [ ] Initialize all components in correct order
- [ ] Start Port9100Listener
- [ ] Start EngineListener
- [ ] Start JobQueue processor
- [ ] Handle cancellation token
- [ ] Implement graceful shutdown

#### 7.2 Component Coordination
- [ ] Wire up event handlers between components
- [ ] Implement health monitoring
- [ ] Handle component failures
- [ ] Restart failed components if possible

#### 7.3 Logging Integration
- [ ] Use Flashback.Core.FileLogger (already AOT-compatible)
- [ ] Log service lifecycle events
- [ ] Log component status
- [ ] Implement log rotation

**Estimated Time**: 3-4 hours

---

### Phase 8: Program Entry Point

#### 8.1 Service Configuration
- [ ] Implement `Program.vb`
- [ ] Configure Host builder (CreateApplicationBuilder)
- [ ] Add Windows Service support (AddWindowsService)
- [ ] Add Linux Systemd support (AddSystemd)
- [ ] Configure dependency injection
- [ ] Load configuration
- [ ] Setup logging (AddFile from Flashback.Core)

#### 8.2 Command Line Arguments
- [ ] Implement help (-h, --help)
- [ ] Implement daemon mode (-d, --daemon) for Linux
- [ ] Implement config file path override
- [ ] Implement version display

#### 8.3 Single Instance Check
- [ ] Implement mutex for single instance enforcement (like Flashback.Engine)
- [ ] Display error if already running

**Estimated Time**: 2-3 hours

---

### Phase 9: Error Handling and Resilience

#### 9.1 Exception Handling
- [ ] Wrap all network operations in try-catch
- [ ] Wrap all file operations in try-catch
- [ ] Log all exceptions with context
- [ ] Prevent service crashes

#### 9.2 Retry Logic
- [ ] Implement exponential backoff for retries
- [ ] Configure max retry attempts
- [ ] Move failed jobs to failed queue
- [ ] Log retry attempts

#### 9.3 Resource Cleanup
- [ ] Ensure all sockets are closed
- [ ] Ensure all files are closed
- [ ] Dispose of all disposable objects
- [ ] Implement using statements where appropriate

**Estimated Time**: 2-3 hours

---

### Phase 10: Testing

#### 10.1 AOT Build Testing
- [ ] Test AOT compilation on Linux (net10.0)
- [ ] Test AOT compilation on Windows (net10.0-windows)
- [ ] Verify no AOT warnings or errors
- [ ] Test runtime behavior of AOT builds

#### 10.2 Unit Tests
- [ ] Test JobQueue operations
- [ ] Test SpoolManager file operations
- [ ] Test ConfigManager parsing
- [ ] Test PrintJob state transitions

#### 10.3 Integration Tests
- [ ] Test Port9100Listener with mock client
- [ ] Test EngineListener with mock Engine
- [ ] Test end-to-end job flow
- [ ] Test reconnection scenarios
- [ ] Test error recovery

#### 10.4 Manual Testing
- [ ] Use Flashback.TestTool to send test jobs
- [ ] Configure real Flashback.Engine connection
- [ ] Verify PDF generation
- [ ] Test disconnection/reconnection
- [ ] Test spool cleanup
- [ ] Test configuration changes
- [ ] Test service restart

**Estimated Time**: 5-7 hours

---

### Phase 11: Documentation

#### 11.1 Code Documentation
- [ ] Add XML comments to all public methods
- [ ] Document complex algorithms
- [ ] Add usage examples in comments

#### 11.2 User Documentation
- [ ] Create README.md for PrintSpooler
- [ ] Document configuration options
- [ ] Provide installation instructions
- [ ] Create troubleshooting guide
- [ ] Add example configurations

#### 11.3 Integration Guide
- [ ] Document how to configure Flashback.Engine
- [ ] Provide example devices.dat entry
- [ ] Document network requirements
- [ ] Create architecture diagrams

**Estimated Time**: 2-3 hours

---

### Phase 12: Deployment and Installation

#### 12.1 Build Scripts
- [ ] Update `scripts/publish_linux.sh` to include PrintSpooler
- [ ] Update `scripts/publish_windows.ps1` to include PrintSpooler
- [ ] Configure release builds with AOT
- [ ] Test builds on target platforms (Linux x64, arm64, Windows x64)

#### 12.2 Installation Scripts
- [ ] Create Windows service installation script
- [ ] Create Linux systemd service file
- [ ] Create uninstall scripts
- [ ] Test installation procedures

#### 12.3 Packaging
- [ ] Update Inno Setup script to include PrintSpooler
- [ ] Create standalone installer
- [ ] Test installer on clean systems

**Estimated Time**: 3-4 hours

---

## Implementation Order

The phases should be implemented in the following order:

1. **Phase 1**: Project Setup (foundation with AOT configuration)
2. **Phase 2**: Configuration Management (simple text parsing, no JSON)
3. **Phase 3**: Data Models and Queue (simple classes, no reflection)
4. **Phase 4**: Spool File Management (FileStream-based)
5. **Phase 5**: Port 9100 Listener (TcpListener-based)
6. **Phase 6**: Engine Connection Listener (TcpListener-based)
7. **Phase 7**: Main Service Worker (BackgroundService)
8. **Phase 8**: Program Entry Point (Host builder)
9. **Phase 9**: Error Handling (robustness)
10. **Phase 10**: Testing (validation + AOT testing)
11. **Phase 11**: Documentation (knowledge transfer)
12. **Phase 12**: Deployment (delivery with AOT builds)

## Total Estimated Time

- **Minimum**: 34 hours
- **Maximum**: 47 hours
- **Average**: 40 hours

This estimate assumes:
- Familiarity with VB.NET and .NET 10
- Familiarity with the Flashback codebase
- Experience with AOT constraints
- No major architectural changes during implementation
- Standard debugging and troubleshooting time

## Key Implementation Notes

### 1. Follow Existing Patterns
- Study [`Flashback.Engine/Worker.vb`](E:\flashback\Flashback.Engine\Worker.vb) for service patterns
- Study [`Flashback.Core/Devs.vb`](E:\flashback\Flashback.Core\Devs.vb) for networking patterns
- Study [`Flashback.Engine/Program.vb`](E:\flashback\Flashback.Engine\Program.vb) for entry point patterns
- Use similar logging approaches
- Follow existing error handling patterns

### 2. Reuse Core Components
- Use `Flashback.Core.FileLogger` for logging (already AOT-compatible)
- Use `Flashback.Core.SecurityUtils` for filename sanitization
- Follow existing configuration patterns (simple text files)

### 3. AOT-Specific Considerations
- **NO JSON serialization**: Use simple text parsing (INI format)
- **NO XML serialization**: Use simple text parsing
- **NO Reflection**: Use direct property access
- **NO Dynamic code**: All types known at compile time
- **Simple collections**: List, Queue, Dictionary (all AOT-compatible)
- **Direct I/O**: FileStream, NetworkStream (all AOT-compatible)
- **Test early**: Build with AOT from the start to catch issues

### 4. Network Programming Best Practices
- Use async/await for all I/O operations
- Implement proper socket disposal (using statements)
- Use CancellationToken for graceful shutdown
- Configure TCP keep-alive settings
- Handle partial reads/writes

### 5. File System Best Practices
- Use streaming for large files (FileStream)
- Implement proper file locking
- Handle disk full scenarios
- Implement atomic file operations where possible

### 6. Threading Best Practices
- Use thread-safe collections (ConcurrentQueue)
- Minimize lock contention
- Use async methods instead of blocking
- Properly handle cancellation

## Success Criteria

The implementation will be considered successful when:

1. ✅ Compiles successfully with AOT on Linux (net10.0)
2. ✅ Compiles successfully with AOT on Windows (net10.0-windows)
3. ✅ Port 9100 listener accepts and stores print jobs
4. ✅ Engine listener accepts Flashback.Engine connections
5. ✅ Jobs are transmitted to Engine without data loss
6. ✅ Spool files are properly managed and cleaned up
7. ✅ Service runs reliably for extended periods
8. ✅ Reconnection scenarios work correctly
9. ✅ Configuration changes are applied correctly
10. ✅ All error scenarios are handled gracefully
11. ✅ Logging provides adequate troubleshooting information
12. ✅ Service can be installed and run on Windows and Linux

## Next Steps

After reviewing and approving this plan:

1. Switch to **Code mode** to begin implementation
2. Start with Phase 1 (Project Setup with AOT configuration)
3. Implement phases sequentially
4. Test AOT compilation after each major phase
5. Update this document with actual progress and any deviations

## Questions or Concerns?

Before proceeding with implementation, please review:
- Does the architecture meet your requirements?
- Are the AOT constraints acceptable?
- Are there any missing features or considerations?
- Do you want to adjust the implementation order?
- Are there any specific coding standards to follow?