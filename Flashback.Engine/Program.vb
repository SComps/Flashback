Imports System.Diagnostics
Imports System.Linq
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Flashback.Core

Module Program
    Sub Main(args As String())
        If OperatingSystem.IsLinux() Then
            If args.Contains("-d") OrElse args.Contains("--daemon") Then
                Dim psi As New ProcessStartInfo(Environment.ProcessPath)
                psi.Arguments = String.Join(" ", args.Where(Function(a) a <> "-d" AndAlso a <> "--daemon"))
                psi.UseShellExecute = False
                psi.CreateNoWindow = True
                ' Redirect output to disconnect from terminal
                psi.RedirectStandardOutput = True
                psi.RedirectStandardError = True
                Process.Start(psi)
                Console.WriteLine("Flashback Engine detached into background.")
                Return
            End If
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
End Module
