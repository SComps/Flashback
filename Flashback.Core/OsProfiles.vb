Imports System.Collections.Generic

Public Class JobInformation
    Public Property JobName As String = "UnknownJob"
    Public Property JobID As String = "0000"
    Public Property User As String = "UnknownUser"

    Public Sub ApplyFallbacks(devName As String)
        If String.IsNullOrWhiteSpace(JobName) OrElse JobName.ToUpper() = "UNKNOWNJOB" Then JobName = "UNKNOWN"
        If String.IsNullOrWhiteSpace(JobID) OrElse JobID = "0000" Then JobID = Now.ToString("HHmmss-ffff")
        If String.IsNullOrWhiteSpace(User) OrElse User.ToUpper() = "UNKNOWNUSER" Then User = If(String.IsNullOrWhiteSpace(devName), "SYSTEM", devName)
    End Sub
End Class

Public Interface IOsProfile
    ReadOnly Property OS As OSType
    ReadOnly Property FirstLine As Double
    ReadOnly Property LinesPerPage As Integer
    ReadOnly Property StartLine As Integer
    ReadOnly Property DefaultFont As String

    Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation
End Interface

Public Class OsProfileFactory
    Private Shared ReadOnly _profiles As New Dictionary(Of OSType, IOsProfile)()

    Shared Sub New()
        RegisterProfile(New Mvs38jProfile())
        RegisterProfile(New ZosProfile())
        RegisterProfile(New VmsProfile())
        RegisterProfile(New MpeProfile())
        RegisterProfile(New RstsProfile())
        RegisterProfile(New Vm370Profile())
        RegisterProfile(New Nos278Profile())
        RegisterProfile(New VmspProfile())
        RegisterProfile(New TandyXenixProfile())
    End Sub

    Private Shared Sub RegisterProfile(profile As IOsProfile)
        _profiles(profile.OS) = profile
    End Sub

    Public Shared Function GetProfile(osType As OSType) As IOsProfile
        If _profiles.ContainsKey(osType) Then
            Return _profiles(osType)
        End If
        Return Nothing
    End Function
End Class

Public Class Mvs38jProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_MVS38J Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 45 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 5 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving MVS 3.8J (OS/VS2) job information.")
        For Each line As String In lines
            line = line.ToUpper().Trim()
            If line <> "" Then
                Try
                    Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                    If parts.Length > 0 AndAlso parts(0).StartsWith("****") Then
                        If parts.Length > 2 AndAlso parts(1) = "END" AndAlso (parts(2) = "JOB" OrElse parts(2) = "TSU") Then
                            If parts.Length > 3 Then info.JobID = parts(3)
                            If parts.Length > 4 Then info.JobName = parts(4)
                            If parts.Length > 7 Then info.User = parts(parts.Length - 7)
                        End If
                    End If
                Catch ex As Exception
                End Try
            End If
        Next
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class ZosProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_ZOS Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 46 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 5 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving Z/OS job information.")
        For Each line As String In lines
            line = line.ToUpper().Trim()
            If line <> "" Then
                Try
                    Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                    If parts.Length > 0 AndAlso parts(0).StartsWith("*") Then
                        If parts.Length > 2 AndAlso parts(1) = "JOBID:" Then info.JobID = parts(2)
                        If parts.Length > 3 AndAlso parts(1) = "JOB" AndAlso parts(2) = "NAME:" Then info.JobName = parts(3)
                        If parts.Length > 3 AndAlso parts(1) = "USER" AndAlso parts(2) = "ID:" Then info.User = parts(3)
                    End If
                Catch ex As Exception
                End Try
            End If
        Next
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class VmsProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_VMS Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 25 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 3 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Dim gotInfo As Boolean = False
        Console.WriteLine($"[{devName}] resolving VMS job information.")
        For Each line In lines
            line = line.ToUpper()
            If line.Trim().StartsWith("JOB") Then
                Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length > 16 Then
                    info.JobName = parts(1)
                    info.JobID = parts(2).Replace("(", "").Replace(")", "")
                    info.User = parts(16)
                    gotInfo = True
                End If
            End If
        Next
        If Not gotInfo Then
            info.JobName = "UNKNOWN"
            info.JobID = Now.ToShortTimeString().Replace(" ", "-").Replace("/", "-")
            info.User = devName
        End If
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class MpeProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_MPE Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 25 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 3 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Dim gotInfo As Boolean = False
        Console.WriteLine($"[{devName}] resolving MPE job information.")
        For Each line In lines
            line = line.ToUpper()
            If line.Trim().Length > 0 Then
                Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length > 5 Then
                    info.JobName = parts(0).Trim().Replace("#", "").Replace(vbNullChar, "").Replace(";", "")
                    info.JobID = parts(1).Trim().Replace("#", "").Replace(";", "").Replace("(", "").Replace(")", "")
                    info.User = parts(5).Replace(";", "")
                    gotInfo = True
                    Exit For
                End If
            End If
        Next
        If Not gotInfo Then
            info.JobName = "UNKNOWN"
            info.JobID = Now.ToShortTimeString().Replace(" ", "-").Replace("/", "-")
            info.User = devName
        End If
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class RstsProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_RSTS Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 27 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 0 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving RSTS/E job information.")
        For Each line As String In lines
            line = line.ToUpper()
            If line.Contains("ENTRY") Then
                Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length > 5 AndAlso parts(4) = "ENTRY" Then
                    Dim jobParts As String() = parts(5).Split(":"c)
                    If jobParts.Length > 0 Then
                        Dim jobData As String = jobParts(If(jobParts.Length > 1, 1, 0))
                        Dim EOU As Integer = jobData.IndexOf("]") + 1
                        If EOU > 0 Then
                            info.User = Microsoft.VisualBasic.Strings.Left(jobData, EOU)
                            info.JobName = Microsoft.VisualBasic.Strings.Right(jobData, Len(jobData) - EOU)
                        End If
                    End If
                    info.JobID = $"{Now.ToShortDateString().Replace("/", "-")}-RSTS"
                End If
            End If
        Next
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class Vm370Profile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_VM370 Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 7 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 2 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving VM/370 job information.")
        For Each line As String In lines
            line = line.ToUpper().Trim()
            Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
            If parts.Length > 2 Then
                If parts(0) = "LOCATION" AndAlso parts(1) = "USERID" AndAlso parts.Length > 3 Then
                    info.User = parts(3)
                End If
                If parts(0) = "SPOOL" AndAlso parts(1) = "FILE" AndAlso parts(2) = "NAME" AndAlso parts.Length > 5 Then
                    info.JobName = $"{parts(4)}.{parts(5)}"
                End If
                If parts(0) = "SPOOL" AndAlso parts(1) = "FILE" AndAlso parts(2) = "ID" AndAlso parts.Length > 3 Then
                    info.JobID = parts(3)
                End If
            End If
        Next
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class Nos278Profile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_NOS278 Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 25 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 3 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Dim gotInfo As Boolean = False
        Console.WriteLine($"[{devName}] resolving NOS 2.7.8 job information.")
        For Each line In lines
            line = line.ToUpper()
            If line.Trim().Length > 0 Then
                Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length > 0 Then
                    If parts(0) = "UJN" AndAlso parts.Length > 5 Then
                        info.JobID = parts(2)
                        info.JobName = parts(5)
                        gotInfo = True
                    End If
                    If parts(0) = "CREATING" AndAlso parts.Length > 7 Then
                        info.User = parts(7)
                        If info.User.Trim() = "" Then info.User = "CONSOLE"
                        gotInfo = True
                        Exit For
                    End If
                End If
            End If
        Next
        If Not gotInfo Then
            info.JobName = "UNKNOWN"
            info.JobID = Now.ToShortTimeString().Replace(" ", "-").Replace("/", "-")
            info.User = devName
        End If
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class VmspProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_VMSP Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 7 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 0 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving VM/SP job information.")
        For Each line As String In lines
            line = line.ToUpper().Trim()
            Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
            If parts.Length > 4 Then
                If parts(2) = "USERID" AndAlso parts(3) = "ORIGIN" Then
                    info.User = parts(0)
                End If
                If parts(2) = "FILENAME" AndAlso parts(3) = "FILETYPE" Then
                    info.JobName = $"{parts(0)}.{parts(1)}"
                End If
                If parts(2) = "SPOOLID" Then
                    info.JobID = parts(0)
                End If
            End If
        Next
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class TandyXenixProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_TANDYXENIX Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 25 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 0 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "Chainprinter" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Console.WriteLine($"[{devName}] OS Type is TANDY XENIX")
        Dim info As New JobInformation() With {
            .JobName = "XENIX",
            .JobID = Now.Ticks.ToString(),
            .User = "XENIX"
        }
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class
