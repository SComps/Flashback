Imports System
Imports System.Diagnostics
Imports System.Linq
Imports System.Threading
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Flashback.Core

Module Program
    Sub Main(args As String())
        ' Single Instance Check
        Dim createdNew As Boolean
        Dim mutex As New Mutex(True, "Global\FlashbackSpooler", createdNew)
        If Not createdNew Then
            Console.WriteLine("Error: Flashback Spooler is already running.")
            Environment.Exit(1)
        End If

        Dim isDaemon As Boolean = False
        Dim configPath As String = "spooler.conf"

        Dim i As Integer = 0
        While i < args.Length
            Dim arg = args(i).ToLower()
            Select Case arg
                Case "-h", "--help"
                    ShowHelp()
                    Environment.Exit(0)
                Case "-d", "--daemon"
                    isDaemon = True
                Case "-c", "--config"
                    If i + 1 < args.Length Then
                        configPath = args(i + 1)
                        i += 1
                    End If
                Case "-v", "--version"
                    ShowVersion()
                    Environment.Exit(0)
                Case Else
            End Select
            i += 1
        End While

        If isDaemon Then
            Dim psi As New ProcessStartInfo(Environment.ProcessPath)
            Dim daemonArgs = args.Where(Function(a) a <> "-d" AndAlso a <> "--daemon").ToList()
            psi.Arguments = String.Join(" ", daemonArgs)
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            Process.Start(psi)
            Console.WriteLine("Flashback Spooler detached into background.")
            mutex.Dispose()
            Environment.Exit(0)
        End If

        Dim builder = Host.CreateApplicationBuilder(args)
        builder.Logging.AddFile()

        If OperatingSystem.IsWindows() Then
            builder.Services.AddWindowsService(Sub(options)
                                                   options.ServiceName = "FlashbackSpooler"
                                               End Sub)
        ElseIf OperatingSystem.IsLinux() Then
            builder.Services.AddSystemd()
        End If

        builder.Services.AddHostedService(Of SpoolerWorker)()

        Dim spoolerHost = builder.Build()
        spoolerHost.Run()
    End Sub

    Private Sub ShowHelp()
        Console.WriteLine("Flashback Spooler Service")
        Console.WriteLine("Receives print jobs on port 9100 and forwards them to Flashback.Engine")
        Console.WriteLine()
        Console.WriteLine("Usage: Flashback.Spooler [options]")
        Console.WriteLine()
        Console.WriteLine("Options:")
        Console.WriteLine("  -h, --help              Show this help message")
        Console.WriteLine("  -v, --version           Show version information")
        Console.WriteLine("  -d, --daemon            Run in background (Linux only)")
        Console.WriteLine("  -c, --config <path>     Specify configuration file path")
        Console.WriteLine()
        Console.WriteLine("Configuration:")
        Console.WriteLine("  Default config file: spooler.conf")
        Console.WriteLine("  If not found, a default configuration will be created")
        Console.WriteLine()
        Console.WriteLine("Network Ports:")
        Console.WriteLine("  Port 9100: Receives print jobs (JetDirect compatible)")
        Console.WriteLine("  Port 9001: Flashback.Engine connects here (configurable)")
        Console.WriteLine()
        Console.WriteLine("Examples:")
        Console.WriteLine("  Flashback.Spooler")
        Console.WriteLine("  Flashback.Spooler -c /etc/flashback/spooler.conf")
        Console.WriteLine("  Flashback.Spooler --daemon")
        Console.WriteLine()
    End Sub

    Private Sub ShowVersion()
        Dim version = Reflection.Assembly.GetExecutingAssembly().GetName().Version
        Console.WriteLine($"Flashback Spooler v{version}")
        Console.WriteLine("Part of the Flashback Print Server Suite")
        Console.WriteLine()
        Console.WriteLine("Copyright (c) 2024-2026")
        Console.WriteLine("Licensed under the terms of the Flashback license")
    End Sub
End Module
