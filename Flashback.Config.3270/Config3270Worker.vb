Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports System.IO
Imports System.Threading
Imports TN3270Framework
Imports Flashback.Core

Public Class Config3270Worker
    Inherits BackgroundService

    Private ReadOnly _logger As ILogger(Of Config3270Worker)
    Private ReadOnly _devList As New List(Of Devs)
    Private _configFile As String
    Private ReadOnly _port As Integer
    Private _server As TN3270Listener
    Private ReadOnly _syspw As String = ""

    Public Sub New(logger As ILogger(Of Config3270Worker), settings As Config3270Settings)
        _logger = logger
        _syspw = If(settings.Password, "")
        _port = settings.Port
        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
        _configFile = Path.Combine(baseDir, "devices.dat")
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        _logger.LogInformation("Flashback 3270 Configuration Server Starting on port {Port}.", _port)

        LoadDevices()

        _server = New TN3270Listener(_port)
        AddHandler _server.ConnectionReceived, AddressOf OnConnection
        _server.Start()

        ' Keep running until cancellation
        While Not stoppingToken.IsCancellationRequested
            Await Task.Delay(1000, stoppingToken)
        End While

        _logger.LogInformation("Flashback 3270 Configuration Server Stopping.")
        _server.StopListening()
    End Function

    Private Sub OnConnection(sender As Object, e As TN3270ConnectionEventArgs)
        _logger.LogInformation("[ConfigServer] New connection from {RemoteEndPoint}", e.RemoteEndPoint)
        Dim session = e.Session
        ' Re-load devices before starting session to ensure consistency
        LoadDevices()
        Dim stateManager As New SessionStateManager(session, _devList, _configFile, _syspw)
        AddHandler session.NegotiationComplete, AddressOf stateManager.InitSession
        AddHandler session.AidKeyReceived, AddressOf stateManager.HandleInput
        AddHandler session.Disconnected, Sub() _logger.LogInformation("[ConfigServer] Session {RemoteEndPoint} disconnected.", e.RemoteEndPoint)
        session.StartNegotiation()
    End Sub

    Private Sub LoadDevices()
        If Not File.Exists(_configFile) Then Return
        _devList.Clear()
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
                        
                        _devList.Add(d)
                    End If
                End While
            End Using
        Catch ex As Exception
            _logger.LogError("Error loading devices: {Error}", ex.Message)
        End Try
    End Sub
End Class
