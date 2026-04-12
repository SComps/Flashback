Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting

Module Program
    Sub Main(args As String())
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
