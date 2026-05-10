Namespace Flashback.Core
    Public Class UserInfo
        Public Property Username As String = ""
        Public Property PasswordHash As String = ""
        Public Property Salt As String = ""
        Public Property HomeFolder As String = "" ' Optional restriction

        Public Function ToConfigLine() As String
            Return $"{Username}||{PasswordHash}||{Salt}||{HomeFolder}"
        End Function

        Public Shared Function FromConfigLine(line As String) As UserInfo
            If String.IsNullOrWhiteSpace(line) Then Return Nothing
            Dim p = line.Split("||")
            If p.Length < 3 Then Return Nothing
            
            Return New UserInfo With {
                .Username = p(0),
                .PasswordHash = p(1),
                .Salt = p(2),
                .HomeFolder = If(p.Length >= 4, p(3), "")
            }
        End Function
    End Class
End Namespace
