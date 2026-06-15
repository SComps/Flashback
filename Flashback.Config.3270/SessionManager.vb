Imports TN3270Framework
Imports Flashback.Core
Imports System.IO

Public Enum ScreenMode
    Login
    Menu
    Edit
    EditEmail
    ConfirmDelete
    Help
    Users
    AddUser
    [Error]
End Enum

Public Class SessionStateManager
    Private _session As TN3270Session
    Private _devList As List(Of Devs)
    Private _configFile As String
    Private _syspw As String

    Private _mode As ScreenMode = ScreenMode.Menu
    Private _startIndex As Integer = 0
    Private _editingIndex As Integer = -1
    Private _statusMsg As String = ""
    Private _statusColor As Byte = TN3270Color.White
    Private _hasUnsavedChanges As Boolean = False
    Private _previousMode As ScreenMode = ScreenMode.Menu
    Private _errorTitle As String = ""
    Private _errorMessage As String = ""
    Private _errorDetails As String = ""

    Public Sub New(session As TN3270Session, devList As List(Of Devs), configFile As String, Optional syspw As String = "")
        _session = session
        _devList = devList
        _configFile = configFile
        _syspw = If(syspw, "")
        If Not String.IsNullOrEmpty(_syspw) Then
            _mode = ScreenMode.Login
        Else
            _mode = ScreenMode.Menu
        End If
    End Sub

    Public Sub InitSession()
        If _mode = ScreenMode.Login Then
            ShowLogin()
        Else
            ShowMenu()
        End If
    End Sub

    Public Sub HandleInput(sender As Object, e As AidKeyEventArgs)
        ' Debug: Log the key code received
        Console.WriteLine($"[DEBUG] Key received: 0x{e.AidKey:X2} in mode {_mode}")
        
        ' Intercept PF1 for global help (except on error screen)
        If e.AidKey = &HF1 AndAlso _mode <> ScreenMode.Error Then ' PF1
            Console.WriteLine("[DEBUG] PF1 pressed - showing help")
            If _mode <> ScreenMode.Help Then
                If _mode = ScreenMode.Edit Then ScrapeEditFields()
                _previousMode = _mode
                _mode = ScreenMode.Help
                ShowHelp()
                Return
            End If
        End If

        _statusMsg = ""
        _statusColor = TN3270Color.White

        Select Case _mode
            Case ScreenMode.Login
                ProcessLoginInput(e)
            Case ScreenMode.Menu
                ProcessMenuInput(e)
            Case ScreenMode.Edit
                ProcessEditInput(e)
            Case ScreenMode.EditEmail
                ProcessEditEmailInput(e)
            Case ScreenMode.ConfirmDelete
                ProcessDeleteInput(e)
            Case ScreenMode.Help
                ProcessHelpInput(e)
            Case ScreenMode.Users
                ProcessUsersInput(e)
            Case ScreenMode.AddUser
                ProcessAddUserInput(e)
            Case ScreenMode.Error
                ProcessErrorInput(e)
        End Select
    End Sub

    Private Sub ProcessLoginInput(e As AidKeyEventArgs)
        If e.AidKey = &HF3 Then
            _session.Close()
            Return
        End If

        If e.AidKey <> &H7D Then
            ShowLogin()
            Return
        End If

        Dim enteredPw = _session.GetFieldValue("txtPw")?.Replace(Chr(0), "").Trim()
        
        If enteredPw = _syspw.Trim() Then
            _mode = ScreenMode.Menu
            ShowMenu()
        Else
            _statusMsg = "INVALID PASSWORD. ACCESS DENIED."
            _statusColor = TN3270Color.Red
            ShowLogin()
        End If
    End Sub

    Private Sub ProcessMenuInput(e As AidKeyEventArgs)
        If e.AidKey = &HF7 Then ' PF7
            _startIndex = Math.Max(0, _startIndex - 5)
            ShowMenu()
            Return
        ElseIf e.AidKey = &HF8 Then ' PF8
            If _startIndex + 5 < _devList.Count Then _startIndex += 5
            ShowMenu()
            Return
        ElseIf e.AidKey = &HF3 Then ' PF3 Exit
            If _hasUnsavedChanges Then
                Dim errTitle As String = ""
                Dim errMsg As String = ""
                Dim errDetails As String = ""
                
                If SaveDevices(errTitle, errMsg, errDetails) Then
                    _session.WriteText(23, 2, "SESSION TERMINATED. AUTO-SAVE COMPLETE.", TN3270Color.Green)
                Else
                    _session.WriteText(23, 2, "SESSION TERMINATED. AUTO-SAVE FAILED!", TN3270Color.Red)
                    Console.WriteLine($"[Config3270] Auto-save failed: {errMsg}")
                End If
            Else
                _session.WriteText(23, 2, "SESSION TERMINATED.", TN3270Color.Yellow)
            End If
            _session.ShowScreen()
            _session.Close()
            Return
        End If

        If e.AidKey <> &H7D Then
            ShowMenu()
            Return
        End If

        Dim cmd = _session.GetFieldValue("txtCmd")?.ToUpper().Trim()
        If String.IsNullOrEmpty(cmd) Then
            ShowMenu()
            Return
        End If

        If cmd = "ADD" Then
            Dim newDev As New Devs()
            _devList.Add(newDev)
            _editingIndex = _devList.Count - 1
            _mode = ScreenMode.Edit
            _hasUnsavedChanges = True
            ShowEdit()
        ElseIf cmd = "SAVE" Then
            Dim errTitle As String = ""
            Dim errMsg As String = ""
            Dim errDetails As String = ""
            
            If SaveDevices(errTitle, errMsg, errDetails) Then
                _hasUnsavedChanges = False
                _session.ClearModifiedTags()
                _statusMsg = "Devices saved successfully."
                _statusColor = TN3270Color.Green
                ShowMenu()
            Else
                ' Show error screen
                _previousMode = ScreenMode.Menu
                _mode = ScreenMode.Error
                _errorTitle = errTitle
                _errorMessage = errMsg
                _errorDetails = errDetails
                ShowError()
            End If
        ElseIf cmd = "EXIT" Then
            _session.Close()
        ElseIf cmd = "USERS" OrElse cmd = "3" Then
            _mode = ScreenMode.Users
            ShowUsers()
        ElseIf cmd.StartsWith("DELETE ") Then
            Dim idStr = cmd.Substring(7).Trim()
            Dim id As Integer
            If Integer.TryParse(idStr, id) AndAlso id > 0 AndAlso id <= _devList.Count Then
                _editingIndex = id - 1
                _mode = ScreenMode.ConfirmDelete
                ShowConfirmDelete()
            Else
                _statusMsg = "Invalid Device ID for Delete."
                _statusColor = TN3270Color.Red
                ShowMenu()
            End If
        Else
            Dim id As Integer
            If Integer.TryParse(cmd, id) AndAlso id > 0 AndAlso id <= _devList.Count Then
                _editingIndex = id - 1
                _mode = ScreenMode.Edit
                ShowEdit()
            Else
                _statusMsg = $"Unknown command: {cmd}"
                _statusColor = TN3270Color.Red
                ShowMenu()
            End If
        End If
    End Sub

    Private Sub ProcessEditInput(e As AidKeyEventArgs)
        Console.WriteLine($"[DEBUG] ProcessEditInput: Key=0x{e.AidKey:X2}")
        
        If e.AidKey = &HF3 Then
            Console.WriteLine("[DEBUG] PF3 pressed - returning to menu")
            _mode = ScreenMode.Menu
            ShowMenu()
            Return
        End If
        
        If e.AidKey = &HF4 Then ' PF4 (correct key code is 0xF4, not 0x64)
            Console.WriteLine("[DEBUG] PF4 pressed - switching to email config")
            ScrapeEditFields()
            _mode = ScreenMode.EditEmail
            ShowEditEmail()
            Return
        End If

        If e.AidKey <> &H7D Then
            Console.WriteLine($"[DEBUG] Unknown key 0x{e.AidKey:X2} - refreshing screen")
            ShowEdit()
            Return
        End If
        
        Console.WriteLine("[DEBUG] Enter pressed - saving device")

        ' Use optimized field scraping
        ScrapeEditFields()
        
        Dim d = _devList(_editingIndex)
        _statusMsg = $"Device '{d.DevName}' updated."
        _statusColor = TN3270Color.Green
        _hasUnsavedChanges = True
        _session.ClearModifiedTags() ' Clear MDT after update
        _mode = ScreenMode.Menu
        ShowMenu()
    End Sub

    Private Sub ScrapeEditFields()
        If _editingIndex < 0 OrElse _editingIndex >= _devList.Count Then Return
        
        Dim d = _devList(_editingIndex)
        
        ' Try to use GetModifiedFields() for efficiency first
        Dim modifiedFields = _session.GetModifiedFields()
        
        If modifiedFields.Count > 0 Then
            ' Only update fields that were actually modified
            For Each field In modifiedFields
                Select Case field.Name
                    Case "txtName"
                        d.DevName = field.Content?.Trim()
                    Case "txtDesc"
                        d.DevDescription = field.Content?.Trim()
                    Case "txtType"
                        d.DevType = Val(field.Content)
                    Case "txtConn"
                        d.ConnType = Val(field.Content)
                    Case "txtOS"
                        d.OS = CType(Val(field.Content), OSType)
                    Case "txtDest"
                        d.DevDest = field.Content?.Trim()
                    Case "txtPDF"
                        Dim pdfVal = field.Content?.Trim().ToUpper()
                        d.PDF = (pdfVal = "TRUE" OrElse pdfVal = "1" OrElse pdfVal = "YES")
                    Case "txtOrient"
                        d.Orientation = Val(field.Content)
                    Case "txtOut"
                        d.OutDest = field.Content?.Trim()
                    Case "txtShade"
                        d.Shading = CType(Val(field.Content), RenderPDF.ShadingColor)
                    Case "txtJob"
                        d.JobNumber = Val(field.Content)
                    Case "txtEnabled"
                        Dim enVal = field.Content?.Trim().ToUpper()
                        If Not String.IsNullOrEmpty(enVal) Then
                            d.Enabled = (enVal = "TRUE" OrElse enVal = "1" OrElse enVal = "YES" OrElse enVal = "Y")
                        End If
                End Select
            Next
        Else
            ' Fallback: scrape all fields using GetFieldValue if no modified fields detected
            Console.WriteLine($"[Config3270] No modified fields detected, scraping all fields for device {_editingIndex + 1}")
            
            Dim val As String
            
            val = _session.GetFieldValue("txtName")
            If val IsNot Nothing Then d.DevName = val.Trim()
            
            val = _session.GetFieldValue("txtDesc")
            If val IsNot Nothing Then d.DevDescription = val.Trim()
            
            val = _session.GetFieldValue("txtType")
            If val IsNot Nothing Then
                Dim tempInt As Integer
                If Integer.TryParse(val, tempInt) Then d.DevType = tempInt
            End If
            
            val = _session.GetFieldValue("txtConn")
            If val IsNot Nothing Then
                Dim tempInt As Integer
                If Integer.TryParse(val, tempInt) Then d.ConnType = tempInt
            End If
            
            val = _session.GetFieldValue("txtOS")
            If val IsNot Nothing Then
                Dim tempInt As Integer
                If Integer.TryParse(val, tempInt) Then d.OS = CType(tempInt, OSType)
            End If
            
            val = _session.GetFieldValue("txtDest")
            If val IsNot Nothing Then d.DevDest = val.Trim()
            
            val = _session.GetFieldValue("txtPDF")
            If val IsNot Nothing Then
                Dim pdfVal = val.Trim().ToUpper()
                d.PDF = (pdfVal = "TRUE" OrElse pdfVal = "1" OrElse pdfVal = "YES")
            End If
            
            val = _session.GetFieldValue("txtOrient")
            If val IsNot Nothing Then
                Dim tempInt As Integer
                If Integer.TryParse(val, tempInt) Then d.Orientation = tempInt
            End If
            
            val = _session.GetFieldValue("txtOut")
            If val IsNot Nothing Then d.OutDest = val.Trim()
            
            val = _session.GetFieldValue("txtShade")
            If val IsNot Nothing Then
                Dim tempInt As Integer
                If Integer.TryParse(val, tempInt) Then d.Shading = CType(tempInt, RenderPDF.ShadingColor)
            End If
            
            val = _session.GetFieldValue("txtJob")
            If val IsNot Nothing Then
                Dim tempInt As Integer
                If Integer.TryParse(val, tempInt) Then d.JobNumber = tempInt
            End If
            
            val = _session.GetFieldValue("txtEnabled")
            If val IsNot Nothing Then
                Dim enVal = val.Trim().ToUpper()
                If Not String.IsNullOrEmpty(enVal) Then
                    d.Enabled = (enVal = "TRUE" OrElse enVal = "1" OrElse enVal = "YES" OrElse enVal = "Y")
                End If
            End If
        End If
    End Sub

    Private Sub ProcessDeleteInput(e As AidKeyEventArgs)
        If e.AidKey = &H7D Then
            Dim cmd = _session.GetFieldValue("txtConfirm")?.ToUpper().Trim()
            if cmd = "Y" Then
                _devList.RemoveAt(_editingIndex)
                _statusMsg = "Device deleted."
                _statusColor = TN3270Color.Yellow
                _hasUnsavedChanges = True
                If _startIndex >= _devList.Count AndAlso _devList.Count > 0 Then
                    _startIndex = Math.Max(0, ((_devList.Count - 1) \ 5) * 5)
                End If
            Else
                _statusMsg = "Delete cancelled."
            End If
            _mode = ScreenMode.Menu
            ShowMenu()
        Else
            ShowConfirmDelete()
        End If
    End Sub

    Private Sub ShowLogin()
        _session.ClearFields()
        Dim dateStr = DateTime.Now.ToString("MM/dd/yy")
        Dim timeStr = DateTime.Now.ToString("HH:mm:ss")

        _session.WriteText(1, 2, "PROGRAM: FLSHBK00", TN3270Color.Turquoise)
        _session.WriteText(1, 30, "FLASHBACK SECURITY", TN3270Color.White)
        _session.WriteText(1, 65, $"DATE: {dateStr}", TN3270Color.Turquoise)
        _session.WriteText(2, 65, $"TIME: {timeStr}", TN3270Color.Turquoise)

        _session.WriteText(10, 25, "ENTER SYSTEM PASSWORD TO CONTINUE", TN3270Color.Turquoise)
        _session.WriteText(12, 25, "SYSPW ===>", TN3270Color.Yellow)
        _session.AddField(12, 36, 8, "", False, TN3270Color.Neutral, TN3270Color.Neutral, TN3270Highlight.None, "txtPw", TN3270Intensity.Hidden)

        If Not String.IsNullOrEmpty(_statusMsg) Then
            _session.WriteText(15, 25, _statusMsg, _statusColor)
        End If

        _session.WriteText(22, 2, "ENTER:LOGIN   PF1:HELP   PF3:EXIT", TN3270Color.White)
        _session.ShowScreen()
    End Sub

    Private Sub ShowMenu()
        _session.ClearFields()
        Dim dateStr = DateTime.Now.ToString("MM/dd/yy")
        Dim timeStr = DateTime.Now.ToString("HH:mm:ss")

        _session.WriteText(1, 2, "PROGRAM: FLSHBK01", TN3270Color.Turquoise)
        _session.WriteText(1, 25, "FLASHBACK CONFIGURATION", TN3270Color.White)
        _session.WriteText(1, 65, $"DATE: {dateStr}", TN3270Color.Turquoise)
        _session.WriteText(2, 2, "TRANSID: CFG1", TN3270Color.Turquoise)
        _session.WriteText(2, 65, $"TIME: {timeStr}", TN3270Color.Turquoise)
        _session.WriteText(3, 1, StrDup(78, "-"), TN3270Color.Blue)

        _session.WriteText(4, 2, "COMMAND ==>", TN3270Color.Yellow)
        _session.AddField(4, 14, 40, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtCmd")

        If Not String.IsNullOrEmpty(_statusMsg) Then
            _session.WriteText(5, 2, "MSG:", TN3270Color.Turquoise)
            _session.WriteText(5, 7, _statusMsg, _statusColor)
        End If

        _session.WriteText(7, 2, "ID   NAME            DESCRIPTION                    OS  PDF  EN   SHADE", TN3270Color.Turquoise)
        _session.WriteText(8, 1, StrDup(78, "-"), TN3270Color.Blue)

        Dim rowPos = 9
        For i = _startIndex To Math.Min(_startIndex + 3, _devList.Count - 1)
            Dim d = _devList(i)
            _session.WriteText(rowPos, 2, (i + 1).ToString("00"), TN3270Color.Yellow)
            _session.WriteText(rowPos, 7, d.DevName.PadRight(14).Substring(0, 14), TN3270Color.White)
            _session.WriteText(rowPos, 23, d.DevDescription.PadRight(29).Substring(0, 29), TN3270Color.White)
            _session.WriteText(rowPos, 54, CInt(d.OS).ToString(), TN3270Color.White)
            _session.WriteText(rowPos, 58, If(d.PDF, "Y", "N"), TN3270Color.Pink)
            _session.WriteText(rowPos, 63, If(d.Enabled, "Y", "N"), TN3270Color.White)
            _session.WriteText(rowPos, 68, d.Shading.ToString().ToUpper(), TN3270Color.Green)
            
            _session.WriteText(rowPos + 1, 7, d.DevDest.PadRight(50).Substring(0, 50), TN3270Color.Green)
            rowPos += 3
        Next

        _session.WriteText(21, 1, StrDup(78, "-"), TN3270Color.Blue)
        _session.WriteText(22, 2, "ENTER:PROCESS   PF1:HELP   PF3:EXIT   PF7:UP   PF8:DOWN", TN3270Color.White)
        _session.WriteText(22, 60, "CMD: ADD, USERS", TN3270Color.Turquoise)
        _session.WriteText(23, 2, "OS:(0)MVS (1)VMS (2)MPE (3)RSTS (4)VM370 (5)NOS (6)VMSP (7)TNDY (8)ZOS (9)ZVM73 (10)GEN", TN3270Color.Turquoise)
        _session.WriteText(24, 2, "CONN:(0)SOCK (1)FILE (2)PHYS (3)RAW", TN3270Color.Turquoise)
        _session.ShowScreen()
    End Sub

    Private Sub ShowEdit()
        _session.ClearFields()
        Dim d = _devList(_editingIndex)
        Dim dateStr = DateTime.Now.ToString("MM/dd/yy")
        Dim timeStr = DateTime.Now.ToString("HH:mm:ss")

        _session.WriteText(1, 2, "PROGRAM: FLSHBK01", TN3270Color.Turquoise)
        _session.WriteText(1, 30, "EDIT DEVICE DETAILS", TN3270Color.White)
        _session.WriteText(1, 65, $"DATE: {dateStr}", TN3270Color.Turquoise)
        _session.WriteText(2, 2, "TRANSID: CFG1", TN3270Color.Turquoise)
        _session.WriteText(2, 65, $"TIME: {timeStr}", TN3270Color.Turquoise)
        _session.WriteText(3, 1, StrDup(78, "-"), TN3270Color.Blue)

        Dim labelCol = 5
        Dim fieldCol = 26

        _session.WriteText(5, labelCol, "        DEVICE NAME:", TN3270Color.Turquoise)
        _session.AddField(5, fieldCol, 15, d.DevName, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtName")

        _session.WriteText(6, labelCol, " DEVICE DESCRIPTION:", TN3270Color.Turquoise)
        _session.AddField(6, fieldCol, 30, d.DevDescription, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtDesc")

        _session.WriteText(8, labelCol, "        DEVICE TYPE:", TN3270Color.Turquoise)
        _session.AddField(8, fieldCol, 1, d.DevType.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtType")

        _session.WriteText(9, labelCol, "    CONNECTION TYPE:", TN3270Color.Turquoise)
        _session.AddField(9, fieldCol, 1, d.ConnType.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtConn")

        _session.WriteText(10, labelCol, "   OPERATING SYSTEM:", TN3270Color.Turquoise)
        _session.AddField(10, fieldCol, 2, CInt(d.OS).ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtOS")

        _session.WriteText(12, labelCol, "        DEVICE SOURCE:", TN3270Color.Turquoise)
        _session.AddField(12, fieldCol, 50, d.DevDest, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtDest")

        _session.WriteText(14, labelCol, "         OUTPUT PDF:", TN3270Color.Turquoise)
        _session.AddField(14, fieldCol, 10, d.PDF.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtPDF")

        _session.WriteText(14, 42, "ORIENTATION:", TN3270Color.Turquoise)
        _session.AddField(14, 55, 1, d.Orientation.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtOrient")

        _session.WriteText(16, labelCol, "   OUTPUT DIRECTORY:", TN3270Color.Turquoise)
        _session.AddField(16, fieldCol, 50, d.OutDest, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtOut")

        _session.WriteText(17, labelCol, "   SHADING COLOR   :", TN3270Color.Turquoise)
        _session.AddField(17, fieldCol, 1, CInt(d.Shading).ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtShade")

        _session.WriteText(18, labelCol, "   NEXT JOB NUMBER :", TN3270Color.Turquoise)
        _session.AddField(18, fieldCol, 6, d.JobNumber.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtJob")

        _session.WriteText(19, labelCol, "   DEVICE ENABLED  :", TN3270Color.Turquoise)
        _session.AddField(19, fieldCol, 5, d.Enabled.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtEnabled")

        _session.WriteText(22, 2, "ENTER:SAVE   PF1:HELP   PF3:CANCEL   PF4:EMAIL CONFIG", TN3270Color.White)
        _session.ShowScreen()
    End Sub

    Private Sub ShowEditEmail()
        _session.ClearFields()
        Dim d = _devList(_editingIndex)
        Dim dateStr = DateTime.Now.ToString("MM/dd/yy")
        Dim timeStr = DateTime.Now.ToString("HH:mm:ss")

        _session.WriteText(1, 2, "PROGRAM: FLSHBK01", TN3270Color.Turquoise)
        _session.WriteText(1, 25, "EMAIL CONFIGURATION", TN3270Color.White)
        _session.WriteText(1, 65, $"DATE: {dateStr}", TN3270Color.Turquoise)
        _session.WriteText(2, 2, "TRANSID: CFG1", TN3270Color.Turquoise)
        _session.WriteText(2, 65, $"TIME: {timeStr}", TN3270Color.Turquoise)
        _session.WriteText(3, 1, StrDup(78, "-"), TN3270Color.Blue)

        _session.WriteText(4, 2, $"DEVICE: {d.DevName}", TN3270Color.Yellow)

        Dim labelCol = 2
        Dim fieldCol = 25

        _session.WriteText(6, labelCol, "   EMAIL ENABLED     :", TN3270Color.Turquoise)
        _session.AddField(6, fieldCol, 5, d.EmailEnabled.ToString().ToLower(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtEmailEnabled")

        _session.WriteText(7, labelCol, "   RECIPIENTS        :", TN3270Color.Turquoise)
        _session.AddField(7, fieldCol, 50, d.EmailRecipients, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtRecipients")

        _session.WriteText(8, labelCol, "   SMTP SERVER       :", TN3270Color.Turquoise)
        _session.AddField(8, fieldCol, 40, d.SmtpServer, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtSmtpServer")

        _session.WriteText(9, labelCol, "   SMTP PORT         :", TN3270Color.Turquoise)
        _session.AddField(9, fieldCol, 5, d.SmtpPort.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtSmtpPort")

        _session.WriteText(10, labelCol, "   SMTP USERNAME     :", TN3270Color.Turquoise)
        _session.AddField(10, fieldCol, 40, d.SmtpUsername, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtSmtpUsername")

        _session.WriteText(11, labelCol, "   SMTP PASSWORD     :", TN3270Color.Turquoise)
        _session.AddField(11, fieldCol, 40, d.SmtpPassword, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtSmtpPassword", TN3270Intensity.Hidden)

        _session.WriteText(12, labelCol, "   USE TLS           :", TN3270Color.Turquoise)
        _session.AddField(12, fieldCol, 5, d.SmtpUseTLS.ToString().ToLower(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtUseTLS")

        _session.WriteText(13, labelCol, "   FROM ADDRESS      :", TN3270Color.Turquoise)
        _session.AddField(13, fieldCol, 50, d.EmailFromAddress, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtFromAddress")

        _session.WriteText(14, labelCol, "   FROM NAME         :", TN3270Color.Turquoise)
        _session.AddField(14, fieldCol, 40, d.EmailFromName, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtFromName")

        _session.WriteText(15, labelCol, "   SUBJECT           :", TN3270Color.Turquoise)
        _session.AddField(15, fieldCol, 50, d.EmailSubject, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtSubject")

        _session.WriteText(16, labelCol, "   BODY              :", TN3270Color.Turquoise)
        _session.AddField(16, fieldCol, 50, d.EmailBody, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtBody")

        _session.WriteText(18, 2, "VARIABLES: {JobName} {DeviceName} {UserName} {PageCount}", TN3270Color.Green)
        _session.WriteText(19, 2, "           {DateTime} {Date} {Time}", TN3270Color.Green)
        _session.WriteText(20, 2, "RECIPIENTS: Separate multiple emails with semicolons", TN3270Color.Green)

        _session.WriteText(22, 2, "ENTER:SAVE   PF1:HELP   PF3:CANCEL", TN3270Color.White)
        _session.ShowScreen()
    End Sub

    Private Sub ProcessEditEmailInput(e As AidKeyEventArgs)
        If e.AidKey = &HF3 Then
            _mode = ScreenMode.Edit
            ShowEdit()
            Return
        End If

        If e.AidKey <> &H7D Then
            ShowEditEmail()
            Return
        End If

        Dim d = _devList(_editingIndex)

        Dim emailEnabledVal = _session.GetFieldValue("txtEmailEnabled")?.Trim().ToLower()
        If Not String.IsNullOrEmpty(emailEnabledVal) Then
            d.EmailEnabled = (emailEnabledVal = "true" OrElse emailEnabledVal = "1" OrElse emailEnabledVal = "yes" OrElse emailEnabledVal = "y")
        End If

        d.EmailRecipients = _session.GetFieldValue("txtRecipients")?.Trim()
        d.SmtpServer = _session.GetFieldValue("txtSmtpServer")?.Trim()

        Dim portVal = _session.GetFieldValue("txtSmtpPort")?.Trim()
        If Not String.IsNullOrEmpty(portVal) AndAlso IsNumeric(portVal) Then
            d.SmtpPort = CInt(portVal)
        End If

        d.SmtpUsername = _session.GetFieldValue("txtSmtpUsername")?.Trim()
        d.SmtpPassword = _session.GetFieldValue("txtSmtpPassword")?.Trim()

        Dim tlsVal = _session.GetFieldValue("txtUseTLS")?.Trim().ToLower()
        If Not String.IsNullOrEmpty(tlsVal) Then
            d.SmtpUseTLS = (tlsVal = "true" OrElse tlsVal = "1" OrElse tlsVal = "yes" OrElse tlsVal = "y")
        End If

        d.EmailFromAddress = _session.GetFieldValue("txtFromAddress")?.Trim()
        d.EmailFromName = _session.GetFieldValue("txtFromName")?.Trim()
        d.EmailSubject = _session.GetFieldValue("txtSubject")?.Trim()
        d.EmailBody = _session.GetFieldValue("txtBody")?.Trim()

        _statusMsg = "Email configuration saved."
        _statusColor = TN3270Color.Yellow
        _hasUnsavedChanges = True
        _session.ClearModifiedTags() ' Clear MDT after update
        _mode = ScreenMode.Edit
        ShowEdit()
    End Sub

    Private Sub ShowConfirmDelete()
        _session.ClearFields()
        Dim d = _devList(_editingIndex)
        _session.WriteText(10, 20, "****************************************", TN3270Color.Red)
        _session.WriteText(11, 20, $"* CONFIRM DELETION OF: {d.DevName.PadRight(15)} *", TN3270Color.Red)
        _session.WriteText(12, 20, "****************************************", TN3270Color.Red)
        _session.WriteText(14, 20, "TYPE 'Y' TO CONFIRM ==> ", TN3270Color.White)
        _session.AddField(14, 44, 1, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtConfirm")
        _session.WriteText(16, 20, "PRESS ENTER TO PROCEED OR PF3 TO CANCEL", TN3270Color.Turquoise)
        _session.ShowScreen()
    End Sub

    Private Sub ShowHelp()
        _session.ClearFields()
        _session.WriteText(1, 2, "PROGRAM: FLSHBK99", TN3270Color.Turquoise)
        _session.WriteText(1, 30, "FLASHBACK HELP SYSTEM", TN3270Color.White)
        _session.WriteText(3, 2, "FIELD DESCRIPTIONS:", TN3270Color.Yellow)
        
        _session.WriteText(5, 2, "DEVICE TYPE     : 0=Generic, 1=Printer, 2=Plotter", TN3270Color.Turquoise)
        _session.WriteText(6, 2, "CONN TYPE       : 0=Socket (Connect to Host), 1=File, 2=Physical, 3=Raw", TN3270Color.Turquoise)
        _session.WriteText(7, 2, "OPERATING SYSTEM: The profile used to parse job headers (0-9).", TN3270Color.Turquoise)
        _session.WriteText(8, 2, "SOURCE          : For Conn 0: Host:Port. For Conn 3: Local Listen Port.", TN3270Color.Turquoise)
        
        _session.WriteText(10, 2, "OUTPUT PDF      : Set to TRUE to generate PDF files in the Output path.", TN3270Color.Turquoise)
        _session.WriteText(11, 2, "ORIENTATION     : 0=Portrait, 1=Landscape.", TN3270Color.Turquoise)
        _session.WriteText(12, 2, "SHADING COLOR   : (0)Plain (1)Green Bar (2)Blue Bar (3)Gray Bar.", TN3270Color.Turquoise)
        
        _session.WriteText(14, 2, "EMAIL CONFIG    : Use GUI tools (Console/WinUI/WPF) for email setup.", TN3270Color.Yellow)
        
        _session.WriteText(16, 2, "COMMANDS (MENU SCREEN):", TN3270Color.Yellow)
        _session.WriteText(18, 2, "ADD             : Create a new device configuration.", TN3270Color.Turquoise)
        _session.WriteText(19, 2, "SAVE            : Explicitly save all changes to disk.", TN3270Color.Turquoise)
        _session.WriteText(20, 2, "DELETE [ID]     : Remove a device by its list ID number.", TN3270Color.Turquoise)
        _session.WriteText(21, 2, "USERS (or 3)    : Manage Web Users for dashboard access.", TN3270Color.Turquoise)

        _session.WriteText(23, 2, "PRESS ENTER OR PF3 TO RETURN TO PREVIOUS SCREEN", TN3270Color.White)
        _session.ShowScreen()
    End Sub

    Private Sub ProcessHelpInput(e As AidKeyEventArgs)
        ' Any key returns from help
        _mode = _previousMode
        Select Case _mode
            Case ScreenMode.Login : ShowLogin()
            Case ScreenMode.Menu : ShowMenu()
            Case ScreenMode.Edit : ShowEdit()
            Case ScreenMode.ConfirmDelete : ShowConfirmDelete()
        End Select
    End Sub

    Private Function SaveDevices(ByRef errorTitle As String, ByRef errorMessage As String, ByRef errorDetails As String) As Boolean
        Try
            ' Validate config file path
            Dim dir = Path.GetDirectoryName(_configFile)
            If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
                errorTitle = "CONFIGURATION SAVE FAILED"
                errorMessage = $"The configuration directory does not exist:{vbCrLf}{vbCrLf}{dir}{vbCrLf}{vbCrLf}Please ensure the application has proper permissions and the{vbCrLf}directory structure is intact."
                errorDetails = "Error: Directory not found"
                Console.WriteLine($"[Config3270] Save failed: Directory not found - {dir}")
                Return False
            End If

            ' Attempt to save
            Using writer As New StreamWriter(_configFile, append:=False)
                For Each d In _devList
                    writer.WriteLine(d.ToConfigLine())
                Next
            End Using
            
            ' Log success
            Console.WriteLine($"[Config3270] Successfully saved {_devList.Count} devices to {_configFile}")
            Return True
            
        Catch ex As UnauthorizedAccessException
            errorTitle = "PERMISSION DENIED"
            errorMessage = $"Unable to write to configuration file:{vbCrLf}{vbCrLf}{_configFile}{vbCrLf}{vbCrLf}The application does not have write permissions to this location.{vbCrLf}Please check file and directory permissions."
            errorDetails = $"Error: {ex.Message}"
            Console.WriteLine($"[Config3270] Permission error: {ex.Message}")
            Return False
            
        Catch ex As IOException
            errorTitle = "FILE ACCESS ERROR"
            errorMessage = $"Unable to access configuration file:{vbCrLf}{vbCrLf}{_configFile}{vbCrLf}{vbCrLf}The file may be locked by another process or the disk may be full.{vbCrLf}Please close other applications and try again."
            errorDetails = $"Error: {ex.Message}"
            Console.WriteLine($"[Config3270] I/O error: {ex.Message}")
            Return False
            
        Catch ex As Exception
            errorTitle = "UNEXPECTED ERROR"
            errorMessage = $"An unexpected error occurred while saving:{vbCrLf}{vbCrLf}{ex.Message}{vbCrLf}{vbCrLf}Please contact your system administrator if this problem persists."
            errorDetails = $"Type: {ex.GetType().Name}"
            Console.WriteLine($"[Config3270] Unexpected error: {ex.Message}")
            Console.WriteLine($"[Config3270] Stack trace: {ex.StackTrace}")
            Return False
        End Try
    End Function

    Private Sub ShowError()
        _session.ClearFields()
        Dim dateStr = DateTime.Now.ToString("MM/dd/yy")
        Dim timeStr = DateTime.Now.ToString("HH:mm:ss")

        _session.WriteText(1, 2, "PROGRAM: FLSHBK99", TN3270Color.Turquoise)
        _session.WriteText(1, 30, "ERROR DISPLAY", TN3270Color.Red)
        _session.WriteText(1, 65, $"DATE: {dateStr}", TN3270Color.Turquoise)
        _session.WriteText(2, 65, $"TIME: {timeStr}", TN3270Color.Turquoise)
        _session.WriteText(3, 1, StrDup(78, "-"), TN3270Color.Blue)

        ' Display error title
        If Not String.IsNullOrEmpty(_errorTitle) Then
            _session.WriteText(5, 2, _errorTitle, TN3270Color.Red)
            _session.WriteText(6, 1, StrDup(78, "="), TN3270Color.Red)
        End If

        ' Display main error message
        If Not String.IsNullOrEmpty(_errorMessage) Then
            Dim startRow = If(String.IsNullOrEmpty(_errorTitle), 5, 8)
            Dim lines = _errorMessage.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
            Dim row = startRow
            For Each line In lines
                If row > 18 Then Exit For ' Don't overflow screen
                _session.WriteText(row, 2, line.PadRight(76).Substring(0, Math.Min(76, line.Length)), TN3270Color.Yellow)
                row += 1
            Next
        End If

        ' Display technical details if available
        If Not String.IsNullOrEmpty(_errorDetails) Then
            _session.WriteText(20, 2, "TECHNICAL DETAILS:", TN3270Color.Turquoise)
            _session.WriteText(21, 2, _errorDetails.PadRight(76).Substring(0, Math.Min(76, _errorDetails.Length)), TN3270Color.White)
        End If

        _session.WriteText(23, 2, "PRESS ENTER OR PF3 TO CONTINUE", TN3270Color.White)
        _session.ShowScreen()
    End Sub

    Private Sub ProcessErrorInput(e As AidKeyEventArgs)
        ' Any key returns from error screen
        _mode = _previousMode
        
        ' Clear error state
        _errorTitle = ""
        _errorMessage = ""
        _errorDetails = ""
        
        ' Return to appropriate screen
        Select Case _mode
            Case ScreenMode.Login : ShowLogin()
            Case ScreenMode.Menu : ShowMenu()
            Case ScreenMode.Edit : ShowEdit()
            Case ScreenMode.EditEmail : ShowEditEmail()
            Case ScreenMode.ConfirmDelete : ShowConfirmDelete()
            Case ScreenMode.Users : ShowUsers()
            Case ScreenMode.AddUser : ShowAddUser()
            Case Else : ShowMenu()
        End Select
    End Sub

    Private Sub ShowUsers()
        _session.ClearFields()
        Dim dateStr = DateTime.Now.ToString("MM/dd/yy")
        Dim timeStr = DateTime.Now.ToString("HH:mm:ss")

        _session.WriteText(1, 2, "PROGRAM: FLSHBK02", TN3270Color.Turquoise)
        _session.WriteText(1, 25, "WEB USER MANAGEMENT", TN3270Color.White)
        _session.WriteText(1, 65, $"DATE: {dateStr}", TN3270Color.Turquoise)
        _session.WriteText(2, 65, $"TIME: {timeStr}", TN3270Color.Turquoise)
        _session.WriteText(3, 1, StrDup(78, "-"), TN3270Color.Blue)

        _session.WriteText(5, 2, "NO  USERNAME             HOME DIRECTORY", TN3270Color.Turquoise)
        _session.WriteText(6, 1, StrDup(78, "-"), TN3270Color.Blue)

        Dim users = UserManager.GetUsers()
        Dim row = 7
        For i = 0 To Math.Min(users.Count - 1, 12)
            Dim u = users(i)
            _session.WriteText(row, 2, (i + 1).ToString("00"), TN3270Color.Yellow)
            _session.WriteText(row, 6, u.Username.PadRight(20), TN3270Color.White)
            _session.WriteText(row, 27, u.HomeFolder.PadRight(50), TN3270Color.White)
            row += 1
        Next

        _session.WriteText(21, 1, StrDup(78, "-"), TN3270Color.Blue)
        _session.WriteText(22, 2, "COMMAND ==>", TN3270Color.Yellow)
        _session.AddField(22, 14, 40, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtUserCmd")
        _session.WriteText(23, 2, "ENTER:PROCESS   PF1:HELP   PF3:MENU (EXIT)", TN3270Color.White)
        _session.WriteText(24, 2, "COMMANDS: ADD, DELETE [NO]", TN3270Color.Turquoise)
        _session.ShowScreen()
    End Sub

    Private Sub ProcessUsersInput(e As AidKeyEventArgs)
        If e.AidKey = &HF3 Then
            _mode = ScreenMode.Menu
            ShowMenu()
            Return
        End If

        If e.AidKey <> &H7D Then
            ShowUsers()
            Return
        End If

        Dim cmd = _session.GetFieldValue("txtUserCmd")?.ToUpper().Trim()
        If cmd = "ADD" Then
            _mode = ScreenMode.AddUser
            ShowAddUser()
        ElseIf cmd.StartsWith("DELETE ") Then
            Dim idStr = cmd.Substring(7).Trim()
            Dim id As Integer
            Dim users = UserManager.GetUsers()
            If Integer.TryParse(idStr, id) AndAlso id > 0 AndAlso id <= users.Count Then
                UserManager.DeleteUser(users(id - 1).Username)
                _statusMsg = "User deleted."
                _statusColor = TN3270Color.Yellow
                ShowUsers()
            End If
        Else
            ShowUsers()
        End If
    End Sub

    Private Sub ShowAddUser()
        _session.ClearFields()
        _session.WriteText(1, 2, "PROGRAM: FLSHBK02", TN3270Color.Turquoise)
        _session.WriteText(1, 30, "ADD NEW WEB USER", TN3270Color.White)
        
        _session.WriteText(10, 20, "USERNAME: ", TN3270Color.Turquoise)
        _session.AddField(10, 30, 20, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtNewUser")
        
        _session.WriteText(12, 20, "PASSWORD: ", TN3270Color.Turquoise)
        _session.AddField(12, 30, 20, "", False, TN3270Color.Neutral, TN3270Color.Neutral, TN3270Highlight.None, "txtNewPass", TN3270Intensity.Hidden)
        
        _session.WriteText(14, 20, "HOME DIR: ", TN3270Color.Turquoise)
        _session.AddField(14, 30, 30, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtNewHome")

        _session.WriteText(22, 2, "ENTER:SAVE   PF3:CANCEL", TN3270Color.White)
        _session.ShowScreen()
    End Sub

    Private Sub ProcessAddUserInput(e As AidKeyEventArgs)
        If e.AidKey = &HF3 Then
            _mode = ScreenMode.Users
            ShowUsers()
            Return
        End If

        If e.AidKey <> &H7D Then
            ShowAddUser()
            Return
        End If

        Dim uname = _session.GetFieldValue("txtNewUser")?.Trim()
        Dim pass = _session.GetFieldValue("txtNewPass")?.Trim()
        Dim hdir = _session.GetFieldValue("txtNewHome")?.Trim()

        If Not String.IsNullOrEmpty(uname) AndAlso Not String.IsNullOrEmpty(pass) Then
            UserManager.AddUser(uname, pass, hdir)
            _statusMsg = "User added successfully."
            _statusColor = TN3270Color.Green
        End If

        _mode = ScreenMode.Users
        ShowUsers()
    End Sub
End Class
