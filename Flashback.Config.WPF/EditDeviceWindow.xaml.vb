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
        Catch ex As Exception
            MessageBox.Show("Editor Init Error: " & ex.Message & vbCrLf & ex.StackTrace)
        End Try
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        Try
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
