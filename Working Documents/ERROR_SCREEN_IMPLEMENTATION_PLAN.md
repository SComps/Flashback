# Error Screen Implementation Plan for Flashback.Config.3270

## Overview

Instead of silently failing or showing brief status messages, implement a dedicated error screen that:
1. Clears the 3270 terminal
2. Displays the full error message with context
3. Waits for user acknowledgment (ENTER or PF3)
4. Returns to the previous screen

## Implementation Steps

### Step 1: Add Error Screen Mode

In [`SessionManager.vb:5-14`](Flashback.Config.3270/SessionManager.vb:5), add `Error` to the `ScreenMode` enum:

```vb
Public Enum ScreenMode
    Login
    Menu
    Edit
    EditEmail
    ConfirmDelete
    Help
    Users
    AddUser
    Error  ' ← Add this
End Enum
```

### Step 2: Add Error State Variables

In [`SessionManager.vb:16-29`](Flashback.Config.3270/SessionManager.vb:16), add variables to track error details:

```vb
Private _mode As ScreenMode = ScreenMode.Menu
Private _startIndex As Integer = 0
Private _editingIndex As Integer = -1
Private _statusMsg As String = ""
Private _statusColor As Byte = TN3270Color.White
Private _hasUnsavedChanges As Boolean = False
Private _previousMode As ScreenMode = ScreenMode.Menu
Private _errorTitle As String = ""        ' ← Add this
Private _errorMessage As String = ""      ' ← Add this
Private _errorDetails As String = ""      ' ← Add this
```

### Step 3: Create ShowError() Method

Add a new method to display the error screen:

```vb
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
        Dim lines = _errorMessage.Split(vbCrLf)
        Dim row = startRow
        For Each line In lines
            If row > 18 Then Exit For ' Don't overflow screen
            _session.WriteText(row, 2, line.PadRight(76).Substring(0, 76), TN3270Color.Yellow)
            row += 1
        Next
    End If

    ' Display technical details if available
    If Not String.IsNullOrEmpty(_errorDetails) Then
        _session.WriteText(20, 2, "TECHNICAL DETAILS:", TN3270Color.Turquoise)
        _session.WriteText(21, 2, _errorDetails.PadRight(76).Substring(0, 76), TN3270Color.White)
    End If

    _session.WriteText(23, 2, "PRESS ENTER OR PF3 TO CONTINUE", TN3270Color.White)
    _session.ShowScreen()
End Sub
```

### Step 4: Create ProcessErrorInput() Method

Add handler for error screen input:

```vb
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
```

### Step 5: Update HandleInput() Method

In [`SessionManager.vb:50-87`](Flashback.Config.3270/SessionManager.vb:50), add error case:

```vb
Public Sub HandleInput(sender As Object, e As AidKeyEventArgs)
    ' Debug: Log the key code received
    Console.WriteLine($"[DEBUG] Key received: 0x{e.AidKey:X2} in mode {_mode}")
    
    ' Intercept PF1 for global help (except on error screen)
    If e.AidKey = &HF1 AndAlso _mode <> ScreenMode.Error Then
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
        Case ScreenMode.Error  ' ← Add this
            ProcessErrorInput(e)
    End Select
End Sub
```

### Step 6: Update SaveDevices() Method

Modify [`SessionManager.vb:569-579`](Flashback.Config.3270/SessionManager.vb:569) to return error information:

```vb
Private Function SaveDevices(ByRef errorTitle As String, ByRef errorMessage As String, ByRef errorDetails As String) As Boolean
    Try
        ' Validate config file path
        Dim dir = Path.GetDirectoryName(_configFile)
        If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
            errorTitle = "CONFIGURATION SAVE FAILED"
            errorMessage = $"The configuration directory does not exist:{vbCrLf}{vbCrLf}{dir}{vbCrLf}{vbCrLf}Please ensure the application has proper permissions and the{vbCrLf}directory structure is intact."
            errorDetails = "Error: Directory not found"
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
```

### Step 7: Update SAVE Command Handler

Modify [`SessionManager.vb:147-153`](Flashback.Config.3270/SessionManager.vb:147) to use error screen:

```vb
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
```

### Step 8: Update Auto-Save on Exit

Modify [`SessionManager.vb:122-126`](Flashback.Config.3270/SessionManager.vb:122) to handle errors on exit:

```vb
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
```

## Benefits of This Approach

1. **Clear Error Communication**: Users see exactly what went wrong
2. **Actionable Information**: Error messages suggest what to do
3. **Professional UX**: Consistent with mainframe error handling patterns
4. **Debugging Support**: Technical details logged to console
5. **User Control**: User must acknowledge error before continuing
6. **Context Preservation**: Returns to the screen they were on

## Error Screen Layout

```
PROGRAM: FLSHBK99                ERROR DISPLAY                  DATE: 06/14/26
                                                                TIME: 22:24:32
------------------------------------------------------------------------------

PERMISSION DENIED
==============================================================================

Unable to write to configuration file:

/home/scott/flashback/devices.dat

The application does not have write permissions to this location.
Please check file and directory permissions.




TECHNICAL DETAILS:
Error: Access to the path '/home/scott/flashback/devices.dat' is denied.

PRESS ENTER OR PF3 TO CONTINUE
```

## Testing Checklist

- [ ] Test with permission denied error
- [ ] Test with file locked by another process
- [ ] Test with non-existent directory
- [ ] Test with disk full scenario
- [ ] Test with invalid characters in device names
- [ ] Verify error screen displays correctly
- [ ] Verify ENTER returns to menu
- [ ] Verify PF3 returns to menu
- [ ] Verify console logging works
- [ ] Verify auto-save error handling on exit