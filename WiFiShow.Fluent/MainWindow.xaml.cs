using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using CsvHelper;
using Microsoft.Win32;
using QRCoder;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WiFiShow.Fluent
{
    public class WiFiProfileViewModel : INotifyPropertyChanged
    {
        private readonly WiFiProfile _profile;
        private bool _showPassword;

        public WiFiProfileViewModel(WiFiProfile profile)
        {
            _profile = profile;
        }

        public string Name => _profile.Name;
        public string Ssid => _profile.Ssid;
        public string RealPassword => _profile.Password;
        public string AuthType => _profile.AuthType;
        public bool IsConnected => _profile.IsConnected;
        public DateTime? LastConnectedTime => _profile.LastConnectedTime;

        public string LastConnectedDisplay
        {
            get
            {
                if (IsConnected)
                    return "Connected";

                if (!_profile.LastConnectedTime.HasValue)
                    return "Never / Unknown";

                var time = _profile.LastConnectedTime.Value.ToLocalTime();
                var now = DateTime.Now;

                if (time.Date == now.Date)
                    return $"Today, {time:t}";
                if (time.Date == now.Date.AddDays(-1))
                    return $"Yesterday, {time:t}";
                if ((now - time).TotalDays < 7)
                    return $"{time:ddd}, {time:t}";
                if (time.Year == now.Year)
                    return $"{time:MMM d}, {time:t}";

                return $"{time:MMM d, yyyy}";
            }
        }

        public long LastConnectedSortKey
        {
            get
            {
                if (IsConnected)
                    return long.MaxValue;
                if (_profile.LastConnectedTime.HasValue)
                    return _profile.LastConnectedTime.Value.Ticks;
                return 0;
            }
        }

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
        private readonly ObservableCollection<WiFiProfileViewModel> _allProfiles = new();
        private readonly ICollectionView _profilesView;
        private bool _showAllPasswords = false;

        public static readonly DependencyProperty CardWidthProperty =
            DependencyProperty.Register(nameof(CardWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(300.0));

        public double CardWidth
        {
            get => (double)GetValue(CardWidthProperty);
            set => SetValue(CardWidthProperty, value);
        }

        private readonly Wpf.Ui.ISnackbarService _snackbarService;
        private string _lastSortColumn = string.Empty;
        private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            _profilesView = CollectionViewSource.GetDefaultView(_allProfiles);
            _profilesView.Filter = FilterProfile;

            CardsList.ItemsSource = _profilesView;
            TableList.ItemsSource = _profilesView;

            _snackbarService = new Wpf.Ui.SnackbarService();
            _snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            LoadNetworks();
        }

        private bool FilterProfile(object item)
        {
            if (item is not WiFiProfileViewModel profile) return false;
            string query = SearchBox.Text;
            if (string.IsNullOrWhiteSpace(query)) return true;

            return profile.Ssid.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   profile.AuthType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   (profile.IsConnected && "connected".Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                   profile.LastConnectedDisplay.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private async void LoadNetworks()
        {
            LoadingSpinner.Visibility = Visibility.Visible;
            _allProfiles.Clear();

            try
            {
                var profiles = await WiFiManager.GetWiFiProfilesAsync();
                foreach (var p in profiles)
                {
                    _allProfiles.Add(new WiFiProfileViewModel(p)
                    {
                        ShowPassword = _showAllPasswords
                    });
                }

                _profilesView.Refresh();
                MainTitleBar.Title = $"Wi-Fi Show - {_allProfiles.Count} saved networks";
            }
            catch (Exception ex)
            {
                _snackbarService.Show("Error", $"Failed to load networks: {ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(4));
            }
            finally
            {
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _profilesView.Refresh();
        }

        private void ViewToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CardsView.Visibility == Visibility.Visible)
            {
                CardsView.Visibility = Visibility.Collapsed;
                TableView.Visibility = Visibility.Visible;
                ViewToggleBtn.Icon = new SymbolIcon { Symbol = SymbolRegular.Grid24 };
            }
            else
            {
                CardsView.Visibility = Visibility.Visible;
                TableView.Visibility = Visibility.Collapsed;
                ViewToggleBtn.Icon = new SymbolIcon { Symbol = SymbolRegular.List24 };
            }
        }

        private void ToggleAllBtn_Click(object sender, RoutedEventArgs e)
        {
            _showAllPasswords = !_showAllPasswords;
            ToggleAllBtn.Content = _showAllPasswords ? "Hide All" : "Show All";
            ToggleAllBtn.Icon = new SymbolIcon { Symbol = _showAllPasswords ? SymbolRegular.EyeOff24 : SymbolRegular.Eye24 };

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
                _snackbarService.Show("Copied", "Password copied to clipboard.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Copy24), TimeSpan.FromSeconds(2));
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
                try
                {
                    using var writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8);
                    using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                    
                    var records = _allProfiles.Select(p => new
                    {
                        p.Name,
                        p.Ssid,
                        Password = p.RealPassword,
                        p.AuthType,
                        p.IsAutoConnect,
                        Status = p.IsConnected ? "Connected" : "Disconnected",
                        LastConnected = p.LastConnectedDisplay
                    }).ToList();

                    csv.WriteRecords(records);
                    _snackbarService.Show("Success", "Exported successfully to CSV.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Document24), TimeSpan.FromSeconds(3));
                }
                catch (Exception ex)
                {
                    _snackbarService.Show("Export Failed", ex.Message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(4));
                }
            }
        }

        private async void AutoConnectToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.ToggleSwitch ts && ts.Tag is string profileName)
            {
                var profile = _allProfiles.FirstOrDefault(p => p.Name == profileName);
                if (profile != null)
                {
                    bool isAuto = ts.IsChecked ?? false;
                    profile.IsAutoConnect = isAuto;
                    await WiFiManager.ToggleAutoConnectAsync(profileName, isAuto);
                    _snackbarService.Show("Auto-Connect Updated", $"Auto-Connect is now {(isAuto ? "enabled" : "disabled")} for {profileName}.", ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.Globe24), TimeSpan.FromSeconds(2));
                }
            }
        }

        private void SortColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string sortBy)
            {
                if (_lastSortColumn == sortBy)
                {
                    _lastSortDirection = _lastSortDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                }
                else
                {
                    _lastSortColumn = sortBy;
                    _lastSortDirection = sortBy == nameof(WiFiProfileViewModel.LastConnectedSortKey)
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                }

                _profilesView.SortDescriptions.Clear();
                _profilesView.SortDescriptions.Add(new SortDescription(sortBy, _lastSortDirection));
            }
        }

        private void QRBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button btn || btn.Tag is not string profileName) return;

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

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = new TitleBar { Title = "Scan to Connect" };
            Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);

            var sp = new StackPanel { Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetRow(sp, 1);

            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = profile.Ssid, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20), HorizontalAlignment = HorizontalAlignment.Center });

            if (!string.IsNullOrEmpty(profile.RealPassword))
            {
                string qrPayload = $"WIFI:T:{profile.AuthType};S:{profile.Ssid};P:{profile.RealPassword};;";
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                byte[] qrBytes = qrCode.GetGraphic(20);

                var bitmapImage = new BitmapImage();
                using (var ms = new MemoryStream(qrBytes))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                }
                bitmapImage.Freeze();

                var img = new System.Windows.Controls.Image
                {
                    Source = bitmapImage,
                    Width = 250,
                    Height = 250,
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                sp.Children.Add(img);

                var actionsSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

                var btnDownload = new Wpf.Ui.Controls.Button { Content = "Download", Icon = new SymbolIcon(SymbolRegular.Save24), Margin = new Thickness(0, 0, 10, 0) };
                btnDownload.Click += (s, ev) =>
                {
                    var sfd = new SaveFileDialog { Filter = "PNG Image|*.png", FileName = $"{profile.Ssid}_QR.png" };
                    if (sfd.ShowDialog() == true)
                    {
                        File.WriteAllBytes(sfd.FileName, qrBytes);
                        System.Windows.MessageBox.Show("QR Code saved successfully!", "Wi-Fi Show");
                    }
                };

                var btnShare = new Wpf.Ui.Controls.Button { Content = "Copy Image", Icon = new SymbolIcon(SymbolRegular.Copy24) };
                btnShare.Click += (s, ev) =>
                {
                    System.Windows.Clipboard.SetImage(bitmapImage);
                    System.Windows.MessageBox.Show("QR Code image copied to clipboard!", "Wi-Fi Show");
                };

                actionsSp.Children.Add(btnDownload);
                actionsSp.Children.Add(btnShare);
                sp.Children.Add(actionsSp);
            }
            else
            {
                sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "No password available for QR code.", HorizontalAlignment = HorizontalAlignment.Center });
            }

            grid.Children.Add(sp);
            detailsWindow.Content = grid;
            detailsWindow.ShowDialog();
        }

        private async void DetailsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button btn || btn.Tag is not string profileName) return;

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

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = new TitleBar { Title = "Details" };
            Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);

            var sp = new StackPanel { Margin = new Thickness(24) };
            Grid.SetRow(sp, 1);

            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = profile.Ssid, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });

            sp.Children.Add(new ScrollViewer
            {
                Content = new System.Windows.Controls.TextBlock { Text = detailsText, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12 }
            });

            grid.Children.Add(sp);
            detailsWindow.Content = grid;
            detailsWindow.ShowDialog();
        }

        private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button btn || btn.Tag is not string profileName) return;

            var result = System.Windows.MessageBox.Show($"Are you sure you want to forget '{profileName}'?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await WiFiManager.DeleteProfileAsync(profileName);
                LoadNetworks();
                _snackbarService.Show("Deleted", $"Forgot network {profileName}.", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.Delete24), TimeSpan.FromSeconds(3));
            }
        }
    }
}