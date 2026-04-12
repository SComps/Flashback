# Flashback Suite: Professional User Manual
**Modern Cross-Platform Printer Services for Legacy Host Systems**

---

## Introduction
Flashback is a robust, high-performance suite designed to modernize print services for legacy operating systems (MVS, VMS, MPE, etc.) by bridging host data streams to modern PDF and network-attached printers.

> [!NOTE]
> Detailed configuration of the specific Guest Operating Systems (e.g., JES2 initialization, VMS device queuing, etc.) is an exercise for the end user and is considered outside the scope of this manual.

---

## 1. System Requirements
- **Runtime**: Windows (x64) with .NET 9 or Linux (x64/ARM64).
- **Network**: TCP/IP connectivity between the host (Source) and the Flashback Service.
- **Privileges**: Administrative or Sudo rights for service/daemon installation.

---

## 2. Installation & Setup

### Windows Installation
1.  **Publish**: Build the binaries using `scripts/publish_windows.ps1`.
2.  **Install**: Open PowerShell as **Administrator** and run:
    ```powershell
    .\scripts\install_services_windows.ps1
    ```
3.  **Verify**: Open the **Flashback Tray Controller** from your taskbar to monitor service status.

### Linux Installation
1.  **Publish**: Build the binaries using `scripts/publish_linux.sh`.
2.  **Install**: Run the installation script as **Sudo**:
    ```bash
    sudo ./scripts/install_services_linux.sh
    ```
3.  **Operations**: Use `systemctl start flashback-engine` to begin processing.

---

## 3. Configuration

### Console Configuration Utility
The primary way to manage devices is the **Flashback.Config.Console** tool. It provides a full-screen interactive interface for:
- Adding/Deleting virtual printer devices.
- Configuring destination IP addresses and ports.
- Setting up PDF rendering preferences (Orientation, Shading, Job Numbering).

### 3270 Remote Administration
For administrators on z/OS or other terminal-heavy systems, the **3270 Config Server** allows remote management via any TN3270 terminal emulator.
- **Port**: Default is `3270`.
- **Security**: Can be protected with a `SYSPW` (System Password).

---

## 4. Host System Integration

Flashback bridges legacy hosts to modern printers via the TCP/IP protocol. Below are typical configurations for popular emulator environments.

### Hercules (MVS, z/OS, VM)
To route printouts from Hercules to Flashback, define a virtual printer in your `.cnf` file pointing to the Flashback Engine's IP and the specific device port.

```text
# DeviceAddress DeviceType FlashbackHost:Port
000E 1403 127.0.0.1:9000
000F 1403 127.0.0.1:9001
```

### SimH (PDP-11, VAX, MicroVAX)
For DEC-style systems, users can bind specific lines of the **DZ11/DZV11** controller to unique Flashback device ports. This is a powerful feature that allows you to "lock in" a guest operating system device (like `LP0:`) to a specific Flashback printer instance.

```text
# Example: Lock DZ Line 0 to a dedicated Flashback port
SET DZ LINES=8
ATTACH DZ LINE=0,CONNECT=127.0.0.1:9005

# Example: Assign a different printer to DZ Line 1
ATTACH DZ LINE=1,CONNECT=127.0.0.1:9006
```

---

## 5. Licensing

Flashback operates in two modes:

| Tier | Capability | Status |
| :--- | :--- | :--- |
| **FREE** | Max 2 Concurrent Printers | Non-Commercial Use |
| **PRO** | Unlimited Printers (As licensed) | Licensed Professional |

To upgrade, place your generated `flashback.lic` file into the application base directory and restart the Engine service.

---

## 6. Security Best Practices
- **SYSPW**: Always set a system password for the 3270 server using the `--password` flag or a `syspw.txt` file.
- **Control Plane**: Ensure the application directory has restricted permissions to protect the `commands.dat` signaling file.
- **Network**: Use a firewall to restrict access to the Flashback ports (3270 and printer ports) to trusted host IPs only.

---

## 7. Troubleshooting & Logs
All operational history is maintained in the `printers.log` file within the application directory.
- **Connection Lost**: The Engine will automatically retry every 5 seconds.
- **Invalid Data**: Ensure the Host OS setting in Flashback matches the source host (e.g., MVS vs. VMS) for correct carriage control processing.

---

## 8. Disclaimer
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
