# 🖨️ Flashback Print Server Suite

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-lightgrey.svg)](https://github.com/scomps/flashback)

**Modern Cross-Platform Print Services for Legacy Host Systems**

Flashback is a robust, high-performance suite that bridges legacy mainframe and minicomputer systems (MVS, z/OS, VMS, MPE, VM/CMS) to modern PDF generation and network printing infrastructure. Transform your vintage computing environment's print output into professional PDFs with full carriage control interpretation, email delivery, and web-based monitoring.

---

## ✨ Key Features

### 🔌 Universal Connectivity
- **Port 9100 (JetDirect)** - Standard network printer protocol compatibility
- **Raw TCP/IP Streams** - Direct connection from any legacy system
- **Bidirectional Communication** - Client or server mode operation

### 📄 Advanced PDF Generation
- **Configurable Orientation** - Portrait or landscape output
- **Professional Shading** - Green bar, blue bar, or plain white backgrounds
- **Automatic Job Numbering** - Sequential tracking of print jobs

### 📧 Email Integration (experimental)
- **Automatic PDF Delivery** - Send generated PDFs directly to email recipients
- **Template Variables** - Dynamic subject and body customization
- **Multiple Recipients** - Semicolon-separated distribution lists
- **SMTP Compatibility** - Should work with Gmail, Office 365, SendGrid, and more (mostly untested)
- **TLS/SSL Support** - Secure email transmission

### 🖥️ Multiple Configuration Interfaces
- **Console Application** - Full-screen interactive TUI for device management
- **3270 Terminal Interface** - Remote administration via TN3270 emulator (configuration only)
- **WPF Desktop Application** - Native Windows GUI with modern design
- **WinUI Application** - Next-generation Windows interface
- **Web-based PDF Viewer** - Browser-based viewing of generated PDFs (optional)

### 🔐 Enterprise Features
- **Licensing System** - Free tier (2 printers) No, I'm not charging for licenses; it was an experiment.
- **Multi-Platform** - Windows (x64) and Linux (x64/ARM64) support
- **Background Services** - Windows Service and Linux systemd integration
- **Comprehensive Logging** - Detailed operational history and debugging

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Legacy Host Systems                          │
│  (MVS, z/OS, VM/CMS, VMS, MPE, Hercules, SimH, etc.)          │
└────────────────┬────────────────────────────────────────────────┘
                 │ TCP/IP connections and port 9100 RAW
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                  Flashback.Engine (Core Service)                │
│  • Connection Management    • Carriage Control Processing       │
│  • PDF Generation          • Email Delivery                     │
│  • Job Queuing             • Web PDF Viewer (optional)          │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 ├──► PDF Files (Local/Network Storage)
                 ├──► Email (SMTP)
                 └──► Network Printers (Future)
                 
┌─────────────────────────────────────────────────────────────────┐
│              Optional: Flashback.Spooler                        │
│  Port 9100 Gateway for Modern Applications                      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                  Configuration Tools                            │
│  • Console TUI    • 3270 Terminal (TN3270)    • WPF/WinUI GUI  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🚀 Quick Start

### Prerequisites

- **Windows**: Windows 10/11 (x64) with .NET 10 Runtime
- **Linux**: Ubuntu 20.04+, Debian 11+, RHEL 8+, or compatible (x64/ARM64)
- **Network**: TCP/IP connectivity between host and Flashback server
- **Privileges**: Administrator (Windows) or sudo (Linux) for service installation

### Installation

#### Windows
You may use the windows installer for painless installation, or build yourself.

```powershell
# 1. Clone or download the repository
git clone https://github.com/scomps/flashback.git
cd flashback

# 2. Build and publish
.\scripts\winpub.ps1

# 3. Install services (requires Administrator)
.\scripts\install_services_windows.ps1

# 4. Launch the tray controller
# Look for the Flashback icon in your system tray
```

#### Linux

```bash
# 1. Clone or download the repository
git clone https://github.com/scomps/flashback.git
cd flashback

# 2. Build and publish
chmod +x scripts/publish_linux.sh
./scripts/publish_linux.sh

# 3. Install services (requires sudo)
sudo ./scripts/install_services_linux.sh

# 4. Start the engine
sudo systemctl start flashback-engine
sudo systemctl status flashback-engine
```

---

## 📖 Documentation

### Core Components (Engine, 3270 config, and spooler can be run in the foreground)

#### Flashback.Engine
The heart of the system - a background service that:
- Listens for incoming print jobs from legacy hosts
- Processes carriage control sequences
- Generates professional PDF documents
- Delivers PDFs via email or file system
- Provides optional web-based PDF viewing

**Configuration**: `devices.dat` file in the application directory

#### Flashback.Spooler (Optional)
A Port 9100 (JetDirect) gateway that:
- Accepts print jobs from modern applications
- Spools jobs to disk with automatic cleanup
- Forwards jobs to Flashback.Engine for processing
- Provides retry logic and queue management

**Use Case**: Bridge modern Windows/Linux applications to Flashback

#### Flashback.Config.Console
Interactive terminal-based configuration utility:
- Full-screen TUI with keyboard navigation
- Add, edit, delete virtual printer devices
- Configure PDF rendering options
- Set up email delivery per device
- Cross-platform (Windows/Linux)

#### Flashback.Config.3270 (Optional)
Remote administration via TN3270 terminal emulator:
- Connect from any 3270 emulator (x3270, c3270, etc.)
- Manage device configuration remotely
- Password protection (SYSPW)
- Perfect for mainframe administrators
- **Note**: TN3270 protocol is used exclusively for this configuration tool, not for print data transmission

**Default Port**: 3270

#### Flashback.Config.WPF / WinUI
Native Windows GUI applications:
- Modern, intuitive interface
- Visual device management
- Email configuration wizard
- Real-time status monitoring
- Windows 10/11 optimized

#### Flashback.Tray
Windows system tray controller:
- Quick access to services
- Start/stop Engine and 3270 server
- View logs
- Launch configuration tools

---

## 🔧 Configuration

### Device Configuration

Each virtual printer device requires:

| Field | Description | Example |
|-------|-------------|---------|
| **Device Name** | Unique identifier | `PRINTER1` |
| **Description** | Human-readable label | `Mainframe Line Printer` |
| **Connection Type** | Client (0) or Server (1) | `0` |
| **Destination** | IP:Port for connection | `192.168.1.100:9000` |
| **Host OS** | Source system type | MVS, VMS, Generic, etc. |
| **PDF Enabled** | Generate PDF output | `true` |
| **Orientation** | Portrait (0) or Landscape (1) | `0` |
| **Shading** | Green bar, Blue bar, or White | `Green` |
| **Output Directory** | PDF destination path | `C:\Flashback\Output` |

### Email Configuration (Per Device) **EXPERIMENTAL**

| Field | Description | Example |
|-------|-------------|---------|
| **Email Enabled** | Enable automatic delivery | `true` |
| **Recipients** | Semicolon-separated addresses | `user@example.com;admin@company.com` |
| **SMTP Server** | Mail server hostname | `smtp.gmail.com` |
| **SMTP Port** | Mail server port | `587` (TLS) or `465` (SSL) |
| **Username** | SMTP authentication user | `sender@example.com` |
| **Password** | SMTP authentication password | `app-password` |
| **Use TLS** | Enable encryption | `true` |
| **From Address** | Sender email | `flashback@company.com` |
| **From Name** | Sender display name | `Flashback Print Server` |
| **Subject** | Email subject (supports variables) | `Print Job: {JobName}` |
| **Body** | Email body (supports variables) | `Attached is your print job.` |

#### Email Template Variables

- `{JobName}` - Generated PDF filename
- `{DeviceName}` - Virtual printer name
- `{UserName}` - Job submitter
- `{PageCount}` - Number of pages
- `{DateTime}` - Full timestamp
- `{Date}` - Date only
- `{Time}` - Time only

---

## 🖥️ Host System Integration

### Hercules (MVS, z/OS, VM/CMS)

Add printer definitions to your Hercules configuration file:

```text
# Device  Type    Destination
000E      1403    192.168.1.100:9000 sockdev
000F      1403    192.168.1.100:9001 sockdev
```

### SimH (PDP-11, VAX, MicroVAX)

Configure DZ11/DZV11 lines to connect to Flashback:
In this case, our 'printer' is a hardcopy terminal on DZ line 0.

```text
SET DZ LINES=8
ATTACH DZ LINE=0 8000
ATTACH DZ LINE=1 8001
```

### Generic TCP/IP

Any system capable of TCP/IP can send print data:

```bash
# Direct connection
cat printfile.txt | nc 192.168.1.100 9000

# Or use telnet
telnet 192.168.1.100 9000 < printfile.txt
```

---

## 📊 Licensing

Flashback operates in two tiers:

| Tier | Capability | Use Case |
|------|------------|----------|
| **FREE** | Up to 2 concurrent printers | Personal, educational, non-commercial |

To get a license, just ask.  This has been a toy I wanted to play with, not a policy.
To activate a license:
1. Obtain your `flashback.lic` file
2. Place it in the Flashback application directory
3. Restart the Flashback.Engine service

---

## 🔒 Security Best Practices

### Network Security
- **Firewall Rules**: Restrict access to Flashback ports to trusted host IPs only
- **TLS/SSL**: Always enable TLS for SMTP email delivery
- **Port Isolation**: Keep Engine ports on internal networks

### Authentication
- **3270 Password**: Always set a system password (`SYSPW`) for the 3270 configuration server
- **File Permissions**: Restrict access to `devices.dat` and `flashback.lic` files
- **Email Credentials**: Use app-specific passwords, not account passwords

### Monitoring
- **Log Review**: Regularly check `printers.log` for suspicious activity
- **Job Tracking**: Monitor job numbers for unexpected patterns
- **Service Status**: Ensure services are running only when needed

---

## 🐛 Troubleshooting

### Connection Issues

**Problem**: Host cannot connect to Flashback
- Verify Flashback.Engine service is running
- Check firewall rules allow the configured port
- Confirm IP address and port in both host and Flashback configuration
- Review `printers.log` for connection attempts

**Problem**: Flashback cannot connect to host
- Ensure host system is listening on the configured port
- Verify network connectivity with `ping` or `telnet`
- Check if host firewall blocks incoming connections

### PDF Generation Issues

**Problem**: PDFs are blank or malformed
- Verify correct Host OS setting (MVS vs VMS vs Generic)
- Check if source data includes proper carriage control
- Review PDF orientation setting (portrait vs landscape)
- Examine raw data in `printers.log` (debug mode)

**Problem**: Fonts look incorrect
- Ensure font files are present in `Assets` directory
- Verify PDF viewer supports embedded fonts
- Note: Font selection is not currently configurable; default font is used

### Email Delivery Issues EXPERIMENTAL

**NOTE** GMAIL AND MANY COMMERCIAL EMAIL SERVICES WILL NOT ACCEPT MAIL FROM
UNREGISTERED SECURE SMTP SERVERS.

**Problem**: Emails not sending
- Recognize that gmail and others will not accept SMTP from just anyone.
- Verify SMTP server, port, and credentials
- Check if TLS/SSL setting matches server requirements
- Ensure firewall allows outbound SMTP connections
- Review `printers.log` for SMTP error messages
- Test with a known-good email configuration first

**Problem**: Authentication failures
- Use app-specific passwords for Gmail, Office 365
- Verify username format (some servers require full email)
- Check if 2FA requires special authentication

---

## 🛠️ Building from Source

### Prerequisites
- .NET 10 SDK
- Git

### Build Commands

```bash
# Clone repository
git clone https://github.com/scomps/flashback.git
cd flashback

# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run tests (if available)
dotnet test

# Publish for production
# Windows
.\scripts\winpub.ps1

# Linux
./scripts/publish_linux.sh
```

---

## 📁 Project Structure

```
flashback/
├── Flashback.Engine/          # Core print processing service
├── Flashback.Spooler/         # Port 9100 gateway (optional)
├── Flashback.Core/            # Shared libraries and utilities
├── Flashback.Config.Console/  # Terminal-based configuration
├── Flashback.Config.3270/     # TN3270 remote administration
├── Flashback.Config.WPF/      # Windows desktop GUI
├── Flashback.Config.WinUI/    # Modern Windows GUI
├── Flashback.Tray/            # Windows system tray controller
├── Flashback.TN3270Framework/ # TN3270 protocol implementation
├── Flashback.TestTool/        # Development testing utilities
├── scripts/                   # Build and deployment scripts
└── Working Documents/         # Technical documentation
```

---

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **Font Assets**: Chain Printer, Dot Matrix, Line Printer, IBM Plex Mono, OCR-B (included for authentic vintage rendering)
- **PDF Generation**: PDFSharp
- **TN3270 Framework**: Custom implementation based on RFC standards VB.NET source code available.
- **Community**: Thanks to all vintage computing enthusiasts and contributors especially Mainframe Enthusiasts on Discord
- MPE Forever! on Discord for all the help with HP3000/MPE and testing when things were really rough!
- @Rudi (just because)
- @MarXtevens for exellent advice and patience
- @misterspock1 for testing when he probably shouldn't
- Cody for not killing me--or making me kill him.
- If I've missed recognizing you here, it's because I'm old.  Forgive me.

---

## 📞 Support

- **Documentation**: [User Manual](USER_MANUAL.md)
- **Issues**: [GitHub Issues](https://github.com/scomps/flashback/issues)
- **Discussions**: [GitHub Discussions](https://github.com/scomps/flashback/discussions)

---

## 🗺️ Roadmap

- [ ] User-selectable font options for PDF generation
- [ ] Automatic printer detection on Windows networks
- [ ] Web-based configuration interface
- [ ] Additional host OS support (RT-11, etc.)

---

<div align="center">

**Made with ❤️ for the vintage computing community**

[⬆ Back to Top](#-flashback-print-server-suite)

</div>
