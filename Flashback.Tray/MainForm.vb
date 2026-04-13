Imports System.ServiceProcess
Imports System.Diagnostics
Imports System.IO
Imports Flashback.Core

Public Class MainForm
    Inherits Form
    
    Private WithEvents trayIcon As NotifyIcon
    Private WithEvents trayMenu As ContextMenuStrip
    Private WithEvents statusTimer As Timer
    
    Private Const EngineServiceName As String = "FlashbackEngine"
    Private Const Config3270ServiceName As String = "FlashbackConfig3270"
    Private Const ConfigFile As String = "devices.dat"
    Private Const CommandFile As String = "commands.dat"
    
    Private engineController As ServiceController
    Private config3270Controller As ServiceController
    Private _deviceMenu As ToolStripMenuItem
    Private _fullConfigPath As String
    Private _fullCmdPath As String
    Private _trayIconHandle As IntPtr = IntPtr.Zero

    <System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Auto)>
    Private Shared Function DestroyIcon(ByVal hIcon As IntPtr) As Boolean
    End Function

    Public Sub New()
        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
        _fullConfigPath = Path.Combine(baseDir, ConfigFile)
        _fullCmdPath = Path.Combine(baseDir, CommandFile)
        trayMenu = New ContextMenuStrip()
        
        trayMenu.Items.Add("Engine: Unknown", Nothing, AddressOf DoNothing).Enabled = False
        trayMenu.Items.Add("Start Engine Service", Nothing, AddressOf StartEngine)
        trayMenu.Items.Add("Stop Engine Service", Nothing, AddressOf StopEngine)
        trayMenu.Items.Add("-")
        
        trayMenu.Items.Add("3270 Config: Unknown", Nothing, AddressOf DoNothing).Enabled = False
        trayMenu.Items.Add("Start 3270 Server", Nothing, AddressOf Start3270)
        trayMenu.Items.Add("Stop 3270 Server", Nothing, AddressOf Stop3270)
        trayMenu.Items.Add("-")

        _deviceMenu = New ToolStripMenuItem("Manage Devices")
        trayMenu.Items.Add(_deviceMenu)
        trayMenu.Items.Add("-")

        trayMenu.Items.Add("License: FREE NON-COMMERCIAL", Nothing, AddressOf DoNothing).Enabled = False
        trayMenu.Items.Add("-")
        
        trayMenu.Items.Add("Configure (Console Tool)", Nothing, AddressOf OpenConsoleTool)
        trayMenu.Items.Add("View Log File", Nothing, AddressOf OpenLog)
        trayMenu.Items.Add("-")
        trayMenu.Items.Add("Exit Controller", Nothing, AddressOf OnExit)

        trayIcon = New NotifyIcon()
        trayIcon.Text = "Flashback Controller"
        
        Dim iconPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "printer.png")
        If File.Exists(iconPath) Then
            Try
                Using originalBmp As New Bitmap(iconPath)
                    ' Resize to precisely 32x32 using 32bpp ARGB to guarantee alpha channel preservation
                    Using bmp As New Bitmap(32, 32, Imaging.PixelFormat.Format32bppArgb)
                        Using g As Graphics = Graphics.FromImage(bmp)
                            g.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                            g.DrawImage(originalBmp, New Rectangle(0, 0, 32, 32))
                        End Using
                        
                        ' Save the handle to a class level variable so it stays alive, keeping the icon visible!
                        _trayIconHandle = bmp.GetHicon()
                        trayIcon.Icon = Icon.FromHandle(_trayIconHandle)
                    End Using
                End Using
            Catch
                trayIcon.Icon = SystemIcons.Application
            End Try
        Else
            trayIcon.Icon = SystemIcons.Application
        End If

        trayIcon.ContextMenuStrip = trayMenu
        trayIcon.Visible = True

        statusTimer = New Timer()
        statusTimer.Interval = 5000
        AddHandler statusTimer.Tick, AddressOf CheckStatus
        statusTimer.Start()

        CheckStatus()
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        Me.Visible = False
        Me.ShowInTaskbar = False
    End Sub

    Private Sub CheckStatus(Optional sender As Object = Nothing, Optional e As EventArgs = Nothing)
        UpdateServiceStatus(EngineServiceName, engineController, 0, 1, 2)
        UpdateServiceStatus(Config3270ServiceName, config3270Controller, 4, 5, 6)
        UpdateDeviceMenu()
        UpdateLicenseStatus()
        
        Try
            Dim engineStatus = If(engineController IsNot Nothing, engineController.Status.ToString(), "Unknown")
            Dim configStatus = If(config3270Controller IsNot Nothing, config3270Controller.Status.ToString(), "Unknown")
            trayIcon.Text = $"Engine: {engineStatus} | 3270: {configStatus}"
        Catch
            trayIcon.Text = "Flashback Controller"
        End Try
    End Sub

    Private Sub UpdateLicenseStatus()
        Try
            Dim l = LicenseManager.GetLicenseInfo()
            If l.IsLicensed Then
                trayMenu.Items(10).Text = $"License: {l.LicensedTo} ({l.MaxPrinters} Prn)"
                trayMenu.Items(10).ForeColor = Drawing.Color.Navy
            Else
                trayMenu.Items(10).Text = "License: FREE NON-COMMERCIAL"
                trayMenu.Items(10).ForeColor = Drawing.Color.DarkGray
            End If
        Catch
        End Try
    End Sub

    Private Sub UpdateDeviceMenu()
        _deviceMenu.DropDownItems.Clear()
        
        If Not File.Exists(_fullConfigPath) Then
            _deviceMenu.DropDownItems.Add("No devices found").Enabled = False
            Return
        End If

        Try
            Dim lines = File.ReadAllLines(_fullConfigPath)
            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim parts = line.Split("||")
                If parts.Length > 0 Then
                    Dim dName = parts(0)
                    Dim dItem = New ToolStripMenuItem(dName)
                    
                    Dim connectBtn = New ToolStripMenuItem("Connect", Nothing, Sub() SendCommand("CONNECT", dName))
                    Dim disconnectBtn = New ToolStripMenuItem("Disconnect", Nothing, Sub() SendCommand("DISCONNECT", dName))
                    
                    dItem.DropDownItems.Add(connectBtn)
                    dItem.DropDownItems.Add(disconnectBtn)
                    _deviceMenu.DropDownItems.Add(dItem)
                End If
            Next
        Catch ex As Exception
            _deviceMenu.DropDownItems.Add("Error loading devices").Enabled = False
        End Try
    End Sub

    Private Sub SendCommand(cmd As String, devName As String)
        Try
            File.AppendAllText(_fullCmdPath, $"{cmd}||{devName}{vbCrLf}")
        Catch ex As Exception
            MessageBox.Show($"Failed to send command: {ex.Message}", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub UpdateServiceStatus(svcName As String, ByRef controller As ServiceController, labelIdx As Integer, startIdx As Integer, stopIdx As Integer)
        Try
            If controller Is Nothing Then controller = New ServiceController(svcName)
            controller.Refresh()
            
            Dim status = controller.Status
            trayMenu.Items(labelIdx).Text = $"{svcName.Replace("Flashback", "")}: {status.ToString()}"
            
            Select Case status
                Case ServiceControllerStatus.Running
                    trayMenu.Items(startIdx).Enabled = False
                    trayMenu.Items(stopIdx).Enabled = True
                    _deviceMenu.Enabled = True
                Case ServiceControllerStatus.Stopped
                    trayMenu.Items(startIdx).Enabled = True
                    trayMenu.Items(stopIdx).Enabled = False
                    _deviceMenu.Enabled = False ' Disable device control if engine is stopped
                Case Else
                    trayMenu.Items(startIdx).Enabled = False
                    trayMenu.Items(stopIdx).Enabled = False
            End Select
        Catch ex As Exception
            trayMenu.Items(labelIdx).Text = $"{svcName.Replace("Flashback", "")}: Not Installed"
            trayMenu.Items(startIdx).Enabled = False
            trayMenu.Items(stopIdx).Enabled = False
        End Try
    End Sub

    Private Sub StartEngine()
        Try
            engineController?.Start()
            CheckStatus()
        Catch ex As Exception
            MessageBox.Show($"Failed to start Engine: {ex.Message}", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub StopEngine()
        Try
            engineController?.Stop()
            CheckStatus()
        Catch ex As Exception
            MessageBox.Show($"Failed to stop Engine: {ex.Message}", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Start3270()
        Try
            config3270Controller?.Start()
            CheckStatus()
        Catch ex As Exception
            MessageBox.Show($"Failed to start 3270 Server: {ex.Message}", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Stop3270()
        Try
            config3270Controller?.Stop()
            CheckStatus()
        Catch ex As Exception
            MessageBox.Show($"Failed to stop 3270 Server: {ex.Message}", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenConsoleTool()
        Try
            Dim path = "Flashback.Config.Console.exe"
            If Not File.Exists(path) Then path = "..\Flashback.Config.Console\bin\Debug\net9.0\Flashback.Config.Console.exe"
            Process.Start(New ProcessStartInfo(path) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show("Could not launch Console Configuration utility.", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub OpenLog()
        Try
            Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory
            Dim path = System.IO.Path.Combine(baseDir, "printers.log")
            If File.Exists(path) Then
                Process.Start(New ProcessStartInfo("notepad.exe", $"""{path}""") With {.UseShellExecute = True})
            Else
                MessageBox.Show("Log file not found.", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub OnExit()
        trayIcon.Visible = False
        If _trayIconHandle <> IntPtr.Zero Then
            DestroyIcon(_trayIconHandle)
            _trayIconHandle = IntPtr.Zero
        End If
        Application.Exit()
    End Sub

    Private Sub DoNothing()
    End Sub
End Class
