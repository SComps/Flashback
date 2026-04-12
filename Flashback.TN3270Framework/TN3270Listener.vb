Imports System
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Collections.Generic

Public Class TN3270Listener
    Private _listener As TcpListener
    Private _isRunning As Boolean
    Private _port As Integer

    Public Event ConnectionReceived(sender As Object, e As TN3270ConnectionEventArgs)

    Public Sub New(port As Integer)
        _port = port
        _listener = New TcpListener(IPAddress.Any, port)
    End Sub

    Public Sub Start()
        _listener.Start()
        _isRunning = True
        Task.Run(AddressOf ListenLoop)
    End Sub

    Public Sub StopListening()
        _isRunning = False
        _listener.Stop()
    End Sub

    Private Async Sub ListenLoop()
        While _isRunning
            Try
                Dim client = Await _listener.AcceptTcpClientAsync()
                Dim session = New TN3270Session(client)
                RaiseEvent ConnectionReceived(Me, New TN3270ConnectionEventArgs(session, client.Client.RemoteEndPoint))
            Catch ex As Exception
                If _isRunning Then Console.WriteLine($"Listener error: {ex.Message}")
            End Try
        End While
    End Sub
End Class

Public Class TN3270ConnectionEventArgs
    Inherits EventArgs
    Public ReadOnly Property Session As TN3270Session
    Public ReadOnly Property RemoteEndPoint As EndPoint

    Public Sub New(session As TN3270Session, endpoint As EndPoint)
        Me.Session = session
        Me.RemoteEndPoint = endpoint
    End Sub
End Class

Public Class TN3270Session
    Private _client As TcpClient
    Private _stream As NetworkStream
    Public ReadOnly Property Client As TcpClient
        Get
            Return _client
        End Get
    End Property
    Public Property Ebcdic As Encoding
    Private _cancelToken As CancellationTokenSource

    Public Event DataReceived(sender As Object, e As DataReceivedEventArgs)
    Public Event AidKeyReceived(sender As Object, e As AidKeyEventArgs)
    Public Event Disconnected(sender As Object, e As EventArgs)
    Public Event ScreenUpdated(sender As Object, e As EventArgs)
    Public Event NegotiationComplete(sender As Object, e As EventArgs)

    Private _binaryAgreed As Boolean = False
    Private _eorAgreed As Boolean = False
    Private _ttypeAgreed As Boolean = False
    Public ReadOnly Property IsNegotiated As Boolean
        Get
            Return _binaryAgreed AndAlso _eorAgreed AndAlso _ttypeAgreed
        End Get
    End Property

    Public Property TerminalType As String = "Unknown"
    Public Property Rows As Integer = 24
    Public Property Columns As Integer = 80
    
    Public ReadOnly Property ScreenSize As Integer
        Get
            Return Rows * Columns
        End Get
    End Property
    
    ' The logical representation of the screen fields
    Public Property Fields As New List(Of TN3270Field)
    
    ' Maintain a reference back to the session in fields for dynamic address calculation
    Public Shared CurrentSession As TN3270Session 
    
    ' Telnet Constants
    Private Const IAC As Byte = 255
    Private Const DO_OPT As Byte = 253
    Private Const DONT As Byte = 254
    Private Const WILL As Byte = 251
    Private Const WONT As Byte = 252
    Private Const SB As Byte = 250
    Private Const SE As Byte = 240
    
    ' Options
    Private Const OPT_BINARY As Byte = 0
    Private Const OPT_EOR As Byte = 25
    Private Const OPT_TERMINAL_TYPE As Byte = 24
    Private Const OPT_NAWS As Byte = 31
    
    ' Telnet Commands
    Private Const CMD_EOR As Byte = 239 ' Correct EOR Command Code (0xEF)

    Public Sub New(client As TcpClient)
        _client = client
        _stream = client.GetStream()
        _cancelToken = New CancellationTokenSource()
        
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Ebcdic = Encoding.GetEncoding("IBM037")
    End Sub

    Public Sub SendReadPartitionQuery()
        ' WSF (0xF3) + Length (High, Low) + Read Partition (0x01) + Query (0xFF)
        Dim cmd As Byte() = {INTERFACE_CMD.WSF, 0, 5, 1, &HFF}
        Dim buffer As New List(Of Byte)(cmd)
        buffer.Add(IAC)
        buffer.Add(CMD_EOR)
        SendRaw(buffer.ToArray())
    End Sub

    Public Sub WriteStructuredField(data As Byte())
        ' WSF (0xF3) + Length (High, Low) + Data
        Dim totalLen = data.Length + 2
        Dim buffer As New List(Of Byte)
        buffer.Add(INTERFACE_CMD.WSF)
        buffer.Add(CByte((totalLen >> 8) And &HFF))
        buffer.Add(CByte(totalLen And &HFF))
        buffer.AddRange(data)
        
        buffer.Add(IAC)
        buffer.Add(CMD_EOR)
        SendRaw(buffer.ToArray())
    End Sub

    Public Sub StartNegotiation()
        Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Starting negotiation...")
        Task.Run(AddressOf HandleConnection)
        
        ' Initial Negotiation: DO TERMINAL-TYPE, DO EOR, DO BINARY, DO NAWS
        SendTelnetCommand(DO_OPT, OPT_TERMINAL_TYPE)
        SendTelnetCommand(DO_OPT, OPT_EOR)
        SendTelnetCommand(DO_OPT, OPT_BINARY)
        SendTelnetCommand(DO_OPT, OPT_NAWS)
        SendTelnetCommand(WILL, OPT_EOR)
        SendTelnetCommand(WILL, OPT_BINARY)
    End Sub

    Private Async Sub HandleConnection()
        Dim buffer(8192) As Byte
        Try
            While _client.Connected
                Dim bytesRead As Integer
                Try
                    bytesRead = Await _stream.ReadAsync(buffer, 0, buffer.Length, _cancelToken.Token)
                Catch ex As OperationCanceledException
                    Exit While
                End Try

                If bytesRead = 0 Then Exit While
                
                Try
                    ProcessData(buffer, bytesRead)
                Catch ex As Exception
                    Console.WriteLine($"Error processing data: {ex.Message}")
                    Exit While
                End Try
            End While
        Catch ex As Exception
            ' Connection dropped
        Finally
            Try
                _client.Close()
            Catch
            End Try
            RaiseEvent Disconnected(Me, EventArgs.Empty)
        End Try
    End Sub
    
    Public Sub Close()
        Try
            _cancelToken.Cancel()
            _client.Close()
        Catch
        End Try
    End Sub

    Private Sub ProcessData(buffer() As Byte, length As Integer)
        Dim i As Integer = 0
        While i < length
            Dim b = buffer(i)
            If b = IAC Then
                i = ProcessTelnetCommand(buffer, i, length)
            Else
                ' 3270 Data Stream Parsing
                If IsNegotiated Then
                    If i + 2 < length Then
                         Dim aid = buffer(i)
                         
                         If aid = &H88 Then
                            ProcessStructuredField(buffer, i + 3, length)
                            i = length ' Structured fields are usually the whole packet
                         Else
                             Dim cursorAddr = DecodeAddress(buffer(i + 1), buffer(i + 2))
                             ParseInboundFields(buffer, i + 3, length)
                             
                             ' ActiveField is the field the cursor is currently inside.
                             ' The field begins at f.Address (the attribute byte). Data is f.Address + 1 to f.Address + f.Length.
                             Dim activeField = Fields.OrderByDescending(Function(f) f.Address).FirstOrDefault(Function(f) f.Address <= cursorAddr AndAlso (f.Address + f.Length) >= cursorAddr)
                             
                             Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] AID Key Received: {aid:X2} at cursor {cursorAddr}")
                             RaiseEvent AidKeyReceived(Me, New AidKeyEventArgs(aid, activeField, cursorAddr))
                             
                             ' Move i to the end of this 3270 packet (usually the rest of the buffer)
                             i = length 
                         End If
                    Else
                         i += 1 
                    End If
                Else
                    ' In NVT mode, just skip or handle as ASCII?
                    ' For TN3270, we usually ignore non-Telnet data until negotiated.
                    i += 1
                End If
            End If
        End While
        
        Dim actualData(length - 1) As Byte
        Array.Copy(buffer, actualData, length)
        RaiseEvent DataReceived(Me, New DataReceivedEventArgs(actualData))
    End Sub
    
    Private Sub ParseInboundFields(buffer() As Byte, startIndex As Integer, length As Integer)
        Dim seenFields As New HashSet(Of TN3270Field)
        Dim i = startIndex
        Dim currentField As TN3270Field = Nothing
        
        While i < length
            Dim b = buffer(i)
            
            If b = ORDER.SBA Then
                 If i + 2 < length Then
                    ' Decode the address the terminal is pointing to
                    Dim addr = DecodeAddress(buffer(i + 1), buffer(i + 2))
                    ' Find the field that contains or precedes this address
                    ' 3270 terminals send the address of the first character of the field (Address + 1)
                    currentField = Fields.OrderByDescending(Function(f) f.Address).FirstOrDefault(Function(f) f.Address < addr)
                    
                    If currentField IsNot Nothing AndAlso Not seenFields.Contains(currentField) Then
                        ' Reset field content the first time we see data for it in this packet
                        currentField.Content = ""
                        seenFields.Add(currentField)
                    End If
                    
                    i += 3
                 Else
                    i += 1
                 End If
            ElseIf b = ORDER.SF Then
                i += 2 ' Skip SF and Attribute
                currentField = Nothing
            ElseIf b = IAC AndAlso i + 1 < length AndAlso buffer(i + 1) = CMD_EOR Then
                Exit While
            Else
                ' Text Content: Append to the currently active field
                If currentField IsNot Nothing AndAlso b <> 0 Then
                     ' Filter out 0x00 (EBCDIC Null) characters
                     Dim charStr = Ebcdic.GetString({b})
                     currentField.Content += charStr
                End If
                i += 1
            End If
        End While
        RaiseEvent ScreenUpdated(Me, EventArgs.Empty)
    End Sub

    Public Event StructuredFieldReceived(sender As Object, e As StructuredFieldEventArgs)

    Private Sub ProcessStructuredField(buffer() As Byte, startIndex As Integer, length As Integer)
        Dim i = startIndex
        While i + 2 < length
            ' Each SF starts with a 2-byte length
            Dim sfLen = (CInt(buffer(i)) << 8) Or buffer(i + 1)
            If sfLen = 0 OrElse i + sfLen > length Then Exit While
            
            Dim sfData(sfLen - 3) As Byte
            Array.Copy(buffer, i + 2, sfData, 0, sfLen - 2)
            
            RaiseEvent StructuredFieldReceived(Me, New StructuredFieldEventArgs(sfData))
            i += sfLen
        End While
    End Sub

    Private Function ProcessTelnetCommand(buffer() As Byte, index As Integer, length As Integer) As Integer
        If index + 1 >= length Then Return length
        
        Dim cmd = buffer(index + 1)
        If cmd = SB Then
            Dim j = index + 2
            While j < length
                If buffer(j) = IAC AndAlso j + 1 < length AndAlso buffer(j + 1) = SE Then
                    HandleSubNegotiation(buffer, index + 2, j - (index + 2))
                    Return j + 2
                End If
                j += 1
            End While
            Return length 
        ElseIf cmd >= 251 AndAlso cmd <= 254 Then
             If index + 2 < length Then
                Dim opt = buffer(index + 2)
                HandleOption(cmd, opt)
                Return index + 3
             End If
        End If
        Return index + 2
    End Function
    
    Private Sub HandleOption(cmd As Byte, opt As Byte)
        Select Case opt
            Case OPT_TERMINAL_TYPE
                If cmd = WILL Then
                    Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Client WILL do Terminal-Type. Requesting identification...")
                    SendRaw({IAC, SB, OPT_TERMINAL_TYPE, 1, IAC, SE})
                ElseIf cmd = DONT OrElse cmd = WONT Then
                    Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Client rejected Terminal-Type negotiation.")
                End If
                
            Case OPT_BINARY
                If cmd = WILL OrElse cmd = DO_OPT Then
                    If Not _binaryAgreed Then
                        _binaryAgreed = True
                        Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Binary mode agreed.")
                        If cmd = WILL Then SendTelnetCommand(DO_OPT, OPT_BINARY) Else SendTelnetCommand(WILL, OPT_BINARY)
                        CheckNegotiationComplete()
                    End If
                End If
                
            Case OPT_EOR
                If cmd = WILL OrElse cmd = DO_OPT Then
                    If Not _eorAgreed Then
                        _eorAgreed = True
                        Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] EOR agreed.")
                        If cmd = WILL Then SendTelnetCommand(DO_OPT, OPT_EOR) Else SendTelnetCommand(WILL, OPT_EOR)
                        CheckNegotiationComplete()
                    End If
                End If
                
            Case OPT_NAWS
                If cmd = WILL Then
                    Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Client supports NAWS (Dynamic Sizing).")
                End If
        End Select
    End Sub

    Private Sub CheckNegotiationComplete()
        If IsNegotiated Then
            Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Pre-negotiation complete. Terminal: {TerminalType}")
            RaiseEvent NegotiationComplete(Me, EventArgs.Empty)
        End If
    End Sub
    
    Private Sub HandleSubNegotiation(buffer() As Byte, offset As Integer, count As Integer)
        If count <= 0 Then Return
        
        Dim opt = buffer(offset)
        If opt = OPT_TERMINAL_TYPE AndAlso buffer(offset + 1) = 0 Then
            Dim typeNameRaw = New Byte(count - 3) {}
            Array.Copy(buffer, offset + 2, typeNameRaw, 0, count - 2)
            TerminalType = Encoding.ASCII.GetString(typeNameRaw).Trim(Chr(0)).ToUpper()
            
            ' Fallback Geometry Mapping (if NAWS isn't sent)
            If Columns = 80 AndAlso Rows = 24 Then
                If TerminalType.EndsWith("-5") OrElse TerminalType.Contains("-5-") Then
                    Rows = 27 : Columns = 132
                ElseIf TerminalType.EndsWith("-4") OrElse TerminalType.Contains("-4-") Then
                    Rows = 43 : Columns = 80
                ElseIf TerminalType.EndsWith("-3") OrElse TerminalType.Contains("-3-") Then
                    Rows = 32 : Columns = 80
                End If
            End If
            
            _ttypeAgreed = True
            Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Terminal Type: {TerminalType} (Initial Geometry: {Columns}x{Rows})")
            CheckNegotiationComplete()
            
        ElseIf opt = OPT_NAWS AndAlso count >= 5 Then
            ' NAWS format: width(2 bytes), height(2 bytes)
            Columns = (CInt(buffer(offset + 1)) << 8) Or buffer(offset + 2)
            Rows = (CInt(buffer(offset + 3)) << 8) Or buffer(offset + 4)
            
            Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Dynamic Geometry Negotiated: {Columns}x{Rows}")
        End If
    End Sub

    Public Sub SendTelnetCommand(action As Byte, optionCode As Byte)
        Dim cmd = {IAC, action, optionCode}
        SendRaw(cmd)
    End Sub
    
    Private Sub SendRaw(data() As Byte)
        Try
            If _stream.CanWrite Then
                _stream.Write(data, 0, data.Length)
                _stream.Flush()
            End If
        Catch
        End Try
    End Sub
    
    ' --- High Level Screen API ---
    
    Public Sub ClearFields()
        Fields.Clear()
    End Sub
    
    Public Function AddField(row As Integer, col As Integer, length As Integer, Optional content As String = "", Optional isProtected As Boolean = True, Optional foreground As Byte = TN3270Color.White, Optional background As Byte = TN3270Color.Neutral, Optional highlighting As Byte = TN3270Highlight.None, Optional name As String = "") As TN3270Field
        Dim f As New TN3270Field(Me)
        f.Row = row
        f.Col = col
        f.Length = length
        f.Content = content
        f.IsProtected = isProtected
        f.ForegroundColor = foreground
        f.BackgroundColor = background
        f.Highlighting = highlighting
        f.Name = name
        Fields.Add(f)
        Return f
    End Function

    Public Sub WriteText(row As Integer, col As Integer, text As String, Optional foreground As Byte = TN3270Color.White, Optional background As Byte = TN3270Color.Neutral)
        ' Logic for arbitrary static text (usually just a temporary protected field)
        Dim f = AddField(row, col, text.Length, text, True, foreground, background)
    End Sub

    Public Function GetFieldByName(name As String) As TN3270Field
        Return Fields.FirstOrDefault(Function(f) String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Function GetFieldValue(name As String) As String
        Return GetFieldByName(name)?.Content
    End Function

    Public Sub ShowScreen(Optional clearScreen As Boolean = True)
        ' Construct 3270 Data Stream from Fields
        Dim buffer As New List(Of Byte)
        
        ' 3270 Command
        ' CRITICAL FIX: To use "Alternate" (e.g. 132x27 or large models)
        ' we MUST send ERASE_WRITE_ALTERNATE (&H7E) instead of ERASE_WRITE (&HF5)
        ' Standard ERASE_WRITE forces many terminals back to 80x24.
        Dim cmd As Byte
        If clearScreen Then
            If Columns > 80 OrElse Rows > 24 Then
                cmd = INTERFACE_CMD.ERASE_WRITE_ALTERNATE
            Else
                cmd = INTERFACE_CMD.ERASE_WRITE
            End If
        Else
            cmd = INTERFACE_CMD.WRITE
        End If
        
        buffer.Add(cmd)
        
        Console.WriteLine($"[Session {_client.Client.RemoteEndPoint}] Sending command: {cmd:X2} (clearScreen={clearScreen}, {Columns}x{Rows})")
        
        ' WCC (Write Control Character)
        ' &HC3 = 11000011 (Binary)
        ' Bits: 11 (Wait) 00 (NoPrint) 0 (NoNL) 0 (NoAlarm) 1 (Restore Keyboard) 1 (Reset MDT)
        buffer.Add(&HC3) 
        
        For Each f In Fields
            ' SBA (Set Buffer Address)
            Dim addr = f.Address
            buffer.Add(ORDER.SBA)
            buffer.AddRange(EncodeAddress(addr))
            
            ' Determine how many extended attribute pairs we need
            Dim attrPairs As New List(Of Byte)
            
            ' 1. Field Attribute (Base - Type 0xC0)
            attrPairs.Add(&HC0)
            attrPairs.Add(f.GetAttributeByte())
            
            ' ... (Extended Attributes logic) ...
            If f.ForegroundColor <> TN3270Color.Neutral Then
                attrPairs.Add(&H42)
                attrPairs.Add(f.ForegroundColor)
            End If
            If f.BackgroundColor <> TN3270Color.Neutral Then
                attrPairs.Add(&H45)
                attrPairs.Add(f.BackgroundColor)
            End If
            If f.Highlighting <> TN3270Highlight.None Then
                attrPairs.Add(&H41)
                attrPairs.Add(f.Highlighting)
            End If
            If f.Transparency <> TN3270Transparency.None Then
                attrPairs.Add(&H46)
                attrPairs.Add(f.Transparency)
            End If

            ' Send SF or SFE
            If attrPairs.Count > 2 Then
                buffer.Add(ORDER.SFE)
                buffer.Add(CByte(attrPairs.Count / 2))
                buffer.AddRange(attrPairs)
            Else
                buffer.Add(ORDER.SF)
                buffer.Add(f.GetAttributeByte())
            End If
            
            ' Content
            If Not String.IsNullOrEmpty(f.Content) Then
                Dim data = Ebcdic.GetBytes(f.Content)
                If data.Length > f.Length Then
                    buffer.AddRange(data.Take(f.Length).ToArray())
                Else
                    buffer.AddRange(data)
                End If
            End If

            ' NEW: Add a "Terminator" attribute immediately after the field
            ' This prevents high-intensity, colors, or underlining from leaking 
            ' into the rest of the screen.
            Dim termAddr = (f.Address + f.Length + 1) Mod ScreenSize
            
            ' Check if there's already a field starting at the terminator address
            ' If so, we don't need to add a terminator as the next field's attribute will handle it.
            Dim fieldAtTerm = Fields.FirstOrDefault(Function(other) other.Address = termAddr)
            
            If fieldAtTerm Is Nothing Then
                buffer.Add(ORDER.SBA)
                buffer.AddRange(EncodeAddress(termAddr))
                buffer.Add(ORDER.SF)
                buffer.Add(&H60) ' Protected, Normal Intensity, Alphanumeric
            End If
        Next
        
        ' Position cursor at the first unprotected field
        Dim firstUnprotected = Fields.FirstOrDefault(Function(f) Not f.IsProtected)
        If firstUnprotected IsNot Nothing Then
            buffer.Add(ORDER.SBA)
            buffer.AddRange(EncodeAddress(firstUnprotected.Address + 1)) 
            buffer.Add(ORDER.IC)
        End If        
        ' Proper TN3270 EOR delimiter: IAC EOR
        buffer.Add(IAC)
        buffer.Add(CMD_EOR)
        SendRaw(buffer.ToArray())
    End Sub
    
    Private Function EncodeAddress(addr As Integer) As Byte()
        Dim codeMap = TN3270Utils.AddressCodeMap
        Dim b1 = (addr >> 6) And &H3F
        Dim b2 = addr And &H3F
        Return {codeMap(b1), codeMap(b2)} 
    End Function
    
    Private Function DecodeAddress(b1 As Byte, b2 As Byte) As Integer
        Dim raw1 = TN3270Utils.ReverseAddressMap(b1)
        Dim raw2 = TN3270Utils.ReverseAddressMap(b2)
        Return (raw1 << 6) Or raw2
    End Function

End Class

Public Class TN3270Field
    Private _session As TN3270Session
    
    Public Property Name As String = ""
    Public Property Row As Integer
    Public Property Col As Integer
    Public Property Length As Integer
    Public Property Content As String = ""
    
    Public Sub New(session As TN3270Session)
        _session = session
    End Sub
    
    ' Parameterless constructor for backward compatibility or serialization
    Public Sub New()
        ' Note: Address calculation will fallback to 80x24 if session is not set
    End Sub
    
    ' Base Attributes
    Public Property IsProtected As Boolean = False
    Public Property IsNumeric As Boolean = False
    Public Property Modified As Boolean = False
    Public Property Intensity As TN3270Intensity = TN3270Intensity.Normal
    
    ' Extended Attributes
    Public Property ForegroundColor As Byte = TN3270Color.Neutral
    Public Property BackgroundColor As Byte = TN3270Color.Neutral
    Public Property Highlighting As Byte = TN3270Highlight.None
    Public Property Transparency As Byte = TN3270Transparency.None

    ''' <summary>
    ''' Constructs the byte representation of the 3270 Field Attribute.
    ''' </summary>
    Public Function GetAttributeByte() As Byte
        ' 3270 attribute byte structure:
        ' Bit 2: Protected, Bit 3: Numeric, Bit 4-5: Intensity, Bit 7: MDT
        Dim attr As Byte = 0
        
        If IsProtected Then attr = attr Or &H20
        If IsNumeric Then attr = attr Or &H10
        
        Select Case Intensity
            Case TN3270Intensity.Normal
                ' 00
            Case TN3270Intensity.High
                attr = attr Or &H8
            Case TN3270Intensity.Hidden
                attr = attr Or &HC
        End Select
        
        If Modified Then attr = attr Or &H1
        
        ' Map to a valid 3270 Graphic character (usually Space based)
        Return TN3270Utils.MapAttributeToGraphic(attr)
    End Function
    
    Public ReadOnly Property Address As Integer
        Get
            ' This calculates the physical address of the ATTRIBUTE BYTE.
            ' By subtracting 1, we ensure the DATA starts exactly at the Row/Col requested.
            Dim cols = If(_session IsNot Nothing, _session.Columns, 80)
            Dim totalCells = If(_session IsNot Nothing, _session.ScreenSize, 1920)
            
            Return (((Row - 1) * cols) + (Col - 1) - 1 + totalCells) Mod totalCells
        End Get
    End Property
End Class

Public Enum TN3270Intensity
    Normal
    High
    Hidden
End Enum

Public Class TN3270Attribute
    ' Bit Constants (Internal)
    Public Const ProtectedField As Byte = &H20
    Public Const Numeric As Byte = &H10
    Public Const IntensityHigh As Byte = &H08
    Public Const IntensityHidden As Byte = &H0C
    Public Const MDT As Byte = &H01
End Class

Public Class TN3270Utils
    Public Shared ReadOnly Property AddressCodeMap As Byte() = {
            &H40, &HC1, &HC2, &HC3, &HC4, &HC5, &HC6, &HC7, &HC8, &HC9, &H4A, &H4B, &H4C, &H4D, &H4E, &H4F,
            &H50, &HD1, &HD2, &HD3, &HD4, &HD5, &HD6, &HD7, &HD8, &HD9, &H5A, &H5B, &H5C, &H5D, &H5E, &H5F,
            &H60, &H61, &HE2, &HE3, &HE4, &HE5, &HE6, &HE7, &HE8, &HE9, &H6A, &H6B, &H6C, &H6D, &H6E, &H6F,
            &HF0, &HF1, &HF2, &HF3, &HF4, &HF5, &HF6, &HF7, &HF8, &HF9, &H7A, &H7B, &H7C, &H7D, &H7E, &H7F
    }
    
    Public Shared ReadOnly Property ReverseAddressMap As Dictionary(Of Byte, Integer)
    
    Shared Sub New()
        ReverseAddressMap = New Dictionary(Of Byte, Integer)
        For i As Integer = 0 To AddressCodeMap.Length - 1
            If Not ReverseAddressMap.ContainsKey(AddressCodeMap(i)) Then
                ReverseAddressMap.Add(AddressCodeMap(i), i)
            End If
        Next
    End Sub

    Public Shared Function MapAttributeToGraphic(attr As Byte) As Byte
        ' Map the raw 6-bit attribute to the valid 3270 graphic set 
        ' (Starting at 0x40 / Space in IBM037)
        Return AddressCodeMap(attr And &H3F)
    End Function
End Class

Public Class DataReceivedEventArgs
    Inherits EventArgs
    Public ReadOnly Property Data As Byte()
    Public Sub New(data() As Byte)
        Me.Data = data
    End Sub
End Class

Public Class AidKeyEventArgs
    Inherits EventArgs
    Public ReadOnly Property AidKey As Byte
    Public ReadOnly Property ActiveField As TN3270Field
    Public ReadOnly Property CursorAddress As Integer

    Public Sub New(key As Byte, activeField As TN3270Field, cursorAddress As Integer)
        Me.AidKey = key
        Me.ActiveField = activeField
        Me.CursorAddress = cursorAddress
    End Sub
End Class

Public Class INTERFACE_CMD
    Public Const WRITE As Byte = &HF1
    Public Const ERASE_WRITE As Byte = &HF5
    Public Const ERASE_WRITE_ALTERNATE As Byte = &H7E ' Required for Large Screen Models
    Public Const WSF As Byte = &HF3 ' Write Structured Field
End Class

Public Class StructuredFieldEventArgs
    Inherits EventArgs
    Public ReadOnly Property Data As Byte()
    Public Sub New(data As Byte())
        Me.Data = data
    End Sub
End Class

Public Class ORDER
    Public Const SBA As Byte = &H11 ' Set Buffer Address
    Public Const SF As Byte = &H1D  ' Start Field
    Public Const SFE As Byte = &H29 ' Start Field Extended
    Public Const IC As Byte = &H13  ' Insert Cursor
    Public Const SA As Byte = &H28  ' Set Attribute
End Class

Public Class TN3270Color
    Public Const Neutral As Byte = &H0
    Public Const Blue As Byte = &HF1
    Public Const Red As Byte = &HF2
    Public Const Pink As Byte = &HF3
    Public Const Green As Byte = &HF4
    Public Const Turquoise As Byte = &HF5
    Public Const Yellow As Byte = &HF6
    Public Const White As Byte = &HF7
End Class

Public Class TN3270Transparency
    Public Const None As Byte = &H0
    Public Const Transparent As Byte = &HF0
    Public Const Opaque As Byte = &HF1
End Class

Public Class TN3270Highlight
    Public Const None As Byte = &H0
    Public Const Blink As Byte = &HF1
    Public Const ReverseVideo As Byte = &HF2
    Public Const Underline As Byte = &HF4
End Class

