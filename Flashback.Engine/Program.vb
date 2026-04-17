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
        Dim mutex As New Mutex(True, "Global\FlashbackEngine", createdNew)
        If Not createdNew Then
            System.Console.WriteLine("Error: Flashback Engine is already running.")
            Environment.Exit(1)
        End If

        Dim isDaemon As Boolean = False
        ' Validate and handle help
        For i As Integer = 0 To args.Length - 1
            Dim arg = args(i).ToLower()
            Select Case arg
                Case "-h", "--help"
                    ShowHelp()
                    Environment.Exit(0)
                Case "-d", "--daemon"
                    isDaemon = True
                Case Else
                    System.Console.WriteLine($"Unknown option: {args(i)}")
                    ShowHelp()
                    Environment.Exit(1)
            End Select
        Next

        If isDaemon Then
            Dim psi As New ProcessStartInfo(Environment.ProcessPath)
            psi.Arguments = String.Join(" ", args.Where(Function(a) a <> "-d" AndAlso a <> "--daemon"))
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            ' Redirect output to disconnect from terminal
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            Process.Start(psi)
            System.Console.WriteLine("Flashback Engine detached into background.")
            mutex.Dispose()
            Environment.Exit(0)
        End If
        
        Dim builder = Host.CreateApplicationBuilder(args)
        
        ' Configure File Logging
        builder.Logging.AddFile()

        If OperatingSystem.IsWindows() Then
            builder.Services.AddWindowsService(Sub(options)
                                                   options.ServiceName = "FlashbackEngine"
                                               End Sub)
        ElseIf OperatingSystem.IsLinux() Then
            builder.Services.AddSystemd()
        End If

        builder.Services.AddHostedService(Of Worker)()

        Dim engineHost = builder.Build()
        engineHost.Run()
    End Sub

    Private Sub ShowHelp()
        Console.WriteLine("Flashback Engine Server")
        Console.WriteLine("Usage: Flashback.Engine [options]")
        Console.WriteLine()
        Console.WriteLine("Options:")
        Console.WriteLine("  -h, --help            Show this help message")
        Console.WriteLine("  -d, --daemon          Run in background (Linux only)")
        Console.WriteLine()
    End Sub
End Module
