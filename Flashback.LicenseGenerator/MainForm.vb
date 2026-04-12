Imports Flashback.Core
Imports System.Windows.Forms

Public Class MainForm
    Inherits Form

    Private lblName As Label
    Private txtName As TextBox
    Private lblCount As Label
    Private numCount As NumericUpDown
    Private btnGenerate As Button
    Private lblStatus As Label

    Public Sub New()
        Me.Text = "Flashback License Key Generator"
        Me.Size = New Drawing.Size(400, 250)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.StartPosition = FormStartPosition.CenterScreen

        lblName = New Label With {.Text = "Licensed User Name:", .Location = New Drawing.Point(20, 30), .Width = 150}
        txtName = New TextBox With {.Location = New Drawing.Point(20, 55), .Width = 340}

        lblCount = New Label With {.Text = "Max Concurrent Printers:", .Location = New Drawing.Point(20, 95), .Width = 200}
        numCount = New NumericUpDown With {
            .Location = New Drawing.Point(20, 120), 
            .Width = 100, 
            .Minimum = 0, 
            .Maximum = 9999, 
            .Value = 10
        }

        btnGenerate = New Button With {
            .Text = "Generate flashback.lic", 
            .Location = New Drawing.Point(20, 165), 
            .Width = 340, 
            .Height = 35
        }
        AddHandler btnGenerate.Click, AddressOf GenerateLicense

        lblStatus = New Label With {
            .Text = "Ready", 
            .Location = New Drawing.Point(20, 205), 
            .Width = 340, 
            .TextAlign = Drawing.ContentAlignment.MiddleCenter
        }

        Me.Controls.AddRange({lblName, txtName, lblCount, numCount, btnGenerate, lblStatus})
    End Sub

    Private Sub GenerateLicense(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(txtName.Text) Then
            MessageBox.Show("Please enter a Licensed User Name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Using sfd As New SaveFileDialog()
            sfd.FileName = "flashback.lic"
            sfd.Filter = "License Files (*.lic)|*.lic"
            If sfd.ShowDialog() = DialogResult.OK Then
                Try
                    LicenseManager.GenerateLicense(txtName.Text, CInt(numCount.Value), sfd.FileName)
                    lblStatus.Text = "SUCCESS: License generated."
                    lblStatus.ForeColor = Drawing.Color.Green
                    Dim countStr As String = If(numCount.Value = 0, "Unlimited", numCount.Value.ToString())
                    MessageBox.Show($"License key for '{txtName.Text}' ({countStr} printers) has been generated successfully.", "License Generated", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show($"Generation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub
End Class
