Imports System.IO
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
#If WINDOWS Then
Imports Makaretu.Dns
#End If

Public Class Devs
    Public Event LogMessage(message As String, color As ConsoleColor)
    Public Event JobNumberChanged(sender As Devs)
    Public Event Disconnected(sender As Devs)

    Public Property DevName As String = "Printer"
    Public Property DevDescription As String = ""
    Public Property DevType As Integer = 0
    Public Property ConnType As Integer = 0
    Public Property DevDest As String = "127.0.0.1:9000"
    Public Property OS As OSType = OSType.OS_MVS38J
    Public Property PDF As Boolean = True
    Public Property Orientation As Integer = 0
    Public Property OutDest As String = "Output"
    Public Property Shading As RenderPDF.ShadingColor = RenderPDF.ShadingColor.Green
    Public Property JobNumber As Integer = 0
    Public Property Enabled As Boolean = True
    Public Property Logger As Microsoft.Extensions.Logging.ILogger

    ' Email Configuration Properties
    Public Property EmailEnabled As Boolean = False
    Public Property EmailRecipients As String = ""
    Public Property SmtpServer As String = ""
    Public Property SmtpPort As Integer = 587
    Public Property SmtpUsername As String = ""
    Public Property SmtpPassword As String = ""
    Public Property SmtpUseTLS As Boolean = True
    Public Property EmailFromAddress As String = "flashback@localhost"
    Public Property EmailFromName As String = "Flashback Print Server"
    Public Property EmailSubject As String = "Print Job: {JobName} from {DeviceName}"
    Public Property EmailBody As String = "Print job '{JobName}' from device '{DeviceName}' has been completed." & vbCrLf & vbCrLf &
                                          "User: {UserName}" & vbCrLf &
                                          "Pages: {PageCount}" & vbCrLf &
                                          "Date/Time: {DateTime}" & vbCrLf & vbCrLf &
                                          "The PDF output is attached to this email."

    Private remoteHost As String
    Private remotePort As Integer
    Private socket As Socket
    Private listener As TcpListener
    Private clientStream As NetworkStream
#If WINDOWS Then
    Private serviceDiscovery As ServiceDiscovery
#End If
    Private _cancellationTokenSource As CancellationTokenSource
    Private IsConnected As Boolean = False
    Private IsConnecting As Boolean = False
    Private _receivingFlag As Integer = 0  ' 0 = False, 1 = True (for thread-safe access)

    Private ReadOnly _connectionLock As New Object()

    Public ReadOnly Property Connected As Boolean
        Get
            SyncLock _connectionLock
                Return IsConnected
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property Connecting As Boolean
        Get
            SyncLock _connectionLock
                Return IsConnecting
            End SyncLock
        End Get
    End Property

    Private Sub Log(msg As String, Optional col As ConsoleColor = ConsoleColor.White)
        RaiseEvent LogMessage(msg, col)
    End Sub

    Private Sub SplitDestination(dest As String)
        Try
            Dim splitDev As String() = dest.Split(":"c)
            If splitDev.Length = 1 Then
                remoteHost = "127.0.0.1"
                remotePort = Val(splitDev(0))
                Return
            End If
            remoteHost = splitDev(0).Trim()
            remotePort = Val(splitDev(1))
        Catch ex As Exception
            Log($"[{DevName}] {ex.Message}", ConsoleColor.Red)
        End Try
    End Sub

    Public Async Sub Connect()
        SyncLock _connectionLock
            If IsConnected OrElse IsConnecting Then
                Log($"[{DevName}] Connect() skipped - already connected or connecting.", ConsoleColor.DarkYellow)
                Return
            End If
            IsConnecting = True
        End SyncLock

        Log($"[{DevName}] Connecting to {DevDest}...", ConsoleColor.Cyan)

        Try
            SplitDestination(DevDest)
            If remotePort > 0 Then
                Await StartAsync()
            Else
                Log($"[{DevName}] Connect skipped: invalid port in destination '{DevDest}'.", ConsoleColor.DarkYellow)
            End If
        Catch ex As Exception
            Log($"[{DevName}] Connection failed: {ex.Message}", ConsoleColor.Yellow)
        Finally
            SyncLock _connectionLock
                IsConnecting = False
            End SyncLock
        End Try
    End Sub

    Public Async Function StartAsync() As Task
        ' Dispose existing CancellationTokenSource if present
        If _cancellationTokenSource IsNot Nothing Then
            Try
                _cancellationTokenSource.Cancel()
                _cancellationTokenSource.Dispose()
            Catch ex As Exception
                Log($"[{DevName}] Error disposing existing CancellationTokenSource: {ex.Message}", ConsoleColor.DarkYellow)
            End Try
        End If

        _cancellationTokenSource = New CancellationTokenSource()

        Try
            If ConnType = 3 Then
                ' Port 9100 listener mode - passively accept incoming connections
                Log($"[{DevName}] Starting Port 9100 Listener on port {remotePort}...", ConsoleColor.Yellow)
                listener = New TcpListener(System.Net.IPAddress.Any, remotePort)
                listener.Start()

#If WINDOWS Then
                Try
                    serviceDiscovery = New ServiceDiscovery()
                    Dim profile = New ServiceProfile("Flashback Printer", "_pdl-datastream._tcp", CUShort(remotePort))
                    serviceDiscovery.Advertise(profile)
                    Log($"[{DevName}] Network discovery active: 'Flashback Printer' (_pdl-datastream._tcp)", ConsoleColor.Gray)
                Catch ex As Exception
                    Log($"[{DevName}] Warning: Could not start network discovery: {ex.Message}", ConsoleColor.DarkGray)
                End Try
#End If

                SyncLock _connectionLock
                    IsConnected = True
                End SyncLock

                Using registration = _cancellationTokenSource.Token.Register(Sub() listener.Stop())
                    While Not _cancellationTokenSource.IsCancellationRequested
                        Try
                            Dim incomingSocket = Await listener.AcceptSocketAsync()
                            Log($"[{DevName}] Accepted connection from {incomingSocket.RemoteEndPoint}", ConsoleColor.Green)

                            Try
                                incomingSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, True)
                                incomingSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60)
                                incomingSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10)
                                incomingSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3)
                            Catch ex As Exception
                                Log($"[{DevName}] Warning: Could not configure TCP keep-alive on accepted socket: {ex.Message}", ConsoleColor.DarkYellow)
                            End Try

                            OutDest = OutDest.Replace("\"c, Path.DirectorySeparatorChar).Replace("/"c, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                            Try
                                If Not Directory.Exists(OutDest) Then
                                    Log($"[{DevName}] Created output directory {OutDest}", ConsoleColor.Cyan)
                                    Directory.CreateDirectory(OutDest)
                                End If
                            Catch ex As Exception
                                If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                                    Log($"[{DevName}] ERROR creating directory: {ex.Message}", ConsoleColor.Red)
                                End If
                            End Try

                            clientStream = New NetworkStream(incomingSocket, True)
                            Await ReceiveDataAsync(_cancellationTokenSource.Token)

                            Try
                                clientStream?.Close()
                                incomingSocket?.Close()
                            Catch ex As Exception
                                Log($"[{DevName}] {ex.Message}", ConsoleColor.Red)
                            End Try
                            Log($"[{DevName}] Session ended. Listening for next job.", ConsoleColor.Gray)
                        Catch ex As ObjectDisposedException
                            Exit While ' Listener was stopped
                        Catch ex As Exception
                            If Not _cancellationTokenSource.IsCancellationRequested Then
                                Log($"[{DevName}] Listener error: {ex.Message}", ConsoleColor.Red)
                            End If
                        End Try
                    End While
                End Using

            Else
                ' Client mode - connect to remote system
                Log($"[{DevName}] Creating socket for connection attempt.", ConsoleColor.Cyan)
                socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

                Try
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, True)
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 120)
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10)
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3)
                    Log($"[{DevName}] TCP keep-alive configured successfully.", ConsoleColor.Gray)
                Catch ex As Exception
                    Log($"[{DevName}] Warning: Could not configure TCP keep-alive: {ex.Message}", ConsoleColor.DarkYellow)
                End Try

                Log($"[{DevName}] Attempting to connect to {remoteHost}:{remotePort} (5s timeout)...", ConsoleColor.Yellow)
                Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(5))
                    Try
                        Await socket.ConnectAsync(remoteHost, remotePort, cts.Token)
                    Catch ex As OperationCanceledException
                        Throw New TimeoutException($"Connection to {remoteHost}:{remotePort} timed out after 5 seconds")
                    End Try
                End Using

                ' Only set IsConnected = True here - after a real successful TCP connect
                SyncLock _connectionLock
                    IsConnected = True
                End SyncLock
                Log($"[{DevName}] Connected to {remoteHost}:{remotePort}.", ConsoleColor.Green)

                OutDest = OutDest.Replace("\"c, Path.DirectorySeparatorChar).Replace("/"c, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                Try
                    If Not Directory.Exists(OutDest) Then
                        Log($"[{DevName}] Created output directory {OutDest}", ConsoleColor.Cyan)
                        Directory.CreateDirectory(OutDest)
                    End If
                Catch ex As Exception
                    If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                        Log($"[{DevName}] ERROR creating directory: {ex.Message}", ConsoleColor.Red)
                    End If
                End Try

                clientStream = New NetworkStream(socket, True)
                Await ReceiveDataAsync(_cancellationTokenSource.Token)
            End If

        Finally
            ' Always clean up resources when StartAsync exits for any reason
            Try
                If clientStream IsNot Nothing Then
                    clientStream.Close()
                    clientStream.Dispose()
                    clientStream = Nothing
                End If
            Catch
                clientStream = Nothing
            End Try

            If ConnType <> 3 Then
                Try
                    If socket IsNot Nothing Then
                        socket.Close()
                        socket.Dispose()
                        socket = Nothing
                    End If
                Catch
                    socket = Nothing
                End Try
            End If

            SyncLock _connectionLock
                IsConnected = False
            End SyncLock

            Log($"[{DevName}] Disconnected.", ConsoleColor.Cyan)

            ' Notify the Worker so it can remove and recreate this device
            RaiseEvent Disconnected(Me)
        End Try
    End Function

    Private Async Function ReceiveDataAsync(cancellationToken As CancellationToken) As Task
        Dim buffer(8192) As Byte
        Dim dataBuilder As New StringBuilder()
        Dim lastReceivedTime As DateTime = DateTime.Now
        Dim inactivityTimeout As TimeSpan = TimeSpan.FromSeconds(1)

        Try
            While Not cancellationToken.IsCancellationRequested
                If Not clientStream.DataAvailable Then
                    Await Task.Delay(100, cancellationToken)

                    ' Port 9100 mode: check if the accepted socket closed
                    If ConnType = 3 Then
                        Try
                            If socket IsNot Nothing AndAlso socket.Poll(0, SelectMode.SelectRead) AndAlso socket.Available = 0 Then
                                If dataBuilder.Length > 0 Then
                                    ProcessDocumentData(dataBuilder.ToString())
                                    dataBuilder.Clear()
                                End If
                                Exit While
                            End If
                        Catch ex As Exception
                            If dataBuilder.Length > 0 Then
                                ProcessDocumentData(dataBuilder.ToString())
                                dataBuilder.Clear()
                            End If
                            Log($"[{DevName}] {ex.Message}", ConsoleColor.Gray)
                            Exit While
                        End Try
                    End If

                    If (DateTime.Now - lastReceivedTime) > inactivityTimeout AndAlso dataBuilder.Length > 0 Then
                        ProcessDocumentData(dataBuilder.ToString())
                        dataBuilder.Clear()
                        lastReceivedTime = DateTime.Now
                    End If
                Else
                    If Interlocked.CompareExchange(_receivingFlag, 1, 0) = 0 Then
                        If ConnType = 3 Then
                            Log($"[{DevName}] receiving raw data from stream.", ConsoleColor.Yellow)
                        ElseIf OS <> OSType.OS_RSTS AndAlso OS <> OSType.OS_NOS278 Then
                            Log($"[{DevName}] receiving data from remote host.", ConsoleColor.Yellow)
                        Else
                            Log($"[{DevName}] receiving data from low speed device. Sit back and relax.", ConsoleColor.Yellow)
                        End If
                    End If
                    Dim recd As Integer = Await clientStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    If recd = 0 Then
                        Log($"[{DevName}] Remote closed connection (EOF).", ConsoleColor.DarkYellow)
                        If dataBuilder.Length > 0 Then
                            ProcessDocumentData(dataBuilder.ToString())
                            dataBuilder.Clear()
                        End If
                        Exit While
                    End If
                    dataBuilder.Append(Encoding.UTF8.GetString(buffer, 0, recd))
                    lastReceivedTime = DateTime.Now
                End If
            End While
        Catch ex As OperationCanceledException
            Log($"[{DevName}] ReceiveDataAsync: Session canceled.", ConsoleColor.Gray)
        Catch ex As IO.IOException
            Log($"[{DevName}] ReceiveDataAsync: IO Error: {ex.Message}", ConsoleColor.Red)
        Catch ex As Exception
            Log($"[{DevName}] ReceiveDataAsync: Error: {ex.Message}", ConsoleColor.Red)
        Finally
            Log($"[{DevName}] ReceiveDataAsync finished.", ConsoleColor.Cyan)
        End Try
    End Function

    Private Sub ProcessDocumentData(documentData As String)
        JobNumber += 1
        RaiseEvent JobNumberChanged(Me)
        Dim lines As New List(Of String)()
        Dim currentLine As New StringBuilder()
        Dim ignoreChars As Integer = 0

        For Each c As Char In documentData
            If ignoreChars = 0 Then
                Select Case c
                    Case vbCr
                        If OS = OSType.OS_VM370 OrElse OS = OSType.OS_MVS38J OrElse OS = OSType.OS_MPE OrElse OS = OSType.OS_ZVM73 Then
                            currentLine.Append(c)
                        ElseIf OS = OSType.OS_TANDYXENIX Then
                            currentLine.Append(vbCrLf)
                            lines.Add(currentLine.ToString())
                            currentLine.Clear()
                        End If
                    Case vbLf
                        If currentLine.Length > 0 Then
                            lines.Add(currentLine.ToString())
                            currentLine.Clear()
                        Else
                            lines.Add(" ")
                            currentLine.Clear()
                        End If
                    Case vbFormFeed
                        lines.Add(currentLine.ToString())
                        lines.Add(c.ToString())
                        currentLine.Clear()
                    Case ChrW(27)
                        ignoreChars = 2
                    Case Else
                        currentLine.Append(c)
                End Select
            Else
                ignoreChars -= 1
            End If
        Next

        If currentLine.Length > 0 Then
            lines.Add(currentLine.ToString().Replace(vbCrLf, vbLf))
        End If

        If lines.Count >= 2 AndAlso lines(lines.Count - 2) = vbFormFeed AndAlso lines(lines.Count - 1) = vbCr Then
            Log($"[{DevName}] Removing extra control characters after job completion for MPE")
            lines.RemoveAt(lines.Count - 1)
            lines.RemoveAt(lines.Count - 1)
        End If

        If lines.Count > 0 AndAlso lines(lines.Count - 1) = vbFormFeed Then
            lines.RemoveAt(lines.Count - 1)
        End If

        If ConnType = 3 OrElse lines.Count > 9 Then
            Dim docCopy = New List(Of String)(lines)
            Task.Run(Sub() ProcessDocument(docCopy))
            Log($"[{DevName}] Waiting for next block/session.")
            Interlocked.Exchange(_receivingFlag, 0)
        Else
            Interlocked.Exchange(_receivingFlag, 0)
        End If
    End Sub

    Public Sub Disconnect()
        Log($"[{DevName}] Disconnect() requested.", ConsoleColor.Cyan)

        Try
            If _cancellationTokenSource IsNot Nothing Then
                Try
                    _cancellationTokenSource.Cancel()
                    _cancellationTokenSource.Dispose()
                Catch ex As Exception
                    Log($"[{DevName}] Error disposing CancellationTokenSource: {ex.Message}", ConsoleColor.DarkYellow)
                End Try
                _cancellationTokenSource = Nothing
            End If

            Try
                clientStream?.Close()
                clientStream?.Dispose()
                clientStream = Nothing
            Catch ex As Exception
                Log($"[{DevName}] Error closing client stream: {ex.Message}", ConsoleColor.DarkYellow)
            End Try

            Try
                socket?.Close()
                socket?.Dispose()
                socket = Nothing
            Catch ex As Exception
                Log($"[{DevName}] Error closing socket: {ex.Message}", ConsoleColor.DarkYellow)
            End Try

            Try
                listener?.Stop()
                listener = Nothing
            Catch ex As Exception
                Log($"[{DevName}] Error stopping listener: {ex.Message}", ConsoleColor.DarkYellow)
            End Try

#If WINDOWS Then
            If serviceDiscovery IsNot Nothing Then
                Try
                    serviceDiscovery.Unadvertise()
                    serviceDiscovery.Dispose()
                    serviceDiscovery = Nothing
                Catch ex As Exception
                    Log($"[{DevName}] Error disposing service discovery: {ex.Message}", ConsoleColor.DarkYellow)
                End Try
            End If
#End If
        Catch ex As Exception
            Log($"[{DevName}] Error during disconnection: {ex.Message}", ConsoleColor.Red)
        Finally
            SyncLock _connectionLock
                IsConnected = False
                IsConnecting = False
            End SyncLock
            Log($"[{DevName}] Disconnect() completed.", ConsoleColor.Cyan)
        End Try
    End Sub

    Private Sub ProcessDocument(doc As List(Of String))
        OutDest = OutDest.Replace("\"c, Path.DirectorySeparatorChar).Replace("/"c, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
        Try
            If Not Directory.Exists(OutDest) Then
                Log($"[{DevName}] Created output directory {OutDest}", ConsoleColor.Yellow)
                Directory.CreateDirectory(OutDest)
            End If

            Dim dataDir = Path.Combine(OutDest, "data")
            If Directory.Exists(dataDir) Then
                Try
                    Directory.Delete(dataDir, True)
                    Log($"[{DevName}] Removed legacy data directory {dataDir}", ConsoleColor.Gray)
                Catch ex As Exception
                    Log($"[{DevName}] Could not remove legacy data directory: {ex.Message}", ConsoleColor.DarkYellow)
                End Try
            End If
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                Log($"[{DevName}] ERROR creating directories: {ex.Message}", ConsoleColor.Red)
            End If
        End Try

        Interlocked.Exchange(_receivingFlag, 0)
        Log($"[{DevName}] received {doc.Count} lines.", ConsoleColor.Cyan)

        Dim JobID As String
        Dim JobName As String
        Dim UserID As String

        If ConnType = 3 Then
            JobID = DateTime.Now.ToString("HHmmss")
            JobName = "RAW_JOB"
            UserID = "GUEST"
            Log($"[{DevName}] RAW MODE: Bypassing OS profile extraction.", ConsoleColor.Gray)
        Else
            If doc.Count > 10 Then
                If OS <> OSType.OS_RSTS AndAlso OS > OSType.OS_MVS38J AndAlso OS <> OSType.OS_ZOS AndAlso OS <> OSType.OS_TANDYXENIX Then
                    Log($"[{DevName}] Examining document information.")
                    Dim idx As Integer = 0
                    While idx < doc.Count
                        doc(idx) = doc(idx).Trim()
                        If doc(idx) = vbFormFeed Then
                            doc(idx) = " " & vbLf
                            Exit While
                        End If
                        If doc(idx).Trim() = "" Then doc(idx) = " "
                        If doc(idx).Trim() <> "" Then Exit While
                        idx += 1
                    End While
                End If

                Dim profile = OsProfileFactory.GetProfile(OS)
                If profile Is Nothing Then
                    Log($"[{DevName}] ERROR: Could not resolve OS Profile for {OS}", ConsoleColor.Red)
                    Return
                End If

                Dim jobInfo As JobInformation = profile.ExtractJobInformation(doc, DevName)
                JobID = jobInfo.JobID
                JobName = SecurityUtils.SanitizeFilename(jobInfo.JobName)
                UserID = SecurityUtils.SanitizeFilename(jobInfo.User)
            Else
                Log(String.Format("[{1}] Ignoring document with {0} lines as line garbage or banners.", doc.Count, DevName))
                Return
            End If
        End If

        Dim userDir = Path.Combine(OutDest, UserID)
        Try
            If Not Directory.Exists(userDir) Then
                Log($"[{DevName}] creating user directory {userDir}", ConsoleColor.Yellow)
                Directory.CreateDirectory(userDir)
            End If
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                Log($"[{DevName}] ERROR creating user directory: {ex.Message}", ConsoleColor.Red)
            End If
            Return
        End Try

        Dim safeDevName = SecurityUtils.SanitizeFilename(DevName)
        Dim pdfName As String = Path.Combine(userDir, $"{safeDevName}-{UserID}-{JobID}-{JobName}_{JobNumber}.pdf")

        Dim renderer As New RenderPDF()
        renderer.Logger = Logger
        renderer.DevName = DevName
        renderer.OS = If(ConnType = 3, OSType.OS_GENERIC, OS)
        renderer.Orientation = Orientation
        renderer.TargetFileName = pdfName
        renderer.Shading = Shading

        Dim pdfPath = renderer.CreatePDF(JobName, doc)

        If EmailEnabled AndAlso Not String.IsNullOrWhiteSpace(pdfPath) AndAlso System.IO.File.Exists(pdfPath) Then
            Task.Run(Async Function()
                Try
                    Dim emailConfig As New EmailConfig()
                    emailConfig.Enabled = EmailEnabled
                    emailConfig.SetRecipientsFromString(EmailRecipients)
                    emailConfig.SmtpServer = SmtpServer
                    emailConfig.SmtpPort = SmtpPort
                    emailConfig.SmtpUsername = SmtpUsername
                    emailConfig.SmtpPassword = SmtpPassword
                    emailConfig.UseTLS = SmtpUseTLS
                    emailConfig.FromAddress = EmailFromAddress
                    emailConfig.FromName = EmailFromName
                    emailConfig.Subject = EmailSubject
                    emailConfig.Body = EmailBody

                    Dim pageCount As Integer = 0
                    Try
                        Using pdfDoc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)
                            pageCount = pdfDoc.PageCount
                        End Using
                    Catch
                    End Try

                    Dim emailService As New EmailService(Logger)
                    Dim success = Await emailService.SendPdfEmailAsync(emailConfig, pdfPath, JobName, DevName, UserID, pageCount)

                    If success Then
                        Log($"[{DevName}] Email sent successfully for job {JobName}", ConsoleColor.Green)
                    Else
                        Log($"[{DevName}] Failed to send email for job {JobName}", ConsoleColor.Red)
                    End If
                Catch ex As Exception
                    Log($"[{DevName}] Email error: {ex.Message}", ConsoleColor.Red)
                End Try
            End Function)
        End If
    End Sub

    Public Function ToConfigLine() As String
        Return $"{DevName}||{DevDescription}||{DevType}||{ConnType}||{DevDest}||{CInt(OS)}||False||{PDF}||{Orientation}||{OutDest}||{CInt(Shading)}||{JobNumber}||{Enabled}||{EmailEnabled}||{EmailRecipients}||{SmtpServer}||{SmtpPort}||{SmtpUsername}||{SmtpPassword}||{SmtpUseTLS}||{EmailFromAddress}||{EmailFromName}||{EmailSubject}||{EmailBody}"
    End Function
End Class
