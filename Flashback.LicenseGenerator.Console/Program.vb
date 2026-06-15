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

    ' ============================================================================
    ' CLI MODE - Generate license and exit
    ' ============================================================================
    
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
                    System.Console.WriteLine($"Unknown option: {args(i)}")
                    System.Console.WriteLine()
                    ShowHelpCLI()
                    Environment.Exit(1)
            End Select
            i += 1
        End While

        ' Validate required parameters
        If String.IsNullOrWhiteSpace(userName) Then
            System.Console.WriteLine("Error: Username is required (use --name)")
            System.Console.WriteLine()
            ShowHelpCLI()
            Environment.Exit(1)
        End If

        ' Validate printer count range
        If printerCount < 0 OrElse printerCount > 9999 Then
            System.Console.WriteLine("Error: Printer count must be between 0 and 9999")
            Environment.Exit(1)
        End If

        ' Generate license and exit
        Try
            LicenseManager.GenerateLicense(userName, printerCount, outputFile)
            System.Console.ForegroundColor = ConsoleColor.Green
            System.Console.WriteLine($"SUCCESS: License generated at {outputFile}")
            System.Console.ResetColor()
            System.Console.WriteLine($"Licensed to: {userName}")
            System.Console.WriteLine($"Max printers: {If(printerCount = 0, "Unlimited", printerCount.ToString())}")
            Environment.Exit(0)
        Catch ex As Exception
            System.Console.ForegroundColor = ConsoleColor.Red
            System.Console.WriteLine($"ERROR: {ex.Message}")
            System.Console.ResetColor()
            Environment.Exit(1)
        End Try
    End Sub

    Private Sub ShowHelpCLI()
        System.Console.WriteLine("Flashback License Key Generator (Console)")
        System.Console.WriteLine()
        System.Console.WriteLine("Usage:")
        System.Console.WriteLine("  Interactive mode (no parameters):")
        System.Console.WriteLine("    Flashback.LicenseGenerator.Console")
        System.Console.WriteLine()
        System.Console.WriteLine("  Command-line mode (with parameters - generates and exits):")
        System.Console.WriteLine("    Flashback.LicenseGenerator.Console --name <username> [options]")
        System.Console.WriteLine()
        System.Console.WriteLine("Options:")
        System.Console.WriteLine("  -n, --name <name>        Licensed user name (required for CLI mode)")
        System.Console.WriteLine("  -p, --printers <count>   Max concurrent printers (default: 10, 0 = unlimited)")
        System.Console.WriteLine("  -o, --output <file>      Output filename or path (default: flashback.lic)")
        System.Console.WriteLine("  -h, --help               Show this help message and exit")
        System.Console.WriteLine()
        System.Console.WriteLine("Mode Selection:")
        System.Console.WriteLine("  • No parameters          → Interactive menu mode")
        System.Console.WriteLine("  • With --name parameter  → CLI mode (generate and exit)")
        System.Console.WriteLine("  • With --help flag       → Show help and exit")
        System.Console.WriteLine()
        System.Console.WriteLine("Examples:")
        System.Console.WriteLine("  # Interactive mode")
        System.Console.WriteLine("  Flashback.LicenseGenerator.Console")
        System.Console.WriteLine()
        System.Console.WriteLine("  # CLI mode - generate and exit")
        System.Console.WriteLine("  Flashback.LicenseGenerator.Console --name ""Acme Corp"" --printers 50")
        System.Console.WriteLine("  Flashback.LicenseGenerator.Console --name ""Test"" --printers 0 --output /tmp/test.lic")
    End Sub

    ' ============================================================================
    ' INTERACTIVE MODE - Menu-driven interface
    ' ============================================================================
    
    Private Sub RunInteractiveMode()
        AddHandler System.Console.CancelKeyPress, AddressOf Console_CancelKeyPress
        
        ' Check terminal size
        Try
            If System.Console.WindowHeight < 24 Or System.Console.WindowWidth < 80 Then
                System.Console.WriteLine("WARNING: Your terminal window should be at least 80x24 for best experience.")
                System.Console.WriteLine("Press any key to continue...")
                System.Console.ReadKey(True)
            End If
        Catch
            ' Ignore if we can't get window size
        End Try

        Do While True
            DisplayMenu()
            Dim cmd As String = GetCommand()
            If cmd Is Nothing Then cmd = ""
            cmd = cmd.ToUpper().Trim()

            Select Case cmd
                Case "EDIT"
                    EditSettings()
                Case "GENERATE"
                    GenerateLicenseInteractive()
                Case "EXIT", "QUIT", "Q"
                    System.Console.ResetColor()
                    System.Console.Clear()
                    Environment.Exit(0)
                Case ""
                    ' Ignore empty input
                Case Else
                    SetError($"Unknown command: {cmd}")
            End Select
        Loop
    End Sub

    Private Sub DisplayMenu()
        System.Console.Clear()
        System.Console.ResetColor()
        
        Dim bannerLine As String = New String("="c, 80)
        Say(bannerLine, 0, 0, ConsoleColor.White)
        Say("           FLASHBACK LICENSE KEY GENERATOR", 0, 1, ConsoleColor.White)
        Say(bannerLine, 0, 2, ConsoleColor.White)
        Say("F1: Help", 70, 1, ConsoleColor.Yellow)
        
        System.Console.WriteLine()
        
        If Not String.IsNullOrEmpty(errorMessage) Then
            Say($"  {errorMessage}", 0, 4, ConsoleColor.Red)
            errorMessage = ""
            System.Console.WriteLine()
        End If

        Say("  Licensed User Name     : ", 0, 6, ConsoleColor.Cyan)
        Say(userName.PadRight(40), 29, 6, ConsoleColor.Yellow)
        
        Say("  Max Concurrent Printers: ", 0, 7, ConsoleColor.Cyan)
        Dim printerStr = printerCount.ToString().PadRight(10)
        If printerCount = 0 Then printerStr &= "(Unlimited)"
        Say(printerStr, 29, 7, ConsoleColor.Yellow)
        
        Say("  Output Filename        : ", 0, 8, ConsoleColor.Cyan)
        Say(outputFile.PadRight(40), 29, 8, ConsoleColor.Yellow)
        
        System.Console.WriteLine()
        System.Console.WriteLine()
        Say("Commands: EDIT, GENERATE, EXIT", 2, 11, ConsoleColor.White)
        System.Console.WriteLine()
        System.Console.WriteLine()
        Say("COMMAND ==> ", 2, 14, ConsoleColor.White)
    End Sub

    Private Function GetCommand() As String
        Dim cmd As String = ""
        Dim cursorLeft = System.Console.CursorLeft
        Dim cursorTop = System.Console.CursorTop
        
        Do
            Dim key = System.Console.ReadKey(True)
            
            If key.Key = ConsoleKey.F1 Then
                DisplayHelp()
                DisplayMenu()
                System.Console.SetCursorPosition(cursorLeft, cursorTop)
                System.Console.Write(cmd)
                Continue Do
            End If
            
            If key.Key = ConsoleKey.Enter Then
                Return cmd
            End If
            
            If key.Key = ConsoleKey.Backspace Then
                If cmd.Length > 0 Then
                    cmd = cmd.Substring(0, cmd.Length - 1)
                    System.Console.Write(vbBack & " " & vbBack)
                End If
            ElseIf Not Char.IsControl(key.KeyChar) Then
                cmd &= key.KeyChar
                System.Console.Write(key.KeyChar)
            End If
        Loop
    End Function

    Private Sub EditSettings()
        System.Console.Clear()
        Dim bannerLine As String = New String("="c, 80)
        Say(bannerLine, 0, 0, ConsoleColor.White)
        Say("           EDIT LICENSE SETTINGS", 0, 1, ConsoleColor.White)
        Say(bannerLine, 0, 2, ConsoleColor.White)
        System.Console.WriteLine()
        System.Console.WriteLine()

        ' Edit username
        System.Console.ForegroundColor = ConsoleColor.Cyan
        System.Console.Write("  Licensed User Name [{0}]: ", userName)
        System.Console.ForegroundColor = ConsoleColor.Yellow
        Dim input = System.Console.ReadLine()
        If Not String.IsNullOrWhiteSpace(input) Then userName = input.Trim()

        ' Edit printer count
        System.Console.ForegroundColor = ConsoleColor.Cyan
        System.Console.Write("  Max Concurrent Printers [{0}]: ", printerCount)
        System.Console.ForegroundColor = ConsoleColor.Yellow
        input = System.Console.ReadLine()
        If Not String.IsNullOrWhiteSpace(input) Then
            Dim count = Val(input)
            If count >= 0 AndAlso count <= 9999 Then
                printerCount = count
            Else
                SetError("Printer count must be between 0 and 9999")
            End If
        End If

        ' Edit output file
        System.Console.ForegroundColor = ConsoleColor.Cyan
        System.Console.Write("  Output Filename [{0}]: ", outputFile)
        System.Console.ForegroundColor = ConsoleColor.Yellow
        input = System.Console.ReadLine()
        If Not String.IsNullOrWhiteSpace(input) Then outputFile = input.Trim()

        System.Console.ResetColor()
    End Sub

    Private Sub GenerateLicenseInteractive()
        If String.IsNullOrWhiteSpace(userName) Then
            SetError("ERROR: Username cannot be empty. Use EDIT command to set it.")
            Return
        End If

        Try
            LicenseManager.GenerateLicense(userName, printerCount, outputFile)
            
            System.Console.Clear()
            System.Console.ForegroundColor = ConsoleColor.Green
            System.Console.WriteLine()
            System.Console.WriteLine("  ╔════════════════════════════════════════════════════════════════════════════╗")
            System.Console.WriteLine("  ║                    LICENSE GENERATED SUCCESSFULLY!                         ║")
            System.Console.WriteLine("  ╚════════════════════════════════════════════════════════════════════════════╝")
            System.Console.WriteLine()
            System.Console.ResetColor()
            
            System.Console.ForegroundColor = ConsoleColor.Cyan
            System.Console.WriteLine($"  Output File    : {outputFile}")
            System.Console.WriteLine($"  Licensed To    : {userName}")
            System.Console.WriteLine($"  Max Printers   : {If(printerCount = 0, "Unlimited", printerCount.ToString())}")
            System.Console.WriteLine()
            System.Console.ResetColor()
            
            System.Console.WriteLine("  Press any key to continue...")
            System.Console.ReadKey(True)
        Catch ex As Exception
            SetError($"ERROR: {ex.Message}")
        End Try
    End Sub

    Private Sub DisplayHelp()
        System.Console.Clear()
        System.Console.ResetColor()
        
        Dim bannerLine As String = New String("="c, 80)
        Say(bannerLine, 0, 0, ConsoleColor.White)
        Say("           HELP - LICENSE GENERATOR", 0, 1, ConsoleColor.White)
        Say(bannerLine, 0, 2, ConsoleColor.White)
        System.Console.WriteLine()

        Say("COMMANDS:", 2, 4, ConsoleColor.Yellow)
        Say("  EDIT     - Modify license settings (username, printer count, output file)", 2, 5, ConsoleColor.Cyan)
        Say("  GENERATE - Create the license file with current settings", 2, 6, ConsoleColor.Cyan)
        Say("  EXIT     - Quit the application", 2, 7, ConsoleColor.Cyan)
        System.Console.WriteLine()

        Say("SETTINGS:", 2, 9, ConsoleColor.Yellow)
        Say("  Licensed User Name     - The name/company that will appear in the license", 2, 10, ConsoleColor.Cyan)
        Say("  Max Concurrent Printers - Maximum number of printers (0 = unlimited)", 2, 11, ConsoleColor.Cyan)
        Say("  Output Filename        - Where to save the license file", 2, 12, ConsoleColor.Cyan)
        Say("                           Can be a simple filename or full path", 2, 13, ConsoleColor.DarkGray)
        System.Console.WriteLine()

        Say("EXAMPLES:", 2, 15, ConsoleColor.Yellow)
        Say("  Output: flashback.lic           → Saves in current directory", 2, 16, ConsoleColor.Cyan)
        Say("  Output: /opt/flashback/app.lic  → Saves to specific path", 2, 17, ConsoleColor.Cyan)
        Say("  Printers: 0                     → Unlimited printers", 2, 18, ConsoleColor.Cyan)
        Say("  Printers: 50                    → Maximum 50 concurrent printers", 2, 19, ConsoleColor.Cyan)
        System.Console.WriteLine()

        Say("Press any key to return to main menu...", 2, 21, ConsoleColor.White)
        System.Console.ReadKey(True)
    End Sub

    ' ============================================================================
    ' UTILITY FUNCTIONS
    ' ============================================================================
    
    Private Sub Say(txt As String, col As Integer, row As Integer, color As ConsoleColor)
        Try
            System.Console.SetCursorPosition(col, row)
            System.Console.ForegroundColor = color
            System.Console.Write(txt)
        Catch
            ' Ignore positioning errors
        End Try
    End Sub

    Private Sub SetError(msg As String)
        errorMessage = msg
        If Not String.IsNullOrEmpty(msg) Then
            Try
                System.Console.Beep()
            Catch
                ' Ignore if beep not supported
            End Try
        End If
    End Sub

    Private Sub Console_CancelKeyPress(sender As Object, e As ConsoleCancelEventArgs)
        e.Cancel = True
        SetError("Use EXIT command to quit.")
    End Sub
End Module

' Made with Bob
