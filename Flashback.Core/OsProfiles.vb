Imports System.Collections.Generic
Imports System.Threading
Imports System.Transactions
Imports Microsoft.Extensions.Logging

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
        RegisterProfile(New Zvm73Profile())
        RegisterProfile(New GenericProfile())
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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving MVS 3.8J (OS/VS2) job information.")
        For Each line As String In lines
            line = line.ToUpper().Trim()
            If line <> "" Then
                Try
                    Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                    If parts.Length > 0 AndAlso parts(0).StartsWith("****") Then
                        If parts.Length > 2 Then
                            If parts.Length > 3 Then info.JobID = $"{parts(2)} {parts(3)}"
                            If parts.Length > 4 Then info.JobName = parts(4)
                            If parts.Length > 7 Then info.User = parts(parts.Length - 7)
                        End If
                    End If
                Catch ex As Exception
                    Console.WriteLine($"ERROR: [ExtractJobInformation]" & vbCrLf & $"{ex.Message}")
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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

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
                    Console.WriteLine($"[{devName}] ERROR parsing line: {ex.Message}")
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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Dim gotInfo As Boolean = False
        Console.WriteLine($"[{devName}] resolving MPE job information.")
        For Each line In lines
            line = line.ToUpper().Trim()
            If line.Contains("#S") AndAlso line.Contains("#O") Then
                Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length >= 2 Then
                    For i As Integer = 0 To parts.Length - 1
                        Dim p As String = parts(i).Trim()
                        If p.StartsWith("#S") Then
                            info.JobName = p.Replace("#", "").Replace(";", "").Trim()
                        ElseIf p.StartsWith("#O") Then
                            info.JobID = p.Replace("#", "").Replace(";", "").Trim()
                        ElseIf p = "*" AndAlso i + 1 < parts.Length AndAlso (info.User = "UnknownUser" OrElse String.IsNullOrEmpty(info.User)) Then
                            ' User ID is typically the first field after the first asterisk
                            info.User = parts(i + 1).Replace(";", "").Trim()
                            gotInfo = True
                        ElseIf p.StartsWith("*") AndAlso p.Length > 1 AndAlso (info.User = "UnknownUser" OrElse String.IsNullOrEmpty(info.User)) Then
                            ' Handle case where asterisk is attached to the User ID (e.g. *MANAGER.SYS)
                            info.User = p.Substring(1).Replace(";", "").Trim()
                            gotInfo = True
                        End If
                    Next
                    If gotInfo Then Exit For
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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving RSTS/E job information.")
        For Each line As String In lines
            line = line.ToUpper()
            If line.Contains("ENTRY") Then
                Dim parts As String() = line.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length > 5 AndAlso parts(3) = "ENTRY" Then
                    Dim jobParts As String() = parts(4).Split(":"c)
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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

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
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

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

Public Class Zvm73Profile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_ZVM73 Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 24 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 2 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        Dim info As New JobInformation()
        Console.WriteLine($"[{devName}] resolving z/VM 7.3 job information.")
        
        ' z/VM 7.3 format:
        ' USERID  USERID  FILE NAME/TYPE=     FILENAME  FILETYPE     ORIGINID= USERID
        ' USERID  USERID  CREATION DATE/TIME= MM/DD/YY  HH:MM:SS     SYSID=    SYSTEMID
        ' USERID  USERID  CLASS=  X     SPID= ####
        
        For Each line As String In lines
            Dim trimmedLine = line.Trim()
            If String.IsNullOrEmpty(trimmedLine) Then Continue For
            
            Try
                ' Split by multiple spaces to get fields
                Dim parts As String() = trimmedLine.Split(New String() {"  "}, StringSplitOptions.RemoveEmptyEntries)
                
                ' Look for lines starting with repeated user ID (e.g., "MAINT730  MAINT730")
                If parts.Length >= 2 Then
                    Dim firstPart = parts(0).Trim()
                    Dim secondPart = parts(1).Trim()
                    
                    ' Check if first two parts are the same (user ID pattern)
                    If firstPart = secondPart AndAlso Not String.IsNullOrEmpty(firstPart) Then
                        ' This is a z/VM 7.3 header line
                        Dim restOfLine = String.Join("  ", parts.Skip(2))
                        
                        ' Extract FILE NAME/TYPE
                        If restOfLine.Contains("FILE NAME/TYPE=") Then
                            Dim fileMatch = System.Text.RegularExpressions.Regex.Match(restOfLine, "FILE NAME/TYPE=\s+(\S+)\s+(\S+)")
                            If fileMatch.Success Then
                                Dim fileName = fileMatch.Groups(1).Value.Trim()
                                Dim fileType = fileMatch.Groups(2).Value.Trim()
                                info.JobName = $"{fileName}.{fileType}"
                            End If
                            
                            ' Extract ORIGINID
                            Dim originMatch = System.Text.RegularExpressions.Regex.Match(restOfLine, "ORIGINID=\s+(\S+)")
                            If originMatch.Success Then
                                info.User = originMatch.Groups(1).Value.Trim()
                            End If
                        End If
                        
                        ' Extract SYSID
                        If restOfLine.Contains("SYSID=") Then
                            Dim sysidMatch = System.Text.RegularExpressions.Regex.Match(restOfLine, "SYSID=\s+(\S+)")
                            If sysidMatch.Success Then
                                Dim sysid = sysidMatch.Groups(1).Value.Trim()
                                ' Store system ID for potential use
                            End If
                        End If
                        
                        ' Extract SPID (Spool ID)
                        If restOfLine.Contains("SPID=") Then
                            Dim spidMatch = System.Text.RegularExpressions.Regex.Match(restOfLine, "SPID=\s+(\d+)")
                            If spidMatch.Success Then
                                info.JobID = spidMatch.Groups(1).Value.Trim()
                            End If
                        End If
                        
                        ' If we haven't found user yet, use the repeated user ID from the line prefix
                        If String.IsNullOrEmpty(info.User) OrElse info.User = "UnknownUser" Then
                            info.User = firstPart
                        End If
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"[{devName}] ERROR parsing z/VM 7.3 line: {ex.Message}")
            End Try
        Next
        
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class

Public Class GenericProfile
    Implements IOsProfile

    Public ReadOnly Property OS As OSType = OSType.OS_GENERIC Implements IOsProfile.OS
    Public ReadOnly Property FirstLine As Double = 10 Implements IOsProfile.FirstLine
    Public ReadOnly Property LinesPerPage As Integer = 66 Implements IOsProfile.LinesPerPage
    Public ReadOnly Property StartLine As Integer = 0 Implements IOsProfile.StartLine
    Public ReadOnly Property DefaultFont As String = "OCR-B" Implements IOsProfile.DefaultFont

    Public Function ExtractJobInformation(lines As List(Of String), devName As String) As JobInformation Implements IOsProfile.ExtractJobInformation
        ' Generic jobs don't usually have parsable headers in the stream
        Dim info As New JobInformation() With {
            .JobName = "GENERIC",
            .JobID = Now.ToString("HHmmss"),
            .User = devName
        }
        info.ApplyFallbacks(devName)
        Return info
    End Function
End Class
