using Microsoft.UI.Xaml.Controls;
using Flashback.Core;
using System;

namespace Flashback.Config.WinUI
{
    public sealed partial class EditDeviceDialog : ContentDialog
    {
        private DeviceItem _item;

        public EditDeviceDialog(DeviceItem item)
        {
            this.InitializeComponent();
            _item = item;
            LoadFields();
        }

        private void LoadFields()
        {
            txtName.Text = _item.Name ?? "";
            txtDesc.Text = _item.Description ?? "";
            
            var p = _item.FullRecord;
            if (p != null && p.Length >= 12)
            {
                // Mapping based on FullRecord indices from WPF VB code
                // p(0)=Name, p(1)=Desc, p(2)=Type, p(3)=OS, p(4)=Dest, p(5)=Conn, p(6)=PDF, p(7)=Shade, p(8)=Orient, p(9)=OutDir...
                
                int osIdx = 0; int.TryParse(p[5], out osIdx); cmbOS.SelectedIndex = Math.Clamp(osIdx, 0, cmbOS.Items.Count - 1);
                int typeIdx = 0; int.TryParse(p[2], out typeIdx); cmbType.SelectedIndex = typeIdx == 0 ? 0 : 1;
                
                int connVal = 0; int.TryParse(p[3], out connVal);
                if (connVal == 3) cmbConn.SelectedIndex = 1;
                else if (connVal == 1) cmbConn.SelectedIndex = 2;
                else cmbConn.SelectedIndex = 0;
                
                txtDest.Text = p[4] ?? "";
                chkPDF.IsChecked = p[7].ToLower() == "true";
                int shadeIdx = 0; int.TryParse(p[10], out shadeIdx); cmbShade.SelectedIndex = Math.Clamp(shadeIdx, 0, cmbShade.Items.Count - 1);
                int orientIdx = 0; int.TryParse(p[8], out orientIdx); cmbOrient.SelectedIndex = Math.Clamp(orientIdx, 0, cmbOrient.Items.Count - 1);
                txtOut.Text = p[9] ?? "";
            }
            if (p != null && p.Length >= 13)
            {
                chkEnabled.IsChecked = p[12].ToLower() == "true";
            }
            else
            {
                chkEnabled.IsChecked = true;
            }
            
            // Load email configuration (fields 13-23)
            if (p != null && p.Length >= 14) chkEmailEnabled.IsChecked = p[13].ToLower() == "true";
            if (p != null && p.Length >= 15) txtEmailRecipients.Text = p[14] ?? "";
            if (p != null && p.Length >= 16) txtSmtpServer.Text = p[15] ?? "";
            if (p != null && p.Length >= 17) { int port = 587; int.TryParse(p[16], out port); numSmtpPort.Value = port; }
            if (p != null && p.Length >= 18) txtSmtpUsername.Text = p[17] ?? "";
            if (p != null && p.Length >= 19) txtSmtpPassword.Password = p[18] ?? "";
            if (p != null && p.Length >= 20) chkSmtpUseTLS.IsChecked = p[19].ToLower() == "true";
            if (p != null && p.Length >= 21) txtEmailFrom.Text = p[20] ?? "flashback@localhost";
            if (p != null && p.Length >= 22) txtEmailFromName.Text = p[21] ?? "Flashback Print Server";
            if (p != null && p.Length >= 23) txtEmailSubject.Text = p[22] ?? "Print Job: {JobName} from {DeviceName}";
            if (p != null && p.Length >= 24) txtEmailBody.Text = p[23] ?? "";
        }

        public void SaveFields()
        {
            _item.Name = txtName.Text;
            _item.Description = txtDesc.Text;
            _item.Type = cmbType.SelectedIndex == 0 ? "Printer" : "3270 Terminal";
            _item.Port = txtDest.Text.Contains(':') ? txtDest.Text.Split(':')[1] : "9100";
            
            var p = _item.FullRecord;
            
            // Ensure array is large enough for all fields including email
            if (p.Length < 24)
            {
                Array.Resize(ref p, 24);
                _item.FullRecord = p;
            }
            
            p[0] = txtName.Text;
            p[1] = txtDesc.Text;
            p[2] = cmbType.SelectedIndex == 0 ? "0" : "1";
            p[5] = cmbOS.SelectedIndex.ToString();
            p[4] = txtDest.Text;
            p[3] = cmbConn.SelectedIndex == 1 ? "3" : (cmbConn.SelectedIndex == 2 ? "1" : "0");
            p[7] = chkPDF.IsChecked.ToString();
            p[8] = cmbOrient.SelectedIndex.ToString();
            p[9] = txtOut.Text;
            p[10] = cmbShade.SelectedIndex.ToString();
            p[12] = chkEnabled.IsChecked.ToString();
            
            // Save email configuration (fields 13-23)
            p[13] = chkEmailEnabled.IsChecked.ToString();
            p[14] = txtEmailRecipients.Text ?? "";
            p[15] = txtSmtpServer.Text ?? "";
            p[16] = ((int)numSmtpPort.Value).ToString();
            p[17] = txtSmtpUsername.Text ?? "";
            p[18] = txtSmtpPassword.Password ?? "";
            p[19] = chkSmtpUseTLS.IsChecked.ToString();
            p[20] = txtEmailFrom.Text ?? "flashback@localhost";
            p[21] = txtEmailFromName.Text ?? "Flashback Print Server";
            p[22] = txtEmailSubject.Text ?? "Print Job: {JobName} from {DeviceName}";
            p[23] = txtEmailBody.Text ?? "";
        }
    }
}
