Imports System.Diagnostics
Imports System.Linq
Imports System.Threading
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting

Module Program
    Sub Main(args As String())
        ' Single Instance Check
        Dim createdNew As Boolean
        Dim mutex As New Mutex(True, "Global\FlashbackConfig3270", createdNew)
        If Not createdNew Then
            System.Console.WriteLine("Error: Flashback 3270 Configuration Server is already running.")
            Environment.Exit(1)
        End If

        Dim isDaemon As Boolean = False
        Dim syspw As String = ""
        Dim port As Integer = 3270

        ' Validate and handle help
        For i As Integer = 0 To args.Length - 1
            Dim arg = args(i).ToLower()
            Select Case arg
                Case "-h", "--help"
                    ShowHelp()
                    Environment.Exit(0)
                Case "-d", "--daemon"
                    isDaemon = True
                Case "-p", "--port"
                    If i + 1 < args.Length Then
                        i += 1
                        Integer.TryParse(args(i), port)
                    End If
                Case "--password"
                    If i + 1 < args.Length Then
                        i += 1
                        syspw = args(i)
                    End If
                Case Else
                    System.Console.WriteLine($"Unknown option: {args(i)}")
                    ShowHelp()
                    Environment.Exit(1)
            End Select
        Next

        If String.IsNullOrEmpty(syspw) AndAlso System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syspw.txt")) Then
            syspw = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syspw.txt")).Trim()
        End If

        If isDaemon Then
            Dim psi As New ProcessStartInfo(Environment.ProcessPath)
            psi.Arguments = String.Join(" ", args.Where(Function(a) a <> "-d" AndAlso a <> "--daemon"))
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            ' Redirect output to disconnect from terminal
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            Process.Start(psi)
            System.Console.WriteLine("Flashback 3270 Server detached into background.")
            mutex.Dispose()
            Environment.Exit(0)
        End If

        Dim builder = Host.CreateApplicationBuilder(args)
        
        Dim settings As New Config3270Settings With {
            .Port = port,
            .Password = syspw
        }
        builder.Services.AddSingleton(settings)

        If OperatingSystem.IsWindows() Then
            builder.Services.AddWindowsService(Sub(options)
                                                   options.ServiceName = "FlashbackConfig3270"
                                               End Sub)
        ElseIf OperatingSystem.IsLinux() Then
            builder.Services.AddSystemd()
        End If

        builder.Services.AddHostedService(Of Config3270Worker)()

        Dim configHost = builder.Build()
        configHost.Run()
    End Sub

    Private Sub ShowHelp()
        System.Console.WriteLine("Flashback 3270 Configuration Server (Build 2026.04.17.02)")
        System.Console.WriteLine("Usage: Flashback.Config.3270 [options]")
        System.Console.WriteLine()
        System.Console.WriteLine("Options:")
        System.Console.WriteLine("  -h, --help            Show this help message")
        System.Console.WriteLine("  -d, --daemon          Run in background")
        System.Console.WriteLine("  -p, --port <port>     Port to listen on (default: 3270)")
        System.Console.WriteLine("  --password <pw>       System password for administrative access")
        System.Console.WriteLine()
    End Sub
End Module

Public Class Config3270Settings
    Public Property Port As Integer
    Public Property Password As String
End Class
