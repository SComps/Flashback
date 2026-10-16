Namespace Protocol
    ''' <summary>
    ''' 3270 data-stream commands (outbound from host).
    ''' </summary>
    Public NotInheritable Class Commands
        Private Sub New()
        End Sub

        Public Const NOP As Byte = &HF0
        Public Const Write As Byte = &HF1
        Public Const ReadBuffer As Byte = &HF2
        Public Const WriteStructuredField As Byte = &HF3
        Public Const EraseWrite As Byte = &HF5
        Public Const ReadModified As Byte = &HF6
        Public Const EraseWriteAlternate As Byte = &H7E
        Public Const ReadModifiedAll As Byte = &H6E
        Public Const EraseAllUnprotected As Byte = &H6F
    End Class

    ''' <summary>
    ''' 3270 orders embedded in the data stream.
    ''' </summary>
    Public NotInheritable Class Orders
        Private Sub New()
        End Sub

        Public Const ProgramTab As Byte = &H5
        Public Const GraphicEscape As Byte = &H8
        Public Const SetBufferAddress As Byte = &H11
        Public Const EraseUnprotectedToAddress As Byte = &H12
        Public Const InsertCursor As Byte = &H13
        Public Const StartField As Byte = &H1D
        Public Const SetAttribute As Byte = &H28
        Public Const StartFieldExtended As Byte = &H29
        Public Const ModifyField As Byte = &H2C
        Public Const RepeatToAddress As Byte = &H3C
    End Class

    ''' <summary>
    ''' Attention Identifier (AID) values returned by the terminal.
    ''' </summary>
    Public NotInheritable Class AidKeys
        Private Sub New()
        End Sub

        Public Const None As Byte = &H60
        Public Const Enter As Byte = &H7D
        Public Const Clear As Byte = &H6D
        Public Const PA1 As Byte = &H6C
        Public Const PA2 As Byte = &H6E
        Public Const PA3 As Byte = &H6B
        Public Const SysReq As Byte = &HF0
        Public Const StructuredField As Byte = &H88

        Public Const PF1 As Byte = &HF1
        Public Const PF2 As Byte = &HF2
        Public Const PF3 As Byte = &HF3
        Public Const PF4 As Byte = &HF4
        Public Const PF5 As Byte = &HF5
        Public Const PF6 As Byte = &HF6
        Public Const PF7 As Byte = &HF7
        Public Const PF8 As Byte = &HF8
        Public Const PF9 As Byte = &HF9
        Public Const PF10 As Byte = &H7A
        Public Const PF11 As Byte = &H7B
        Public Const PF12 As Byte = &H7C
        Public Const PF13 As Byte = &HC1
        Public Const PF14 As Byte = &HC2
        Public Const PF15 As Byte = &HC3
        Public Const PF16 As Byte = &HC4
        Public Const PF17 As Byte = &HC5
        Public Const PF18 As Byte = &HC6
        Public Const PF19 As Byte = &HC7
        Public Const PF20 As Byte = &HC8
        Public Const PF21 As Byte = &HC9
        Public Const PF22 As Byte = &H4A
        Public Const PF23 As Byte = &H4B
        Public Const PF24 As Byte = &H4C

        ''' <summary>Returns a human-readable name for an AID byte.</summary>
        Public Shared Function GetName(aid As Byte) As String
            Select Case aid
                Case Enter : Return "ENTER"
                Case Clear : Return "CLEAR"
                Case PA1 : Return "PA1"
                Case PA2 : Return "PA2"
                Case PA3 : Return "PA3"
                Case SysReq : Return "SYSREQ"
                Case StructuredField : Return "STRUCTURED_FIELD"
                Case PF1 : Return "PF1"
                Case PF2 : Return "PF2"
                Case PF3 : Return "PF3"
                Case PF4 : Return "PF4"
                Case PF5 : Return "PF5"
                Case PF6 : Return "PF6"
                Case PF7 : Return "PF7"
                Case PF8 : Return "PF8"
                Case PF9 : Return "PF9"
                Case PF10 : Return "PF10"
                Case PF11 : Return "PF11"
                Case PF12 : Return "PF12"
                Case PF13 : Return "PF13"
                Case PF14 : Return "PF14"
                Case PF15 : Return "PF15"
                Case PF16 : Return "PF16"
                Case PF17 : Return "PF17"
                Case PF18 : Return "PF18"
                Case PF19 : Return "PF19"
                Case PF20 : Return "PF20"
                Case PF21 : Return "PF21"
                Case PF22 : Return "PF22"
                Case PF23 : Return "PF23"
                Case PF24 : Return "PF24"
                Case Else : Return $"UNKNOWN(0x{aid:X2})"
            End Select
        End Function
    End Class

    ''' <summary>Base 3270 field-attribute bit masks (before graphic mapping).</summary>
    Public NotInheritable Class FieldAttributeBits
        Private Sub New()
        End Sub

        Public Const ProtectedField As Byte = &H20
        Public Const Numeric As Byte = &H10
        Public Const IntensityHigh As Byte = &H8
        Public Const IntensityHidden As Byte = &HC
        Public Const ModifiedDataTag As Byte = &H1
    End Class

    ''' <summary>Extended field attribute type codes (SFE pairs).</summary>
    Public NotInheritable Class ExtendedAttributeType
        Private Sub New()
        End Sub

        Public Const FieldAttribute As Byte = &HC0
        Public Const Highlight As Byte = &H41
        Public Const ForegroundColor As Byte = &H42
        Public Const BackgroundColor As Byte = &H45
        Public Const Transparency As Byte = &H46
    End Class
End Namespace
