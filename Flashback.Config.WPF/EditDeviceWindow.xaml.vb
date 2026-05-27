Public Class EditDeviceWindow
    Public Property Device As DeviceItem

    Public Sub New(item As DeviceItem)
        Try
            InitializeComponent()
            Device = item
            
            ' Tab: Identity
            txtName.Text = item.Name
            txtDesc.Text = item.Description
            cmbOS.SelectedIndex = Math.Min(Val(item.FullRecord(5)), Math.Max(0, cmbOS.Items.Count - 1))
            
            If item.FullRecord.Length >= 13 Then
                chkEnabled.IsChecked = (item.FullRecord(12).ToLower() = "true")
            Else
                chkEnabled.IsChecked = True
            End If

            ' Tab: Network
            cmbType.SelectedIndex = If(item.FullRecord(2) = "0", 0, 1)
            
            Dim connVal = Val(item.FullRecord(3))
            If connVal = 3 Then
                cmbConn.SelectedIndex = 1
            ElseIf connVal = 1 Then
                cmbConn.SelectedIndex = 2
            Else
                cmbConn.SelectedIndex = 0
            End If
            
            txtDest.Text = item.FullRecord(4)

            ' Tab: Rendering
            chkPDF.IsChecked = (item.FullRecord(7).ToLower() = "true")
            cmbOrient.SelectedIndex = Math.Min(Val(item.FullRecord(8)), Math.Max(0, cmbOrient.Items.Count - 1))
            txtOut.Text = item.FullRecord(9)
            cmbShade.SelectedIndex = Math.Min(Val(item.FullRecord(10)), Math.Max(0, cmbShade.Items.Count - 1))
            
            ' Tab: Email (fields 13-23, backward compatible)
            If item.FullRecord.Length >= 14 Then chkEmailEnabled.IsChecked = (item.FullRecord(13).ToLower() = "true")
            If item.FullRecord.Length >= 15 Then txtEmailRecipients.Text = item.FullRecord(14)
            If item.FullRecord.Length >= 16 Then txtSmtpServer.Text = item.FullRecord(15)
            If item.FullRecord.Length >= 17 Then txtSmtpPort.Text = item.FullRecord(16)
            If item.FullRecord.Length >= 18 Then txtSmtpUsername.Text = item.FullRecord(17)
            If item.FullRecord.Length >= 19 Then txtSmtpPassword.Password = item.FullRecord(18)
            If item.FullRecord.Length >= 20 Then chkSmtpUseTLS.IsChecked = (item.FullRecord(19).ToLower() = "true")
            If item.FullRecord.Length >= 21 Then txtEmailFrom.Text = item.FullRecord(20)
            If item.FullRecord.Length >= 22 Then txtEmailFromName.Text = item.FullRecord(21)
            If item.FullRecord.Length >= 23 Then txtEmailSubject.Text = item.FullRecord(22)
            If item.FullRecord.Length >= 24 Then txtEmailBody.Text = item.FullRecord(23)
        Catch ex As Exception
            MessageBox.Show("Editor Init Error: " & ex.Message & vbCrLf & ex.StackTrace)
        End Try
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        Try
            ' Ensure array is large enough for all fields including email
            If Device.FullRecord.Length < 24 Then
                ReDim Preserve Device.FullRecord(23)
            End If
            
            Device.Name = txtName.Text
            Device.Description = txtDesc.Text
            Device.FullRecord(0) = txtName.Text
            Device.FullRecord(1) = txtDesc.Text
            Device.FullRecord(2) = If(cmbType.SelectedIndex = 0, "0", "1")
            Device.FullRecord(5) = cmbOS.SelectedIndex.ToString()
            Device.FullRecord(3) = If(cmbConn.SelectedIndex = 1, "3", If(cmbConn.SelectedIndex = 2, "1", "0"))
            Device.FullRecord(4) = txtDest.Text
            Device.Port = txtDest.Text.Split(":"c).Last()
            Device.FullRecord(7) = chkPDF.IsChecked.ToString()
            Device.FullRecord(8) = cmbOrient.SelectedIndex.ToString()
            Device.FullRecord(9) = txtOut.Text
            Device.FullRecord(10) = cmbShade.SelectedIndex.ToString()
            Device.FullRecord(12) = chkEnabled.IsChecked.ToString()
            Device.Enabled = chkEnabled.IsChecked.Value
            
            ' Save email configuration (fields 13-23)
            Device.FullRecord(13) = chkEmailEnabled.IsChecked.ToString()
            Device.FullRecord(14) = If(txtEmailRecipients.Text, "")
            Device.FullRecord(15) = If(txtSmtpServer.Text, "")
            Device.FullRecord(16) = If(txtSmtpPort.Text, "587")
            Device.FullRecord(17) = If(txtSmtpUsername.Text, "")
            Device.FullRecord(18) = If(txtSmtpPassword.Password, "")
            Device.FullRecord(19) = chkSmtpUseTLS.IsChecked.ToString()
            Device.FullRecord(20) = If(txtEmailFrom.Text, "flashback@localhost")
            Device.FullRecord(21) = If(txtEmailFromName.Text, "Flashback Print Server")
            Device.FullRecord(22) = If(txtEmailSubject.Text, "Print Job: {JobName} from {DeviceName}")
            Device.FullRecord(23) = If(txtEmailBody.Text, "")

            Me.DialogResult = True
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("Save Error: " & ex.Message)
        End Try
    End Sub

    Private Sub Cancel_Click(sender As Object, e As RoutedEventArgs)
        Me.DialogResult = False
        Me.Close()
    End Sub
End Class
