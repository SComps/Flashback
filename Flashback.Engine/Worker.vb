Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports System.IO
Imports System.Threading
Imports Flashback.Core

Public Class Worker
    Inherits BackgroundService

    Private ReadOnly _logger As ILogger(Of Worker)
    Private ReadOnly _devList As New List(Of Devs)
    Private _configFile As String = "devices.dat"
    Private _configDate As DateTime
    Private WithEvents _statTimer As New System.Timers.Timer

    Public Sub New(logger As ILogger(Of Worker))
        _logger = logger
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        _logger.LogInformation("Flashback Engine Service Starting.")

        ' Initial load
        LoadDevices()

        _statTimer.Interval = 5000
        _statTimer.Enabled = True

        ' Loop until stopped
        While Not stoppingToken.IsCancellationRequested
            ' Auto-reconnect logic for devices marked as Auto but not connected
            For Each d In _devList
                If d.Auto AndAlso Not d.Connected Then
                    _logger.LogInformation("Auto-connect: Attempting to connect {Dev}...", d.DevName)
                    d.Connect()
                End If
            Next

            Await Task.Delay(5000, stoppingToken)
        End While

        _logger.LogInformation("Flashback Engine Service Stopping.")
        Cleanup()
    End Function

    Private Sub LoadDevices()
        _logger.LogInformation("Reloading devices from {ConfigFile}...", _configFile)
        
        ' Disconnect existing
        Cleanup()

        If Not File.Exists(_configFile) Then Return

        Try
            Using rdr As New StreamReader(_configFile)
                While Not rdr.EndOfStream
                    Dim line = rdr.ReadLine()
                    If String.IsNullOrWhiteSpace(line) Then Continue While
                    Dim p = line.Split("||", StringSplitOptions.TrimEntries)
                    
                    If p.Length >= 10 Then
                        Dim d As New Devs()
                        d.DevName = p(0)
                        d.DevDescription = p(1)
                        d.DevType = Val(p(2))
                        d.ConnType = Val(p(3))
                        d.DevDest = p(4)
                        d.OS = CType(Val(p(5)), OSType)
                        d.Auto = (p(6) = "True")
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

                        ' Hook up the logger
                        AddHandler d.LogMessage, Sub(msg, col) _logger.LogInformation("{Dev}: {Msg}", d.DevName, msg)
                        
                        _devList.Add(d)
                        
                        If d.Auto Then
                            d.Connect()
                        End If
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
            Dim currentCfgDate = File.GetLastWriteTime(_configFile)
            If currentCfgDate > _configDate Then
                _logger.LogInformation("Configuration file change detected.")
                LoadDevices()
            End If
        Catch ex As Exception
        End Try
    End Sub
End Class
