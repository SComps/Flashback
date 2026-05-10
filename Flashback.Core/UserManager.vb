Imports System.IO
Imports System.Linq

Namespace Flashback.Core
    Public Class UserManager
        Private Shared ReadOnly _usersFile As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users.dat")
        Private Shared _users As List(Of UserInfo)

        Public Shared Function GetUsers() As List(Of UserInfo)
            If _users Is Nothing Then LoadUsers()
            Return _users
        End Function

        Public Shared Sub LoadUsers()
            _users = New List(Of UserInfo)
            If Not File.Exists(_usersFile) Then Return
            
            Try
                Dim lines = File.ReadAllLines(_usersFile)
                For Each line In lines
                    Dim u = UserInfo.FromConfigLine(line)
                    If u IsNot Nothing Then _users.Add(u)
                Next
            Catch
            End Try
        End Sub

        Public Shared Sub SaveUsers()
            If _users Is Nothing Then Return
            Try
                File.WriteAllLines(_usersFile, _users.Select(Function(u) u.ToConfigLine()))
            Catch
            End Try
        End Sub

        Public Shared Function Authenticate(username As String, password As String) As UserInfo
            If _users Is Nothing Then LoadUsers()
            Dim user = _users.FirstOrDefault(Function(u) u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            If user IsNot Nothing Then
                If SecurityUtils.VerifyPassword(password, user.Salt, user.PasswordHash) Then
                    Return user
                End If
            End If
            Return Nothing
        End Function

        Public Shared Sub AddUser(username As String, password As String, Optional homeFolder As String = "")
            If _users Is Nothing Then LoadUsers()
            
            ' Remove existing if any
            Dim existing = _users.FirstOrDefault(Function(u) u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            If existing IsNot Nothing Then _users.Remove(existing)
            
            Dim salt = SecurityUtils.GenerateSalt()
            Dim hash = SecurityUtils.HashPassword(password, salt)
            
            Dim newUser As New UserInfo With {
                .Username = username,
                .PasswordHash = hash,
                .Salt = salt,
                .HomeFolder = homeFolder
            }
            _users.Add(newUser)
            SaveUsers()
        End Sub

        Public Shared Sub DeleteUser(username As String)
            If _users Is Nothing Then LoadUsers()
            Dim user = _users.FirstOrDefault(Function(u) u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            If user IsNot Nothing Then
                _users.Remove(user)
                SaveUsers()
            End If
        End Sub
    End Class
End Namespace
