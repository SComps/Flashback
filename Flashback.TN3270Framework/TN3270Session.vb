Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports TN3270Framework.Protocol
Imports TN3270Framework.Model
Imports TN3270Framework.Events

''' <summary>
''' Represents a single TN3270 client session: Telnet negotiation, 3270 data-stream
''' parsing/generation, and a high-level field-oriented screen API.
''' </summary>
Public Class TN3270Session
    Implements IScreenGeometry

#Region "Fields and properties"

    Private ReadOnly _client As TcpClient
    Private ReadOnly _stream As NetworkStream
    Private ReadOnly _cancelToken As CancellationTokenSource
    Private ReadOnly _fields As New List(Of TN3270Field)

    Private _binaryAgreed As Boolean
    Private _eorAgreed As Boolean
    Private _ttypeAgreed As Boolean

    Public ReadOnly Property Client As TcpClient
        Get
            Return _client
        End Get
    End Property

    Public Property Ebcdic As Encoding

    Public Property TerminalType As String = "Unknown"
    Public Property Rows As Integer = 24
    Public Property Columns As Integer = 80

    Public ReadOnly Property ScreenSize As Integer Implements IScreenGeometry.ScreenSize
        Get
            Return Rows * Columns
        End Get
    End Property

    Private ReadOnly Property IScreenGeometry_Columns As Integer Implements IScreenGeometry.Columns
        Get
            Return Columns
        End Get
    End Property

    Public ReadOnly Property IsNegotiated As Boolean
        Get
            Return _binaryAgreed AndAlso _eorAgreed AndAlso _ttypeAgreed
        End Get
    End Property

    Public ReadOnly Property Fields As IReadOnlyList(Of TN3270Field)
        Get
            Return _fields
        End Get
    End Property

#End Region

#Region "Events"

    Public Event DataReceived As EventHandler(Of DataReceivedEventArgs)
    Public Event AidKeyReceived As EventHandler(Of AidKeyEventArgs)
    Public Event Disconnected As EventHandler
    Public Event ScreenUpdated As EventHandler
    Public Event NegotiationComplete As EventHandler
    Public Event StructuredFieldReceived As EventHandler(Of StructuredFieldEventArgs)

#End Region

#Region "Construction / lifetime"

    Public Sub New(client As TcpClient)
        If client Is Nothing Then Throw New ArgumentNullException(NameOf(client))
        _client = client
        _stream = client.GetStream()
        _cancelToken = New CancellationTokenSource()

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Ebcdic = Encoding.GetEncoding("IBM037")
    End Sub

    Public Sub Close()
        Try
            _cancelToken.Cancel()
            _client.Close()
        Catch
        End Try
    End Sub

#End Region

#Region "Telnet negotiation"

    Public Sub StartNegotiation()
        Task.Run(AddressOf HandleConnection)

        SendTelnetCommand(Telnet.DO_OPT, Telnet.OPT_TERMINAL_TYPE)
        SendTelnetCommand(Telnet.DO_OPT, Telnet.OPT_EOR)
        SendTelnetCommand(Telnet.DO_OPT, Telnet.OPT_BINARY)
        SendTelnetCommand(Telnet.DO_OPT, Telnet.OPT_NAWS)
        SendTelnetCommand(Telnet.WILL, Telnet.OPT_EOR)
        SendTelnetCommand(Telnet.WILL, Telnet.OPT_BINARY)
    End Sub

    Private Async Sub HandleConnection()
        Dim buffer(8192) As Byte
        Try
            While _client.Connected
                Dim bytesRead As Integer
                Try
                    bytesRead = Await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cancelToken.Token).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    Exit While
                End Try

                If bytesRead = 0 Then Exit While

                Try
                    ProcessData(buffer, bytesRead)
                Catch
                    Exit While
                End Try
            End While
        Catch
            ' connection dropped
        Finally
            Try
                _client.Close()
            Catch
            End Try
            RaiseEvent Disconnected(Me, EventArgs.Empty)
        End Try
    End Sub

    Private Sub ProcessData(buffer() As Byte, length As Integer)
        Dim i As Integer = 0
        While i < length
            If buffer(i) = Telnet.IAC Then
                i = ProcessTelnetCommand(buffer, i, length)
            ElseIf IsNegotiated Then
                If i + 2 < length Then
                    Dim aid = buffer(i)
                    If aid = AidKeys.StructuredField Then
                        ProcessStructuredField(buffer, i + 3, length)
                        i = length
                    Else
                        Dim cursorAddr = AddressCodec.Decode(buffer(i + 1), buffer(i + 2))
                        ParseInboundFields(buffer, i + 3, length)

                        Dim activeField = _fields.
                            OrderByDescending(Function(f) f.Address).
                            FirstOrDefault(Function(f) f.Address <= cursorAddr AndAlso (f.Address + f.Length) >= cursorAddr)

                        RaiseEvent AidKeyReceived(Me, New AidKeyEventArgs(aid, activeField, cursorAddr))
                        i = length
                    End If
                Else
                    i += 1
                End If
            Else
                ' Ignore non-Telnet data until negotiation completes
                i += 1
            End If
        End While

        Dim actualData(length - 1) As Byte
        Array.Copy(buffer, actualData, length)
        RaiseEvent DataReceived(Me, New DataReceivedEventArgs(actualData))
    End Sub

    Private Function ProcessTelnetCommand(buffer() As Byte, index As Integer, length As Integer) As Integer
        If index + 1 >= length Then Return index + 1

        Dim cmd = buffer(index + 1)
        Select Case cmd
            Case Telnet.DO_OPT, Telnet.DONT, Telnet.WILL, Telnet.WONT
                If index + 2 < length Then
                    HandleOption(cmd, buffer(index + 2))
                    Return index + 3
                End If
            Case Telnet.SB
                Dim seIndex = index + 2
                While seIndex < length - 1
                    If buffer(seIndex) = Telnet.IAC AndAlso buffer(seIndex + 1) = Telnet.SE Then
                        HandleSubNegotiation(buffer, index + 2, seIndex - (index + 2))
                        Return seIndex + 2
                    End If
                    seIndex += 1
                End While
            Case Telnet.IAC
                ' Escaped 0xFF data byte
                Return index + 2
        End Select
        Return index + 2
    End Function

    Private Sub HandleOption(cmd As Byte, opt As Byte)
        Select Case cmd
            Case Telnet.WILL
                Select Case opt
                    Case Telnet.OPT_BINARY
                        _binaryAgreed = True
                        SendTelnetCommand(Telnet.DO_OPT, opt)
                    Case Telnet.OPT_EOR
                        _eorAgreed = True
                        SendTelnetCommand(Telnet.DO_OPT, opt)
                    Case Telnet.OPT_TERMINAL_TYPE
                        SendTelnetCommand(Telnet.DO_OPT, opt)
                        ' Request terminal type
                        SendRaw({Telnet.IAC, Telnet.SB, Telnet.OPT_TERMINAL_TYPE, Telnet.TTYPE_SEND, Telnet.IAC, Telnet.SE})
                    Case Telnet.OPT_NAWS
                        SendTelnetCommand(Telnet.DO_OPT, opt)
                    Case Else
                        SendTelnetCommand(Telnet.DONT, opt)
                End Select

            Case Telnet.DO_OPT
                Select Case opt
                    Case Telnet.OPT_BINARY
                        _binaryAgreed = True
                        SendTelnetCommand(Telnet.WILL, opt)
                    Case Telnet.OPT_EOR
                        _eorAgreed = True
                        SendTelnetCommand(Telnet.WILL, opt)
                    Case Else
                        SendTelnetCommand(Telnet.WONT, opt)
                End Select

            Case Telnet.WONT
                Select Case opt
                    Case Telnet.OPT_BINARY : _binaryAgreed = False
                    Case Telnet.OPT_EOR : _eorAgreed = False
                    Case Telnet.OPT_TERMINAL_TYPE : _ttypeAgreed = False
                End Select

            Case Telnet.DONT
                Select Case opt
                    Case Telnet.OPT_BINARY : _binaryAgreed = False
                    Case Telnet.OPT_EOR : _eorAgreed = False
                End Select
        End Select

        CheckNegotiationComplete()
    End Sub

    Private Sub HandleSubNegotiation(buffer() As Byte, offset As Integer, count As Integer)
        If count < 1 Then Return
        Dim opt = buffer(offset)

        If opt = Telnet.OPT_TERMINAL_TYPE AndAlso count >= 3 AndAlso buffer(offset + 1) = Telnet.TTYPE_IS Then
            TerminalType = Encoding.ASCII.GetString(buffer, offset + 2, count - 2).Trim()
            _ttypeAgreed = True

            ' Infer model size from common terminal-type strings
            Dim tt = TerminalType.ToUpperInvariant()
            If tt.Contains("3279-5") OrElse tt.Contains("3278-5") OrElse tt.Contains("DYNAMIC") Then
                Rows = 27 : Columns = 132
            ElseIf tt.Contains("3279-4") OrElse tt.Contains("3278-4") Then
                Rows = 43 : Columns = 80
            ElseIf tt.Contains("3279-3") OrElse tt.Contains("3278-3") Then
                Rows = 32 : Columns = 80
            ElseIf tt.Contains("3279-2") OrElse tt.Contains("3278-2") OrElse tt.Contains("IBM-3278") Then
                Rows = 24 : Columns = 80
            End If

            CheckNegotiationComplete()
        ElseIf opt = Telnet.OPT_NAWS AndAlso count >= 5 Then
            ' NAWS: width (2 bytes) + height (2 bytes), network byte order
            Dim width = (CInt(buffer(offset + 1)) << 8) Or buffer(offset + 2)
            Dim height = (CInt(buffer(offset + 3)) << 8) Or buffer(offset + 4)
            If width > 0 AndAlso height > 0 Then
                Columns = width
                Rows = height
            End If
        End If
    End Sub

    Private Sub CheckNegotiationComplete()
        If IsNegotiated Then
            RaiseEvent NegotiationComplete(Me, EventArgs.Empty)
        End If
    End Sub

    Public Sub SendTelnetCommand(action As Byte, optionCode As Byte)
        SendRaw({Telnet.IAC, action, optionCode})
    End Sub

#End Region

#Region "Inbound field parsing"

    Private Sub ParseInboundFields(buffer() As Byte, startIndex As Integer, length As Integer)
        Dim seenFields As New HashSet(Of TN3270Field)
        Dim i = startIndex
        Dim currentField As TN3270Field = Nothing

        While i < length
            Dim b = buffer(i)

            If b = Orders.SetBufferAddress AndAlso i + 2 < length Then
                Dim addr = AddressCodec.Decode(buffer(i + 1), buffer(i + 2))
                ' Terminals send the address of the first data character (Attribute + 1)
                currentField = _fields.
                    OrderByDescending(Function(f) f.Address).
                    FirstOrDefault(Function(f) f.Address < addr)

                If currentField IsNot Nothing AndAlso Not seenFields.Contains(currentField) Then
                    currentField.Content = ""
                    currentField.Modified = True
                    seenFields.Add(currentField)
                End If
                i += 3
            ElseIf currentField IsNot Nothing Then
                ' Accumulate data characters (stop at IAC or next order)
                If b = Telnet.IAC OrElse b = Orders.SetBufferAddress OrElse b = Orders.StartField Then
                    currentField = Nothing
                    Continue While
                End If
                Dim ch = Ebcdic.GetString({b})
                If currentField.Content.Length < currentField.Length Then
                    currentField.Content &= ch
                End If
                i += 1
            Else
                i += 1
            End If
        End While
    End Sub

    Private Sub ProcessStructuredField(buffer() As Byte, startIndex As Integer, length As Integer)
        Dim count = Math.Max(0, length - startIndex)
        Dim data(count - 1) As Byte
        If count > 0 Then Array.Copy(buffer, startIndex, data, 0, count)
        RaiseEvent StructuredFieldReceived(Me, New StructuredFieldEventArgs(data))
    End Sub

#End Region

#Region "High-level screen API"

    Public Sub ClearFields()
        _fields.Clear()
    End Sub

    Public Function AddField(row As Integer,
                             col As Integer,
                             length As Integer,
                             Optional content As String = "",
                             Optional isProtected As Boolean = True,
                             Optional foreground As Byte = Color.White,
                             Optional background As Byte = Color.Neutral,
                             Optional highlighting As Byte = Highlight.None,
                             Optional name As String = "",
                             Optional intensity As FieldIntensity = FieldIntensity.Normal) As TN3270Field

        Dim f As New TN3270Field(Me) With {
            .Row = row,
            .Col = col,
            .Length = length,
            .Content = If(content, ""),
            .IsProtected = isProtected,
            .ForegroundColor = foreground,
            .BackgroundColor = background,
            .Highlighting = highlighting,
            .Intensity = intensity,
            .Name = If(name, "")
        }

        ' Hidden (non-display) fields are non-detectable: the operator cannot set MDT.
        ' Pre-arm MDT so Read Modified still returns their content.
        If intensity = FieldIntensity.Hidden Then
            f.Modified = True
        End If

        _fields.Add(f)
        Return f
    End Function

    ''' <summary>Legacy overload accepting the old TN3270Intensity enum.</summary>
    Public Function AddField(row As Integer,
                             col As Integer,
                             length As Integer,
                             content As String,
                             isProtected As Boolean,
                             foreground As Byte,
                             background As Byte,
                             highlighting As Byte,
                             name As String,
                             intensity As TN3270Intensity) As TN3270Field
        Return AddField(row, col, length, content, isProtected, foreground, background, highlighting, name, CType(intensity, FieldIntensity))
    End Function

    Public Sub WriteText(row As Integer, col As Integer, text As String,
                         Optional foreground As Byte = Color.White,
                         Optional background As Byte = Color.Neutral)
        AddField(row, col, text.Length, text, True, foreground, background)
    End Sub

    Public Function GetFieldByName(name As String) As TN3270Field
        Return _fields.FirstOrDefault(Function(f) String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Function GetFieldValue(name As String) As String
        Dim f = GetFieldByName(name)
        Return If(f?.Content, Nothing)
    End Function

    Public Function GetFieldAt(row As Integer, col As Integer) As TN3270Field
        Dim addr = (((row - 1) * Columns) + (col - 1)) Mod ScreenSize
        Return _fields.FirstOrDefault(Function(f) f.Address < addr AndAlso addr <= f.Address + f.Length)
    End Function

    Public Function GetNextUnprotectedField(currentField As TN3270Field) As TN3270Field
        Dim unprotected = _fields.Where(Function(f) Not f.IsProtected).OrderBy(Function(f) f.Address).ToList()
        If unprotected.Count = 0 Then Return Nothing
        If currentField Is Nothing Then Return unprotected(0)

        Dim idx = unprotected.FindIndex(Function(f) f Is currentField)
        If idx < 0 OrElse idx = unprotected.Count - 1 Then Return unprotected(0)
        Return unprotected(idx + 1)
    End Function

    Public Function GetModifiedFields() As List(Of TN3270Field)
        Return _fields.Where(Function(f) f.Modified).ToList()
    End Function

    Public Sub ClearModifiedTags()
        For Each field In _fields
            ' Keep hidden fields pre-armed so they remain returned on Read Modified
            field.Modified = (field.Intensity = FieldIntensity.Hidden)
        Next
    End Sub

    Public Sub ShowScreen(Optional clearScreen As Boolean = True)
        Dim buffer As New List(Of Byte)

        Dim cmd As Byte
        If clearScreen Then
            cmd = If(Columns > 80 OrElse Rows > 24, Commands.EraseWriteAlternate, Commands.EraseWrite)
        Else
            cmd = Commands.Write
        End If
        buffer.Add(cmd)

        ' WCC: restore keyboard, preserve MDT (&HC2)
        buffer.Add(&HC2)

        For Each f In _fields
            buffer.Add(Orders.SetBufferAddress)
            buffer.AddRange(AddressCodec.Encode(f.Address))

            If f.HasExtendedAttributes Then
                Dim pairs As New List(Of Byte)
                pairs.Add(ExtendedAttributeType.FieldAttribute)
                pairs.Add(f.GetAttributeByte())

                If f.ForegroundColor <> Color.Neutral Then
                    pairs.Add(ExtendedAttributeType.ForegroundColor)
                    pairs.Add(f.ForegroundColor)
                End If
                If f.BackgroundColor <> Color.Neutral Then
                    pairs.Add(ExtendedAttributeType.BackgroundColor)
                    pairs.Add(f.BackgroundColor)
                End If
                If f.Highlighting <> Highlight.None Then
                    pairs.Add(ExtendedAttributeType.Highlight)
                    pairs.Add(f.Highlighting)
                End If
                If f.FieldTransparency <> Transparency.None Then
                    pairs.Add(ExtendedAttributeType.Transparency)
                    pairs.Add(f.FieldTransparency)
                End If

                buffer.Add(Orders.StartFieldExtended)
                buffer.Add(CByte(pairs.Count \ 2))
                buffer.AddRange(pairs)
            Else
                buffer.Add(Orders.StartField)
                buffer.Add(f.GetAttributeByte())
            End If

            If Not String.IsNullOrEmpty(f.Content) Then
                Dim data = Ebcdic.GetBytes(f.Content)
                Dim take = Math.Min(data.Length, f.Length)
                For n = 0 To take - 1
                    buffer.Add(data(n))
                Next
            End If

            ' Terminator attribute so intensity/color does not bleed into following cells
            Dim termAddr = (f.Address + f.Length + 1) Mod ScreenSize
            Dim collision = _fields.Any(Function(other) other.Address = termAddr OrElse
                                                       (termAddr > other.Address AndAlso termAddr <= other.Address + other.Length))
            If Not collision Then
                buffer.Add(Orders.SetBufferAddress)
                buffer.AddRange(AddressCodec.Encode(termAddr))
                buffer.Add(Orders.StartField)
                buffer.Add(&H60) ' protected, normal, alphanumeric
            End If
        Next

        ' Place cursor on first unprotected field
        Dim firstUnprotected = _fields.FirstOrDefault(Function(f) Not f.IsProtected)
        If firstUnprotected IsNot Nothing Then
            buffer.Add(Orders.SetBufferAddress)
            buffer.AddRange(AddressCodec.Encode(firstUnprotected.Address + 1))
            buffer.Add(Orders.InsertCursor)
        End If

        buffer.Add(Telnet.IAC)
        buffer.Add(Telnet.EOR)
        SendRaw(buffer.ToArray())
        RaiseEvent ScreenUpdated(Me, EventArgs.Empty)
    End Sub

#End Region

#Region "Host-initiated read / erase commands"

    Public Sub EraseAllUnprotected()
        SendRaw({Commands.EraseAllUnprotected, &HC3, Telnet.IAC, Telnet.EOR})
    End Sub

    Public Sub ReadModified()
        SendRaw({Commands.ReadModified, Telnet.IAC, Telnet.EOR})
    End Sub

    Public Sub ReadModifiedAll()
        SendRaw({Commands.ReadModifiedAll, Telnet.IAC, Telnet.EOR})
    End Sub

    Public Sub ReadBuffer()
        SendRaw({Commands.ReadBuffer, Telnet.IAC, Telnet.EOR})
    End Sub

    Public Sub SendReadPartitionQuery()
        ' WSF + length + Read Partition Query
        Dim cmd As Byte() = {Commands.WriteStructuredField, 0, 5, 1, &HFF, Telnet.IAC, Telnet.EOR}
        SendRaw(cmd)
    End Sub

    Public Sub WriteStructuredField(data As Byte())
        Dim totalLen = data.Length + 2
        Dim buffer As New List(Of Byte)
        buffer.Add(Commands.WriteStructuredField)
        buffer.Add(CByte((totalLen >> 8) And &HFF))
        buffer.Add(CByte(totalLen And &HFF))
        buffer.AddRange(data)
        buffer.Add(Telnet.IAC)
        buffer.Add(Telnet.EOR)
        SendRaw(buffer.ToArray())
    End Sub

    Public Sub SetCharacterSet(codePage As String)
        Ebcdic = CharacterSets.GetEncoding(codePage)
    End Sub

#End Region

#Region "Transport"

    Private Sub SendRaw(data As Byte())
        Try
            If _client.Connected AndAlso _stream.CanWrite Then
                _stream.Write(data, 0, data.Length)
                _stream.Flush()
            End If
        Catch
        End Try
    End Sub

#End Region

End Class
