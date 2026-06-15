# Linux-Compatible Console License Generator - Implementation Plan

## Executive Summary

This document outlines the pathway to create a Linux-compatible, console-based version of the Flashback License Generator. The current implementation ([`Flashback.LicenseGenerator`](Flashback.LicenseGenerator/Flashback.LicenseGenerator.vbproj:1)) is a Windows Forms application that cannot run on Linux. The new console version will provide the same functionality in a cross-platform terminal interface.

## Current State Analysis

### Existing License Generator

**Project:** [`Flashback.LicenseGenerator`](Flashback.LicenseGenerator/Flashback.LicenseGenerator.vbproj:1)
- **Type:** Windows Forms Application (`WinExe`)
- **Target:** `net10.0-windows` (Windows-only)
- **Dependencies:** Windows Forms, [`Flashback.Core`](Flashback.Core/Flashback.Core.vbproj:1)
- **Key Files:**
  - [`Program.vb`](Flashback.LicenseGenerator/Program.vb:1) - Windows Forms entry point
  - [`MainForm.vb`](Flashback.LicenseGenerator/MainForm.vb:1) - GUI implementation

### Core License Functionality

**Location:** [`Flashback.Core/LicenseManager.vb`](Flashback.Core/LicenseManager.vb:1)

The license generation logic is already platform-independent:
- **Encryption:** AES encryption using standard .NET cryptography
- **Format:** Simple pipe-delimited format: `{userName}|{printerCount}`
- **Key Method:** [`LicenseManager.GenerateLicense()`](Flashback.Core/LicenseManager.vb:46)
- **No Windows Dependencies:** Uses only standard .NET APIs

### Reference Implementation

**Project:** [`Flashback.Config.Console`](Flashback.Config.Console/Flashback.Config.Console.vbproj:1)

This existing console application demonstrates:
- Cross-platform console UI patterns
- Interactive menu systems with F1 help
- Input validation and error handling
- Multi-target framework support (`net10.0` and `net10.0-windows`)
- Command-line argument parsing

## Architecture Design

### Project Structure

```
Flashback.LicenseGenerator.Console/
├── Flashback.LicenseGenerator.Console.vbproj
├── Program.vb                    # Entry point and main logic
└── README.md                     # Usage documentation
```

### Technology Stack

- **Language:** Visual Basic .NET
- **Framework:** .NET 10.0 (cross-platform)
- **Dependencies:** [`Flashback.Core`](Flashback.Core/Flashback.Core.vbproj:1) (for [`LicenseManager`](Flashback.Core/LicenseManager.vb:12))
- **UI:** Console-based (System.Console)

### User Interface Design

The application uses **intelligent mode detection**:
- **Command-line parameters present** → Direct CLI mode (generate and exit)
- **No parameters** → Interactive menu mode
- **Help flag (`-h` or `--help`)** → Show help and exit

#### Mode 1: Command-Line Mode (with parameters)

```bash
# Generate license with default filename (flashback.lic)
Flashback.LicenseGenerator.Console --name "Company Name" --printers 10

# Specify custom output filename
Flashback.LicenseGenerator.Console --name "Acme Corp" --printers 50 --output custom-license.lic

# Specify full path for output file
Flashback.LicenseGenerator.Console --name "Enterprise" --printers 0 --output /opt/flashback/flashback.lic

# Unlimited printers with custom filename
Flashback.LicenseGenerator.Console --name "Enterprise" --printers 0 --output enterprise.lic

# Show help
Flashback.LicenseGenerator.Console --help
```

**Behavior:** When command-line parameters are provided, the application:
1. Validates all inputs
2. Generates the license file
3. Displays success/error message
4. Exits immediately (no interactive mode)

#### Mode 2: Interactive Menu (no parameters)

```bash
# Run without parameters - enters interactive mode
Flashback.LicenseGenerator.Console
```

**Interactive Screen:**
```
================================================================================
           FLASHBACK LICENSE KEY GENERATOR
================================================================================

Licensed User Name     : [                              ]
Max Concurrent Printers: [10    ] (0 = Unlimited)
Output Filename        : [flashback.lic                 ]

Commands: EDIT, GENERATE, EXIT
F1: Help

Note: Output filename can be a simple filename (saved in current directory)
      or a full path (e.g., /opt/flashback/license.lic)

COMMAND ==>
```

**Behavior:** When no parameters are provided, the application:
1. Displays interactive menu
2. Allows editing settings
3. Generates license on command
4. Stays running until EXIT command

## Implementation Pathway

### Phase 1: Project Setup

**File:** `Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>Flashback.LicenseGenerator.Console</RootNamespace>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Flashback.Core\Flashback.Core.vbproj" />
  </ItemGroup>
</Project>
```

**Key Points:**
- `OutputType>Exe` for console application
- `TargetFramework>net10.0` (no `-windows` suffix) for Linux compatibility
- Reference to [`Flashback.Core`](Flashback.Core/Flashback.Core.vbproj:1) for [`LicenseManager`](Flashback.Core/LicenseManager.vb:12)

### Phase 2: Core Implementation

**File:** `Flashback.LicenseGenerator.Console/Program.vb`

#### Structure Overview

```vb
Imports System.Console
Imports System.IO
Imports Flashback.Core

Module Program
    ' Configuration
    Private userName As String = ""
    Private printerCount As Integer = 10
    Private outputFile As String = "flashback.lic"
    Private errorMessage As String = ""

    Sub Main(args As String())
        ' Intelligent mode detection:
        ' - If args.Length > 0 AND not just help flag → CLI mode (generate and exit)
        ' - If args.Length = 0 → Interactive mode
        ' - If --help flag → Show help and exit
        
        If args.Length > 0 Then
            ' Check for help flag first
            If args(0).ToLower() = "-h" OrElse args(0).ToLower() = "--help" Then
                ShowHelpCLI()
                Environment.Exit(0)
            End If
            
            ' Has parameters → Run in CLI mode
            RunCLIMode(args)
        Else
            ' No parameters → Run in interactive mode
            RunInteractiveMode()
        End If
    End Sub

    ' Interactive mode functions
    Private Sub RunInteractiveMode()
    Private Sub DisplayMenu()
    Private Sub GetUserInput()
    Private Sub GenerateLicenseInteractive()
    Private Sub DisplayHelp()

    ' CLI mode functions
    Private Sub RunCLIMode(args As String())
    Private Sub ShowHelpCLI()

    ' Utility functions
    Private Sub Say(txt As String, col As Integer, row As Integer, color As ConsoleColor)
    Private Sub SetError(msg As String)
    Private Function GetString(prompt As String, defaultValue As String, maxLen As Integer) As String
End Module
```

#### Key Features

1. **Intelligent Mode Detection:**
   - **Automatic:** Detects mode based on presence of command-line arguments
   - **CLI Mode:** When parameters provided → generate and exit immediately
   - **Interactive Mode:** When no parameters → full menu system
   - **Help Mode:** `--help` flag → display help and exit

2. **Input Validation:**
   - Username: Required, non-empty
   - Printer count: 0-9999 (0 = unlimited)
   - Output path: Valid file path

3. **Error Handling:**
   - File write permissions
   - Invalid input
   - Encryption errors

4. **User Experience:**
   - Clear prompts and feedback
   - F1 help system
   - Color-coded output
   - Success/error messages

### Phase 3: Command-Line Interface

#### Argument Parsing

```vb
Private Sub RunCLIMode(args As String())
    ' Parse command-line arguments
    Dim i As Integer = 0
    While i < args.Length
        Select Case args(i).ToLower()
            Case "-n", "--name"
                If i + 1 < args.Length Then
                    userName = args(i + 1)
                    i += 1
                End If
            Case "-p", "--printers"
                If i + 1 < args.Length Then
                    printerCount = Val(args(i + 1))
                    i += 1
                End If
            Case "-o", "--output"
                If i + 1 < args.Length Then
                    outputFile = args(i + 1)
                    i += 1
                End If
            Case Else
                Console.WriteLine($"Unknown option: {args(i)}")
                ShowHelpCLI()
                Environment.Exit(1)
        End Select
        i += 1
    End While

    ' Validate required parameters
    If String.IsNullOrWhiteSpace(userName) Then
        Console.WriteLine("Error: Username is required (use --name)")
        Console.WriteLine()
        ShowHelpCLI()
        Environment.Exit(1)
    End If

    ' Validate printer count range
    If printerCount < 0 OrElse printerCount > 9999 Then
        Console.WriteLine("Error: Printer count must be between 0 and 9999")
        Environment.Exit(1)
    End If

    ' Generate license and exit
    Try
        LicenseManager.GenerateLicense(userName, printerCount, outputFile)
        Console.WriteLine($"SUCCESS: License generated at {outputFile}")
        Console.WriteLine($"Licensed to: {userName}")
        Console.WriteLine($"Max printers: {If(printerCount = 0, "Unlimited", printerCount.ToString())}")
        Environment.Exit(0)
    Catch ex As Exception
        Console.WriteLine($"ERROR: {ex.Message}")
        Environment.Exit(1)
    End Try
End Sub
```

**Important:** CLI mode generates the license and exits immediately. It does NOT enter interactive mode.

#### Help Text

```vb
Private Sub ShowHelpCLI()
    Console.WriteLine("Flashback License Key Generator (Console)")
    Console.WriteLine()
    Console.WriteLine("Usage:")
    Console.WriteLine("  Interactive mode (no parameters):")
    Console.WriteLine("    Flashback.LicenseGenerator.Console")
    Console.WriteLine()
    Console.WriteLine("  Command-line mode (with parameters - generates and exits):")
    Console.WriteLine("    Flashback.LicenseGenerator.Console --name <username> [options]")
    Console.WriteLine()
    Console.WriteLine("Options:")
    Console.WriteLine("  -n, --name <name>        Licensed user name (required for CLI mode)")
    Console.WriteLine("  -p, --printers <count>   Max concurrent printers (default: 10, 0 = unlimited)")
    Console.WriteLine("  -o, --output <file>      Output file path (default: flashback.lic)")
    Console.WriteLine("  -h, --help               Show this help message and exit")
    Console.WriteLine()
    Console.WriteLine("Mode Selection:")
    Console.WriteLine("  • No parameters          → Interactive menu mode")
    Console.WriteLine("  • With --name parameter  → CLI mode (generate and exit)")
    Console.WriteLine("  • With --help flag       → Show help and exit")
    Console.WriteLine()
    Console.WriteLine("Examples:")
    Console.WriteLine("  # Interactive mode")
    Console.WriteLine("  Flashback.LicenseGenerator.Console")
    Console.WriteLine()
    Console.WriteLine("  # CLI mode - generate and exit")
    Console.WriteLine("  Flashback.LicenseGenerator.Console --name ""Acme Corp"" --printers 50")
    Console.WriteLine("  Flashback.LicenseGenerator.Console --name ""Test User"" --printers 0 --output /tmp/test.lic")
End Sub
```

### Phase 4: Interactive Mode Implementation

#### Main Menu Display

```vb
Private Sub DisplayMenu()
    Console.Clear()
    Console.ForegroundColor = ConsoleColor.White
    Console.WriteLine(New String("="c, 80))
    Console.WriteLine("           FLASHBACK LICENSE KEY GENERATOR")
    Console.WriteLine(New String("="c, 80))
    Console.WriteLine()

    If Not String.IsNullOrEmpty(errorMessage) Then
        Console.ForegroundColor = ConsoleColor.Red
        Console.WriteLine($"  {errorMessage}")
        Console.WriteLine()
        errorMessage = ""
    End If

    Console.ForegroundColor = ConsoleColor.Cyan
    Console.WriteLine($"  Licensed User Name    : {userName}")
    Console.WriteLine($"  Max Concurrent Printers: {printerCount} {If(printerCount = 0, "(Unlimited)", "")}")
    Console.WriteLine($"  Output File           : {outputFile}")
    Console.WriteLine()

    Console.ForegroundColor = ConsoleColor.White
    Console.WriteLine("Commands: EDIT, GENERATE, EXIT")
    Console.WriteLine("F1: Help")
    Console.WriteLine()
    Console.Write("COMMAND ==> ")
    Console.ResetColor()
End Sub
```

#### Input Collection

```vb
Private Sub EditSettings()
    Console.Clear()
    Console.WriteLine("EDIT LICENSE SETTINGS")
    Console.WriteLine(New String("-"c, 80))
    Console.WriteLine()

    Console.Write("Licensed User Name: ")
    Dim input = Console.ReadLine()
    If Not String.IsNullOrWhiteSpace(input) Then userName = input

    Console.Write($"Max Concurrent Printers [{printerCount}]: ")
    input = Console.ReadLine()
    If Not String.IsNullOrWhiteSpace(input) Then
        Dim count = Val(input)
        If count >= 0 AndAlso count <= 9999 Then
            printerCount = count
        Else
            SetError("Printer count must be between 0 and 9999")
        End If
    End If

    Console.Write($"Output File [{outputFile}]: ")
    input = Console.ReadLine()
    If Not String.IsNullOrWhiteSpace(input) Then outputFile = input
End Sub
```

#### License Generation

```vb
Private Sub GenerateLicense()
    If String.IsNullOrWhiteSpace(userName) Then
        SetError("ERROR: Username cannot be empty")
        Return
    End If

    Try
        LicenseManager.GenerateLicense(userName, printerCount, outputFile)
        Console.ForegroundColor = ConsoleColor.Green
        Console.WriteLine()
        Console.WriteLine("SUCCESS: License key generated!")
        Console.WriteLine($"  File: {outputFile}")
        Console.WriteLine($"  Licensed to: {userName}")
        Console.WriteLine($"  Max printers: {If(printerCount = 0, "Unlimited", printerCount.ToString())}")
        Console.WriteLine()
        Console.WriteLine("Press any key to continue...")
        Console.ResetColor()
        Console.ReadKey(True)
    Catch ex As Exception
        SetError($"ERROR: {ex.Message}")
    End Try
End Sub
```

### Phase 5: Build and Deployment

#### Build Script for Linux

**File:** `scripts/publish_license_generator.sh`

```bash
#!/bin/bash

# Flashback License Generator Console - Linux Build Script

echo "Building Flashback License Generator Console for Linux..."

# Clean previous builds
rm -rf ./publish/license-generator-linux

# Build for Linux x64
dotnet publish Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -o ./publish/license-generator-linux

if [ $? -eq 0 ]; then
    echo "Build successful!"
    echo "Output: ./publish/license-generator-linux/"
    
    # Make executable
    chmod +x ./publish/license-generator-linux/Flashback.LicenseGenerator.Console
    
    echo ""
    echo "To run:"
    echo "  ./publish/license-generator-linux/Flashback.LicenseGenerator.Console"
else
    echo "Build failed!"
    exit 1
fi
```

#### Cross-Platform Build Script

**File:** `scripts/publish_license_generator_all.sh`

```bash
#!/bin/bash

# Build for multiple platforms

platforms=("linux-x64" "linux-arm64" "osx-x64" "osx-arm64" "win-x64")

for platform in "${platforms[@]}"; do
    echo "Building for $platform..."
    
    dotnet publish Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj \
        -c Release \
        -r $platform \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -o ./publish/license-generator-$platform
    
    if [ $? -eq 0 ]; then
        echo "✓ $platform build successful"
    else
        echo "✗ $platform build failed"
    fi
    echo ""
done

echo "All builds complete!"
```

### Phase 6: Documentation

#### README.md

**File:** `Flashback.LicenseGenerator.Console/README.md`

```markdown
# Flashback License Generator (Console)

Cross-platform console application for generating Flashback license keys.

## Features

- **Cross-Platform:** Runs on Linux, macOS, and Windows
- **Dual Mode:** Interactive menu or command-line arguments
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

The application automatically detects which mode to use based on command-line arguments:

### Interactive Mode (No Parameters)

Run without any parameters to enter interactive mode:

```bash
./Flashback.LicenseGenerator.Console
```

**What happens:**
- Displays interactive menu
- Allows editing all settings
- Generates license on command
- Stays running until you type EXIT

**Interactive workflow:**
1. Run the application
2. Type `EDIT` to modify settings
3. Enter licensed user name
4. Set max concurrent printers (0 = unlimited)
5. Specify output file path
6. Type `GENERATE` to create the license
7. Type `EXIT` to quit

### Command-Line Mode (With Parameters)

Provide parameters to generate a license and exit immediately:

```bash
# Basic usage - generates license and exits
./Flashback.LicenseGenerator.Console --name "Company Name" --printers 10

# Specify output file
./Flashback.LicenseGenerator.Console --name "Acme Corp" --printers 50 --output /path/to/flashback.lic

# Unlimited printers
./Flashback.LicenseGenerator.Console --name "Enterprise" --printers 0

# Show help and exit
./Flashback.LicenseGenerator.Console --help
```

**What happens:**
- Validates all parameters
- Generates the license file
- Displays success/error message
- Exits immediately (does NOT enter interactive mode)

**Perfect for:**
- Automation scripts
- CI/CD pipelines
- Batch processing
- Remote execution via SSH

### Options

- `-n, --name <name>` - Licensed user name (required)
- `-p, --printers <count>` - Max concurrent printers (default: 10, 0 = unlimited)
- `-o, --output <file>` - Output file path (default: flashback.lic)
- `-h, --help` - Show help message

## License File

The generated `flashback.lic` file should be placed in the same directory as the Flashback Engine executable.

## Building from Source

```bash
# Build for current platform
dotnet build Flashback.LicenseGenerator.Console/Flashback.LicenseGenerator.Console.vbproj

# Publish for Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# Publish for macOS
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

# Publish for Windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Support

For issues or questions, please refer to the main Flashback documentation.
```

## Testing Strategy

### Unit Testing

1. **License Generation:**
   - Valid inputs produce correct license files
   - Invalid inputs are rejected
   - File permissions are handled correctly

2. **Input Validation:**
   - Empty username is rejected
   - Printer count range (0-9999) is enforced
   - Invalid file paths are caught

3. **Command-Line Parsing:**
   - All argument combinations work correctly
   - Unknown arguments are rejected
   - Help text is displayed correctly

### Integration Testing

1. **Cross-Platform:**
   - Test on Linux (Ubuntu, Debian, RHEL)
   - Test on macOS (Intel and ARM)
   - Test on Windows (for compatibility)

2. **License Validation:**
   - Generated licenses work with Flashback Engine
   - Encryption/decryption is consistent
   - License info is correctly parsed

### Manual Testing Checklist

- [ ] Interactive mode displays correctly
- [ ] All menu commands work
- [ ] F1 help is accessible
- [ ] Input validation works
- [ ] Error messages are clear
- [ ] Success messages are displayed
- [ ] Command-line mode works
- [ ] All CLI arguments are parsed
- [ ] Help text is complete
- [ ] Generated licenses are valid
- [ ] File permissions are respected
- [ ] Cross-platform compatibility verified

## Deployment Considerations

### Linux Deployment

1. **Binary Distribution:**
   - Single-file executable (self-contained)
   - No .NET runtime required
   - Approximately 60-80 MB

2. **Package Distribution:**
   - Create .deb package for Debian/Ubuntu
   - Create .rpm package for RHEL/Fedora
   - Include in system PATH

3. **Permissions:**
   - Executable: `chmod +x`
   - Write access to output directory

### Security Considerations

1. **Encryption Keys:**
   - Keys are embedded in [`LicenseManager`](Flashback.Core/LicenseManager.vb:13-14)
   - Same keys used across all Flashback components
   - No external key management needed

2. **File Permissions:**
   - Generated license files should be readable by Flashback Engine
   - Recommend 644 permissions (rw-r--r--)

3. **Input Sanitization:**
   - Username is stored as-is (no special characters filtered)
   - File paths are validated before writing

## Migration Path

### For Existing Windows Users

1. **Current Process:**
   - Run Windows Forms application
   - Fill in GUI form
   - Click "Generate" button
   - Save file dialog

2. **New Process (Interactive):**
   - Run console application
   - Enter same information at prompts
   - License generated automatically

3. **New Process (CLI):**
   - Script license generation
   - Automate deployment
   - No GUI required

### Advantages of Console Version

1. **Cross-Platform:** Works on Linux servers
2. **Scriptable:** Can be automated
3. **Remote Access:** Works over SSH
4. **Lightweight:** No GUI dependencies
5. **CI/CD Friendly:** Easy to integrate

## Implementation Timeline

### Phase 1: Core Development (2-3 hours)
- Create project structure
- Implement basic interactive mode
- Add command-line parsing
- Basic error handling

### Phase 2: Polish & Testing (1-2 hours)
- Add F1 help system
- Improve error messages
- Add input validation
- Test on Linux

### Phase 3: Documentation & Deployment (1 hour)
- Write README
- Create build scripts
- Test deployment process
- Document usage

**Total Estimated Time:** 4-6 hours

## Success Criteria

- [ ] Application runs on Linux without errors
- [ ] Generated licenses work with Flashback Engine
- [ ] Interactive mode is user-friendly
- [ ] Command-line mode supports automation
- [ ] Help documentation is complete
- [ ] Build process is documented
- [ ] Cross-platform compatibility verified

## Future Enhancements

1. **Batch Generation:**
   - Generate multiple licenses from CSV
   - Bulk license creation

2. **License Validation:**
   - Verify existing license files
   - Display license information

3. **Configuration File:**
   - Save default settings
   - Template support

4. **Advanced Features:**
   - Expiration dates
   - Feature flags
   - License renewal

## Conclusion

This implementation plan provides a clear pathway to create a Linux-compatible console version of the Flashback License Generator. The design leverages existing patterns from [`Flashback.Config.Console`](Flashback.Config.Console/Program.vb:1) and the platform-independent [`LicenseManager`](Flashback.Core/LicenseManager.vb:12) to create a robust, cross-platform solution.

The dual-mode approach (interactive and CLI) ensures the tool is both user-friendly for manual use and automation-friendly for scripting and CI/CD integration.