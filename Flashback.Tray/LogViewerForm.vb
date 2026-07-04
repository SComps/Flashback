Imports System.IO
Imports System.Threading
Imports System.Timers

Public Class LogViewerForm
    Inherits Form
    
    Private WithEvents txtLog As TextBox
    Private WithEvents btnClose As Button
    Private WithEvents btnClear As Button
    Private WithEvents chkAutoScroll As CheckBox
    Private WithEvents fileWatcher As FileSystemWatcher
    Private updateTimer As Timers.Timer
    
    Private logFilePath As String
    Private lastFileSize As Long = 0
    Private maxLines As Integer = 1000
    
    Public Sub New(logPath As String)
        logFilePath = logPath
        InitializeComponents()
        LoadInitialLog()
        SetupFileWatcher()
    End Sub
    
    Private Sub InitializeComponents()
        Me.Text = "Flashback Log Viewer"
        Me.Size = New Size(900, 600)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.MinimumSize = New Size(600, 400)
        
        ' TextBox for log display
        txtLog = New TextBox()
        txtLog.Multiline = True
        txtLog.ScrollBars = ScrollBars.Both
        txtLog.WordWrap = False
        txtLog.ReadOnly = True
        
        ' Use Consolas (monospace), fallback to Courier New if not available
        Dim monoFont As Font
        Try
            monoFont = New Font("Consolas", 9.5F)
        Catch
            monoFont = New Font("Courier New", 9.5F)
        End Try
        txtLog.Font = monoFont
        txtLog.BackColor = Color.White
        txtLog.ForeColor = Color.Black
        txtLog.Dock = DockStyle.Fill
        
        ' Bottom panel for controls
        Dim pnlBottom As New Panel()
        pnlBottom.Height = 40
        pnlBottom.Dock = DockStyle.Bottom
        pnlBottom.Padding = New Padding(5)
        
        ' Auto-scroll checkbox
        chkAutoScroll = New CheckBox()
        chkAutoScroll.Text = "Auto-scroll"
        chkAutoScroll.Checked = True
        chkAutoScroll.Location = New Point(10, 10)
        chkAutoScroll.AutoSize = True
        
        ' Clear button
        btnClear = New Button()
        btnClear.Text = "Clear Display"
        btnClear.Location = New Point(120, 8)
        btnClear.AutoSize = True
        
        ' Close button
        btnClose = New Button()
        btnClose.Text = "Close"
        btnClose.Location = New Point(230, 8)
        btnClose.AutoSize = True
        
        pnlBottom.Controls.Add(chkAutoScroll)
        pnlBottom.Controls.Add(btnClear)
        pnlBottom.Controls.Add(btnClose)
        
        Me.Controls.Add(txtLog)
        Me.Controls.Add(pnlBottom)
        
        ' Update timer for periodic refresh
        updateTimer = New Timers.Timer(1000) ' Check every second
        AddHandler updateTimer.Elapsed, AddressOf UpdateTimer_Tick
        updateTimer.Start()
    End Sub
    
    Private Sub LoadInitialLog()
        Try
            If File.Exists(logFilePath) Then
                Dim fileInfo As New FileInfo(logFilePath)
                lastFileSize = fileInfo.Length
                
                ' Read last N lines
                Dim lines = File.ReadAllLines(logFilePath)
                Dim startIndex = Math.Max(0, lines.Length - maxLines)
                Dim displayLines = lines.Skip(startIndex).ToArray()
                
                txtLog.Text = String.Join(Environment.NewLine, displayLines)
                If chkAutoScroll.Checked Then
                    ScrollToBottom()
                End If
            Else
                txtLog.Text = "Log file not found: " & logFilePath
            End If
        Catch ex As Exception
            txtLog.Text = "Error loading log: " & ex.Message
        End Try
    End Sub
    
    Private Sub SetupFileWatcher()
        Try
            Dim directory = Path.GetDirectoryName(logFilePath)
            Dim fileName = Path.GetFileName(logFilePath)
            
            fileWatcher = New FileSystemWatcher(directory)
            fileWatcher.Filter = fileName
            fileWatcher.NotifyFilter = NotifyFilters.LastWrite Or NotifyFilters.Size
            fileWatcher.EnableRaisingEvents = True
        Catch ex As Exception
            ' File watcher setup failed, will rely on timer
        End Try
    End Sub
    
    Private Sub FileWatcher_Changed(sender As Object, e As FileSystemEventArgs) Handles fileWatcher.Changed
        ' Use BeginInvoke to update UI from file watcher thread
        If Me.InvokeRequired Then
            Me.BeginInvoke(New Action(AddressOf CheckForNewContent))
        Else
            CheckForNewContent()
        End If
    End Sub
    
    Private Sub UpdateTimer_Tick(sender As Object, e As ElapsedEventArgs)
        If Me.InvokeRequired Then
            Me.BeginInvoke(New Action(AddressOf CheckForNewContent))
        Else
            CheckForNewContent()
        End If
    End Sub
    
    Private Sub CheckForNewContent()
        Try
            If Not File.Exists(logFilePath) Then Return
            
            Dim fileInfo As New FileInfo(logFilePath)
            If fileInfo.Length > lastFileSize Then
                ' File has grown, read new content
                Using fs As New FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    fs.Seek(lastFileSize, SeekOrigin.Begin)
                    Using reader As New StreamReader(fs)
                        Dim newContent = reader.ReadToEnd()
                        If Not String.IsNullOrEmpty(newContent) Then
                            AppendLog(newContent)
                        End If
                    End Using
                End Using
                lastFileSize = fileInfo.Length
            ElseIf fileInfo.Length < lastFileSize Then
                ' File was truncated or recreated, reload
                lastFileSize = 0
                LoadInitialLog()
            End If
        Catch ex As Exception
            ' Ignore errors during file reading (file might be locked)
        End Try
    End Sub
    
    Private Sub AppendLog(content As String)
        txtLog.AppendText(content)
        
        ' Trim if too many lines
        Dim lines = txtLog.Lines
        If lines.Length > maxLines Then
            Dim keepLines = lines.Skip(lines.Length - maxLines).ToArray()
            txtLog.Text = String.Join(Environment.NewLine, keepLines)
        End If
        
        If chkAutoScroll.Checked Then
            ScrollToBottom()
        End If
    End Sub
    
    Private Sub ScrollToBottom()
        txtLog.SelectionStart = txtLog.Text.Length
        txtLog.ScrollToCaret()
    End Sub
    
    Private Sub BtnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        txtLog.Clear()
    End Sub
    
    Private Sub BtnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub
    
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        updateTimer?.Stop()
        fileWatcher?.Dispose()
        MyBase.OnFormClosing(e)
    End Sub
End Class
