using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using CsvHelper;
using QRCoder;
using Microsoft.Win32;
using System.Globalization;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;

namespace WiFiShow.Fluent
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

    public partial class MainWindow : FluentWindow
    {
        private ObservableCollection<WiFiProfileViewModel> _allProfiles = new ObservableCollection<WiFiProfileViewModel>();
        private ObservableCollection<WiFiProfileViewModel> _filteredProfiles = new ObservableCollection<WiFiProfileViewModel>();
        private bool _showAllPasswords = false;

        public static readonly DependencyProperty CardWidthProperty =
            DependencyProperty.Register(nameof(CardWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(300.0));

        public double CardWidth
        {
            get => (double)GetValue(CardWidthProperty);
            set => SetValue(CardWidthProperty, value);
        }

        private Wpf.Ui.ISnackbarService _snackbarService;
        private string _lastSortColumn = "";
        private System.ComponentModel.ListSortDirection _lastSortDirection = System.ComponentModel.ListSortDirection.Ascending;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            CardsList.ItemsSource = _filteredProfiles;
            TableList.ItemsSource = _filteredProfiles;
            
            _snackbarService = new Wpf.Ui.SnackbarService();
            _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
            
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
                foreach (var p in _allProfiles.Where(x => x.Ssid.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _filteredProfiles.Add(p);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            FilterList(SearchBox.Text);
        }

        private void ViewToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CardsView.Visibility == Visibility.Visible)
            {
                CardsView.Visibility = Visibility.Collapsed;
                TableView.Visibility = Visibility.Visible;
            }
            else
            {
                CardsView.Visibility = Visibility.Visible;
                TableView.Visibility = Visibility.Collapsed;
            }
        }

        private void ToggleAllBtn_Click(object sender, RoutedEventArgs e)
        {
            _showAllPasswords = !_showAllPasswords;
            ToggleAllBtn.Content = _showAllPasswords ? "Hide All" : "Show All";
            ToggleAllBtn.Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = _showAllPasswords ? Wpf.Ui.Controls.SymbolRegular.EyeOff24 : Wpf.Ui.Controls.SymbolRegular.Eye24 };

            foreach (var p in _allProfiles)
            {
                p.ShowPassword = _showAllPasswords;
            }
        }

        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string pwd && !string.IsNullOrEmpty(pwd))
            {
                System.Windows.Clipboard.SetText(pwd);
                _snackbarService.Show("Copied", "Password copied to clipboard.", Wpf.Ui.Controls.ControlAppearance.Success, new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Copy24), TimeSpan.FromSeconds(2));
            }
        }

        private void ToggleSinglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string profileName)
            {
                var profile = _allProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile != null)
                {
                    profile.ShowPassword = !profile.ShowPassword;
                }
            }
        }

        private void CardsView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double totalWidth = e.NewSize.Width - 10;
            if (totalWidth <= 0) return;
            int columns = Math.Max(1, (int)(totalWidth / 320));
            CardWidth = (totalWidth / columns) - 20;
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
                _snackbarService.Show("Success", "Exported successfully to CSV.", Wpf.Ui.Controls.ControlAppearance.Success, new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Document24), TimeSpan.FromSeconds(3));
            }
        }

        private async void AutoConnectToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ts.Tag is string profileName)
            {
                var profile = _allProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile != null)
                {
                    bool isAuto = ts.IsChecked ?? false;
                    profile.IsAutoConnect = isAuto;
                    await WiFiManager.ToggleAutoConnectAsync(profileName, isAuto);
                    _snackbarService.Show("Auto-Connect Updated", $"Auto-Connect is now {(isAuto ? "enabled" : "disabled")} for {profileName}.", Wpf.Ui.Controls.ControlAppearance.Secondary, new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Globe24), TimeSpan.FromSeconds(2));
                }
            }
        }

        private void SortColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string sortBy)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_filteredProfiles);
                
                if (_lastSortColumn == sortBy)
                {
                    _lastSortDirection = _lastSortDirection == System.ComponentModel.ListSortDirection.Ascending 
                        ? System.ComponentModel.ListSortDirection.Descending 
                        : System.ComponentModel.ListSortDirection.Ascending;
                }
                else
                {
                    _lastSortColumn = sortBy;
                    _lastSortDirection = System.ComponentModel.ListSortDirection.Ascending;
                }

                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription(sortBy, _lastSortDirection));
            }
        }

        private void QRBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string profileName)
            {
                var profile = _allProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile == null) return;

                var detailsWindow = new FluentWindow
                {
                    Title = "QR Code - " + profile.Ssid,
                    Width = 400,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    WindowBackdropType = WindowBackdropType.Mica,
                    ExtendsContentIntoTitleBar = true,
                    WindowCornerPreference = WindowCornerPreference.Round
                };
                
                ApplicationThemeManager.Apply(detailsWindow);

                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var titleBar = new TitleBar { Title = "Scan to Connect" };
                System.Windows.Controls.Grid.SetRow(titleBar, 0);
                grid.Children.Add(titleBar);

                var sp = new StackPanel { Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(sp, 1);
                
                sp.Children.Add(new System.Windows.Controls.TextBlock { Text = profile.Ssid, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,20), HorizontalAlignment = HorizontalAlignment.Center });

                if (!string.IsNullOrEmpty(profile.RealPassword))
                {
                    string qrPayload = $"WIFI:T:{profile.AuthType};S:{profile.Ssid};P:{profile.RealPassword};;";
                    var qrGenerator = new QRCodeGenerator();
                    var qrCodeData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
                    var qrCode = new QRCode(qrCodeData);
                    var qrImage = qrCode.GetGraphic(8);

                    var img = new System.Windows.Controls.Image
                    {
                        Source = BitmapToImageSource(qrImage),
                        Width = 250,
                        Height = 250,
                        Margin = new Thickness(0,0,0,10),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    sp.Children.Add(img);
                }
                else
                {
                    sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "No password available for QR code.", HorizontalAlignment = HorizontalAlignment.Center });
                }

                grid.Children.Add(sp);
                detailsWindow.Content = grid;
                detailsWindow.ShowDialog();
            }
        }

        private async void DetailsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string profileName)
            {
                var profile = _allProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile == null) return;

                string detailsText = await WiFiManager.GetProfileDetailsAsync(profileName);
                
                var detailsWindow = new FluentWindow
                {
                    Title = "Profile Details - " + profile.Ssid,
                    Width = 600,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    WindowBackdropType = WindowBackdropType.Mica,
                    ExtendsContentIntoTitleBar = true,
                    WindowCornerPreference = WindowCornerPreference.Round
                };
                
                ApplicationThemeManager.Apply(detailsWindow);

                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var titleBar = new TitleBar { Title = "Details" };
                System.Windows.Controls.Grid.SetRow(titleBar, 0);
                grid.Children.Add(titleBar);

                var sp = new StackPanel { Margin = new Thickness(24) };
                System.Windows.Controls.Grid.SetRow(sp, 1);
                
                sp.Children.Add(new System.Windows.Controls.TextBlock { Text = profile.Ssid, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,10) });

                sp.Children.Add(new ScrollViewer
                {
                    Content = new System.Windows.Controls.TextBlock { Text = detailsText, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12 }
                });

                grid.Children.Add(sp);
                detailsWindow.Content = grid;
                detailsWindow.ShowDialog();
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
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string profileName)
            {
                var result = System.Windows.MessageBox.Show($"Are you sure you want to forget '{profileName}'?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await WiFiManager.DeleteProfileAsync(profileName);
                    LoadNetworks();
                    _snackbarService.Show("Deleted", $"Forgot network {profileName}.", Wpf.Ui.Controls.ControlAppearance.Danger, new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Delete24), TimeSpan.FromSeconds(3));
                }
            }
        }
    }
}