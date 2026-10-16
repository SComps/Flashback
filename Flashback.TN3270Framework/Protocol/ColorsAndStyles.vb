Namespace Protocol
    ''' <summary>3270 extended highlighting values.</summary>
    Public NotInheritable Class Highlight
        Private Sub New()
        End Sub

        Public Const None As Byte = &H0
        Public Const Blink As Byte = &HF1
        Public Const ReverseVideo As Byte = &HF2
        Public Const Underline As Byte = &HF4
    End Class

    ''' <summary>3270 base and extended color values.</summary>
    Public NotInheritable Class Color
        Private Sub New()
        End Sub

        Public Const Neutral As Byte = &H0
        Public Const Blue As Byte = &HF1
        Public Const Red As Byte = &HF2
        Public Const Pink As Byte = &HF3
        Public Const Green As Byte = &HF4
        Public Const Turquoise As Byte = &HF5
        Public Const Yellow As Byte = &HF6
        Public Const White As Byte = &HF7
    End Class

    ''' <summary>3270 transparency attribute values.</summary>
    Public NotInheritable Class Transparency
        Private Sub New()
        End Sub

        Public Const None As Byte = &H0
        Public Const Transparent As Byte = &HF0
        Public Const Opaque As Byte = &HF1
    End Class

    ''' <summary>Field intensity / visibility.</summary>
    Public Enum FieldIntensity
        Normal = 0
        High = 1
        Hidden = 2
    End Enum
End Namespace
