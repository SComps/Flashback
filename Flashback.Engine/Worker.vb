Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports System.IO
Imports System.Threading
Imports Flashback.Core

Public Class Worker
    Inherits BackgroundService

    Private ReadOnly _logger As ILogger(Of Worker)
    Private ReadOnly _registry As PrinterRegistry
    Private ReadOnly _devList As New List(Of Devs)
    Private _configFile As String
    Private _cmdFile As String
    Private _configDate As DateTime
    Private WithEvents _statTimer As System.Timers.Timer
    Private WithEvents _cmdTimer As System.Timers.Timer
    Private _timersDisposed As Boolean = False

    ' Prevents concurrent Reconcile() calls from the stat timer and the main loop
    ' firing close together. TryEnter: if already running, the caller simply skips —
    ' the next tick will pick up any remaining work.
    Private ReadOnly _reconcileLock As New Object()

    ' Synchronizes all reads and writes to devices.dat across threads (SaveDevices,
    ' EnableDeviceInConfig, DisableDeviceInConfig, Reconcile, and StatTimer).
    Private ReadOnly _configFileLock As New Object()

    Public Sub New(logger As ILogger(Of Worker), registry As PrinterRegistry)
        _logger = logger
        _registry = registry
        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
        _configFile = Path.Combine(baseDir, "devices.dat")
        _cmdFile = Path.Combine(baseDir, "commands.dat")
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        Dim version = Reflection.Assembly.GetExecutingAssembly().GetName().Version
        _logger.LogInformation("Flashback Engine Service v{Ver} Starting.", version.ToString())

        _statTimer = New System.Timers.Timer()
        _cmdTimer = New System.Timers.Timer()

        ' Initial load: bring up all enabled devices from devices.dat
        Reconcile()

        ' Config-file watcher: detects changes made by the web admin or config tool
        _statTimer.Interval = 5000
        _statTimer.Enabled = True

        ' Command-file poller: processes CONNECT / DISCONNECT signals from the web admin
        _cmdTimer.Interval = 500
        _cmdTimer.Enabled = True

        ' Main loop: reconciles every 15 seconds.
        ' This is both the steady-state health check AND the reconnect retry mechanism.
        ' Any device that dropped its connection will have been removed from _devList by
        ' OnDeviceDisconnected; the next Reconcile pass sees it absent and recreates it.
        ' No separate retry timer is needed.
        While Not stoppingToken.IsCancellationRequested
            Await Task.Delay(15000, stoppingToken)
            If Not stoppingToken.IsCancellationRequested Then
                Reconcile()
            End If
        End While

        _logger.LogInformation("Flashback Engine Service Stopping.")
        Cleanup()
    End Function

    ''' <summary>
    ''' Single method that reconciles the live device list against devices.dat.
    '''
    ''' Rules applied for each config entry:
    '''   Disabled  → disconnect and remove if currently active; skip recreation.
    '''   Enabled + present + critical settings unchanged → update non-critical props in place.
    '''   Enabled + present + critical settings changed   → disconnect; recreate next pass.
    '''   Enabled + absent  → create fresh object and connect (subject to license cap).
    '''   Stale (deleted from config entirely) → disconnect and remove.
    '''
    ''' Called on startup, every 15 seconds (reconnect retry), and on config file changes.
    ''' Protected by _reconcileLock to prevent concurrent execution.
    ''' </summary>
    Private Sub Reconcile()
        If Not Monitor.TryEnter(_reconcileLock) Then Return
        Try
            Dim lines As String()
            SyncLock _configFileLock
                If Not File.Exists(_configFile) Then Return
                _configDate = File.GetLastWriteTime(_configFile)
                lines = File.ReadAllLines(_configFile)
            End SyncLock

            Dim lic = LicenseManager.GetLicenseInfo()
            Dim configNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim loadedCount As Integer

            SyncLock _devList
                loadedCount = _devList.Count
            End SyncLock

            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split("||", StringSplitOptions.TrimEntries)
                If p.Length < 10 Then Continue For

                Dim devName = p(0)
                configNames.Add(devName)

                Dim isEnabled = If(p.Length >= 13, (p(12) = "True"), True)

                Dim existing As Devs = Nothing
                SyncLock _devList
                    existing = _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase))
                End SyncLock

                ' ── Disabled in config: disconnect and remove if currently active ──────────
                If Not isEnabled Then
                    If existing IsNot Nothing Then
                        _logger.LogInformation("{Dev} disabled in config. Disconnecting.", devName)
                        SyncLock _devList
                            _devList.Remove(existing)
                        End SyncLock
                        _registry.Unregister(existing)
                        existing.Disconnect()
                    End If
                    Continue For
                End If

                ' ── Enabled and already active: check for setting changes ─────────────────
                If existing IsNot Nothing Then
                    Dim newConnType = CInt(Val(p(3)))
                    Dim newDevDest = p(4)
                    Dim newOS = CType(CInt(Val(p(5))), OSType)

                    If (existing.DevDest <> newDevDest) OrElse
                       (existing.OS <> newOS) OrElse
                       (existing.ConnType <> newConnType) Then
                        ' Connection-critical settings changed: disconnect now. The device
                        ' will be absent on the next Reconcile pass and recreated with
                        ' the new settings automatically.
                        _logger.LogInformation("Connection settings changed for {Dev}. Disconnecting; will reconnect with new settings on next cycle.", devName)
                        SyncLock _devList
                            _devList.Remove(existing)
                        End SyncLock
                        _registry.Unregister(existing)
                        existing.Disconnect()
                        Continue For
                    End If

                    ' Non-critical settings changed: update the live object in place.
                    existing.DevDescription = p(1)
                    existing.DevType = CInt(Val(p(2)))
                    existing.PDF = (p(7) = "True")
                    existing.Orientation = CInt(Val(p(8)))
                    existing.OutDest = p(9)

                    If p.Length >= 12 Then
                        existing.Shading = CType(CInt(Val(p(10))), RenderPDF.ShadingColor)
                        existing.JobNumber = CInt(Val(p(11)))
                    End If

                    If p.Length >= 14 Then existing.EmailEnabled = (p(13) = "True")
                    If p.Length >= 15 Then existing.EmailRecipients = p(14)
                    If p.Length >= 16 Then existing.SmtpServer = p(15)
                    If p.Length >= 17 Then existing.SmtpPort = CInt(Val(p(16)))
                    If p.Length >= 18 Then existing.SmtpUsername = p(17)
                    If p.Length >= 19 Then existing.SmtpPassword = p(18)
                    If p.Length >= 20 Then existing.SmtpUseTLS = (p(19) = "True")
                    If p.Length >= 21 Then existing.EmailFromAddress = p(20)
                    If p.Length >= 22 Then existing.EmailFromName = p(21)
                    If p.Length >= 23 Then existing.EmailSubject = p(22)
                    If p.Length >= 24 Then existing.EmailBody = p(23)

                    Continue For
                End If

                ' ── Enabled but not active: create and connect (subject to license cap) ───
                If lic.MaxPrinters > 0 AndAlso loadedCount >= lic.MaxPrinters Then Continue For

                Dim d As Devs = Nothing
                Dim shouldConnect As Boolean = False
                SyncLock _devList
                    ' Re-check inside lock to prevent a race where the stat timer and the
                    ' main loop both enter this path milliseconds apart and each create a
                    ' duplicate object for the same printer.
                    If _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase)) Is Nothing Then
                        d = CreateDevice(p)
                        If d IsNot Nothing Then
                            _devList.Add(d)
                            loadedCount = _devList.Count
                            shouldConnect = True
                        End If
                    End If
                End SyncLock

                If shouldConnect AndAlso d IsNot Nothing Then
                    _logger.LogInformation("{Dev} not active. Creating and connecting.", devName)
                    _registry.Register(d)
                    d.Connect()
                End If
            Next

            ' ── Remove devices deleted from devices.dat entirely ─────────────────────────
            Dim stale As New List(Of Devs)
            SyncLock _devList
                stale.AddRange(_devList.Where(Function(d) Not configNames.Contains(d.DevName)))
                For Each d In stale
                    _devList.Remove(d)
                Next
            End SyncLock
            For Each d In stale
                _registry.Unregister(d)
                _logger.LogInformation("{Dev} removed from config. Disconnecting.", d.DevName)
                d.Disconnect()
            Next

        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR in Reconcile: {Error}", ex.Message)
            End If
        Finally
            Monitor.Exit(_reconcileLock)
        End Try
    End Sub

    ''' <summary>
    ''' Called by the Disconnected event on a Devs object.
    ''' Removes the device from _devList and the registry. If the device is still
    ''' enabled (unexpected drop), the next Reconcile() pass will recreate and reconnect it.
    ''' If Enabled=False (manual stop), Reconcile() will see it disabled and skip it.
    ''' </summary>
    Private Sub OnDeviceDisconnected(dev As Devs)
        SyncLock _devList
            _devList.Remove(dev)
        End SyncLock
        _registry.Unregister(dev)
        RemoveHandler dev.Disconnected, AddressOf OnDeviceDisconnected

        If dev.Enabled Then
            _logger.LogInformation("{Dev} disconnected. Will reconnect on next cycle.", dev.DevName)
        Else
            _logger.LogInformation("{Dev} stopped (disabled). Auto-reconnect suppressed.", dev.DevName)
        End If
    End Sub

    ''' <summary>
    ''' Persists runtime-updated settings (job counters etc.) back to devices.dat
    ''' without disturbing entries for devices not currently in _devList.
    ''' Uses a merge strategy: read existing lines, overwrite only the lines
    ''' for live devices, preserve all others — so a device between retry cycles
    ''' is never silently dropped from the config file.
    ''' </summary>
    Private Sub SaveDevices()
        Try
            SyncLock _configFileLock
                If Not File.Exists(_configFile) Then Return

                Dim separator() As String = {"||"}
                Dim lines = File.ReadAllLines(_configFile)

                ' Snapshot live device config lines, keyed by name
                Dim liveLines As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                SyncLock _devList
                    For Each d In _devList
                        liveLines(d.DevName) = d.ToConfigLine()
                    Next
                End SyncLock

                ' Update only the lines belonging to currently live devices
                For i = 0 To lines.Length - 1
                    If String.IsNullOrWhiteSpace(lines(i)) Then Continue For
                    Dim p = lines(i).Split(separator, StringSplitOptions.None)
                    If p.Length < 1 Then Continue For
                    Dim name = p(0)
                    If liveLines.ContainsKey(name) Then
                        lines(i) = liveLines(name)
                    End If
                Next

                File.WriteAllLines(_configFile, lines)
                _configDate = File.GetLastWriteTime(_configFile)
            End SyncLock
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR saving configuration: {Error}", ex.Message)
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Flips a device's Enabled flag to True directly in devices.dat.
    ''' Used when a CONNECT command arrives for a device not currently in _devList
    ''' (it was previously stopped and removed). The next Reconcile() pass will see
    ''' it as enabled and create + connect it immediately.
    ''' </summary>
    Private Sub EnableDeviceInConfig(devName As String)
        Try
            SyncLock _configFileLock
                If Not File.Exists(_configFile) Then Return
                Dim separator() As String = {"||"}
                Dim lines = File.ReadAllLines(_configFile)
                Dim changed = False
                For i = 0 To lines.Length - 1
                    If String.IsNullOrWhiteSpace(lines(i)) Then Continue For
                    Dim p = lines(i).Split(separator, StringSplitOptions.None)
                    If p.Length < 13 Then Continue For
                    If p(0).Equals(devName, StringComparison.OrdinalIgnoreCase) Then
                        p(12) = "True"
                        lines(i) = String.Join("||", p)
                        changed = True
                        Exit For
                    End If
                Next
                If changed Then
                    File.WriteAllLines(_configFile, lines)
                    _configDate = File.GetLastWriteTime(_configFile)
                    _logger.LogInformation("Signal: Re-enabled {Dev} in config. Will reconnect on next cycle.", devName)
                End If
            End SyncLock
        Catch ex As Exception
            _logger.LogError("ERROR enabling device {Dev} in config: {Error}", devName, ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Flips a device's Enabled flag to False directly in devices.dat.
    ''' Used when a DISCONNECT command arrives for a device not currently in _devList
    ''' (it is between retry cycles). Prevents Reconcile() from recreating it.
    ''' </summary>
    Private Sub DisableDeviceInConfig(devName As String)
        Try
            SyncLock _configFileLock
                If Not File.Exists(_configFile) Then Return
                Dim separator() As String = {"||"}
                Dim lines = File.ReadAllLines(_configFile)
                Dim changed = False
                For i = 0 To lines.Length - 1
                    If String.IsNullOrWhiteSpace(lines(i)) Then Continue For
                    Dim p = lines(i).Split(separator, StringSplitOptions.None)
                    If p.Length < 13 Then Continue For
                    If p(0).Equals(devName, StringComparison.OrdinalIgnoreCase) Then
                        p(12) = "False"
                        lines(i) = String.Join("||", p)
                        changed = True
                        Exit For
                    End If
                Next
                If changed Then
                    File.WriteAllLines(_configFile, lines)
                    _configDate = File.GetLastWriteTime(_configFile)
                    _logger.LogInformation("Signal: Disabled {Dev} in config. Auto-reconnect suppressed.", devName)
                End If
            End SyncLock
        Catch ex As Exception
            _logger.LogError("ERROR disabling device {Dev} in config: {Error}", devName, ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Creates and wires up a new Devs object from a config line token array.
    ''' Does NOT call Connect() — the caller is responsible for that.
    ''' </summary>
    Private Function CreateDevice(p As String()) As Devs
        Try
            _logger.LogInformation("Creating device object for {Dev}.", p(0))
            Dim d As New Devs()
            d.DevName = p(0)
            d.DevDescription = p(1)
            d.DevType = CInt(Val(p(2)))
            d.ConnType = CInt(Val(p(3)))
            d.DevDest = p(4)
            d.OS = CType(CInt(Val(p(5))), OSType)
            d.PDF = (p(7) = "True")
            d.Orientation = CInt(Val(p(8)))
            d.OutDest = p(9)

            If p.Length >= 12 Then
                d.Shading = CType(CInt(Val(p(10))), RenderPDF.ShadingColor)
                d.JobNumber = CInt(Val(p(11)))
            End If

            d.Enabled = True

            If p.Length >= 14 Then d.EmailEnabled = (p(13) = "True")
            If p.Length >= 15 Then d.EmailRecipients = p(14)
            If p.Length >= 16 Then d.SmtpServer = p(15)
            If p.Length >= 17 Then d.SmtpPort = CInt(Val(p(16)))
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
            _registry.Unregister(d)
            _logger.LogInformation("Device object destroyed: {Dev}", d.DevName)
            d.Disconnect()
        Next
    End Sub

    ''' <summary>
    ''' Fires every 5 seconds. Triggers Reconcile() when devices.dat has changed,
    ''' applying any edits made by the web admin or config tool immediately.
    ''' </summary>
    Private Sub StatTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _statTimer.Elapsed
        If _timersDisposed Then Return

        Try
            Dim shouldReconcile As Boolean = False
            SyncLock _configFileLock
                If File.Exists(_configFile) Then
                    Dim currentCfgDate = File.GetLastWriteTime(_configFile)
                    If currentCfgDate > _configDate Then
                        shouldReconcile = True
                    End If
                End If
            End SyncLock

            If shouldReconcile Then
                _logger.LogInformation("Configuration file change detected.")
                Reconcile()
            End If
        Catch ex As ObjectDisposedException
            Return
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR monitoring configuration: {Error}", ex.Message)
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Fires every 500 ms. Processes CONNECT and DISCONNECT commands written to
    ''' commands.dat by the web admin panel.
    ''' </summary>
    Private Sub CmdTimer_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles _cmdTimer.Elapsed
        If _timersDisposed Then Return
        If Not File.Exists(_cmdFile) Then Return

        Try
            Dim lines As String()
            Try
                lines = File.ReadAllLines(_cmdFile)
                File.Delete(_cmdFile)
            Catch ex As IOException
                ' WebWorker or admin panel may be actively writing; retry next tick
                Return
            End Try

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

                Select Case cmd
                    Case "CONNECT"
                        _logger.LogInformation("Signal: Manual connect requested for {Dev}", devName)
                        If target IsNot Nothing Then
                            ' Device object is already created and in _devList
                            target.Enabled = True
                            SaveDevices()
                            If target.Connected Then
                                _logger.LogInformation("{Dev} is already connected.", devName)
                            Else
                                _logger.LogInformation("{Dev} connection attempt is already in progress.", devName)
                            End If
                        Else
                            ' Device is not in _devList (was stopped and removed, or between
                            ' retry cycles). Flip Enabled=True in devices.dat then run
                            ' Reconcile immediately so it reconnects without waiting 15 seconds.
                            EnableDeviceInConfig(devName)
                            Reconcile()
                        End If

                    Case "DISCONNECT"
                        _logger.LogInformation("Signal: Manual disconnect requested for {Dev}", devName)
                        If target IsNot Nothing Then
                            ' Disable in memory and persist BEFORE disconnecting so that
                            ' OnDeviceDisconnected sees Enabled=False and logs "stopped",
                            ' and Reconcile() won't recreate it on the next pass.
                            target.Enabled = False
                            SaveDevices()
                            target.Disconnect()
                        Else
                            ' Device is between retry cycles (not in _devList but enabled
                            ' in config). Flip Enabled=False so Reconcile() won't recreate it.
                            DisableDeviceInConfig(devName)
                        End If
                End Select
            Next
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                _logger.LogError("ERROR processing command file: {Error}", ex.Message)
            End If
        End Try
    End Sub
End Class
