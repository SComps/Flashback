Namespace Protocol
    ''' <summary>
    ''' Telnet protocol constants used by TN3270 negotiation.
    ''' </summary>
    Public NotInheritable Class Telnet
        Private Sub New()
        End Sub

        ' Command prefix
        Public Const IAC As Byte = 255

        ' Option negotiation
        Public Const WILL As Byte = 251
        Public Const WONT As Byte = 252
        Public Const DO_OPT As Byte = 253
        Public Const DONT As Byte = 254

        ' Subnegotiation
        Public Const SB As Byte = 250
        Public Const SE As Byte = 240

        ' End of Record (used as TN3270 record delimiter when EOR is negotiated)
        Public Const EOR As Byte = 239

        ' Common options
        Public Const OPT_BINARY As Byte = 0
        Public Const OPT_TERMINAL_TYPE As Byte = 24
        Public Const OPT_EOR As Byte = 25
        Public Const OPT_NAWS As Byte = 31          ' Negotiate About Window Size
        Public Const OPT_TN3270E As Byte = 40      ' TN3270 Enhancements (future)

        ' Terminal-type subnegotiation
        Public Const TTYPE_IS As Byte = 0
        Public Const TTYPE_SEND As Byte = 1
    End Class
End Namespace
