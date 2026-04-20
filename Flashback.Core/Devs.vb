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
    Public Property Logger As Microsoft.Extensions.Logging.ILogger

    Private remoteHost As String
    Private remotePort As Integer
    Private client As TcpClient
    Private listener As TcpListener
    Private clientStream As NetworkStream
#If WINDOWS Then
    Private serviceDiscovery As ServiceDiscovery
#End If
    Private _cancellationTokenSource As CancellationTokenSource
    Private currentDocument As New List(Of String)()
    Private IsConnected As Boolean = False
    Private IsConnecting As Boolean = False
    Private Receiving As Boolean = False

    Public ReadOnly Property Connected As Boolean
        Get
            Try
                ' Live socket check: Is the TCP client active and responsive?
                If client IsNot Nothing AndAlso client.Client IsNot Nothing Then
                    Return client.Connected AndAlso Not (client.Client.Poll(0, SelectMode.SelectRead) AndAlso client.Available = 0)
                End If
            Catch
            End Try
            Return IsConnected
        End Get
    End Property

    Public ReadOnly Property Connecting As Boolean
        Get
            Return IsConnecting
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
        If IsConnected OrElse IsConnecting Then Exit Sub
        IsConnecting = True
        Try
            SplitDestination(DevDest)
            If remotePort > 0 Then
                Await StartAsync()
            End If
        Finally
            IsConnecting = False
        End Try
    End Sub

    Public Async Function StartAsync() As Task
        _cancellationTokenSource = New CancellationTokenSource()

        Try
            If ConnType = 3 Then
                Log($"[{DevName}] Starting Port 9100 Listener on port {remotePort}...", ConsoleColor.Yellow)
                listener = New TcpListener(System.Net.IPAddress.Any, remotePort)
                listener.Start()
                
#If WINDOWS Then
                ' Advertise the service on the network as "Flashback Printer"
                Try
                    serviceDiscovery = New ServiceDiscovery()
                    Dim profile = New ServiceProfile("Flashback Printer", "_pdl-datastream._tcp", CUShort(remotePort))
                    serviceDiscovery.Advertise(profile)
                    Log($"[{DevName}] Network discovery active: 'Flashback Printer' (_pdl-datastream._tcp)", ConsoleColor.Gray)
                Catch ex As Exception
                    Log($"[{DevName}] Warning: Could not start network discovery: {ex.Message}", ConsoleColor.DarkGray)
                End Try
#End If

                ' Wait for incoming connections continuously
                IsConnected = True
                Using registration = _cancellationTokenSource.Token.Register(Sub() listener.Stop())
                    While Not _cancellationTokenSource.IsCancellationRequested
                        Try
                            client = Await listener.AcceptTcpClientAsync()
                            Log($"[{DevName}] Accepted connection from {client.Client.RemoteEndPoint}", ConsoleColor.Green)
                            
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
                            
                            clientStream = client.GetStream()
                            Await ReceiveDataAsync(_cancellationTokenSource.Token)
                            
                            Try
                                clientStream?.Close()
                                client?.Close()
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
                client = New TcpClient()
                Log($"[{DevName}] Attempting to connect to {remoteHost}:{remotePort}...", ConsoleColor.Yellow)
                Await client.ConnectAsync(remoteHost, remotePort)
                IsConnected = True
                Log($"[{DevName}] Connection successful.", ConsoleColor.Green)
                
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
                
                clientStream = client.GetStream()
                Await ReceiveDataAsync(_cancellationTokenSource.Token)
            End If
            
        Catch ex As Exception
            Log($"[{DevName}] {ex.Message}", ConsoleColor.Red)
            IsConnected = False
        Finally
            Try
                Disconnect()
            Catch disconnectEx As Exception
                Log($"[{DevName}] {disconnectEx.Message}", ConsoleColor.Red)
            End Try
            IsConnected = False
            
            If ConnType = 3 Then
                 Log($"[{DevName}] Port 9100 Listener stopped.", ConsoleColor.Gray)
                 listener?.Stop()
                 listener = Nothing
            End If
        End Try
    End Function

    Private Async Function ReceiveDataAsync(cancellationToken As CancellationToken) As Task
        Dim buffer(8192) As Byte
        Dim dataBuilder As New StringBuilder()
        Dim lastReceivedTime As DateTime = DateTime.Now
        Dim inactivityTimeout As TimeSpan = TimeSpan.FromSeconds(5)

        Try
            While Not cancellationToken.IsCancellationRequested
                If Not clientStream.DataAvailable Then
                    Await Task.Delay(100, cancellationToken)
                    
                    ' Keep-alive / check for disconnected
                    Try
                        If ConnType <> 3 Then 
                            clientStream.WriteByte(0)
                        Else
                            ' Silent connection check for JetDirect
                            If client.Client.Poll(0, SelectMode.SelectRead) AndAlso client.Available = 0 Then
                                If dataBuilder.Length > 0 Then
                                    ProcessDocumentData(dataBuilder.ToString())
                                    dataBuilder.Clear()
                                End If
                                Exit While
                            End If
                        End If
                    Catch ex As Exception
                        ' Connection closed by peer - this is often how Port 9100 jobs end
                        If dataBuilder.Length > 0 Then
                            ProcessDocumentData(dataBuilder.ToString())
                            dataBuilder.Clear()
                        End If
                        Log($"[{DevName}] {ex.Message}", ConsoleColor.Gray)
                        Exit While
                    End Try

                    If (DateTime.Now - lastReceivedTime) > inactivityTimeout AndAlso dataBuilder.Length > 0 Then
                        ProcessDocumentData(dataBuilder.ToString())
                        dataBuilder.Clear()
                        lastReceivedTime = DateTime.Now
                    End If
                Else
                    If Not Receiving Then
                        Receiving = True
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
                        ' EOF reached
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
            Log($"[{DevName}] {ex.Message}", ConsoleColor.Gray)
        Catch ex As IO.IOException
            Log($"[{DevName}] {ex.Message}", ConsoleColor.Red)
        Catch ex As Exception
            Log($"[{DevName}] {ex.Message}", ConsoleColor.Red)
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
                        If OS = OSType.OS_VM370 OrElse OS = OSType.OS_MVS38J OrElse OS = OSType.OS_MPE Then
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

        ' If it's a Raw connection, we process even small documents and don't care about line count minima
        If ConnType = 3 OrElse lines.Count > 9 Then
            currentDocument.AddRange(lines)
            Dim docCopy As New List(Of String)(currentDocument)
            Task.Run(Sub() ProcessDocument(docCopy))
            Log($"[{DevName}] Waiting for next block/session.")
            currentDocument.Clear()
            Receiving = False
        Else
            Receiving = False
        End If
    End Sub

    Public Sub Disconnect()
        Try
            _cancellationTokenSource?.Cancel()
            clientStream?.Close()
            client?.Close()
            listener?.Stop()
#If WINDOWS Then
            If serviceDiscovery IsNot Nothing Then
                serviceDiscovery.Unadvertise()
                serviceDiscovery.Dispose()
                serviceDiscovery = Nothing
            End If
#End If
        Catch ex As Exception
            Log($"[{DevName}] Error during disconnection: {ex.Message}", ConsoleColor.Red)
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
            If Not Directory.Exists(dataDir) Then
                Log($"[{DevName}] Created data directory {dataDir}", ConsoleColor.Yellow)
                Directory.CreateDirectory(dataDir)
            End If
        Catch ex As Exception
            If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
                Log($"[{DevName}] ERROR creating directories: {ex.Message}", ConsoleColor.Red)
            End If
        End Try

        Receiving = False
        Log($"[{DevName}] received {doc.Count} lines.", ConsoleColor.Cyan)

        ' Logic for Raw / Listener mode
        Dim JobID As String
        Dim JobName As String
        Dim UserID As String

        If ConnType = 3 Then
            ' Bypass OS Profile extraction
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

        Dim pdfName As String = Path.Combine(userDir, $"{DevName}-{UserID}-{JobID}-{JobName}_{JobNumber}.pdf")

        Dim renderer As New RenderPDF()
        renderer.Logger = Logger
        renderer.DevName = DevName
        renderer.OS = If(ConnType = 3, OSType.OS_GENERIC, OS) ' Use Generic profile for raw/listener jobs
        renderer.Orientation = Orientation
        renderer.TargetFileName = pdfName
        renderer.Shading = Shading

        renderer.CreatePDF(JobName, doc)
    End Sub
    Public Function ToConfigLine() As String
        Return $"{DevName}||{DevDescription}||{DevType}||{ConnType}||{DevDest}||{CInt(OS)}||False||{PDF}||{Orientation}||{OutDest}||{CInt(Shading)}||{JobNumber}"
    End Function
End Class

