Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.IO

Public Class SecurityUtils
    Public Shared Function SanitizeFilename(input As String) As String
        If String.IsNullOrWhiteSpace(input) Then Return "Unknown"
        
        ' Whitelist: only allow characters that are safe in both filenames AND URLs.
        ' This prevents issues with #, %, &, +, = etc. that are valid in filenames
        ' but cause problems when served through web/nginx.
        Dim cleanBuilder As New StringBuilder(input.Length)
        
        For Each c As Char In input
            If Char.IsLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c OrElse c = "."c Then
                cleanBuilder.Append(c)
            Else
                cleanBuilder.Append("_"c)
            End If
        Next
        
        Dim clean = cleanBuilder.ToString().Trim("."c)
        If String.IsNullOrWhiteSpace(clean) Then Return "Unknown"
        Return clean
    End Function

    Public Shared Function GenerateSalt() As String
        Dim bytes(15) As Byte
        RandomNumberGenerator.Fill(bytes)
        Return Convert.ToBase64String(bytes)
    End Function

    Public Shared Function HashPassword(password As String, salt As String) As String
        Dim combined = password & salt
        Dim bytes = Encoding.UTF8.GetBytes(combined)
        Using sha As SHA256 = SHA256.Create()
            Dim hash = sha.ComputeHash(bytes)
            Return Convert.ToBase64String(hash)
        End Using
    End Function

    Public Shared Function VerifyPassword(password As String, salt As String, hash As String) As Boolean
        Dim newHash = HashPassword(password, salt)
        Return newHash = hash
    End Function
End Class
