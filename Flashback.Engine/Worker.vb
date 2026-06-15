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
    Private WithEvents _statTimer As System.Timers.Timer
    Private WithEvents _cmdTimer As System.Timers.Timer
    Private _timersDisposed As Boolean = False

    Public Sub New(logger As ILogger(Of Worker))
        _logger = logger
        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
        _configFile = Path.Combine(baseDir, "devices.dat")
        _cmdFile = Path.Combine(baseDir, "commands.dat")
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        Dim version = Reflection.Assembly.GetExecutingAssembly().GetName().Version
        _logger.LogInformation("Flashback Engine Service v{Ver} Starting.", version.ToString())

        ' Initialize timers
        _statTimer = New System.Timers.Timer()
        _cmdTimer = New System.Timers.Timer()
        
        LoadDevices()

        _statTimer.Interval = 5000
        _statTimer.Enabled = True

        _cmdTimer.Interval = 500
        _cmdTimer.Enabled = True

        While Not stoppingToken.IsCancellationRequested
            ' Create a snapshot of devices to iterate safely
            Dim devicesSnapshot As List(Of Devs)
            SyncLock _devList
                devicesSnapshot = New List(Of Devs)(_devList)
            End SyncLock
            
            For Each d In devicesSnapshot
                If d.Enabled AndAlso Not d.Connected AndAlso Not d.Connecting Then
                    _logger.LogInformation("DIAGNOSTIC: {Dev} appears offline (Connected={IsConn}, Connecting={IsConnecting}). Attempting connect...", d.DevName, d.Connected, d.Connecting)
                    d.Connect()
                ElseIf Not d.Enabled AndAlso (d.Connected OrElse d.Connecting) Then
                    _logger.LogInformation("DIAGNOSTIC: {Dev} is disabled but currently connected. Disconnecting...", d.DevName)
                    d.Disconnect()
                Else
                    _logger.LogTrace("DIAGNOSTIC: {Dev} check. Connected={IsConn}, Connecting={IsConnecting}, Enabled={IsEnabled}", d.DevName, d.Connected, d.Connecting, d.Enabled)
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
        Else
            If Not String.IsNullOrEmpty(lic.Error) Then
                _logger.LogError("LICENSE ERROR: {Error}", lic.Error)
            End If
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
                
                If existing IsNot Nothing Then
                    ' Check if connection-critical settings changed
                    Dim newConnType = Val(p(3))
                    Dim newDevDest = p(4)
                    Dim newOS = CType(Val(p(5)), OSType)
                    Dim newEnabled = If(p.Length >= 13, (p(12) = "True"), True)
                    
                    Dim needsReconnect = (existing.DevDest <> newDevDest) OrElse
                                        (existing.OS <> newOS) OrElse
                                        (existing.ConnType <> newConnType)
                    
                    If Not needsReconnect Then
                        ' Only non-critical settings changed - update in place without reconnecting
                        _logger.LogInformation("DIAGNOSTIC: {Dev} non-critical config update. Updating properties without reconnect.", devName)
                        
                        existing.DevDescription = p(1)
                        existing.DevType = Val(p(2))
                        existing.PDF = (p(7) = "True")
                        existing.Orientation = Val(p(8))
                        existing.OutDest = p(9)
                        
                        If p.Length >= 12 Then
                            existing.Shading = CType(Val(p(10)), RenderPDF.ShadingColor)
                            existing.JobNumber = Val(p(11))
                        End If
                        
                        ' Update email configuration
                        If p.Length >= 14 Then existing.EmailEnabled = (p(13) = "True")
                        If p.Length >= 15 Then existing.EmailRecipients = p(14)
                        If p.Length >= 16 Then existing.SmtpServer = p(15)
                        If p.Length >= 17 Then existing.SmtpPort = Val(p(16))
                        If p.Length >= 18 Then existing.SmtpUsername = p(17)
                        If p.Length >= 19 Then existing.SmtpPassword = p(18)
                        If p.Length >= 20 Then existing.SmtpUseTLS = (p(19) = "True")
                        If p.Length >= 21 Then existing.EmailFromAddress = p(20)
                        If p.Length >= 22 Then existing.EmailFromName = p(21)
                        If p.Length >= 23 Then existing.EmailSubject = p(22)
                        If p.Length >= 24 Then existing.EmailBody = p(23)
                        
                        ' Handle enabled state change
                        If existing.Enabled <> newEnabled Then
                            existing.Enabled = newEnabled
                            If newEnabled Then
                                _logger.LogInformation("{Dev} enabled - initiating connection.", devName)
                                existing.Connect()
                            Else
                                _logger.LogInformation("{Dev} disabled - disconnecting.", devName)
                                existing.Disconnect()
                            End If
                        End If
                        
                        activeDevices.Add(existing)
                        _devList.Remove(existing)
                        loadedCount += 1
                        Continue For
                    End If
                    
                    ' Connection-critical settings changed - need to recreate
                    _logger.LogInformation("Connection settings changed for {Dev}. Dest({OldDest} -> {NewDest}), OS({OldOS} -> {NewOS}), ConnType({OldConn} -> {NewConn})",
                                          devName, existing.DevDest, newDevDest, existing.OS, newOS, existing.ConnType, newConnType)
                    _logger.LogInformation("Disconnecting {Dev} before recreating device object...", devName)
                    existing.Disconnect()
                    ' Give disconnect time to complete
                    Threading.Thread.Sleep(500)
                End If
                
                ' Create new device object (either new device or connection settings changed)
                If existing IsNot Nothing Then
                    _logger.LogInformation("Recreating device object for {Dev}.", devName)
                Else
                    _logger.LogInformation("Creating new device object for {Dev}.", devName)
                End If
                
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
                If p.Length >= 13 Then
                    d.Enabled = (p(12) = "True")
                End If
                
                ' Load email configuration (backward compatible - fields 13-23)
                If p.Length >= 14 Then d.EmailEnabled = (p(13) = "True")
                If p.Length >= 15 Then d.EmailRecipients = p(14)
                If p.Length >= 16 Then d.SmtpServer = p(15)
                If p.Length >= 17 Then d.SmtpPort = Val(p(16))
                If p.Length >= 18 Then d.SmtpUsername = p(17)
                If p.Length >= 19 Then d.SmtpPassword = p(18)
                If p.Length >= 20 Then d.SmtpUseTLS = (p(19) = "True")
                If p.Length >= 21 Then d.EmailFromAddress = p(20)
                If p.Length >= 22 Then d.EmailFromName = p(21)
                If p.Length >= 23 Then d.EmailSubject = p(22)
                If p.Length >= 24 Then d.EmailBody = p(23)

                AddHandler d.LogMessage, Sub(msg, col) _logger.LogInformation("{Dev}: {Msg}", d.DevName, msg)
                AddHandler d.JobNumberChanged, Sub(s) SaveDevices()
                d.Logger = _logger
                _logger.LogInformation("Device object created: {Dev}", d.DevName)
                If d.Enabled Then
                    d.Connect()
                Else
                    _logger.LogInformation("Device {Dev} is disabled, will not connect.", d.DevName)
                End If
                activeDevices.Add(d)
                loadedCount += 1
            Next

            ' Anything left in _devList is no longer in the config or was replaced
            CleanupDevices()
            _devList.AddRange(activeDevices)
            _configDate = File.GetLastWriteTime(_configFile)
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR loading configuration: {Error}", ex.Message)
            End If
        End Try
    End Sub

    Private Sub CleanupDevices()
        ' Only disconnect and remove old devices, don't touch timers
        _logger.LogInformation("Cleaning up old device objects...")
        
        ' Create snapshot and clear list under lock
        Dim devicesSnapshot As List(Of Devs)
        SyncLock _devList
            devicesSnapshot = New List(Of Devs)(_devList)
            _devList.Clear()
        End SyncLock
        
        ' Disconnect devices outside the lock
        For Each d In devicesSnapshot
            _logger.LogInformation("Device object destroyed: {Dev}", d.DevName)
            d.Disconnect()
        Next
    End Sub
    
    Private Sub Cleanup()
        ' This is called when the service is stopping - dispose everything
        If _timersDisposed Then Return
        _timersDisposed = True
        
        ' Disable and dispose timers to prevent new events
        Try
            If _statTimer IsNot Nothing Then
                _statTimer.Enabled = False
                _statTimer.Dispose()
            End If
        Catch ex As Exception
            _logger.LogWarning("Error disposing stat timer: {Error}", ex.Message)
        End Try
        
        Try
            If _cmdTimer IsNot Nothing Then
                _cmdTimer.Enabled = False
                _cmdTimer.Dispose()
            End If
        Catch ex As Exception
            _logger.LogWarning("Error disposing cmd timer: {Error}", ex.Message)
        End Try
        
        ' Small delay to allow any in-flight timer events to complete
        Threading.Thread.Sleep(100)
        
        _logger.LogInformation("Stopping all printer connection tasks...")
        
        ' Create snapshot and clear list under lock
        Dim devicesSnapshot As List(Of Devs)
        SyncLock _devList
            devicesSnapshot = New List(Of Devs)(_devList)
            _devList.Clear()
        End SyncLock
        
        ' Disconnect devices outside the lock
        For Each d In devicesSnapshot
            _logger.LogInformation("Device object destroyed: {Dev}", d.DevName)
            d.Disconnect()
        Next
    End Sub

    Private Sub StatTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _statTimer.Elapsed
        ' Guard against events firing after disposal
        If _timersDisposed Then Return
        
        Try
            If File.Exists(_configFile) Then
                Dim currentCfgDate = File.GetLastWriteTime(_configFile)
                If currentCfgDate > _configDate Then
                    _logger.LogInformation("Configuration file change detected.")
                    LoadDevices()
                End If
            End If
        Catch ex As ObjectDisposedException
            ' Timer was disposed while event was firing - this is normal during shutdown
            Return
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR monitoring configuration: {Error}", ex.Message)
            End If
        End Try
    End Sub

    Private Sub CmdTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _cmdTimer.Elapsed
        ' Guard against events firing after disposal
        If _timersDisposed Then Return
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
                
                ' Find target device under lock
                Dim target As Devs = Nothing
                SyncLock _devList
                    target = _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase))
                End SyncLock
                
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
