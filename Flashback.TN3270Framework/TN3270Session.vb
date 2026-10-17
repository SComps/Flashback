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
    Private _lastQueryReply As QueryReplySummary
    Private _negotiationNotified As Boolean

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

    ''' <summary>Most recent parsed Query Reply summary, if any.</summary>
    Public ReadOnly Property LastQueryReply As QueryReplySummary
        Get
            Return _lastQueryReply
        End Get
    End Property

    ''' <summary>Default WCC used by ShowScreen when none is supplied.</summary>
    Public Property DefaultWcc As WriteControlCharacter = WriteControlCharacter.DefaultPreserveMdt

#End Region

#Region "Events"

    Public Event DataReceived As EventHandler(Of DataReceivedEventArgs)
    Public Event AidKeyReceived As EventHandler(Of AidKeyEventArgs)
    Public Event Disconnected As EventHandler
    Public Event ScreenUpdated As EventHandler
    Public Event NegotiationComplete As EventHandler
    Public Event StructuredFieldReceived As EventHandler(Of StructuredFieldEventArgs)
    Public Event QueryReplyReceived As EventHandler(Of QueryReplyEventArgs)

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
                        ' AID 0x88 — structured fields follow (no cursor address)
                        ' Match classic layout: skip AID then parse length-prefixed SFs
                        ProcessStructuredField(buffer, i + 1, length - (i + 1))
                        i = length
                    ElseIf IsKnownAid(aid) Then
                        ' Only raise for real 3270 AIDs — ignores Telnet/NVT residue
                        Dim cursorAddr = AddressCodec.Decode(buffer(i + 1), buffer(i + 2))
                        ParseInboundFields(buffer, i + 3, length)

                        Dim activeField = _fields.
                            OrderByDescending(Function(f) f.Address).
                            FirstOrDefault(Function(f) f.Address <= cursorAddr AndAlso (f.Address + f.Length) >= cursorAddr)

                        RaiseEvent AidKeyReceived(Me, New AidKeyEventArgs(aid, activeField, cursorAddr))
                        i = length
                    Else
                        ' Not Telnet, not a known AID — skip byte (negotiation tail / noise)
                        i += 1
                    End If
                Else
                    i += 1
                End If
            Else
                ' Not yet negotiated: ignore non-Telnet payload
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
            Case Telnet.EOR
                ' Record boundary — consume IAC EOR, no further action
                Return index + 2
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
            Dim width = (CInt(buffer(offset + 1)) << 8) Or buffer(offset + 2)
            Dim height = (CInt(buffer(offset + 3)) << 8) Or buffer(offset + 4)
            If width > 0 AndAlso height > 0 Then
                Columns = width
                Rows = height
            End If
        End If
    End Sub

    Private Sub CheckNegotiationComplete()
        If IsNegotiated AndAlso Not _negotiationNotified Then
            _negotiationNotified = True
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

            ' Stop at Telnet IAC (should be IAC EOR at end of record)
            If b = Telnet.IAC Then
                Exit While
            End If

            If b = Orders.SetBufferAddress AndAlso i + 2 < length Then
                Dim addr = AddressCodec.Decode(buffer(i + 1), buffer(i + 2))
                currentField = _fields.
                    OrderByDescending(Function(f) f.Address).
                    FirstOrDefault(Function(f) f.Address < addr)

                If currentField IsNot Nothing AndAlso Not seenFields.Contains(currentField) Then
                    currentField.Content = ""
                    currentField.Modified = True
                    seenFields.Add(currentField)
                End If
                i += 3

            ElseIf b = Orders.StartField OrElse b = Orders.StartFieldExtended Then
                ' Host-style orders occasionally appear in inbound; skip attribute payload
                currentField = Nothing
                If b = Orders.StartFieldExtended AndAlso i + 1 < length Then
                    Dim pairCount = buffer(i + 1)
                    i += 2 + (pairCount * 2)
                Else
                    i += 2
                End If

            ElseIf b = Orders.GraphicEscape AndAlso i + 1 < length Then
                ' GE + byte — treat following byte as a graphic if we have a field
                If currentField IsNot Nothing Then
                    Dim ch = Ebcdic.GetString({buffer(i + 1)})
                    If currentField.Content.Length < currentField.Length Then
                        currentField.Content &= ch
                    End If
                End If
                i += 2

            ElseIf b = Orders.ProgramTab OrElse b = Orders.RepeatToAddress OrElse
                   b = Orders.EraseUnprotectedToAddress OrElse b = Orders.InsertCursor OrElse
                   b = Orders.SetAttribute OrElse b = Orders.ModifyField Then
                ' Orders we do not fully interpret inbound: skip conservatively
                currentField = Nothing
                If (b = Orders.RepeatToAddress OrElse b = Orders.EraseUnprotectedToAddress) AndAlso i + 2 < length Then
                    i += 3
                ElseIf b = Orders.SetAttribute AndAlso i + 2 < length Then
                    i += 3
                ElseIf b = Orders.ModifyField AndAlso i + 1 < length Then
                    Dim pairCount = buffer(i + 1)
                    i += 2 + (pairCount * 2)
                Else
                    i += 1
                End If

            ElseIf currentField IsNot Nothing Then
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
        Dim count = Math.Max(0, length)
        Dim data(Math.Max(count - 1, 0)) As Byte
        If count > 0 Then Array.Copy(buffer, startIndex, data, 0, count)

        Dim summary = StructuredFieldParser.ParseQueryReplies(buffer, startIndex, length)
        If summary IsNot Nothing AndAlso summary.RawReplies.Count > 0 Then
            _lastQueryReply = summary
            ApplyQueryReplyToSession(summary)
            RaiseEvent QueryReplyReceived(Me, New QueryReplyEventArgs(summary))
        End If

        RaiseEvent StructuredFieldReceived(Me, New StructuredFieldEventArgs(data, summary))
    End Sub

    ''' <summary>
    ''' Apply Usable Area (and related) Query Reply data to session geometry when present.
    ''' </summary>
    Private Sub ApplyQueryReplyToSession(summary As QueryReplySummary)
        If summary.UsableArea Is Nothing Then Return
        Dim ua = summary.UsableArea
        If ua.WidthChars > 0 AndAlso ua.HeightChars > 0 Then
            Columns = ua.WidthChars
            Rows = ua.HeightChars
        End If
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

        If intensity = FieldIntensity.Hidden Then
            f.Modified = True
        End If

        _fields.Add(f)
        Return f
    End Function

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
            field.Modified = (field.Intensity = FieldIntensity.Hidden)
        Next
    End Sub

    ''' <summary>
    ''' Build and send the current field list.
    ''' </summary>
    ''' <param name="clearScreen">True = Erase/Write (or Alternate); False = Write.</param>
    ''' <param name="wcc">Optional write-control character; defaults to <see cref="DefaultWcc"/>.</param>
    Public Sub ShowScreen(Optional clearScreen As Boolean = True, Optional wcc As WriteControlCharacter = Nothing)
        Dim buffer As New List(Of Byte)

        Dim cmd As Byte
        If clearScreen Then
            cmd = If(Columns > 80 OrElse Rows > 24, Commands.EraseWriteAlternate, Commands.EraseWrite)
        Else
            cmd = Commands.Write
        End If
        buffer.Add(cmd)

        Dim control = If(wcc, DefaultWcc)
        buffer.Add(control.ToByte())

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

            Dim termAddr = (f.Address + f.Length + 1) Mod ScreenSize
            Dim collision = _fields.Any(Function(other) other.Address = termAddr OrElse
                                                       (termAddr > other.Address AndAlso termAddr <= other.Address + other.Length))
            If Not collision Then
                buffer.Add(Orders.SetBufferAddress)
                buffer.AddRange(AddressCodec.Encode(termAddr))
                buffer.Add(Orders.StartField)
                buffer.Add(&H60)
            End If
        Next

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

#Region "Outbound order helpers"

    ''' <summary>
    ''' Emit Repeat to Address: fill from the current buffer position through
    ''' <paramref name="endAddress"/> with <paramref name="fillChar"/> (EBCDIC byte).
    ''' Low-level helper for custom paints; prefer the field API for normal screens.
    ''' </summary>
    Public Sub RepeatToAddress(endAddress As Integer, fillChar As Byte,
                               Optional clearScreen As Boolean = False,
                               Optional wcc As WriteControlCharacter = Nothing)
        Dim buffer As New List(Of Byte)
        buffer.Add(If(clearScreen,
                      If(Columns > 80 OrElse Rows > 24, Commands.EraseWriteAlternate, Commands.EraseWrite),
                      Commands.Write))
        buffer.Add(If(wcc, DefaultWcc).ToByte())
        buffer.Add(Orders.RepeatToAddress)
        buffer.AddRange(AddressCodec.Encode(endAddress Mod ScreenSize))
        buffer.Add(fillChar)
        buffer.Add(Telnet.IAC)
        buffer.Add(Telnet.EOR)
        SendRaw(buffer.ToArray())
    End Sub

    ''' <summary>
    ''' Emit Erase Unprotected to Address: clear unprotected positions up to
    ''' <paramref name="endAddress"/>.
    ''' </summary>
    Public Sub EraseUnprotectedToAddress(endAddress As Integer,
                                         Optional wcc As WriteControlCharacter = Nothing)
        Dim buffer As New List(Of Byte)
        buffer.Add(Commands.Write)
        buffer.Add(If(wcc, DefaultWcc).ToByte())
        buffer.Add(Orders.EraseUnprotectedToAddress)
        buffer.AddRange(AddressCodec.Encode(endAddress Mod ScreenSize))
        buffer.Add(Telnet.IAC)
        buffer.Add(Telnet.EOR)
        SendRaw(buffer.ToArray())
    End Sub

    ''' <summary>
    ''' Position the cursor without rewriting the whole screen.
    ''' </summary>
    Public Sub SetCursor(row As Integer, col As Integer,
                         Optional wcc As WriteControlCharacter = Nothing)
        Dim addr = (((row - 1) * Columns) + (col - 1)) Mod ScreenSize
        Dim buffer As New List(Of Byte)
        buffer.Add(Commands.Write)
        buffer.Add(If(wcc, DefaultWcc).ToByte())
        buffer.Add(Orders.SetBufferAddress)
        buffer.AddRange(AddressCodec.Encode(addr))
        buffer.Add(Orders.InsertCursor)
        buffer.Add(Telnet.IAC)
        buffer.Add(Telnet.EOR)
        SendRaw(buffer.ToArray())
    End Sub

#End Region

#Region "Host-initiated read / erase / query"

    Public Sub EraseAllUnprotected(Optional wcc As WriteControlCharacter = Nothing)
        Dim control = If(wcc, WriteControlCharacter.ResetMdtAndRestore)
        SendRaw({Commands.EraseAllUnprotected, control.ToByte(), Telnet.IAC, Telnet.EOR})
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

    ''' <summary>
    ''' Request Query Reply from the terminal (Read Partition Query).
    ''' Results arrive via StructuredFieldReceived / QueryReplyReceived.
    ''' </summary>
    Public Sub SendReadPartitionQuery()
        Dim cmd As Byte() = {
            Commands.WriteStructuredField, 0, 5,
            StructuredFieldTypes.ReadPartition, StructuredFieldTypes.Query,
            Telnet.IAC, Telnet.EOR
        }
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

    ''' <summary>
    ''' Send a Set‑Model screen‑control command (SCS 0x30) to force the
    ''' client into a specific 3270 model (2 = 80×24, 3 = 80×27, 4 = 132×27).
    ''' </summary>
    ''' <param name="model">Terminal model to request (Model2 = 80×24, Model3 = 80×27, Model4 = 132×27).</param>
    Public Sub SetModel(model As TerminalModel)
        Dim modelNumber As Byte = CByte(model)

        ' Structured‑field header for a generic Screen‑Control command
        '   0x01 – SF identifier
        '   0x84 – SF type = SCS (Screen Control Set)
        '   Length = 6 bytes (header 4 + opcode 1 + parameter 1)
        Dim sfHeader As Byte() = {&H01, &H84, &H00, &H06}
        Dim payload As Byte() = {&H30, modelNumber}   ' 0x30 = Set‑Model
        Dim sf As Byte() = sfHeader.Concat(payload).ToArray()

        WriteStructuredField(sf)
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


    ''' <summary>
    ''' True when <paramref name="aid"/> is a standard 3270 attention identifier.
    ''' Used so Telnet option bytes and NVT data never surface as AidKeyReceived.
    ''' </summary>
    Private Shared Function IsKnownAid(aid As Byte) As Boolean
        Select Case aid
            Case AidKeys.Enter, AidKeys.Clear,
                 AidKeys.PA1, AidKeys.PA2, AidKeys.PA3,
                 AidKeys.SysReq, AidKeys.StructuredField,
                 AidKeys.PF1, AidKeys.PF2, AidKeys.PF3, AidKeys.PF4,
                 AidKeys.PF5, AidKeys.PF6, AidKeys.PF7, AidKeys.PF8,
                 AidKeys.PF9, AidKeys.PF10, AidKeys.PF11, AidKeys.PF12,
                 AidKeys.PF13, AidKeys.PF14, AidKeys.PF15, AidKeys.PF16,
                 AidKeys.PF17, AidKeys.PF18, AidKeys.PF19, AidKeys.PF20,
                 AidKeys.PF21, AidKeys.PF22, AidKeys.PF23, AidKeys.PF24
                Return True
            Case Else
                Return False
        End Select
    End Function

End Class
