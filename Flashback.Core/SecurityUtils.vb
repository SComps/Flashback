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

    ''' <summary>
    ''' Resolves and reads the system password file from the given base directory.
    ''' Tries the following filenames in order: SYSPW, syspw, SYSPW.txt, syspw.txt.
    ''' This covers the canonical name, the lowercase variant (Linux case-sensitive
    ''' filesystems), and both .txt fallbacks for existing deployments.
    ''' Returns String.Empty if no file is found (open access mode).
    ''' Maximum enforced password length is 25 characters.
    ''' </summary>
    Public Shared Function ReadSyspw(baseDir As String) As String
        Dim candidates = {"SYSPW", "syspw", "SYSPW.txt", "syspw.txt"}
        For Each name In candidates
            Dim filePath = Path.Combine(baseDir, name)
            If File.Exists(filePath) Then
                Return File.ReadAllText(filePath).Trim()
            End If
        Next
        Return String.Empty
    End Function
End Class
