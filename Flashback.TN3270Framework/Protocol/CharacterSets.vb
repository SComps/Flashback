Imports System.Text

Namespace Protocol
    ''' <summary>
    ''' EBCDIC code-page helpers for international 3270 sessions.
    ''' </summary>
    Public NotInheritable Class CharacterSets
        Private Sub New()
        End Sub

        Shared Sub New()
            ' Register once at class initialisation — RegisterProvider is idempotent
            ' but acquires a lock internally, so calling it per-request is wasteful.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        End Sub

        ''' <summary>
        ''' Resolve a friendly name or IBM code-page identifier to a .NET Encoding.
        ''' Falls back to IBM037 (US/Canada) when the requested set is unknown.
        ''' </summary>
        Public Shared Function GetEncoding(codePage As String) As Encoding
            Select Case codePage.ToUpperInvariant()
                Case "IBM037", "US", "USA", "CANADA"
                    Return Encoding.GetEncoding("IBM037")
                Case "IBM273", "GERMAN", "AUSTRIAN"
                    Return Encoding.GetEncoding("IBM273")
                Case "IBM277", "DANISH", "NORWEGIAN"
                    Return Encoding.GetEncoding("IBM277")
                Case "IBM278", "FINNISH", "SWEDISH"
                    Return Encoding.GetEncoding("IBM278")
                Case "IBM280", "ITALIAN"
                    Return Encoding.GetEncoding("IBM280")
                Case "IBM284", "SPANISH"
                    Return Encoding.GetEncoding("IBM284")
                Case "IBM285", "UK", "GB"
                    Return Encoding.GetEncoding("IBM285")
                Case "IBM297", "FRENCH"
                    Return Encoding.GetEncoding("IBM297")
                Case "IBM500", "INTERNATIONAL", "INTL"
                    Return Encoding.GetEncoding("IBM500")
                Case Else
                    Return Encoding.GetEncoding("IBM037")
            End Select
        End Function

        ''' <summary>Human-readable label for a numeric code-page identifier.</summary>
        Public Shared Function GetCodePageName(codePage As Integer) As String
            Select Case codePage
                Case 37 : Return "US/Canada (IBM037)"
                Case 273 : Return "German/Austrian (IBM273)"
                Case 277 : Return "Danish/Norwegian (IBM277)"
                Case 278 : Return "Finnish/Swedish (IBM278)"
                Case 280 : Return "Italian (IBM280)"
                Case 284 : Return "Spanish (IBM284)"
                Case 285 : Return "UK (IBM285)"
                Case 297 : Return "French (IBM297)"
                Case 500 : Return "International (IBM500)"
                Case Else : Return $"Unknown ({codePage})"
            End Select
        End Function
    End Class
End Namespace
