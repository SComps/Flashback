Imports System.Console
Imports System.IO
Imports System.Text
Imports Flashback.Core

Module Program
    Dim devList As New List(Of Devs)
    Private max_Rows As Integer = 24
    Private max_Cols As Integer = 80
    Private ErrMsg As String = ""
    Private configFile As String = "devices.dat"
    Private StartShow As Integer = 0
    Private StopShow As Integer = 0

    Sub Main(args As String())
        If args.Length > 0 Then
            Dim firstArg = args(0).ToLower()
            If firstArg = "-h" OrElse firstArg = "--help" Then
                ShowHelpCLI()
                Environment.Exit(0)
            End If

            If args.Length > 1 Then
                System.Console.WriteLine("Too many arguments.")
                ShowHelpCLI()
                Environment.Exit(1)
            End If

            If firstArg.StartsWith("-") Then
                System.Console.WriteLine($"Unknown option: {args(0)}")
                ShowHelpCLI()
                Environment.Exit(1)
            End If

            configFile = args(0)
        End If

        AddHandler System.Console.CancelKeyPress, AddressOf Console_CancelKeyPress
        
        max_Rows = System.Console.WindowHeight
        max_Cols = System.Console.WindowWidth
        
        If max_Rows < 24 Or max_Cols < 80 Then
            System.Console.WriteLine("CONFIGURATION: Your terminal window must be at least 80x24.")
            Return
        End If

        System.Console.Clear()
        If Not File.Exists(configFile) Then
            File.Create(configFile).Dispose()
        End If

        devList = LoadDevs()
        StartShow = 0
        StopShow = StartShow + 4
        If StopShow > devList.Count - 1 Then StopShow = devList.Count - 1

        Do While True
            DisplayMenu()
            Dim sel As String = GetCmd()
            If sel Is Nothing Then sel = ""
            sel = sel.ToUpper().Trim()

            If sel.StartsWith("DELETE") Then
                Dim parts As String() = sel.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                if parts.Length <> 2 Then
                    SetError("Invalid command structure.")
                Else
                    Dim id As Integer = Val(parts(1))
                    If id <= 0 OrElse id > devList.Count Then
                        SetError("Invalid entry ID.")
                    Else
                        System.Console.SetCursorPosition(1, 5)
                        System.Console.ForegroundColor = ConsoleColor.Red
                        System.Console.Write($"Press Y to confirm deletion of item {id} ===> ")
                        ConsoleResetColor()
                        If System.Console.ReadKey().KeyChar.ToString().ToUpper() = "Y" Then
                            devList.RemoveAt(id - 1)
                            If StartShow >= devList.Count AndAlso devList.Count > 0 Then StartShow = Math.Max(0, devList.Count - 5)
                            StopShow = Math.Min(devList.Count - 1, StartShow + 4)
                        Else
                            SetError("Deletion cancelled.")
                        End If
                    End If
                End If
            ElseIf sel.StartsWith("CONNECT") Then
                Dim parts = sel.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length = 2 Then
                    Dim id = Val(parts(1))
                    If id > 0 AndAlso id <= devList.Count Then
                        SendEngineCommand("CONNECT", devList(id - 1).DevName)
                        SetError($"Connect signal sent for {devList(id - 1).DevName}")
                    End If
                End If
            ElseIf sel.StartsWith("DISCONNECT") Then
                Dim parts = sel.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
                If parts.Length = 2 Then
                    Dim id = Val(parts(1))
                    If id > 0 AndAlso id <= devList.Count Then
                        SendEngineCommand("DISCONNECT", devList(id - 1).DevName)
                        SetError($"Disconnect signal sent for {devList(id - 1).DevName}")
                    End If
                End If
            Else
                Select Case sel
                    Case "SAVE"
                        SaveDevices()
                        SetError("Configuration saved.")
                    Case "ADD"
                        Dim nd As New Devs()
                        devList.Add(nd)
                        EditItem(devList.Count)
                        StopShow = Math.Min(devList.Count - 1, StartShow + 4)
                    Case "EXIT"
                        If OkToQuit() Then
                            System.Console.ResetColor()
                            System.Console.Clear()
                            Environment.Exit(0)
                        End If
                    Case "D", "DOWN"
                        If StopShow < devList.Count - 1 Then
                            StartShow += 5
                            If StartShow >= devList.Count Then StartShow = Math.Max(0, devList.Count - 1)
                            StopShow = Math.Min(devList.Count - 1, StartShow + 4)
                        End If
                    Case "U", "UP"
                        StartShow = Math.Max(0, StartShow - 5)
                        StopShow = Math.Min(devList.Count - 1, StartShow + 4)
                    Case ""
                        ' Ignore empty
                    Case Else
                        Dim itemID As Integer = Val(sel)
                        If itemID > 0 AndAlso itemID <= devList.Count Then
                            EditItem(itemID)
                        Else
                            SetError($"ERR: Invalid Command {sel}")
                        End If
                End Select
            End If
        Loop
    End Sub

    Private Function OkToQuit() As Boolean
        System.Console.Clear()
        Say("Unsaved changes may be lost. Are you sure? [Y/n] ==> ", 0, 0, ConsoleColor.White)
        Return System.Console.ReadLine().ToUpper().StartsWith("Y")
    End Function

    Private Sub EditItem(itemIdx As Integer)
        Dim idx As Integer = itemIdx - 1
        Dim d As Devs = devList(idx)

        Dim valCol = 26

        ' Internal helper for edit screen redraw
        Dim RedrawEdit As Action = Sub()
                                       EditItemHeader()
                                       DrawEditLabels()
                                       ' Print all existing data so it's visible before you cursor into the field
                                       Say(If(d.DevName, "").PadRight(20), valCol, 4, ConsoleColor.Yellow)
                                       Say(If(d.DevDescription, "").PadRight(40), valCol, 5, ConsoleColor.Yellow)
                                       Say(d.DevType.ToString().PadRight(1), valCol, 6, ConsoleColor.Yellow)
                                       Say(d.ConnType.ToString().PadRight(1), valCol, 7, ConsoleColor.Yellow)
                                       Say(CInt(d.OS).ToString().PadRight(2), valCol, 8, ConsoleColor.Yellow)
                                       Say(If(d.DevDest, "").PadRight(50), valCol, 9, ConsoleColor.Yellow)
                                       Say(If(d.PDF, "YES", "NO").PadRight(3), valCol, 10, ConsoleColor.Yellow)
                                       Say(d.Orientation.ToString().PadRight(1), valCol, 11, ConsoleColor.Yellow)
                                       Say(If(d.OutDest, "").PadRight(50), valCol, 12, ConsoleColor.Yellow)
                                       Say(CInt(d.Shading).ToString().PadRight(1), valCol, 13, ConsoleColor.Yellow)
                                       Say(d.JobNumber.ToString().PadRight(6), valCol, 14, ConsoleColor.Yellow)
                                   End Sub

        RedrawEdit.Invoke()

        d.DevName = GetStringWithHelp(d.DevName, valCol, 4, 20, ConsoleColor.Yellow, RedrawEdit)
        d.DevDescription = GetStringWithHelp(d.DevDescription, valCol, 5, 40, ConsoleColor.Yellow, RedrawEdit)
        d.DevType = Val(GetStringWithHelp(d.DevType.ToString(), valCol, 6, 1, ConsoleColor.Yellow, RedrawEdit))
        d.ConnType = Val(GetStringWithHelp(d.ConnType.ToString(), valCol, 7, 1, ConsoleColor.Yellow, RedrawEdit))
        d.OS = CType(Val(GetStringWithHelp(CInt(d.OS).ToString(), valCol, 8, 2, ConsoleColor.Yellow, RedrawEdit)), OSType)
        d.DevDest = GetStringWithHelp(d.DevDest, valCol, 9, 50, ConsoleColor.Yellow, RedrawEdit)
        d.PDF = GetBoolWithHelp(d.PDF, valCol, 10, RedrawEdit)
        d.Orientation = Val(GetStringWithHelp(d.Orientation.ToString(), valCol, 11, 1, ConsoleColor.Yellow, RedrawEdit))
        d.OutDest = GetStringWithHelp(d.OutDest, valCol, 12, 50, ConsoleColor.Yellow, RedrawEdit)
        
        Dim shadeVal As Integer = Val(GetStringWithHelp(CInt(d.Shading).ToString(), valCol, 13, 1, ConsoleColor.Yellow, RedrawEdit))
        d.Shading = CType(shadeVal, RenderPDF.ShadingColor)
        
        d.JobNumber = Val(GetStringWithHelp(d.JobNumber.ToString(), valCol, 14, 6, ConsoleColor.Yellow, RedrawEdit))

        Say("Save changes? (Y/n) ==> ", 0, 17, ConsoleColor.Green)
        Dim saveInput As String = ""
        If Not GetInputWithHelp(saveInput, 25, 17, 1, ConsoleColor.White, AddressOf DisplayMenu) Then
            ' If they hit F1 here, we just loop back and ask again
            EditItem(itemIdx)
            Return
        End If

        If saveInput.ToUpper().StartsWith("Y") Then
            devList(idx) = d
        End If
    End Sub

    Private Sub ShowHelpCLI()
        System.Console.WriteLine("Flashback Console Configuration Tool")
        System.Console.WriteLine("Usage: Flashback.Config.Console [config_file]")
        System.Console.WriteLine()
        System.Console.WriteLine("Options:")
        System.Console.WriteLine("  -h, --help            Show this help message")
        System.Console.WriteLine()
    End Sub

    Private Sub DisplayHelp()
        ConsoleResetColor()
        System.Console.Clear()
        Dim bannerLine As String = New String("="c, max_Cols)
        Say(bannerLine, 0, 0, ConsoleColor.White)
        Say(CenterString("C O N S O L E   H E L P", max_Cols), 0, 1, ConsoleColor.White)
        Say(bannerLine, 0, 2, ConsoleColor.White)

        Say("FIELD DESCRIPTIONS:", 2, 4, ConsoleColor.Yellow)
        Say("DEVICE TYPE     : 0=Generic, 1=Printer, 2=Plotter", 2, 6, ConsoleColor.Cyan)
        Say("CONN TYPE       : 0=Socket (Connect to Host), 1=File, 2=Physical, 3=Raw", 2, 7, ConsoleColor.Cyan)
        Say("OPERATING SYSTEM: The profile used to parse job headers (0-9).", 2, 8, ConsoleColor.Cyan)
        Say("DESTINATION     : For Conn 0: Host:Port. For Conn 3: Local Listen Port.", 2, 9, ConsoleColor.Cyan)
        Say("OUTPUT PDF      : Set to YES to generate PDF files.", 2, 10, ConsoleColor.Cyan)
        Say("ORIENTATION     : 0=Portrait, 1=Landscape.", 2, 11, ConsoleColor.Cyan)
        Say("SHADING COLOR   : (0)Plain (1)Green Bar (2)Blue Bar (3)Gray Bar.", 2, 12, ConsoleColor.Cyan)

        Say("COMMANDS:", 2, 15, ConsoleColor.Yellow)
        Say("ADD, SAVE, EXIT, [ID] to Edit, DELETE [ID], CONNECT [ID], UP, DOWN", 2, 17, ConsoleColor.Cyan)

        Say("Press any key to return...", 2, 20, ConsoleColor.White)
        System.Console.ReadKey(True)
    End Sub

    Private Function GetInputWithHelp(ByRef result As String, col As Integer, row As Integer, maxLength As Integer, color As ConsoleColor, redrawAction As Action) As Boolean
        ' Returns True if Enter was pressed
        ' Returns False if F1 was pressed
        Dim sb As New StringBuilder(result)
        System.Console.SetCursorPosition(col, row)
        System.Console.ForegroundColor = color
        System.Console.Write(sb.ToString().PadRight(maxLength))
        System.Console.SetCursorPosition(col + sb.Length, row)

        Do
            Dim key = System.Console.ReadKey(True)
            If key.Key = ConsoleKey.F1 Then
                DisplayHelp()
                If redrawAction IsNot Nothing Then redrawAction()
                Return False
            End If

            If key.Key = ConsoleKey.Enter Then
                result = sb.ToString()
                Return True
            End If

            If key.Key = ConsoleKey.Backspace Then
                If sb.Length > 0 Then
                    sb.Length -= 1
                    System.Console.SetCursorPosition(col + sb.Length, row)
                    System.Console.Write(" ")
                    System.Console.SetCursorPosition(col + sb.Length, row)
                End If
            ElseIf Not Char.IsControl(key.KeyChar) AndAlso sb.Length < maxLength Then
                sb.Append(key.KeyChar)
                System.Console.Write(key.KeyChar)
            End If
        Loop
    End Function

    Private Function GetStringWithHelp(initialValue As String, col As Integer, row As Integer, maxLen As Integer, color As ConsoleColor, redrawAction As Action) As String
        Dim result As String = initialValue
        Do While Not GetInputWithHelp(result, col, row, maxLen, color, redrawAction)
            ' Loop continues if F1 was pressed (GetInputWithHelp returns False)
            ' redrawAction is called inside GetInputWithHelp
        Loop
        Return result
    End Function

    Private Function GetBoolWithHelp(current As Boolean, col As Integer, row As Integer, redrawAction As Action) As Boolean
        Dim s As String = If(current, "YES", "NO")
        Do While Not GetInputWithHelp(s, col, row, 3, ConsoleColor.Yellow, redrawAction)
            s = If(current, "YES", "NO")
        Loop
        s = s.ToUpper()
        Return (s = "YES" OrElse s = "Y" OrElse s = "TRUE" OrElse s = "1")
    End Function

    Private Sub Say(txt As String, col As Integer, row As Integer, color As ConsoleColor)
        System.Console.SetCursorPosition(col, row)
        System.Console.ForegroundColor = color
        System.Console.Write(txt)
    End Sub

    Private Function CenterString(s As String, width As Integer) As String
        If s.Length >= width Then Return s
        Dim leftPadding As Integer = (width - s.Length) \ 2
        Return New String(" "c, leftPadding) & s
    End Function

    Private Sub SetError(msg As String)
        ErrMsg = msg
        If Not String.IsNullOrEmpty(msg) Then System.Console.Beep()
    End Sub

    Private Sub ConsoleResetColor()
        System.Console.ForegroundColor = ConsoleColor.White
        System.Console.BackgroundColor = ConsoleColor.Black
    End Sub

    Private Sub Console_CancelKeyPress(sender As Object, e As ConsoleCancelEventArgs)
        e.Cancel = True
        SetError("Use EXIT command.")
    End Sub

    Private Function GetCmd() As String
        Say("COMMAND ==> ", 1, 4, ConsoleColor.White)
        Dim cmd As String = ""
        Do While Not GetInputWithHelp(cmd, 13, 4, 40, ConsoleColor.White, AddressOf DisplayMenu)
            ' Loop if help was shown
        Loop
        Return cmd
    End Function

    Private Sub DisplayMenu()
        ConsoleResetColor()
        System.Console.Clear()
        Dim bannerLine As String = New String("="c, max_Cols)
        Say(bannerLine, 0, 0, ConsoleColor.White)
        Say($" FLASHBACK DEVICE CONFIGURATION [{configFile}] - {devList.Count} devices.", 0, 1, ConsoleColor.White)
        Say(bannerLine, 0, 2, ConsoleColor.White)
        
        Say(ErrMsg, 1, 5, ConsoleColor.Red)
        ErrMsg = ""

        Say("ID   NAME            DESCRIPTION                    OS  PDF   SHADE", 0, 6, ConsoleColor.Cyan)
        Say(New String("-"c, max_Cols), 0, 7, ConsoleColor.Blue)

        Dim rowCount As Integer = 8
        For i As Integer = StartShow To StopShow
            If i < 0 OrElse i >= devList.Count Then Continue For
            Dim d As Devs = devList(i)
            Say((i + 1).ToString("00"), 0, rowCount, ConsoleColor.Yellow)
            Say(d.DevName.PadRight(15), 5, rowCount, ConsoleColor.White)
            Say(d.DevDescription.PadRight(30), 21, rowCount, ConsoleColor.White)
            Say(CInt(d.OS).ToString(), 52, rowCount, ConsoleColor.White)
            Say(If(d.PDF, "YES", "NO "), 56, rowCount, ConsoleColor.Yellow)
            Say(d.Shading.ToString().ToUpper(), 61, rowCount, ConsoleColor.Green)

            Say($"   -> {d.DevDest}", 5, rowCount + 1, ConsoleColor.Yellow)
            rowCount += 2
        Next

        Say("Commands: ADD, SAVE, EXIT, [ID] to Edit, DELETE [ID], CONNECT [ID], UP, DOWN", 0, max_Rows - 2, ConsoleColor.Cyan)
        Say("F1: Help", max_Cols - 10, 1, ConsoleColor.Yellow)
    End Sub

    Private Sub SendEngineCommand(cmd As String, devName As String)
        Try
            Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
            Dim cmdPath As String = Path.Combine(baseDir, "commands.dat")
            File.AppendAllText(cmdPath, $"{cmd}||{devName}{vbCrLf}")
        Catch
        End Try
    End Sub

    Private Function LoadDevs() As List(Of Devs)
        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
        Dim configPath As String = Path.Combine(baseDir, "devices.dat")
        Dim list As New List(Of Devs)()
        If Not File.Exists(configPath) Then Return list
        Try
            Using r As New StreamReader(configPath)
                While Not r.EndOfStream
                    Dim line As String = r.ReadLine()
                    If String.IsNullOrWhiteSpace(line) Then Continue While
                    Dim p() As String = line.Split("||", StringSplitOptions.TrimEntries)
                    If p.Length >= 10 Then
                        Dim d As New Devs()
                        d.DevName = p(0)
                        d.DevDescription = p(1)
                        d.DevType = Val(p(2))
                        d.ConnType = Val(p(3))
                        d.DevDest = p(4)
                        d.OS = CType(Val(p(5)), OSType)
                        d.PDF = (p(7) = "True")
                        d.Orientation = Val(p(8))
                        d.OutDest = p(9)
                        
                        If p.Length >= 12 Then
                            d.Shading = CType(Val(p(10)), RenderPDF.ShadingColor)
                            d.JobNumber = Val(p(11))
                        End If
                        list.Add(d)
                    End If
                End While
            End Using
        Catch ex As Exception
            SetError("Error loading config: " & ex.Message)
        End Try
        Return list
    End Function

    Private Sub SaveDevices()
        Try
            Using w As New StreamWriter(configFile, False)
                For Each d As Devs In devList
                    w.WriteLine($"{d.DevName}||{d.DevDescription}||{d.DevType}||{d.ConnType}||{d.DevDest}||" &
                                $"{CInt(d.OS)}||True||{d.PDF}||{d.Orientation}||{d.OutDest}||" &
                                $"{CInt(d.Shading)}||{d.JobNumber}")
                Next
            End Using
        Catch ex As Exception
            SetError("Error saving config: " & ex.Message)
        End Try
    End Sub

    Private Sub EditItemHeader()
        System.Console.Clear()
        Dim bannerLine As String = New String("="c, max_Cols)
        Say(bannerLine, 0, 0, ConsoleColor.White)
        Say(CenterString("E D I T   D E V I C E", max_Cols), 0, 1, ConsoleColor.White)
        Say(bannerLine, 0, 2, ConsoleColor.White)
        Say("F1: Help", max_Cols - 10, 1, ConsoleColor.Yellow)
    End Sub

    Private Sub DrawEditLabels()
        Dim labelColSum = 5
        Say("       DEVICE NAME:", labelColSum, 4, ConsoleColor.Cyan)
        Say("DEVICE DESCRIPTION:", labelColSum, 5, ConsoleColor.Cyan)
        Say("       DEVICE TYPE:", labelColSum, 6, ConsoleColor.Cyan)
        Say("   CONNECTION TYPE:", labelColSum, 7, ConsoleColor.Cyan)
        Say("  OPERATING SYSTEM:", labelColSum, 8, ConsoleColor.Cyan)
        Say("DEVICE DESTINATION:", labelColSum, 9, ConsoleColor.Cyan)
        Say("        OUTPUT PDF:", labelColSum, 10, ConsoleColor.Cyan)
        Say("       ORIENTATION:", labelColSum, 11, ConsoleColor.Cyan)
        Say("        OUTPUT DIR:", labelColSum, 12, ConsoleColor.Cyan)
        Say("   SHADING COLOR  :", labelColSum, 13, ConsoleColor.Cyan)
        Say("   START JOB NO.  :", labelColSum, 14, ConsoleColor.Cyan)

        Say("(0:Prn 1:Rdr)", 45, 6, ConsoleColor.DarkGray)
        Say("(0:Sock 1:File 2:Phys 3:Raw)", 45, 7, ConsoleColor.DarkGray)
        Say("(0-9, 0:MVS 9:GENERIC)", 45, 8, ConsoleColor.DarkGray)
        Say("(0:Land 1:Port)", 45, 12, ConsoleColor.DarkGray)
        Say("(0:Green 1:Blue 2:None)", 45, 14, ConsoleColor.DarkGray)
    End Sub
End Module
