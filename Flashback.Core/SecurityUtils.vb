Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.IO

Public Class SecurityUtils
    Public Shared Function SanitizeFilename(input As String) As String
        If String.IsNullOrWhiteSpace(input) Then Return "Unknown"
        
        Dim filename = Path.GetFileName(input)
        Dim invalidChars = Path.GetInvalidFileNameChars().Concat({Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar}).Distinct().ToArray()
        Dim clean = New String(filename.Where(Function(c) Not invalidChars.Contains(c)).ToArray())
        
        clean = clean.Trim("."c)
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
