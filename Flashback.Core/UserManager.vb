Imports System.IO
Imports System.Linq

Public Class UserManager
    Private Shared ReadOnly _usersFile As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users.dat")
    Private Shared _users As List(Of UserInfo)
    Private Shared ReadOnly _lock As New Object()

    ''' <summary>Returns a snapshot of the current user list. Safe to enumerate without holding the lock.</summary>
    Public Shared Function GetUsers() As List(Of UserInfo)
        SyncLock _lock
            If _users Is Nothing Then LoadUsersInternal()
            Return New List(Of UserInfo)(_users)
        End SyncLock
    End Function

    Public Shared Sub LoadUsers()
        SyncLock _lock
            LoadUsersInternal()
        End SyncLock
    End Sub

    ''' <summary>Must be called while holding _lock.</summary>
    Private Shared Sub LoadUsersInternal()
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
        SyncLock _lock
            If _users Is Nothing Then Return
            Try
                File.WriteAllLines(_usersFile, _users.Select(Function(u) u.ToConfigLine()))
            Catch
            End Try
        End SyncLock
    End Sub

    Public Shared Function Authenticate(username As String, password As String) As UserInfo
        SyncLock _lock
            LoadUsersInternal() ' Reload inside the lock to pick up changes from config tools
            Dim user = _users.FirstOrDefault(Function(u) u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            If user IsNot Nothing Then
                If SecurityUtils.VerifyPassword(password, user.Salt, user.PasswordHash) Then
                    Return user
                End If
            End If
        End SyncLock
        Return Nothing
    End Function

    Public Shared Sub AddUser(username As String, password As String, Optional homeFolder As String = "")
        SyncLock _lock
            If _users Is Nothing Then LoadUsersInternal()
            Dim existing = _users.FirstOrDefault(Function(u) u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            If existing IsNot Nothing Then _users.Remove(existing)

            Dim salt = SecurityUtils.GenerateSalt()
            Dim hash = SecurityUtils.HashPassword(password, salt)
            _users.Add(New UserInfo With {
                .Username = username,
                .PasswordHash = hash,
                .Salt = salt,
                .HomeFolder = homeFolder
            })
        End SyncLock
        SaveUsers()
    End Sub

    Public Shared Sub DeleteUser(username As String)
        SyncLock _lock
            If _users Is Nothing Then LoadUsersInternal()
            Dim user = _users.FirstOrDefault(Function(u) u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            If user IsNot Nothing Then _users.Remove(user)
        End SyncLock
        SaveUsers()
    End Sub
End Class
