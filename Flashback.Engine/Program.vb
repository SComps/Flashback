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
        Dim webPort As Integer = 0

        ' Validate and handle help
        Dim i As Integer = 0
        While i < args.Length
            Dim arg = args(i).ToLower()
            Select Case arg
                Case "-h", "--help"
                    ShowHelp()
                    Environment.Exit(0)
                Case "-d", "--daemon"
                    isDaemon = True
                Case "-w", "--web"
                    If i + 1 < args.Length Then
                        If Integer.TryParse(args(i + 1), webPort) Then
                            i += 1
                        End If
                    End If
                Case Else
                    ' Ignore unknown args in service mode as they might be injected by host
            End Select
            i += 1
        End While

        ' Also check Environment Variable as fallback
        Dim envPort As String = Environment.GetEnvironmentVariable("FLASHBACK_WEB_PORT")
        If webPort = 0 AndAlso Not String.IsNullOrEmpty(envPort) Then
            Integer.TryParse(envPort, webPort)
        End If

        If isDaemon Then
            Dim psi As New ProcessStartInfo(Environment.ProcessPath)
            Dim daemonArgs = args.Where(Function(a) a <> "-d" AndAlso a <> "--daemon").ToList()
            psi.Arguments = String.Join(" ", daemonArgs)
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
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

        ' Register the core worker
        builder.Services.AddHostedService(Of Worker)()

        ' Always set the env var if we have a port so the WebWorker can find it
        If webPort > 0 Then
            Environment.SetEnvironmentVariable("FLASHBACK_WEB_PORT", webPort.ToString())
            builder.Services.AddHostedService(Of WebWorker)()
        End If

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
        Console.WriteLine("  -w, --web <port>      Enable web server on specified port")
        Console.WriteLine()
    End Sub
End Module
