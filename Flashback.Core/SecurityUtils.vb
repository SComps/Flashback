Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.IO

Public Class SecurityUtils
    Public Shared Function SanitizeFilename(input As String) As String
        If String.IsNullOrWhiteSpace(input) Then Return "Unknown"
        
        Dim invalidChars = Path.GetInvalidFileNameChars().Concat({Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar}).Distinct().ToArray()
        Dim cleanBuilder As New StringBuilder(input.Length)
        
        For Each c As Char In input
            If invalidChars.Contains(c) Then
                cleanBuilder.Append("_"c)
            Else
                cleanBuilder.Append(c)
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
