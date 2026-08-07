using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CsvHelper;
using MaterialDesignThemes.Wpf;
using QRCoder;
using Microsoft.Win32;
using System.Globalization;

namespace WiFiShow.WPF
{
    public class WiFiProfileViewModel : INotifyPropertyChanged
    {
        private WiFiProfile _profile;
        private bool _showPassword;

        public WiFiProfileViewModel(WiFiProfile profile)
        {
            _profile = profile;
        }

        public string Name => _profile.Name;
        public string Ssid => _profile.Ssid;
        public string RealPassword => _profile.Password;
        public string AuthType => _profile.AuthType;

        public bool IsAutoConnect
        {
            get => _profile.IsAutoConnect;
            set
            {
                if (_profile.IsAutoConnect != value)
                {
                    _profile.IsAutoConnect = value;
                    OnPropertyChanged(nameof(IsAutoConnect));
                }
            }
        }

        public bool ShowPassword
        {
            get => _showPassword;
            set
            {
                if (_showPassword != value)
                {
                    _showPassword = value;
                    OnPropertyChanged(nameof(ShowPassword));
                    OnPropertyChanged(nameof(Password));
                }
            }
        }

        public string Password => ShowPassword ? RealPassword : new string('•', RealPassword.Length > 0 ? 8 : 0);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<WiFiProfileViewModel> _allProfiles = new();
        private ObservableCollection<WiFiProfileViewModel> _filteredProfiles = new();
        private bool _showAllPasswords = false;

        public MainWindow()
        {
            InitializeComponent();
            CardsList.ItemsSource = _filteredProfiles;
            TableView.ItemsSource = _filteredProfiles;
            LoadNetworks();
        }

        private async void LoadNetworks()
        {
            LoadingSpinner.Visibility = Visibility.Visible;
            _allProfiles.Clear();
            _filteredProfiles.Clear();

            var profiles = await WiFiManager.GetWiFiProfilesAsync();
            foreach (var p in profiles)
            {
                var vm = new WiFiProfileViewModel(p);
                vm.ShowPassword = _showAllPasswords;
                _allProfiles.Add(vm);
            }

            FilterList(SearchBox.Text);
            LoadingSpinner.Visibility = Visibility.Collapsed;
        }

        private void FilterList(string query)
        {
            _filteredProfiles.Clear();
            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var p in _allProfiles) _filteredProfiles.Add(p);
            }
            else
            {
                foreach (var p in _allProfiles.Where(x => x.Ssid.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    _filteredProfiles.Add(p);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterList(SearchBox.Text);
        }

        private void ViewToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewToggleBtn.IsChecked == true)
            {
                CardsView.Visibility = Visibility.Collapsed;
                TableViewContainer.Visibility = Visibility.Visible;
            }
            else
            {
                CardsView.Visibility = Visibility.Visible;
                TableViewContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void ToggleAllBtn_Click(object sender, RoutedEventArgs e)
        {
            _showAllPasswords = !_showAllPasswords;
            foreach (var p in _allProfiles)
            {
                p.ShowPassword = _showAllPasswords;
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadNetworks();
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv",
                FileName = "wifi_profiles.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    var records = _allProfiles.Select(p => new
                    {
                        p.Name,
                        p.Ssid,
                        Password = p.RealPassword,
                        p.AuthType,
                        p.IsAutoConnect
                    }).ToList();
                    
                    csv.WriteRecords(records);
                }
                MessageBox.Show("Exported successfully!");
            }
        }

        private async void AutoConnectToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.Tag is string profileName)
            {
                var profile = _allProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile != null)
                {
                    bool isAuto = tb.IsChecked ?? false;
                    profile.IsAutoConnect = isAuto;
                    await WiFiManager.ToggleAutoConnectAsync(profileName, isAuto);
                }
            }
        }

        private async void DetailsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string profileName)
            {
                var profile = _allProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile == null) return;

                string detailsText = await WiFiManager.GetProfileDetailsAsync(profileName);
                
                // Build dialog content
                var sp = new StackPanel { Margin = new Thickness(20), MinWidth = 400 };
                sp.Children.Add(new TextBlock { Text = profile.Ssid, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,10) });

                // QR Code
                if (!string.IsNullOrEmpty(profile.RealPassword))
                {
                    // WIFI:T:WPA;S:mynetwork;P:mypass;;
                    string qrPayload = $"WIFI:T:{profile.AuthType};S:{profile.Ssid};P:{profile.RealPassword};;";
                    var qrGenerator = new QRCodeGenerator();
                    var qrCodeData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
                    var qrCode = new QRCode(qrCodeData);
                    var qrImage = qrCode.GetGraphic(5);

                    var img = new System.Windows.Controls.Image
                    {
                        Source = BitmapToImageSource(qrImage),
                        Width = 150,
                        Height = 150,
                        Margin = new Thickness(0,0,0,10)
                    };
                    sp.Children.Add(img);
                }

                sp.Children.Add(new ScrollViewer
                {
                    MaxHeight = 300,
                    Content = new TextBlock { Text = detailsText, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12 }
                });

                var closeBtn = new Button { Content = "CLOSE", Margin = new Thickness(0,10,0,0) };
                closeBtn.Click += (s, ev) => DialogHost.CloseDialogCommand.Execute(null, closeBtn);
                sp.Children.Add(closeBtn);

                await DialogHost.Show(sp, "RootDialog");
            }
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                return bitmapimage;
            }
        }

        private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string profileName)
            {
                var result = MessageBox.Show($"Are you sure you want to forget '{profileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    await WiFiManager.DeleteProfileAsync(profileName);
                    LoadNetworks();
                }
            }
        }

        // --- Custom Title Bar Interactions ---
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Maximize_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}