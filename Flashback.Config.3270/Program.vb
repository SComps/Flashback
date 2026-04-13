Imports System.Diagnostics
Imports System.Linq
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting

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
                Console.WriteLine("Flashback 3270 Server detached into background.")
                Return
            End If
        End If
        ' Arguments Logic
        Dim syspw As String = ""
        Dim port As Integer = 3270

        Dim pwArgIdx = Array.IndexOf(args, "--password")
        If pwArgIdx >= 0 AndAlso args.Length > pwArgIdx + 1 Then
            syspw = args(pwArgIdx + 1)
        ElseIf System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syspw.txt")) Then
            syspw = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syspw.txt")).Trim()
        End If

        Dim portArgIdx = Array.IndexOf(args, "-p")
        If portArgIdx < 0 Then portArgIdx = Array.IndexOf(args, "--port")
        If portArgIdx >= 0 AndAlso args.Length > portArgIdx + 1 Then
            Integer.TryParse(args(portArgIdx + 1), port)
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
End Module

Public Class Config3270Settings
    Public Property Port As Integer
    Public Property Password As String
End Class
