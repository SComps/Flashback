Imports System.Collections.ObjectModel
Imports System.IO
Imports Flashback.Core

Class MainWindow
    Public Property Devices As New ObservableCollection(Of DeviceItem)
    Private configFile As String = "devices.dat"
    Private pwFile As String = "syspw.txt"
    Private themeFile As String = "uipalette.dat"
    Private _syspw As String = ""
    Private _loading As Boolean = True

    Public Sub New()
        ' STAGE 1: SAFE BOOT
        ' No themes, no styles, just raw initialization
        InitializeComponent()
        DataContext = Me
    End Sub

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            ' Load logic deferred until window is fully alive
            LoadSecurity()
            LoadConfig()
            
            If String.IsNullOrWhiteSpace(_syspw) Then
                overlayLock.Visibility = Visibility.Collapsed
                LoadDevices()
            End If
            
            ' Try to apply the saved theme, but catch any "BAML" or resource errors
            Try
                ApplyThemeMode()
            Catch
                ' Soft fallback: App remains in default Windows mode
            End Try
            
            _loading = False
        Catch
            _loading = False
            overlayLock.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Sub LoadConfig()
        cmbUITheme.SelectedIndex = 0
        If File.Exists(themeFile) Then
            Dim val = Microsoft.VisualBasic.Val(File.ReadAllText(themeFile).Trim())
            cmbUITheme.SelectedIndex = Math.Min(CInt(val), 2)
        End If
    End Sub

    ' STAGE 2: THEME INJECTION
    Private Sub ApplyThemeMode()
        Dim mode = cmbUITheme.SelectedIndex
        If mode = 1 Then UpdatePalette(False) ' Pro Midnight
        If mode = 2 Then UpdatePalette(True)  ' Pro Snow
        ' Mode 0 (Auto) removed for initial stability - focus on explicit overrides
    End Sub

    ' Late-binding of colors to avoid XAML Parse errors
    Private Sub UpdatePalette(isLight As Boolean)
        Try
            ' Define dynamic brushes only if safely accessible
            If isLight Then
                SetDynamicColor("BrushBackground", "#F3F3F7")
                SetDynamicColor("BrushSurface", "#FFFFFF")
                SetDynamicColor("BrushTextPrimary", "#1A1A1F")
            Else
                SetDynamicColor("BrushBackground", "#0F0F12")
                SetDynamicColor("BrushSurface", "#1A1A1F")
                SetDynamicColor("BrushTextPrimary", "#FFFFFF")
            End If
        Catch
        End Try
    End Sub

    Private Sub SetDynamicColor(name As String, hex As String)
        Try
            Dim color = DirectCast(ColorConverter.ConvertFromString(hex), Color)
            ' Check if existing, or create new
            If Application.Current.Resources.Contains(name) Then
                DirectCast(Application.Current.Resources(name), SolidColorBrush).Color = color
            Else
                Application.Current.Resources.Add(name, New SolidColorBrush(color))
            End If
        Catch
        End Try
    End Sub

    ' (Omitted: All remaining CRUD and signaling logic is already stable and remains in file)
    ' I'll provide the full file update below.

    Private Sub LoadDevices()
        Devices.Clear()
        If Not File.Exists(configFile) Then Return
        Try
            Dim lines = File.ReadAllLines(configFile)
            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim parts = line.Split(New String() {"||"}, StringSplitOptions.None)
                If parts.Length >= 6 Then
                    Devices.Add(New DeviceItem With {.Name = parts(0), .Description = parts(1), .Type = If(parts(2) = "0", "Printer", "3270 Terminal"), .Port = parts(4).Split(":"c).Last(), .FullRecord = parts})
                End If
            Next
            DeviceList.ItemsSource = Devices
        Catch
        End Try
    End Sub
    Private Sub LoadSecurity()
        If File.Exists(pwFile) Then _syspw = File.ReadAllText(pwFile).Trim()
    End Sub
    Private Sub cmbUITheme_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _loading Then Return
        File.WriteAllText(themeFile, cmbUITheme.SelectedIndex.ToString())
        ApplyThemeMode()
    End Sub
    Private Sub Nav_Click(sender As Object, e As RoutedEventArgs)
        viewPrinters.Visibility = Visibility.Collapsed
        viewSettings.Visibility = Visibility.Collapsed
        viewSecurity.Visibility = Visibility.Collapsed
        If sender Is btnNavPrinters Then viewPrinters.Visibility = Visibility.Visible
        If sender Is btnNavSettings Then viewSettings.Visibility = Visibility.Visible
        If sender Is btnNavSecurity Then viewSecurity.Visibility = Visibility.Visible
    End Sub
    Private Sub Login_Click(sender As Object, e As RoutedEventArgs)
        If pbLogin.Password.Trim() = _syspw Then
            overlayLock.Visibility = Visibility.Collapsed
            LoadDevices()
        Else
            lblLoginError.Visibility = Visibility.Visible
        End If
    End Sub
    Private Sub pbLogin_KeyDown(sender As Object, e As System.Windows.Input.KeyEventArgs)
        If e.Key = System.Windows.Input.Key.Enter Then Login_Click(Nothing, Nothing)
    End Sub
    Private Sub AddDevice_Click(sender As Object, e As RoutedEventArgs)
        Dim newRecord = New String() {"New Device", "Flashback Device", "0", "3", "127.0.0.1:9100", "0", "False", "True", "0", "Output", "0", "0"}
        Dim newItem As New DeviceItem With {.Name = "New Device", .Type = "Printer", .Port = "9100", .FullRecord = newRecord}
        Dim editWin As New EditDeviceWindow(newItem)
        editWin.Owner = Me
        If editWin.ShowDialog() = True Then
            Devices.Add(newItem)
            SaveDevices()
            LoadDevices()
        End If
    End Sub
    Private Sub SaveDevices()
        Try
            Dim lines As New List(Of String)
            For Each item In Devices
                Dim p = item.FullRecord
                p(0) = item.Name
                p(2) = If(item.Type = "Printer", "0", "1")
                Dim hostPart = p(4).Split(":"c)(0)
                p(4) = $"{hostPart}:{item.Port}"
                lines.Add(String.Join("||", p))
            Next
            File.WriteAllLines(configFile, lines)
        Catch
        End Try
    End Sub
    Private Sub Edit_Click(sender As Object, e As RoutedEventArgs)
        Dim item = CType(CType(sender, Button).DataContext, DeviceItem)
        If item Is Nothing Then Return
        Dim editWin As New EditDeviceWindow(item)
        editWin.Owner = Me
        If editWin.ShowDialog() = True Then
            SaveDevices()
            LoadDevices()
        End If
    End Sub
    Private Sub Delete_Click(sender As Object, e As RoutedEventArgs)
        Dim item = CType(CType(sender, Button).DataContext, DeviceItem)
        If item Is Nothing Then Return
        If MessageBox.Show($"Delete {item.Name}?", "Confirm", MessageBoxButton.YesNo) = MessageBoxResult.Yes Then
            Devices.Remove(item)
            SaveDevices()
        End If
    End Sub
    Private Sub ShowHelp_Click(sender As Object, e As RoutedEventArgs)
        overlayHelp.Visibility = Visibility.Visible
    End Sub
    Private Sub CloseHelp_Click(sender As Object, e As RoutedEventArgs)
        overlayHelp.Visibility = Visibility.Collapsed
    End Sub
    Private Sub Signal_Connect(sender As Object, e As RoutedEventArgs)
        Dim item = CType(CType(sender, Button).DataContext, DeviceItem)
        If item Is Nothing Then Return
        File.AppendAllText("commands.dat", $"{CType(sender, Button).Tag}||{item.Name}{vbCrLf}")
        MessageBox.Show("Signal Sent")
    End Sub
End Class
