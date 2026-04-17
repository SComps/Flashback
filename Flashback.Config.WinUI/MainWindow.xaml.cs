using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Flashback.Core;

namespace Flashback.Config.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<DeviceItem> Devices { get; set; } = new ObservableCollection<DeviceItem>();
        private string configFile = "devices.dat";
        private string pwFile = "syspw.txt";
        private string themeFile = "uipalette.dat";
        private string _syspw = "";
        private bool _loading = true;

        public MainWindow()
        {
            this.InitializeComponent();
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));
            this.AppWindow.SetIcon("Assets\\printer.ico");
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            
            LoadSecurity();
            LoadConfig();

            if (string.IsNullOrWhiteSpace(_syspw))
            {
                overlayLock.Visibility = Visibility.Collapsed;
                LoadDevices();
            }

            _loading = false;
            
            // Start splash screen timer
            var splashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            splashTimer.Tick += (s, e) => 
            { 
                splashTimer.Stop(); 
                splashOverlay.Visibility = Visibility.Collapsed; 
            };
            splashTimer.Start();
        }

        private void splashOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            splashOverlay.Visibility = Visibility.Collapsed;
        }

        private void LoadConfig()
        {
            cmbUITheme.SelectedIndex = 0;
            if (File.Exists(themeFile))
            {
                if (int.TryParse(File.ReadAllText(themeFile).Trim(), out int val))
                {
                    cmbUITheme.SelectedIndex = Math.Clamp(val, 0, 2);
                    ApplyThemeMode();
                }
            }
        }

        private void ApplyThemeMode()
        {
            var root = this.Content as FrameworkElement;
            if (root == null) return;

            switch (cmbUITheme.SelectedIndex)
            {
                case 1: // Midnight
                    root.RequestedTheme = ElementTheme.Dark;
                    break;
                case 2: // Snow
                    root.RequestedTheme = ElementTheme.Light;
                    break;
                default:
                    root.RequestedTheme = ElementTheme.Default;
                    break;
            }
        }

        private void LoadDevices()
        {
            Devices.Clear();
            if (!File.Exists(configFile)) return;
            try
            {
                var lines = File.ReadAllLines(configFile);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split("||");
                    if (parts.Length >= 6)
                    {
                        Devices.Add(new DeviceItem 
                        { 
                            Name = parts[0], 
                            Description = parts[1], 
                            Type = parts[2] == "0" ? "Printer" : "3270 Terminal", 
                            Port = parts[4].Split(':').Last(), 
                            FullRecord = parts 
                        });
                    }
                }
                DeviceList.ItemsSource = Devices;
            }
            catch { }
        }

        private void LoadSecurity()
        {
            if (File.Exists(pwFile)) _syspw = File.ReadAllText(pwFile).Trim();
        }

        private void cmbUITheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            File.WriteAllText(themeFile, cmbUITheme.SelectedIndex.ToString());
            ApplyThemeMode();
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            viewPrinters.Visibility = Visibility.Collapsed;
            viewSettings.Visibility = Visibility.Collapsed;
            viewSecurity.Visibility = Visibility.Collapsed;

            var selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem == null) return;

            switch (selectedItem.Tag?.ToString())
            {
                case "printers":
                    viewPrinters.Visibility = Visibility.Visible;
                    break;
                case "settings":
                    viewSettings.Visibility = Visibility.Visible;
                    break;
                case "security":
                    viewSecurity.Visibility = Visibility.Visible;
                    break;
                case "help":
                    // Show help dialog or content
                    break;
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (pbLogin.Password.Trim() == _syspw)
            {
                overlayLock.Visibility = Visibility.Collapsed;
                LoadDevices();
            }
            else
            {
                lblLoginError.Visibility = Visibility.Visible;
            }
        }

        private void pbLogin_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) Login_Click(null, null);
        }

        private async void AddDevice_Click(object sender, RoutedEventArgs e)
        {
            var newRecord = new string[] { "New Device", "Flashback Device", "0", "3", "127.0.0.1:9100", "0", "False", "True", "0", "Output", "0", "0" };
            var newItem = new DeviceItem { Name = "New Device", Type = "Printer", Port = "9100", FullRecord = newRecord };
            
            var dialog = new EditDeviceDialog(newItem);
            dialog.XamlRoot = this.Content.XamlRoot;
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                dialog.SaveFields();
                Devices.Add(newItem);
                SaveDevices();
                LoadDevices();
            }
        }

        private void SaveDevices()
        {
            try
            {
                var lines = new List<string>();
                foreach (var item in Devices)
                {
                    var p = item.FullRecord;
                    p[0] = item.Name;
                    p[2] = item.Type == "Printer" ? "0" : "1";
                    var hostPart = p[4].Split(':')[0];
                    p[4] = $"{hostPart}:{item.Port}";
                    lines.Add(string.Join("||", p));
                }
                File.WriteAllLines(configFile, lines);
            }
            catch { }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.DataContext as DeviceItem;
            if (item == null) return;
            
            var dialog = new EditDeviceDialog(item);
            dialog.XamlRoot = this.Content.XamlRoot;
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                dialog.SaveFields();
                SaveDevices();
                LoadDevices();
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.DataContext as DeviceItem;
            if (item == null) return;
            
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Confirm Delete",
                Content = $"Are you sure you want to delete {item.Name}?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Devices.Remove(item);
                SaveDevices();
            }
        }
    }
}
