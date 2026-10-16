Namespace Protocol
    ''' <summary>
    ''' Encodes and decodes 3270 buffer addresses using the standard 6-bit graphic code map.
    ''' Supports the classic 12-bit (2-byte) form used by virtually all TN3270 clients.
    ''' </summary>
    Public NotInheritable Class AddressCodec
        Private Sub New()
        End Sub

        ''' <summary>
        ''' IBM 3270 buffer-address code points (64 values).
        ''' Index is the 6-bit value; content is the byte placed on the wire.
        ''' </summary>
        Public Shared ReadOnly CodeMap As Byte() = {
            &H40, &HC1, &HC2, &HC3, &HC4, &HC5, &HC6, &HC7, &HC8, &HC9, &H4A, &H4B, &H4C, &H4D, &H4E, &H4F,
            &H50, &HD1, &HD2, &HD3, &HD4, &HD5, &HD6, &HD7, &HD8, &HD9, &H5A, &H5B, &H5C, &H5D, &H5E, &H5F,
            &H60, &H61, &HE2, &HE3, &HE4, &HE5, &HE6, &HE7, &HE8, &HE9, &H6A, &H6B, &H6C, &H6D, &H6E, &H6F,
            &HF0, &HF1, &HF2, &HF3, &HF4, &HF5, &HF6, &HF7, &HF8, &HF9, &H7A, &H7B, &H7C, &H7D, &H7E, &H7F
        }

        Private Shared ReadOnly _reverseMap As Dictionary(Of Byte, Integer)

        Shared Sub New()
            _reverseMap = New Dictionary(Of Byte, Integer)(64)
            For i = 0 To CodeMap.Length - 1
                If Not _reverseMap.ContainsKey(CodeMap(i)) Then
                    _reverseMap.Add(CodeMap(i), i)
                End If
            Next
        End Sub

        ''' <summary>Encode a linear buffer address into the standard 2-byte 3270 form.</summary>
        Public Shared Function Encode(address As Integer) As Byte()
            Dim b1 = (address >> 6) And &H3F
            Dim b2 = address And &H3F
            Return {CodeMap(b1), CodeMap(b2)}
        End Function

        ''' <summary>Decode a 2-byte 3270 address back to a linear buffer offset.</summary>
        Public Shared Function Decode(b1 As Byte, b2 As Byte) As Integer
            Dim raw1 As Integer
            Dim raw2 As Integer
            If Not _reverseMap.TryGetValue(b1, raw1) Then raw1 = 0
            If Not _reverseMap.TryGetValue(b2, raw2) Then raw2 = 0
            Return (raw1 << 6) Or raw2
        End Function

        ''' <summary>
        ''' Map a raw 6-bit field-attribute value onto a valid 3270 graphic character
        ''' (the form actually transmitted in an SF order).
        ''' </summary>
        Public Shared Function MapAttributeToGraphic(attr As Byte) As Byte
            Return CodeMap(attr And &H3F)
        End Function
    End Class
End Namespace
