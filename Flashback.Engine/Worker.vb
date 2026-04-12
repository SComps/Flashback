Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports System.IO
Imports System.Threading
Imports Flashback.Core

Public Class Worker
    Inherits BackgroundService

    Private ReadOnly _logger As ILogger(Of Worker)
    Private ReadOnly _devList As New List(Of Devs)
    Private _configFile As String
    Private _cmdFile As String
    Private _configDate As DateTime
    Private WithEvents _statTimer As New System.Timers.Timer
    Private WithEvents _cmdTimer As New System.Timers.Timer

    Public Sub New(logger As ILogger(Of Worker))
        _logger = logger
        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
        _configFile = Path.Combine(baseDir, "devices.dat")
        _cmdFile = Path.Combine(baseDir, "commands.dat")
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        _logger.LogInformation("Flashback Engine Service Starting.")

        LoadDevices()

        _statTimer.Interval = 5000
        _statTimer.Enabled = True

        _cmdTimer.Interval = 500
        _cmdTimer.Enabled = True

        While Not stoppingToken.IsCancellationRequested
            For Each d In _devList
                If Not d.Connected AndAlso Not d.Connecting Then
                    _logger.LogInformation("Attempting to connect {Dev}...", d.DevName)
                    d.Connect()
                End If
            Next

            Await Task.Delay(5000, stoppingToken)
        End While

        _logger.LogInformation("Flashback Engine Service Stopping.")
        Cleanup()
    End Function

    Private Sub LoadDevices()
        Dim lic = LicenseManager.GetLicenseInfo()
        If lic.IsLicensed Then
            Dim limitStr As String = If(lic.MaxPrinters = 0, "Unlimited", lic.MaxPrinters.ToString())
            _logger.LogInformation("LICENSE: Licensed to {User}. Max concurrent printers: {Count}", lic.LicensedTo, limitStr)
        Else
            If Not String.IsNullOrEmpty(lic.Error) Then
                _logger.LogError("LICENSE ERROR: {Error}", lic.Error)
            End If
            _logger.LogWarning("LICENSE: No valid license found. Running in FREE NON-COMMERCIAL USE mode (Max 2 printers).")
        End If

        _logger.LogInformation("Reloading devices from {ConfigFile}...", _configFile)
        
        Cleanup()
        If Not File.Exists(_configFile) Then Return

        Try
            Dim loadedCount As Integer = 0
            Using rdr As New StreamReader(_configFile)
                While Not rdr.EndOfStream
                    Dim line = rdr.ReadLine()
                    If String.IsNullOrWhiteSpace(line) Then Continue While
                    
                    If lic.MaxPrinters > 0 AndAlso loadedCount >= lic.MaxPrinters Then
                        _logger.LogWarning("LICENSE LIMIT REACHED: Ignoring device '{Line}' (Limit: {Limit})", line.Split("||")(0), lic.MaxPrinters)
                        Continue While
                    End If

                    Dim p = line.Split("||", StringSplitOptions.TrimEntries)
                    
                    If p.Length >= 10 Then
                        Dim d As New Devs()
                        d.DevName = p(0)
                        d.DevDescription = p(1)
                        d.DevType = Val(p(2))
                        d.ConnType = Val(p(3))
                        d.DevDest = p(4)
                        d.OS = CType(Val(p(5)), OSType)
                        d.PDF = (p(7) = "True")
                        d.Orientation = Val(p(8))
                        d.OutDest = p(9)

                        If p.Length = 12 Then
                            d.Shading = CType(Val(p(10)), RenderPDF.ShadingColor)
                            d.JobNumber = Val(p(11))
                        ElseIf p.Length >= 13 Then
                            d.Shading = CType(Val(p(11)), RenderPDF.ShadingColor)
                            d.JobNumber = Val(p(12))
                        End If

                        AddHandler d.LogMessage, Sub(msg, col) _logger.LogInformation("{Dev}: {Msg}", d.DevName, msg)
                        d.Logger = _logger
                        _devList.Add(d)
                        d.Connect()
                        loadedCount += 1
                    End If
                End While
            End Using
            _configDate = File.GetLastWriteTime(_configFile)
        Catch ex As Exception
            _logger.LogError("Error loading configuration: {Error}", ex.Message)
        End Try
    End Sub

    Private Sub Cleanup()
        For Each d In _devList
            d.Disconnect()
        Next
        _devList.Clear()
    End Sub

    Private Sub StatTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _statTimer.Elapsed
        Try
            If File.Exists(_configFile) Then
                Dim currentCfgDate = File.GetLastWriteTime(_configFile)
                If currentCfgDate > _configDate Then
                    _logger.LogInformation("Configuration file change detected.")
                    LoadDevices()
                End If
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub CmdTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _cmdTimer.Elapsed
        If Not File.Exists(_cmdFile) Then Return
        
        Try
            Dim lines = File.ReadAllLines(_cmdFile)
            File.Delete(_cmdFile)
            
            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim parts = line.Split("||")
                If parts.Length < 2 Then Continue For
                
                Dim cmd = parts(0).ToUpper()
                Dim devName = parts(1)
                
                Dim target = _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase))
                If target IsNot Nothing Then
                    Select Case cmd
                        Case "CONNECT"
                            _logger.LogInformation("Signal: Manual connect requested for {Dev}", devName)
                            target.Connect()
                        Case "DISCONNECT"
                            _logger.LogInformation("Signal: Manual disconnect requested for {Dev}", devName)
                            target.Disconnect()
                    End Select
                End If
            Next
        Catch ex As Exception
            _logger.LogError("Error processing command file: {Error}", ex.Message)
        End Try
    End Sub
End Class
