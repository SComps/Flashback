# Flashback License Generator (Console)

Cross-platform console application for generating Flashback license keys.

## Features

- **Cross-Platform:** Runs on Linux, macOS, and Windows
- **Dual Mode:** Interactive menu or command-line arguments
- **Intelligent Mode Detection:** Automatically selects mode based on parameters
- **Simple:** Easy-to-use interface for license generation
- **Secure:** Uses AES encryption for license files

## Installation

### Linux/macOS

1. Download the appropriate binary for your platform
2. Make it executable: `chmod +x Flashback.LicenseGenerator.Console`
3. Run: `./Flashback.LicenseGenerator.Console`

### Windows

1. Download the Windows binary
2. Run: `Flashback.LicenseGenerator.Console.exe`

## Usage

The application automatically detects which mode to use based on command-line arguments.

### Interactive Mode (No Parameters)

Run without any parameters to enter interactive mode:

```bash
./Flashback.LicenseGenerator.Console
```

**What happens:**
- Displays interactive menu
- Type `EDIT` to modify settings
- Type `GENERATE` to create license
- Type `EXIT` to quit
- Press `F1` for help

**Interactive workflow:**
1. Run the application
2. Type `EDIT` to modify settings
3. Enter licensed user name
4. Set max concurrent printers (0 = unlimited)
5. Specify output filename or path
6. Type `GENERATE` to create the license
7. Type `EXIT` to quit

### Command-Line Mode (With Parameters)

Provide parameters to generate a license and exit immediately:

```bash
# Basic usage - generates license and exits
./Flashback.LicenseGenerator.Console --name "Company Name" --printers 10

# Specify output file
./Flashback.LicenseGenerator.Console --name "Acme Corp" --printers 50 --output custom-license.lic

# Full path specification
./Flashback.LicenseGenerator.Console --name "Enterprise" --printers 0 --output /opt/flashback/flashback.lic

# Show help and exit
./Flashback.LicenseGenerator.Console --help
```

**What happens:**
- Validates all parameters
- Generates the license file
- Displays success/error message
- Exits immediately (does NOT enter interactive mode)

### Command-Line Options

| Parameter | Description | Required | Default |
|-----------|-------------|----------|---------|
| `--name` or `-n` | Licensed user name | Yes (CLI mode) | None |
| `--printers` or `-p` | Max concurrent printers (0 = unlimited) | No | 10 |
| `--output` or `-o` | Output filename or full path | No | flashback.lic |
| `--help` or `-h` | Show help and exit | No | N/A |

### Output File Specification

The `--output` parameter (or Output Filename in interactive mode) accepts:

- **Simple filename:** `flashback.lic` - Saves in current directory
- **Relative path:** `licenses/flashback.lic` - Saves in subdirectory
- **Absolute path:** `/opt/flashback/flashback.lic` - Saves to specific location

## Examples

### Interactive Mode Examples

```bash
# Start interactive mode
./Flashback.LicenseGenerator.Console

# Then type:
EDIT                    # Modify settings
GENERATE                # Create license
EXIT                    # Quit application
```

### CLI Mode Examples

```bash
# Generate with default filename (flashback.lic)
./Flashback.LicenseGenerator.Console --name "Acme Corporation" --printers 25

# Generate with custom filename
./Flashback.LicenseGenerator.Console --name "Test Company" --printers 5 --output test-license.lic

# Generate with full path
./Flashback.LicenseGenerator.Console --name "Enterprise Inc" --printers 100 --output /var/lib/flashback/flashback.lic

# Unlimited printers
./Flashback.LicenseGenerator.Console --name "Unlimited Corp" --printers 0

# Show help
./Flashback.LicenseGenerator.Console --help
```

### Automation Examples

```bash
# Use in a shell script
#!/bin/bash
./Flashback.LicenseGenerator.Console \
    --name "Automated License" \
    --printers 50 \
    --output /opt/flashback/flashback.lic

if [ $? -eq 0 ]; then
    echo "License generated successfully"
else
    echo "License generation failed"
    exit 1
fi
```

```bash
# Generate multiple licenses
for company in "Company A" "Company B" "Company C"; do
    ./Flashback.LicenseGenerator.Console \
        --name "$company" \
        --printers 10 \
        --output "${company// /_}.lic"
done
```

## License File

The generated `flashback.lic` file should be placed in the same directory as the Flashback Engine executable.

### License File Format

- **Encrypted:** AES-encrypted binary file
- **Content:** Username and printer count
- **Portable:** Can be copied to other systems

## Building from Source

### Prerequisites

- .NET 10.0 SDK or later
- Visual Basic compiler support

### Build Commands

```bash
# Build for current platform
dotnet build Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj

# Run without building
dotnet run --project Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj

# Publish for Linux (self-contained, single file)
dotnet publish Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o ./publish/license-generator-linux

# Publish for macOS
dotnet publish Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj \
    -c Release \
    -r osx-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o ./publish/license-generator-macos

# Publish for Windows
dotnet publish Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o ./publish/license-generator-windows
```

## Troubleshooting

### Permission Denied

If you get "Permission denied" when running on Linux/macOS:

```bash
chmod +x Flashback.LicenseGenerator.Console
```

### Cannot Write License File

Ensure you have write permissions to the output directory:

```bash
# Check permissions
ls -la /path/to/output/directory

# Create directory if needed
mkdir -p /path/to/output/directory

# Set permissions
chmod 755 /path/to/output/directory
```

### Terminal Size Warning

If you see a terminal size warning in interactive mode, resize your terminal to at least 80x24 characters.

## Comparison with Windows Forms Version

| Feature | Windows Forms | Console |
|---------|---------------|---------|
| Platform | Windows only | Linux, macOS, Windows |
| Interface | GUI | Text-based menu |
| Automation | No | Yes (CLI mode) |
| Remote Access | Requires RDP | Works over SSH |
| Dependencies | Windows Forms | None (cross-platform) |
| File Size | ~5 MB | ~60-80 MB (self-contained) |

## Support

For issues or questions, please refer to the main Flashback documentation.

## License

Part of the Flashback project. See main project LICENSE for details.