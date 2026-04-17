Imports System
Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        ' Single Instance Check
        Dim createdNew As Boolean
        Dim mutex As New System.Threading.Mutex(True, "Global\FlashbackTray", createdNew)
        If Not createdNew Then
            ' Tray app usually exits silently if already running
            Return
        End If

        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub
End Module
