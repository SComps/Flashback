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
                
                int osIdx = 0; int.TryParse(p[3], out osIdx); cmbOS.SelectedIndex = Math.Clamp(osIdx, 0, 9);
                int typeIdx = 0; int.TryParse(p[2], out typeIdx); cmbType.SelectedIndex = Math.Clamp(typeIdx, 0, 1);
                int connIdx = 0; int.TryParse(p[5], out connIdx); cmbConn.SelectedIndex = Math.Clamp(connIdx, 0, 2);
                txtDest.Text = p[4] ?? "";
                chkPDF.IsChecked = p[6].ToLower() == "true";
                int shadeIdx = 0; int.TryParse(p[10], out shadeIdx); cmbShade.SelectedIndex = Math.Clamp(shadeIdx, 0, 2);
                int orientIdx = 0; int.TryParse(p[8], out orientIdx); cmbOrient.SelectedIndex = Math.Clamp(orientIdx, 0, 1);
                txtOut.Text = p[9] ?? "";
            }
        }

        public void SaveFields()
        {
            _item.Name = txtName.Text;
            _item.Description = txtDesc.Text;
            _item.Type = cmbType.SelectedIndex == 0 ? "Printer" : "3270 Terminal";
            _item.Port = txtDest.Text.Contains(':') ? txtDest.Text.Split(':')[1] : "9100";
            
            var p = _item.FullRecord;
            p[0] = txtName.Text;
            p[1] = txtDesc.Text;
            p[2] = cmbType.SelectedIndex.ToString();
            p[3] = cmbOS.SelectedIndex.ToString();
            p[4] = txtDest.Text;
            p[5] = cmbConn.SelectedIndex.ToString();
            p[6] = chkPDF.IsChecked.ToString();
            p[8] = cmbOrient.SelectedIndex.ToString();
            p[9] = txtOut.Text;
            p[10] = cmbShade.SelectedIndex.ToString();
        }
    }
}
