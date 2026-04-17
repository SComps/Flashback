Class Application
    Protected Overrides Async Sub OnStartup(e As StartupEventArgs)
        MyBase.OnStartup(e)
        
        ' Prevent shutdown when splash closes
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown

        ' Create and show splash
        Dim splash As New SplashWindow()
        splash.Show()

        ' Wait for 6 seconds, unless closed via click
        Dim timerTask = Task.Delay(6000)
        
        ' Monitor if splash closes early (click)
        While Not timerTask.IsCompleted AndAlso splash.IsVisible
            Await Task.Delay(100)
        End While

        If splash.IsVisible Then splash.Close()

        ' Start main window
        Dim main As New MainWindow()
        main.Show()
        
        ' Restore default shutdown behavior
        Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose
    End Sub
End Class
