Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting

Module Program
    Sub Main(args As String())
#If LINUX Then
        If args.Contains("-d") OrElse args.Contains("--daemon") Then
            Dim psi As New Process.StartInfo("/proc/self/exe")
            psi.Arguments = String.Join(" ", args.Where(Function(a) a <> "-d" AndAlso a <> "--daemon"))
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            Process.Start(psi)
            Console.WriteLine("Flashback 3270 Server detached into background.")
            Return
        End If
#End If
        Dim builder = Host.CreateApplicationBuilder(args)

#If WINDOWS Then
        builder.Services.AddWindowsService(Sub(options)
                                               options.ServiceName = "FlashbackConfig3270"
                                           End Sub)
#ElseIf LINUX Then
        builder.Services.AddSystemd()
#End If

        builder.Services.AddHostedService(Of Config3270Worker)()

        Dim configHost = builder.Build()
        configHost.Run()
    End Sub
End Module
