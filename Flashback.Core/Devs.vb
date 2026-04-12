Imports System.IO
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading

Public Class Devs
    Public Event LogMessage(message As String, color As ConsoleColor)

    Public Property DevName As String = "Printer"
    Public Property DevDescription As String = ""
    Public Property DevType As Integer = 0
    Public Property ConnType As Integer = 0
    Public Property DevDest As String = "127.0.0.1:9000"
    Public Property OS As OSType = OSType.OS_MVS38J
    Public Property Auto As Boolean = False
    Public Property PDF As Boolean = True
    Public Property Orientation As Integer = 0
    Public Property OutDest As String = "Output"
    Public Property Shading As RenderPDF.ShadingColor = RenderPDF.ShadingColor.Green
    Public Property JobNumber As Integer = 0

    Private remoteHost As String
    Private remotePort As Integer
    Private client As TcpClient
    Private clientStream As NetworkStream
    Private _cancellationTokenSource As CancellationTokenSource
    Private currentDocument As New List(Of String)()
    Private IsConnected As Boolean = False
    Private Receiving As Boolean = False

    Public ReadOnly Property Connected As Boolean
        Get
            Return IsConnected
        End Get
    End Property

    Private Sub Log(msg As String, Optional col As ConsoleColor = ConsoleColor.White)
        RaiseEvent LogMessage(msg, col)
    End Sub

    Private Sub SplitDestination(dest As String)
        Dim splitDev As String() = dest.Split(":"c)
        If splitDev.Length = 1 Then
            Throw New Exception($"Error: malformed destination {dest}")
        End If
        remoteHost = splitDev(0).Trim()
        remotePort = Val(splitDev(1))
        If String.IsNullOrWhiteSpace(remoteHost) Then Throw New Exception("Destination does not contain a valid hostname")
        If remotePort = 0 Then Throw New Exception("Destination does not contain a valid port.")
    End Sub

    Public Async Sub Connect()
        SplitDestination(DevDest)
        Await StartAsync()
    End Sub

    Public Async Function StartAsync() As Task
        client = New TcpClient()
        _cancellationTokenSource = New CancellationTokenSource()

        Try
            Log($"[{DevName}] Attempting to connect...", ConsoleColor.Yellow)
            Await client.ConnectAsync(remoteHost, remotePort)
            Log($"[{DevName}] Connection successful.", ConsoleColor.Green)
            
            OutDest = OutDest.TrimEnd("/"c, "\"c)
            If Not Directory.Exists(OutDest) Then
                Log($"[{DevName}] Created output directory {OutDest}", ConsoleColor.Cyan)
                Directory.CreateDirectory(OutDest)
            End If
            
            clientStream = client.GetStream()
            IsConnected = True
            Await ReceiveDataAsync(_cancellationTokenSource.Token)
        Catch ex As Exception
            Log($"[{DevName}] unable to connect to remote host.", ConsoleColor.Red)
            IsConnected = False
        Finally
            Try
                Disconnect()
            Catch disconnectEx As Exception
                Log($"[{DevName}] Error during disconnection: {disconnectEx.Message}", ConsoleColor.Red)
                IsConnected = False
            End Try
            IsConnected = False
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
                    If IsConnected Then
                        Try
                            clientStream.WriteByte(0) ' Keepalive
                        Catch ex As Exception
                            Exit While
                        End Try
                    End If
                    If (DateTime.Now - lastReceivedTime) > inactivityTimeout AndAlso dataBuilder.Length > 0 Then
                        ProcessDocumentData(dataBuilder.ToString())
                        dataBuilder.Clear()
                        lastReceivedTime = DateTime.Now
                    End If
                Else
                    If Not Receiving Then
                        Receiving = True
                        If OS <> OSType.OS_RSTS AndAlso OS <> OSType.OS_NOS278 Then
                            Log($"[{DevName}] receiving data from remote host.", ConsoleColor.Yellow)
                        Else
                            Log($"[{DevName}] receiving data from low speed device. Sit back and relax.", ConsoleColor.Yellow)
                        End If
                    End If
                    Dim recd As Integer = Await clientStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    If recd > 0 Then
                        dataBuilder.Append(Encoding.UTF8.GetString(buffer, 0, recd))
                        lastReceivedTime = DateTime.Now
                    End If
                End If
            End While
        Catch ex As OperationCanceledException
            Log("Receiving canceled.")
        Catch ex As Exception
            If ex.HResult = -2146232800 Then
                Log($"[{DevDest}] Disconnected from remote host.", ConsoleColor.Red)
            Else
                Log($"Error receiving data: [{ex.HResult}] {ex.ToString()}", ConsoleColor.Red)
            End If
        End Try
    End Function

    Private Sub ProcessDocumentData(documentData As String)
        JobNumber += 1
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

        If lines.Count > 9 Then
            currentDocument.AddRange(lines)
            Dim docCopy As New List(Of String)(currentDocument)
            Task.Run(Sub() ProcessDocument(docCopy))
            Log($"[{DevName}] Waiting for new document.")
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
        Catch ex As Exception
            Log($"[{DevName}] Error during disconnection: {ex.Message}", ConsoleColor.Red)
        End Try
    End Sub

    Private Sub ProcessDocument(doc As List(Of String))
        OutDest = OutDest.TrimEnd("/"c, "\"c)
        If Not Directory.Exists(OutDest) Then
            Log($"[{DevName}] Created output directory {OutDest}", ConsoleColor.Yellow)
            Directory.CreateDirectory(OutDest)
        End If
        Dim dataDir = Path.Combine(OutDest, "data")
        If Not Directory.Exists(dataDir) Then
            Log($"[{DevName}] Created data directory {dataDir}", ConsoleColor.Yellow)
            Directory.CreateDirectory(dataDir)
        End If

        Receiving = False
        Log($"[{DevName}] received {doc.Count} lines from remote host.", ConsoleColor.Cyan)

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
            Dim JobID = jobInfo.JobID
            Dim JobName = jobInfo.JobName
            Dim UserID = jobInfo.User

            Dim userDir = Path.Combine(OutDest, UserID)
            If Not Directory.Exists(userDir) Then
                Log($"[{DevName}] creating user directory {userDir}", ConsoleColor.Yellow)
                Directory.CreateDirectory(userDir)
            Else
                Log($"[{DevName}] directory {userDir} already exists.", ConsoleColor.Yellow)
            End If

            Dim pdfName As String = Path.Combine(userDir, $"{DevName}-{UserID}-{JobID}-{JobName}_{JobNumber}.pdf")

            Dim renderer As New RenderPDF()
            renderer.DevName = DevName
            renderer.OS = OS
            renderer.Orientation = Orientation
            renderer.TargetFileName = pdfName
            renderer.Shading = Shading

            renderer.CreatePDF(JobName, doc)
        Else
            Log(String.Format("[{1}] Ignoring document with {0} lines as line garbage or banners.", doc.Count, DevName))
            Receiving = False
        End If
    End Sub
End Class
