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

    Private Sub SaveDevices()
        Try
            SyncLock _devList
                File.WriteAllLines(_configFile, _devList.Select(Function(d) d.ToConfigLine()))
                _configDate = File.GetLastWriteTime(_configFile)
            End SyncLock
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR saving configuration: {Error}", ex.Message)
            End If
        End Try
    End Sub

    Private Sub LoadDevices()
        Dim lic = LicenseManager.GetLicenseInfo()
        If lic.IsLicensed Then
            Dim limitStr As String = If(lic.MaxPrinters = 0, "Unlimited", lic.MaxPrinters.ToString())
            _logger.LogInformation("LICENSE: Licensed to {User}. Max concurrent printers: {Count}", lic.LicensedTo, limitStr)
        If Not lic.IsLicensed AndAlso Not String.IsNullOrEmpty(lic.Error) Then
            _logger.LogError("LICENSE ERROR: {Error}", lic.Error)
        End If

        If Not lic.IsLicensed Then
            _logger.LogWarning("LICENSE: No valid license found. Running in FREE mode (Max 2 printers).")
        End If

        If Not File.Exists(_configFile) Then Return

        Try
            Dim activeDevices As New List(Of Devs)
            Dim loadedCount As Integer = 0
            Dim lines = File.ReadAllLines(_configFile)
            
            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split("||", StringSplitOptions.TrimEntries)
                If p.Length < 10 Then Continue For
                
                If lic.MaxPrinters > 0 AndAlso loadedCount >= lic.MaxPrinters Then Continue For

                Dim devName = p(0)
                Dim existing = _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase))
                
                If existing IsNot Nothing AndAlso existing.DevDest = p(4) AndAlso existing.OS = CType(Val(p(5)), OSType) Then
                    ' Configuration matches, just update JobNumber and keep current connection
                    existing.JobNumber = Val(If(p.Length >= 12, p(11), p(p.Length - 1)))
                    activeDevices.Add(existing)
                    _devList.Remove(existing)
                Else
                    ' New or significantly changed device
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
                    
                    If p.Length >= 12 Then
                        d.Shading = CType(Val(p(10)), RenderPDF.ShadingColor)
                        d.JobNumber = Val(p(11))
                    End If

                    AddHandler d.LogMessage, Sub(msg, col) _logger.LogInformation("{Dev}: {Msg}", d.DevName, msg)
                    AddHandler d.JobNumberChanged, Sub(s) SaveDevices()
                    d.Logger = _logger
                    d.Connect()
                    activeDevices.Add(d)
                End If
                loadedCount += 1
            Next

            ' Anything left in _devList is no longer in the config or was replaced
            Cleanup()
            _devList.AddRange(activeDevices)
            _configDate = File.GetLastWriteTime(_configFile)
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR loading configuration: {Error}", ex.Message)
            End If
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
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR monitoring configuration: {Error}", ex.Message)
            End If
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
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR processing command file: {Error}", ex.Message)
            End If
        End Try
    End Sub
End Class
