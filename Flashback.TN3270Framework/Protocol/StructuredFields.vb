Imports System.Text

Namespace Protocol
    ''' <summary>Well-known structured-field / Query Reply type codes.</summary>
    Public NotInheritable Class StructuredFieldTypes
        Private Sub New()
        End Sub

        ' Outbound (host → terminal) request types inside WSF / Read Partition
        Public Const ReadPartition As Byte = &H1
        Public Const Query As Byte = &HFF
        Public Const QueryList As Byte = &H2

        ' Inbound Query Reply types (terminal → host), SFID after length
        Public Const QueryReply As Byte = &H81

        ' Query Reply subtype codes (QCODE)
        Public Const QCodeSummary As Byte = &H80
        Public Const QCodeUsableArea As Byte = &H81
        Public Const QCodeImage As Byte = &H82
        Public Const QCodeTextPartitions As Byte = &H83
        Public Const QCodeAlphabetics As Byte = &H84
        Public Const QCodeColor As Byte = &H86
        Public Const QCodeHighlight As Byte = &H87
        Public Const QCodeReplyModes As Byte = &H88
        Public Const QCodeOemAuxDevice As Byte = &H8F
        Public Const QCodeDistributedDataManagement As Byte = &H91
        Public Const QCodeStoragePools As Byte = &H96
        Public Const QCodeOemProprietary As Byte = &H99
        Public Const QCodeDataStreams As Byte = &HA2
        Public Const QCodeImpPriv As Byte = &HA6
        Public Const QCodeCharacterSets As Byte = &HB0
        Public Const QCodeNull As Byte = &HFF
    End Class

    ''' <summary>Parsed Usable Area Query Reply (QCODE 0x81).</summary>
    Public Class UsableAreaReply
        Public Property Units As Byte
        Public Property AddressMode As Byte
        Public Property WidthChars As Integer
        Public Property HeightChars As Integer
        Public Property XUnitsPerChar As Integer
        Public Property YUnitsPerChar As Integer
        Public Property BufferSize As Integer
        Public Property Supports12BitAddressing As Boolean
        Public Property Supports14BitAddressing As Boolean
        Public Property Supports16BitAddressing As Boolean

        Public Overrides Function ToString() As String
            Return $"UsableArea {WidthChars}x{HeightChars} buf={BufferSize} addr12={Supports12BitAddressing} addr14={Supports14BitAddressing}"
        End Function
    End Class

    ''' <summary>Parsed Color Query Reply (QCODE 0x86) — summary only.</summary>
    Public Class ColorReply
        Public Property AcceptsColor As Boolean
        Public Property AttributeTypes As Byte
        Public Property ColorPairs As New List(Of (Attr As Byte, Value As Byte))

        Public Overrides Function ToString() As String
            Return $"Color accepts={AcceptsColor} pairs={ColorPairs.Count}"
        End Function
    End Class

    ''' <summary>Parsed Highlight Query Reply (QCODE 0x87) — summary only.</summary>
    Public Class HighlightReply
        Public Property AttributeTypes As Byte
        Public Property Supported As New List(Of Byte)

        Public ReadOnly Property SupportsBlink As Boolean
            Get
                Return Supported.Contains(Highlight.Blink)
            End Get
        End Property
        Public ReadOnly Property SupportsReverse As Boolean
            Get
                Return Supported.Contains(Highlight.ReverseVideo)
            End Get
        End Property
        Public ReadOnly Property SupportsUnderline As Boolean
            Get
                Return Supported.Contains(Highlight.Underline)
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return $"Highlight count={Supported.Count} blink={SupportsBlink} reverse={SupportsReverse} ul={SupportsUnderline}"
        End Function
    End Class

    ''' <summary>Aggregate of Query Reply data learned from a terminal.</summary>
    Public Class QueryReplySummary
        Public Property RawReplies As New List(Of Byte())
        Public Property UsableArea As UsableAreaReply
        Public Property Color As ColorReply
        Public Property Highlight As HighlightReply
        Public Property SummaryCodes As New List(Of Byte)
        Public Property CharacterSetCodes As New List(Of Byte)

        Public ReadOnly Property HasUsableArea As Boolean
            Get
                Return UsableArea IsNot Nothing
            End Get
        End Property
        Public ReadOnly Property HasColor As Boolean
            Get
                Return Color IsNot Nothing AndAlso Color.AcceptsColor
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Parses inbound 3270 structured-field payloads, especially Query Replies
    ''' that follow AID 0x88 (Structured Field).
    ''' </summary>
    Public NotInheritable Class StructuredFieldParser
        Private Sub New()
        End Sub

        ''' <summary>
        ''' Parse a buffer that begins at the first structured-field byte after the AID
        ''' (i.e. typical layout: AID=0x88, then one or more [len-hi][len-lo][SFID][data...]).
        ''' </summary>
        Public Shared Function ParseQueryReplies(buffer() As Byte, offset As Integer, length As Integer) As QueryReplySummary
            Dim summary As New QueryReplySummary
            Dim i = offset
            Dim endPos = Math.Min(buffer.Length, offset + length)

            While i + 3 <= endPos
                Dim len = (CInt(buffer(i)) << 8) Or buffer(i + 1)
                If len < 3 Then Exit While
                If i + len > endPos Then
                    ' Truncated — take what we can for raw storage
                    len = endPos - i
                End If

                Dim sfid = buffer(i + 2)
                Dim payloadStart = i + 3
                Dim payloadLen = Math.Max(0, len - 3)
                Dim raw(len - 1) As Byte
                Array.Copy(buffer, i, raw, 0, len)
                summary.RawReplies.Add(raw)

                If sfid = StructuredFieldTypes.QueryReply AndAlso payloadLen >= 1 Then
                    Dim qcode = buffer(payloadStart)
                    Select Case qcode
                        Case StructuredFieldTypes.QCodeSummary
                            ParseSummary(buffer, payloadStart + 1, payloadLen - 1, summary)
                        Case StructuredFieldTypes.QCodeUsableArea
                            summary.UsableArea = ParseUsableArea(buffer, payloadStart + 1, payloadLen - 1)
                        Case StructuredFieldTypes.QCodeColor
                            summary.Color = ParseColor(buffer, payloadStart + 1, payloadLen - 1)
                        Case StructuredFieldTypes.QCodeHighlight
                            summary.Highlight = ParseHighlight(buffer, payloadStart + 1, payloadLen - 1)
                        Case StructuredFieldTypes.QCodeCharacterSets
                            ParseCharacterSets(buffer, payloadStart + 1, payloadLen - 1, summary)
                    End Select
                End If

                i += Math.Max(len, 1)
            End While

            Return summary
        End Function

        Private Shared Sub ParseSummary(buffer() As Byte, start As Integer, count As Integer, summary As QueryReplySummary)
            For n = 0 To count - 1
                If start + n < buffer.Length Then
                    summary.SummaryCodes.Add(buffer(start + n))
                End If
            Next
        End Sub

        Private Shared Function ParseUsableArea(buffer() As Byte, start As Integer, count As Integer) As UsableAreaReply
            ' Layout (common): flags(1) + addr flags / units + width(2) + height(2) + ...
            If count < 5 Then Return Nothing
            Dim r As New UsableAreaReply
            r.Units = buffer(start)
            If count >= 2 Then r.AddressMode = buffer(start + 1)

            ' Width / height are often 2-byte big-endian character counts
            If count >= 6 Then
                r.WidthChars = (CInt(buffer(start + 2)) << 8) Or buffer(start + 3)
                r.HeightChars = (CInt(buffer(start + 4)) << 8) Or buffer(start + 5)
            End If
            If count >= 9 Then
                r.XUnitsPerChar = buffer(start + 6)
                r.YUnitsPerChar = buffer(start + 7)
            End If
            If count >= 11 Then
                r.BufferSize = (CInt(buffer(start + 9)) << 8) Or buffer(start + 10)
            ElseIf r.WidthChars > 0 AndAlso r.HeightChars > 0 Then
                r.BufferSize = r.WidthChars * r.HeightChars
            End If

            ' Address capability flags vary by emulator; derive sensible defaults from size
            r.Supports12BitAddressing = True
            r.Supports14BitAddressing = (r.BufferSize > 4096) OrElse (r.WidthChars * r.HeightChars > 4096)
            r.Supports16BitAddressing = False
            Return r
        End Function

        Private Shared Function ParseColor(buffer() As Byte, start As Integer, count As Integer) As ColorReply
            Dim r As New ColorReply With {.AcceptsColor = count > 0}
            If count < 1 Then Return r
            r.AttributeTypes = buffer(start)
            Dim n = start + 1
            While n + 1 < start + count
                r.ColorPairs.Add((buffer(n), buffer(n + 1)))
                n += 2
            End While
            Return r
        End Function

        Private Shared Function ParseHighlight(buffer() As Byte, start As Integer, count As Integer) As HighlightReply
            Dim r As New HighlightReply
            If count < 1 Then Return r
            r.AttributeTypes = buffer(start)
            For n = 1 To count - 1
                r.Supported.Add(buffer(start + n))
            Next
            Return r
        End Function

        Private Shared Sub ParseCharacterSets(buffer() As Byte, start As Integer, count As Integer, summary As QueryReplySummary)
            For n = 0 To count - 1
                If start + n < buffer.Length Then
                    summary.CharacterSetCodes.Add(buffer(start + n))
                End If
            Next
        End Sub
    End Class
End Namespace
