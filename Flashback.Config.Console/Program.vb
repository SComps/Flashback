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
        If args.Length > 0 Then configFile = args(0)
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
                If parts.Length <> 2 Then
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
        System.Console.Clear()
        
        Dim bannerLine As String = New String("="c, max_Cols)
        Say(bannerLine, 0, 0, ConsoleColor.White)
        Say(CenterString("E D I T   D E V I C E", max_Cols), 0, 1, ConsoleColor.White)
        Say(bannerLine, 0, 2, ConsoleColor.White)
        
        Dim labelCol = 5
        Dim valCol = 26
        
        Say("       DEVICE NAME:", labelCol, 4, ConsoleColor.Cyan)
        Say("DEVICE DESCRIPTION:", labelCol, 5, ConsoleColor.Cyan)
        Say("       DEVICE TYPE:", labelCol, 6, ConsoleColor.Cyan)
        Say("   CONNECTION TYPE:", labelCol, 7, ConsoleColor.Cyan)
        Say("  OPERATING SYSTEM:", labelCol, 8, ConsoleColor.Cyan)
        Say("DEVICE DESTINATION:", labelCol, 9, ConsoleColor.Cyan)
        Say("        OUTPUT PDF:", labelCol, 10, ConsoleColor.Cyan)
        Say("       ORIENTATION:", labelCol, 11, ConsoleColor.Cyan)
        Say("        OUTPUT DIR:", labelCol, 12, ConsoleColor.Cyan)
        Say("   SHADING COLOR  :", labelCol, 13, ConsoleColor.Cyan)
        Say("   START JOB NO.  :", labelCol, 14, ConsoleColor.Cyan)

        Say("(0:Prn 1:Rdr)", 45, 6, ConsoleColor.DarkGray)
        Say("(0:Sock 1:File 2:Phys)", 45, 7, ConsoleColor.DarkGray)
        Say("(0-8, 0:MVS 8:ZOS)", 45, 8, ConsoleColor.DarkGray)
        Say("(0:Land 1:Port)", 45, 12, ConsoleColor.DarkGray)
        Say("(0:Green 1:Blue 2:None)", 45, 14, ConsoleColor.DarkGray)

        d.DevName = GetString(d.DevName, valCol, 4, 20, ConsoleColor.Yellow)
        d.DevDescription = GetString(d.DevDescription, valCol, 5, 40, ConsoleColor.Yellow)
        d.DevType = Val(GetString(d.DevType.ToString(), valCol, 6, 1, ConsoleColor.Yellow))
        d.ConnType = Val(GetString(d.ConnType.ToString(), valCol, 7, 1, ConsoleColor.Yellow))
        d.OS = CType(Val(GetString(Integer.Parse(d.OS).ToString(), valCol, 8, 1, ConsoleColor.Yellow)), OSType)
        d.DevDest = GetString(d.DevDest, valCol, 9, 50, ConsoleColor.Yellow)
        d.PDF = GetBool(d.PDF, valCol, 10)
        d.Orientation = Val(GetString(d.Orientation.ToString(), valCol, 11, 1, ConsoleColor.Yellow))
        d.OutDest = GetString(d.OutDest, valCol, 12, 50, ConsoleColor.Yellow)
        
        Dim shadeVal As Integer = GetString(CInt(d.Shading).ToString(), valCol, 13, 1, ConsoleColor.Yellow)
        d.Shading = CType(shadeVal, RenderPDF.ShadingColor)
        
        d.JobNumber = Val(GetString(d.JobNumber.ToString(), valCol, 14, 6, ConsoleColor.Yellow))

        Say("Save changes? (Y/n) ==> ", 0, 17, ConsoleColor.Green)
        If System.Console.ReadLine().ToUpper().StartsWith("Y") Then
            devList(idx) = d
        End If
    End Sub

    Private Function GetBool(current As Boolean, col As Integer, row As Integer) As Boolean
        Dim s As String = GetString(If(current, "YES", "NO"), col, row, 3, ConsoleColor.Yellow).ToUpper()
        Return (s = "YES" OrElse s = "Y" OrElse s = "TRUE" OrElse s = "1")
    End Function

    Private Function GetString(initialValue As String, col As Integer, row As Integer, maxLen As Integer, color As ConsoleColor) As String
        System.Console.SetCursorPosition(col, row)
        System.Console.ForegroundColor = color
        System.Console.Write(initialValue.PadRight(maxLen))
        System.Console.SetCursorPosition(col, row)
        
        Dim result As String = System.Console.ReadLine()
        If String.IsNullOrWhiteSpace(result) Then Return initialValue
        Return If(result.Length > maxLen, result.Substring(0, maxLen), result)
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
        System.Console.SetCursorPosition(13, 4)
        Return System.Console.ReadLine()
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

        Dim row As Integer = 8
        For i As Integer = StartShow To StopShow
            If i < 0 OrElse i >= devList.Count Then Continue For
            Dim d As Devs = devList(i)
            Say((i + 1).ToString("00"), 0, row, ConsoleColor.Yellow)
            Say(d.DevName.PadRight(15), 5, row, ConsoleColor.White)
            Say(d.DevDescription.PadRight(30), 21, row, ConsoleColor.White)
            Say(CInt(d.OS).ToString(), 52, row, ConsoleColor.White)
            Say(If(d.PDF, "YES", "NO "), 56, row, ConsoleColor.Yellow)
            Say(d.Shading.ToString().ToUpper(), 61, row, ConsoleColor.Green)
            
            Say($"   -> {d.DevDest}", 5, row + 1, ConsoleColor.DarkGray)
            row += 2
        Next

        Say("Commands: ADD, SAVE, EXIT, [ID] to Edit, DELETE [ID], CONNECT [ID], UP, DOWN", 0, max_Rows - 2, ConsoleColor.Cyan)
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
        configFile = Path.Combine(baseDir, "devices.dat")
        Dim list As New List(Of Devs)()
        If Not File.Exists(configFile) Then Return list
        Try
            Using r As New StreamReader(configFile)
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
                        
                        If p.Length = 12 Then
                            d.Shading = CType(Val(p(10)), RenderPDF.ShadingColor)
                            d.JobNumber = Val(p(11))
                        ElseIf p.Length >= 13 Then
                            d.Shading = CType(Val(p(11)), RenderPDF.ShadingColor)
                            d.JobNumber = Val(p(12))
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
End Module
