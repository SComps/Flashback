Imports System.Diagnostics
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting

Module Program
    Sub Main(args As String())
#If LINUX Then
        If args.Contains("-d") OrElse args.Contains("--daemon") Then
            Dim psi As New ProcessStartInfo("/proc/self/exe")
            psi.Arguments = String.Join(" ", args.Where(Function(a) a <> "-d" AndAlso a <> "--daemon"))
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            Process.Start(psi)
            Console.WriteLine("Flashback Engine detached into background.")
            Return
        End If
#End If
        Dim builder = Host.CreateApplicationBuilder(args)

#If WINDOWS Then
        builder.Services.AddWindowsService(Sub(options)
                                               options.ServiceName = "FlashbackEngine"
                                           End Sub)
#ElseIf LINUX Then
        builder.Services.AddSystemd()
#End If

        builder.Services.AddHostedService(Of Worker)()

        Dim engineHost = builder.Build()
        engineHost.Run()
    End Sub
End Module
