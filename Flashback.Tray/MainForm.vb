Imports System.ServiceProcess
Imports System.Diagnostics
Imports System.IO

Public Class MainForm
    Inherits Form
    
    Private WithEvents trayIcon As NotifyIcon
    Private WithEvents trayMenu As ContextMenuStrip
    Private WithEvents statusTimer As Timer
    
    Private Const EngineServiceName As String = "FlashbackEngine"
    Private Const Config3270ServiceName As String = "FlashbackConfig3270"
    
    Private engineController As ServiceController
    Private config3270Controller As ServiceController

    Public Sub New()
        ' Initialize components manually for a clean, lean app
        trayMenu = New ContextMenuStrip()
        
        ' Engine Service Section
        trayMenu.Items.Add("Engine: Unknown", Nothing, AddressOf DoNothing).Enabled = False
        trayMenu.Items.Add("Start Engine", Nothing, AddressOf StartEngine)
        trayMenu.Items.Add("Stop Engine", Nothing, AddressOf StopEngine)
        trayMenu.Items.Add("-")
        
        ' 3270 Config Service Section
        trayMenu.Items.Add("3270 Config: Unknown", Nothing, AddressOf DoNothing).Enabled = False
        trayMenu.Items.Add("Start 3270 Server", Nothing, AddressOf Start3270)
        trayMenu.Items.Add("Stop 3270 Server", Nothing, AddressOf Stop3270)
        trayMenu.Items.Add("-")
        
        ' Tools Section
        trayMenu.Items.Add("Configure (Console Tool)", Nothing, AddressOf OpenConsoleTool)
        trayMenu.Items.Add("View Log File", Nothing, AddressOf OpenLog)
        trayMenu.Items.Add("-")
        trayMenu.Items.Add("Exit Controller", Nothing, AddressOf OnExit)

        trayIcon = New NotifyIcon()
        trayIcon.Text = "Flashback Controller"
        trayIcon.Icon = SystemIcons.Application
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
        ' Update Engine
        UpdateServiceStatus(EngineServiceName, engineController, 0, 1, 2)
        
        ' Update 3270 Config
        UpdateServiceStatus(Config3270ServiceName, config3270Controller, 4, 5, 6)
        
        ' Tooltip
        Try
            Dim engineStatus = If(engineController IsNot Nothing, engineController.Status.ToString(), "Unknown")
            Dim configStatus = If(config3270Controller IsNot Nothing, config3270Controller.Status.ToString(), "Unknown")
            trayIcon.Text = $"FB Engine: {engineStatus} | 3270: {configStatus}"
        Catch
            trayIcon.Text = "Flashback Controller"
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
                Case ServiceControllerStatus.Stopped
                    trayMenu.Items(startIdx).Enabled = True
                    trayMenu.Items(stopIdx).Enabled = False
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
            Dim path = "printers.log"
            If File.Exists(path) Then
                Process.Start(New ProcessStartInfo("notepad.exe", path))
            Else
                MessageBox.Show("Log file not found.", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub OnExit()
        trayIcon.Visible = False
        Application.Exit()
    End Sub

    Private Sub DoNothing()
    End Sub
End Class
