Imports System.IO
Imports System.Security.Cryptography
Imports System.Text

Public Class LicenseInfo
    Public Property LicensedTo As String = "FREE NON-COMMERCIAL USE"
    Public Property MaxPrinters As Integer = 2 ' 0 = Unlimited
    Public Property IsLicensed As Boolean = False
    Public Property [Error] As String = ""
End Class

Public Class LicenseManager
    Private Shared ReadOnly Key As Byte() = Encoding.UTF8.GetBytes("Fl@shB@ck2026Prn") 
    Private Shared ReadOnly IV As Byte() = Encoding.UTF8.GetBytes("PrntEngineL1cIV!")

    Public Shared Function GetLicenseInfo() As LicenseInfo
        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
        Dim licPath As String = Path.Combine(baseDir, "flashback.lic")
        
        If Not File.Exists(licPath) Then
            Return New LicenseInfo()
        End If

        Try
            Dim encryptedData = File.ReadAllBytes(licPath)
            Dim decryptedText As String = Decrypt(encryptedData)
            
            ' Manual Parsing (AOT Compatible)
            Dim info As New LicenseInfo()
            Dim parts = decryptedText.Split("|"c)
            If parts.Length >= 2 Then
                info.LicensedTo = parts(0)
                info.MaxPrinters = Val(parts(1))
                info.IsLicensed = True
                Return info
            End If
            
            Throw New Exception("Invalid license format.")
        Catch ex As Exception
            Dim errInfo As New LicenseInfo()
            errInfo.Error = $"{ex.GetType().Name}: {ex.Message}"
            Return errInfo
        End Try
    End Function

    Public Shared Sub GenerateLicense(userName As String, printerCount As Integer, outPath As String)
        ' Simple Pipe-Delimited Format (AOT Compatible)
        Dim data = $"{userName}|{printerCount}"
        Dim encrypted = Encrypt(data)
        File.WriteAllBytes(outPath, encrypted)
    End Sub

    Private Shared Function Encrypt(plainText As String) As Byte()
        Using aesAlg As Aes = Aes.Create()
            aesAlg.Key = Key
            aesAlg.IV = IV
            Dim encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV)
            Using msEncrypt As New MemoryStream()
                Using csEncrypt As New CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)
                    Using swEncrypt As New StreamWriter(csEncrypt)
                        swEncrypt.Write(plainText)
                    End Using
                    Return msEncrypt.ToArray()
                End Using
            End Using
        End Using
    End Function

    Private Shared Function Decrypt(cipherText As Byte()) As String
        Using aesAlg As Aes = Aes.Create()
            aesAlg.Key = Key
            aesAlg.IV = IV
            Dim decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV)
            Using msDecrypt As New MemoryStream(cipherText)
                Using csDecrypt As New CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)
                    Using srDecrypt As New StreamReader(csDecrypt)
                        Return srDecrypt.ReadToEnd()
                    End Using
                End Using
            End Using
        End Using
    End Function
End Class
