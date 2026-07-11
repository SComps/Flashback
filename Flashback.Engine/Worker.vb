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
            Await Task.Delay(30000, stoppingToken)

            ' Re-examine config and recreate any devices that have disappeared
            If Not stoppingToken.IsCancellationRequested Then
                RecreateDisconnectedDevices()
            End If
        End While

        _logger.LogInformation("Flashback Engine Service Stopping.")
        Cleanup()
    End Function

    ''' <summary>
    ''' Called every 30 seconds. Reads the config and ensures every enabled device
    ''' exists in _devList. Any device that was destroyed (via the Disconnected event)
    ''' will be absent and gets recreated fresh here.
    ''' </summary>
    Private Sub RecreateDisconnectedDevices()
        If Not File.Exists(_configFile) Then Return

        Try
            Dim lic = LicenseManager.GetLicenseInfo()
            Dim lines = File.ReadAllLines(_configFile)
            Dim loadedCount As Integer = 0

            SyncLock _devList
                loadedCount = _devList.Count
            End SyncLock

            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split("||", StringSplitOptions.TrimEntries)
                If p.Length < 10 Then Continue For

                If lic.MaxPrinters > 0 AndAlso loadedCount >= lic.MaxPrinters Then Continue For

                Dim devName = p(0)
                Dim isEnabled = If(p.Length >= 13, (p(12) = "True"), True)

                If Not isEnabled Then Continue For

                ' Check if this device already exists in _devList (connected or currently connecting)
                Dim existing As Devs = Nothing
                SyncLock _devList
                    existing = _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase))
                End SyncLock

                If existing IsNot Nothing Then Continue For  ' Device exists - leave it alone

                ' Device is absent - recreate it fresh
                _logger.LogInformation("{Dev} is absent from device list. Recreating and connecting.", devName)
                Dim d = CreateDevice(p)
                If d IsNot Nothing Then
                    SyncLock _devList
                        _devList.Add(d)
                        loadedCount = _devList.Count
                    End SyncLock
                    d.Connect()
                End If
            Next
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR in RecreateDisconnectedDevices: {Error}", ex.Message)
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Called by the Disconnected event on a Devs object. Immediately removes the
    ''' device from _devList so RecreateDisconnectedDevices() will rebuild it fresh.
    ''' </summary>
    Private Sub OnDeviceDisconnected(dev As Devs)
        Dim devName = dev.DevName
        SyncLock _devList
            _devList.Remove(dev)
        End SyncLock
        _logger.LogInformation("{Dev} disconnected and removed from device list. Will reconnect on next cycle.", devName)
    End Sub

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
            Dim newDevices As New List(Of Devs)
            Dim loadedCount As Integer = 0
            Dim lines = File.ReadAllLines(_configFile)

            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split("||", StringSplitOptions.TrimEntries)
                If p.Length < 10 Then Continue For

                If lic.MaxPrinters > 0 AndAlso loadedCount >= lic.MaxPrinters Then Continue For

                Dim devName = p(0)
                Dim newEnabled = If(p.Length >= 13, (p(12) = "True"), True)

                Dim existing As Devs = Nothing
                SyncLock _devList
                    existing = _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase))
                End SyncLock

                ' Device exists and is being disabled - disconnect and remove
                If existing IsNot Nothing AndAlso Not newEnabled Then
                    _logger.LogInformation("{Dev} is being disabled. Disconnecting and removing.", devName)
                    existing.Disconnect()
                    SyncLock _devList
                        _devList.Remove(existing)
                    End SyncLock
                    Continue For
                End If

                ' Device doesn't exist and is disabled - skip it
                If existing Is Nothing AndAlso Not newEnabled Then
                    Continue For
                End If

                ' Device exists and is enabled - check for connection-critical config changes
                If existing IsNot Nothing AndAlso newEnabled Then
                    Dim newConnType = Val(p(3))
                    Dim newDevDest = p(4)
                    Dim newOS = CType(Val(p(5)), OSType)

                    Dim needsReconnect = (existing.DevDest <> newDevDest) OrElse
                                        (existing.OS <> newOS) OrElse
                                        (existing.ConnType <> newConnType)

                    If Not needsReconnect Then
                        ' Non-critical settings changed - update in place, no reconnect needed
                        existing.DevDescription = p(1)
                        existing.DevType = Val(p(2))
                        existing.PDF = (p(7) = "True")
                        existing.Orientation = Val(p(8))
                        existing.OutDest = p(9)

                        If p.Length >= 12 Then
                            existing.Shading = CType(Val(p(10)), RenderPDF.ShadingColor)
                            existing.JobNumber = Val(p(11))
                        End If

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

                        ' Device stays in _devList - just track it as still active
                        activeDevices.Add(existing)
                        loadedCount += 1
                        Continue For
                    End If

                    ' Connection-critical settings changed - disconnect and recreate
                    _logger.LogInformation("Connection settings changed for {Dev}. Disconnecting before recreating.", devName)
                    existing.Disconnect()
                    SyncLock _devList
                        _devList.Remove(existing)
                    End SyncLock
                    Threading.Thread.Sleep(500)
                    ' Fall through to create new device
                End If

                ' Create new device (new or being recreated) - only if enabled
                If newEnabled Then
                    Dim d = CreateDevice(p)
                    If d IsNot Nothing Then
                        activeDevices.Add(d)
                        newDevices.Add(d)
                        loadedCount += 1
                    End If
                End If
            Next

            ' Remove from _devList anything no longer in the config (not in activeDevices)
            Dim activeNames = New HashSet(Of String)(activeDevices.Select(Function(d) d.DevName), StringComparer.OrdinalIgnoreCase)
            Dim staleDevices As New List(Of Devs)
            SyncLock _devList
                staleDevices.AddRange(_devList.Where(Function(d) Not activeNames.Contains(d.DevName)))
                For Each d In staleDevices
                    _devList.Remove(d)
                Next
                ' Add the newly created devices to _devList
                _devList.AddRange(newDevices)
            End SyncLock
            _configDate = File.GetLastWriteTime(_configFile)

            ' Disconnect stale devices outside the lock
            For Each d In staleDevices
                _logger.LogInformation("Device object destroyed: {Dev}", d.DevName)
                d.Disconnect()
            Next

            ' Connect only the newly created devices
            For Each d In newDevices
                d.Connect()
            Next
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR loading configuration: {Error}", ex.Message)
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Creates and wires up a new Devs object from a config line token array.
    ''' Does NOT call Connect() - the caller is responsible for that.
    ''' </summary>
    Private Function CreateDevice(p As String()) As Devs
        Try
            _logger.LogInformation("Creating device object for {Dev}.", p(0))
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

            d.Enabled = True

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
            AddHandler d.Disconnected, AddressOf OnDeviceDisconnected
            d.Logger = _logger

            _logger.LogInformation("Device object created: {Dev}", d.DevName)
            Return d
        Catch ex As Exception
            _logger.LogError("ERROR creating device {Dev}: {Error}", p(0), ex.Message)
            Return Nothing
        End Try
    End Function

    Private Sub CleanupDevices()
        Dim devicesSnapshot As List(Of Devs)
        SyncLock _devList
            devicesSnapshot = New List(Of Devs)(_devList)
            _devList.Clear()
        End SyncLock

        For Each d In devicesSnapshot
            _logger.LogInformation("Device object destroyed: {Dev}", d.DevName)
            d.Disconnect()
        Next
    End Sub

    Private Sub Cleanup()
        If _timersDisposed Then Return
        _timersDisposed = True

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

        Threading.Thread.Sleep(100)

        _logger.LogInformation("Stopping all printer connection tasks...")

        Dim devicesSnapshot As List(Of Devs)
        SyncLock _devList
            devicesSnapshot = New List(Of Devs)(_devList)
            _devList.Clear()
        End SyncLock

        For Each d In devicesSnapshot
            _logger.LogInformation("Device object destroyed: {Dev}", d.DevName)
            d.Disconnect()
        Next
    End Sub

    Private Sub StatTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _statTimer.Elapsed
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
            Return
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR monitoring configuration: {Error}", ex.Message)
            End If
        End Try
    End Sub

    Private Sub CmdTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _cmdTimer.Elapsed
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
