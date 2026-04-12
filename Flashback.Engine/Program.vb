Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting

Module Program
    Sub Main(args As String())
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
