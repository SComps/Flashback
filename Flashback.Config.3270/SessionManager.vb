Imports TN3270Framework
Imports Flashback.Core
Imports System.IO

Public Enum ScreenMode
    Login
    Menu
    Edit
    ConfirmDelete
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
        _statusMsg = ""
        _statusColor = TN3270Color.White

        Select Case _mode
            Case ScreenMode.Login
                ProcessLoginInput(e)
            Case ScreenMode.Menu
                ProcessMenuInput(e)
            Case ScreenMode.Edit
                ProcessEditInput(e)
            Case ScreenMode.ConfirmDelete
                ProcessDeleteInput(e)
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

        Dim enteredPw = _session.GetFieldValue("txtPw")
        If enteredPw = _syspw Then
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
            If _hasUnsavedChanges Then SaveDevices()
            _session.WriteText(23, 2, "SESSION TERMINATED. AUTO-SAVE COMPLETE.", TN3270Color.Red)
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
            SaveDevices()
            _hasUnsavedChanges = False
            _statusMsg = "Devices saved successfully."
            _statusColor = TN3270Color.Green
            ShowMenu()
        ElseIf cmd = "EXIT" Then
            _session.Close()
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
        If e.AidKey = &HF3 Then
            _mode = ScreenMode.Menu
            ShowMenu()
            Return
        End If

        If e.AidKey <> &H7D Then
            ShowEdit()
            Return
        End If

        Dim d = _devList(_editingIndex)
        d.DevName = _session.GetFieldValue("txtName")?.Trim()
        d.DevDescription = _session.GetFieldValue("txtDesc")?.Trim()
        d.DevType = Val(_session.GetFieldValue("txtType"))
        d.ConnType = Val(_session.GetFieldValue("txtConn"))
        d.OS = CType(Val(_session.GetFieldValue("txtOS")), OSType)
        d.DevDest = _session.GetFieldValue("txtDest")?.Trim()
        
        Dim pdfVal = _session.GetFieldValue("txtPDF")?.Trim().ToUpper()
        d.PDF = (pdfVal = "TRUE" OrElse pdfVal = "1" OrElse pdfVal = "YES")
        
        d.Orientation = Val(_session.GetFieldValue("txtOrient"))
        d.OutDest = _session.GetFieldValue("txtOut")?.Trim()
        
        d.Shading = CType(Val(_session.GetFieldValue("txtShade")), RenderPDF.ShadingColor)
        d.JobNumber = Val(_session.GetFieldValue("txtJob"))

        _statusMsg = $"Device '{d.DevName}' updated."
        _statusColor = TN3270Color.Green
        _hasUnsavedChanges = True
        _mode = ScreenMode.Menu
        ShowMenu()
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
        _session.WriteText(12, 25, "SYSPW ===> ", TN3270Color.Yellow)
        _session.AddField(12, 36, 8, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.None, "txtPw").Intensity = TN3270Intensity.Hidden

        If Not String.IsNullOrEmpty(_statusMsg) Then
            _session.WriteText(15, 25, _statusMsg, _statusColor)
        End If

        _session.WriteText(22, 2, "ENTER:LOGIN   PF3:EXIT", TN3270Color.White)
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

        _session.WriteText(7, 2, "ID   NAME            DESCRIPTION                    OS  PDF  SHADE", TN3270Color.Turquoise)
        _session.WriteText(8, 1, StrDup(78, "-"), TN3270Color.Blue)

        Dim rowPos = 9
        For i = _startIndex To Math.Min(_startIndex + 3, _devList.Count - 1)
            Dim d = _devList(i)
            _session.WriteText(rowPos, 2, (i + 1).ToString("00"), TN3270Color.Yellow)
            _session.WriteText(rowPos, 7, d.DevName.PadRight(14).Substring(0, 14), TN3270Color.White)
            _session.WriteText(rowPos, 23, d.DevDescription.PadRight(29).Substring(0, 29), TN3270Color.White)
            _session.WriteText(rowPos, 54, CInt(d.OS).ToString(), TN3270Color.White)
            _session.WriteText(rowPos, 58, If(d.PDF, "YES ", "NO  "), TN3270Color.Pink)
            _session.WriteText(rowPos, 64, d.Shading.ToString().ToUpper(), TN3270Color.Green)
            
            _session.WriteText(rowPos + 1, 7, d.DevDest.PadRight(50).Substring(0, 50), TN3270Color.Green)
            rowPos += 3
        Next

        _session.WriteText(21, 1, StrDup(78, "-"), TN3270Color.Blue)
        _session.WriteText(22, 2, "ENTER:PROCESS   PF3:EXIT   PF7:UP   PF8:DOWN", TN3270Color.White)
        _session.WriteText(23, 2, "OS:(0)MVS (1)VMS (2)MPE (3)RSTS (4)VM370 (5)NOS (6)VM/SP (7)TNDY (8)ZOS", TN3270Color.Turquoise)
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
        _session.AddField(5, fieldCol, 15, d.DevName, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtName").Modified = True

        _session.WriteText(6, labelCol, " DEVICE DESCRIPTION:", TN3270Color.Turquoise)
        _session.AddField(6, fieldCol, 30, d.DevDescription, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtDesc").Modified = True

        _session.WriteText(8, labelCol, "        DEVICE TYPE:", TN3270Color.Turquoise)
        _session.AddField(8, fieldCol, 1, d.DevType.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtType").Modified = True

        _session.WriteText(9, labelCol, "    CONNECTION TYPE:", TN3270Color.Turquoise)
        _session.AddField(9, fieldCol, 1, d.ConnType.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtConn").Modified = True

        _session.WriteText(10, labelCol, "   OPERATING SYSTEM:", TN3270Color.Turquoise)
        _session.AddField(10, fieldCol, 1, CInt(d.OS).ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtOS").Modified = True

        _session.WriteText(12, labelCol, " DEVICE DESTINATION:", TN3270Color.Turquoise)
        _session.AddField(12, fieldCol, 50, d.DevDest, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtDest").Modified = True

        _session.WriteText(14, labelCol, "         OUTPUT PDF:", TN3270Color.Turquoise)
        _session.AddField(14, fieldCol, 10, d.PDF.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtPDF").Modified = True

        _session.WriteText(14, 42, "ORIENTATION:", TN3270Color.Turquoise)
        _session.AddField(14, 55, 1, d.Orientation.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtOrient").Modified = True

        _session.WriteText(16, labelCol, "   OUTPUT DIRECTORY:", TN3270Color.Turquoise)
        _session.AddField(16, fieldCol, 50, d.OutDest, False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtOut").Modified = True

        _session.WriteText(17, labelCol, "   SHADING COLOR   :", TN3270Color.Turquoise)
        _session.AddField(17, fieldCol, 1, CInt(d.Shading).ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtShade").Modified = True

        _session.WriteText(18, labelCol, "   NEXT JOB NUMBER :", TN3270Color.Turquoise)
        _session.AddField(18, fieldCol, 6, d.JobNumber.ToString(), False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtJob").Modified = True

        _session.WriteText(22, 2, "ENTER:SAVE   PF3:CANCEL", TN3270Color.White)
        _session.ShowScreen()
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

    Private Sub SaveDevices()
        Try
            Using writer As New StreamWriter(_configFile, append:=False)
                For Each d In _devList
                    writer.WriteLine($"{d.DevName}||{d.DevDescription}||{d.DevType}||{d.ConnType}||{d.DevDest}||" &
                                     $"{CInt(d.OS)}||True||{d.PDF}||{d.Orientation}||{d.OutDest}||" &
                                     $"{CInt(d.Shading)}||{d.JobNumber}")
                Next
            End Using
        Catch ex As Exception
            ' Log to console or elsewhere
        End Try
    End Sub
End Class
