Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json

Public Class LicenseInfo
    Public Property LicensedTo As String = "FREE NON-COMMERCIAL USE"
    Public Property MaxPrinters As Integer = 2
    Public Property IsLicensed As Boolean = False
End Class

Public Class LicenseManager
    Private Shared ReadOnly Key As Byte() = Encoding.UTF8.GetBytes("Fl@shB@ck2026!Prn") 
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
            Dim info = JsonSerializer.Deserialize(Of LicenseInfo)(decryptedText)
            info.IsLicensed = True
            Return info
        Catch
            Return New LicenseInfo()
        End Try
    End Function

    Public Shared Sub GenerateLicense(userName As String, printerCount As Integer, outPath As String)
        Dim info As New LicenseInfo With {
            .LicensedTo = userName,
            .MaxPrinters = printerCount,
            .IsLicensed = True
        }
        Dim json = JsonSerializer.Serialize(info)
        Dim encrypted = Encrypt(json)
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
