Imports System.ServiceProcess
Imports System.Diagnostics
Imports System.IO

Public Class MainForm
    Inherits Form
    
    Private WithEvents trayIcon As NotifyIcon
    Private WithEvents trayMenu As ContextMenuStrip
    Private WithEvents statusTimer As Timer
    
    Private Const ServiceName As String = "FlashbackEngine"
    Private serviceController As ServiceController

    Public Sub New()
        ' Initialize components manually for a clean, lean app
        trayMenu = New ContextMenuStrip()
        trayMenu.Items.Add("Flashback Engine: Unknown", Nothing, AddressOf DoNothing).Enabled = False
        trayMenu.Items.Add("-")
        trayMenu.Items.Add("Start Engine", Nothing, AddressOf StartService)
        trayMenu.Items.Add("Stop Engine", Nothing, AddressOf StopService)
        trayMenu.Items.Add("-")
        trayMenu.Items.Add("Configure (Console)", Nothing, AddressOf OpenConsoleConfig)
        trayMenu.Items.Add("Configure (3270)", Nothing, AddressOf Open3270Config)
        trayMenu.Items.Add("View Log File", Nothing, AddressOf OpenLog)
        trayMenu.Items.Add("-")
        trayMenu.Items.Add("Exit", Nothing, AddressOf OnExit)

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
        Try
            If serviceController Is Nothing Then serviceController = New ServiceController(ServiceName)
            serviceController.Refresh()
            
            Dim status = serviceController.Status
            trayMenu.Items(0).Text = $"Engine: {status.ToString()}"
            
            Select Case status
                Case ServiceControllerStatus.Running
                    trayMenu.Items(2).Enabled = False ' Start
                    trayMenu.Items(3).Enabled = True  ' Stop
                    trayIcon.Text = "Flashback Engine: Running"
                Case ServiceControllerStatus.Stopped
                    trayMenu.Items(2).Enabled = True  ' Start
                    trayMenu.Items(3).Enabled = False ' Stop
                    trayIcon.Text = "Flashback Engine: Stopped"
                Case Else
                    trayMenu.Items(2).Enabled = False
                    trayMenu.Items(3).Enabled = False
            End Select
        Catch ex As Exception
            trayMenu.Items(0).Text = "Engine: Service Not Installed"
            trayMenu.Items(2).Enabled = False
            trayMenu.Items(3).Enabled = False
        End Try
    End Sub

    Private Sub StartService()
        Try
            serviceController?.Start()
            CheckStatus()
        Catch ex As Exception
            MessageBox.Show($"Failed to start service: {ex.Message}", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub StopService()
        Try
            serviceController?.Stop()
            CheckStatus()
        Catch ex As Exception
            MessageBox.Show($"Failed to stop service: {ex.Message}", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenConsoleConfig()
        Try
            Dim path = "Flashback.Config.Console.exe"
            If Not File.Exists(path) Then path = "..\Flashback.Config.Console\bin\Debug\net9.0\Flashback.Config.Console.exe"
            
            Process.Start(New ProcessStartInfo(path) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show("Could not launch Console Configuration utility.", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub Open3270Config()
        Try
            Dim path = "Flashback.Config.3270.exe"
            If Not File.Exists(path) Then path = "..\Flashback.Config.3270\bin\Debug\net9.0\Flashback.Config.3270.exe"
            
            Process.Start(New ProcessStartInfo(path) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show("Could not launch 3270 Configuration server.", "Flashback", MessageBoxButtons.OK, MessageBoxIcon.Warning)
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
