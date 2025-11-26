using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PassManager.Models;
using PassManager.Security;
using PassManager.UI;
using PassManager.Configuration;
using PassManager.Features.Updates.Services;
using System.Threading.Tasks;

namespace PassManager
{
    public partial class MainWindow : Window
    {
        private DataManager _dataManager;
        private ThemeManager _themeManager;
        private AudioManager _audioManager;
        private UpdateService _updateService;

        private Section _currentSection;
        private int _loginAttempts = 0;
        private const int MaxLoginAttempts = 3;
        private System.Threading.CancellationTokenSource _volumeMenuCancellation;
        private bool _isMouseInVolumeArea = false;

        public MainWindow()
        {
            InitializeComponent();
            _dataManager = DataManager.Instance;
            _themeManager = new ThemeManager(this);
            _audioManager = new AudioManager();
            _updateService = new UpdateService();

            Loaded += MainWindow_Loaded;
            MasterPasswordBox.KeyDown += MasterPasswordBox_KeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply default theme
            _themeManager.ApplyTheme("default");

            // Apply animated background setting
            UpdateAnimatedBackground();

            // Check if first time setup
            if (!_dataManager.MasterPasswordExists())
            {
                LoginPromptText.Text = "Create Master Password (min 8 chars, uppercase, lowercase, digit & symbol):";
                LoginButton.Content = "Create";
            }
        }

        private void MasterPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string password = MasterPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                LoginErrorText.Text = "Please enter a password";
                return;
            }

            try
            {
                if (!_dataManager.MasterPasswordExists())
                {
                    // Create new master password
                    if (!PasswordHasher.IsPasswordValid(password))
                    {
                        LoginErrorText.Text = "Password must be at least 8 characters with uppercase, lowercase, digit and symbol";
                        return;
                    }

                    _dataManager.SetupMasterPassword(password);
                    ShowMainApplication();
                }
                else
                {
                    // Verify existing password
                    if (_dataManager.VerifyMasterPassword(password))
                    {
                        ShowMainApplication();
                    }
                    else
                    {
                        _loginAttempts++;
                        if (_loginAttempts >= MaxLoginAttempts)
                        {
                            MessageBox.Show("Too many failed attempts. Application will close.",
                                "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            LoginErrorText.Text = $"Incorrect password. {MaxLoginAttempts - _loginAttempts} attempts remaining.";
                            MasterPasswordBox.Clear();
                            MasterPasswordBox.Focus();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowMainApplication()
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            MainAppPanel.Visibility = Visibility.Visible;

            // Apply saved theme
            _themeManager.ApplyTheme(_dataManager.Settings.Theme);

            // Apply animated background setting
            UpdateAnimatedBackground();

            // Initialize audio and show volume menu only for themes with music
            UpdateVolumeMenuVisibility();

            // Load sections
            RefreshSections();

            // Check for updates in the background (fire and forget)
            _ = CheckForUpdatesOnStartupAsync();
        }

        private void UpdateVolumeMenuVisibility()
        {
            // Only show volume menu if music is enabled AND current theme has music
            string musicFile = _themeManager.GetMusicFile(_dataManager.Settings.Theme);
            bool shouldShowMenu = _dataManager.Settings.MusicEnabled && !string.IsNullOrEmpty(musicFile);

            VolumeMenuContainer.Visibility = shouldShowMenu ? Visibility.Visible : Visibility.Collapsed;

            if (shouldShowMenu)
            {
                VolumeSlider.Value = _dataManager.Settings.Volume;
                _audioManager.SetVolume(_dataManager.Settings.Volume / 100.0f);
                _audioManager.LoadMusic(musicFile);

                // Auto-play if music was playing when app closed
                if (_dataManager.Settings.MusicPlaying)
                {
                    _audioManager.Play();
                    PlayPauseButton.Content = "⏸️"; // Pause icon
                }
                else
                {
                    PlayPauseButton.Content = "▶️"; // Play icon
                }
            }
        }

        public void UpdateAnimatedBackground()
        {
            bool isAnimated = _dataManager.Settings.AnimatedBackground;

            if (isAnimated)
            {
                // Show animated background layer
                AnimatedBackgroundLayer.Visibility = Visibility.Visible;
                StaticBlurOverlay.Visibility = Visibility.Collapsed;

                // Start the animation
                var storyboard = (Storyboard)this.FindResource("AnimatedBackgroundStoryboard");
                storyboard.Begin();
            }
            else
            {
                // Hide animated background, show static blur
                AnimatedBackgroundLayer.Visibility = Visibility.Collapsed;
                StaticBlurOverlay.Visibility = Visibility.Visible;

                // Stop the animation if running
                try
                {
                    var storyboard = (Storyboard)this.FindResource("AnimatedBackgroundStoryboard");
                    storyboard.Stop();
                }
                catch { }
            }
        }

        private void RefreshSections(string searchFilter = "")
        {
            SectionsListPanel.Children.Clear();

            var sections = _dataManager.Sections;
            if (!string.IsNullOrWhiteSpace(searchFilter))
            {
                sections = sections.Where(s => s.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            foreach (var section in sections)
            {
                var button = CreateSectionButton(section);
                SectionsListPanel.Children.Add(button);
            }

            // Show/hide tutorial
            UpdateSectionsTutorial();
        }

        private void UpdateSectionsTutorial()
        {
            bool shouldShowTutorial = _dataManager.Sections.Count == 0;
            NoSectionsTutorial.Visibility = shouldShowTutorial ? Visibility.Visible : Visibility.Collapsed;

            if (shouldShowTutorial)
            {
                // Start pointing hand animation
                var storyboard = (Storyboard)FindResource("PointingHandAnimation");
                storyboard.Begin(PointingHandSections);
            }
        }

        private Button CreateSectionButton(Section section)
        {
            var textBlock = new TextBlock
            {
                Text = $"📁 {section.Name}",
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            var button = new Button
            {
                Content = textBlock,
                Style = FindResource("ListButton") as Style,
                Tag = section
            };

            button.Click += (s, e) =>
            {
                _currentSection = section;
                RefreshAccounts();
            };

            button.MouseRightButtonUp += (s, e) =>
            {
                ShowSectionContextMenu(section);
            };

            return button;
        }

        private void ShowSectionContextMenu(Section section)
        {
            var result = MessageBox.Show($"Delete section '{section.Name}' and all its accounts?",
                "Delete Section", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _dataManager.RemoveSection(section);
                if (_currentSection == section)
                {
                    _currentSection = null;
                    AccountsHeaderText.Text = "Select a section";
                    AccountsListPanel.Children.Clear();
                    AddAccountButton.IsEnabled = false;
                    AccountSearchBox.IsEnabled = false;
                }
                RefreshSections();
            }
        }

        private void RefreshAccounts(string searchFilter = "")
        {
            AccountsListPanel.Children.Clear();

            if (_currentSection == null)
            {
                NoAccountsTutorial.Visibility = Visibility.Collapsed;
                return;
            }

            AccountsHeaderText.Text = $"🔑 {_currentSection.Name}";
            AddAccountButton.IsEnabled = true;
            AccountSearchBox.IsEnabled = true;
            BackButton.Visibility = Visibility.Visible;

            var accounts = _currentSection.Accounts;
            if (!string.IsNullOrWhiteSpace(searchFilter))
            {
                accounts = accounts.Where(a => a.Email.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            foreach (var account in accounts)
            {
                var card = CreateAccountCard(account);
                AccountsListPanel.Children.Add(card);
            }

            // Show/hide tutorial
            UpdateAccountsTutorial();
        }

        private void UpdateAccountsTutorial()
        {
            bool shouldShowTutorial = _currentSection != null && _currentSection.Accounts.Count == 0;
            NoAccountsTutorial.Visibility = shouldShowTutorial ? Visibility.Visible : Visibility.Collapsed;

            if (shouldShowTutorial)
            {
                // Start pointing hand animation
                var storyboard = (Storyboard)FindResource("PointingHandAnimation");
                storyboard.Begin(PointingHandAccounts);
            }
        }

        private Border CreateAccountCard(Account account)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackPanel = new StackPanel();

            // Type
            var typeText = new TextBlock
            {
                Text = $"🔐 {account.Type}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };

            // Email
            var emailText = new TextBlock
            {
                Text = $"📧 {account.Email}",
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };

            // Password (no buttons here)
            var passwordText = new TextBlock
            {
                Text = new string('•', account.Password.Length),
                FontSize = 13,
                Tag = account.Password,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };

            stackPanel.Children.Add(typeText);
            stackPanel.Children.Add(emailText);
            stackPanel.Children.Add(passwordText);

            // All action buttons in vertical layout on the right
            var buttonPanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };

            // First row: Edit and Delete
            var editDeletePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            var editButton = new Button
            {
                Content = "✏️ Edit",
                Width = 70,
                Height = 28,
                FontSize = 11,
                Margin = new Thickness(0, 0, 3, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            ApplyButtonStyle(editButton);
            editButton.Click += (s, e) => ShowEditAccountDialog(account);

            var deleteButton = new Button
            {
                Content = "🗑️ Delete",
                Width = 75,
                Height = 28,
                FontSize = 11,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            ApplyButtonStyle(deleteButton, Color.FromRgb(255, 107, 107));
            deleteButton.Click += (s, e) => DeleteAccount(account);

            editDeletePanel.Children.Add(editButton);
            editDeletePanel.Children.Add(deleteButton);

            // Second row: Show and Copy (same height as Edit/Delete)
            var showCopyPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var showButton = new Button
            {
                Content = "👁 Show",
                Width = 70,
                Height = 28,
                FontSize = 11,
                Margin = new Thickness(0, 0, 3, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            ApplyButtonStyle(showButton);

            showButton.Click += (s, e) =>
            {
                if (passwordText.Text == new string('•', account.Password.Length))
                {
                    passwordText.Text = account.Password;
                    showButton.Content = "👁 Hide";
                }
                else
                {
                    passwordText.Text = new string('•', account.Password.Length);
                    showButton.Content = "👁 Show";
                }
            };

            var copyButton = new Button
            {
                Content = "📋 Copy",
                Width = 75,
                Height = 28,
                FontSize = 11,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            ApplyButtonStyle(copyButton);

            copyButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(account.Password);
                    // Flash button to indicate success instead of MessageBox
                    var originalContent = copyButton.Content;
                    copyButton.Content = "✓ Copied";
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() => copyButton.Content = originalContent);
                    });
                }
                catch (Exception ex)
                {
                    // Silently fail or log error
                    System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
                }
            };

            showCopyPanel.Children.Add(showButton);
            showCopyPanel.Children.Add(copyButton);

            buttonPanel.Children.Add(editDeletePanel);
            buttonPanel.Children.Add(showCopyPanel);

            Grid.SetColumn(stackPanel, 0);
            Grid.SetColumn(buttonPanel, 1);

            grid.Children.Add(stackPanel);
            grid.Children.Add(buttonPanel);

            border.Child = grid;

            return border;
        }

        private void SectionSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshSections(SectionSearchBox.Text);
        }

        private void AccountSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshAccounts(AccountSearchBox.Text);
        }

        private void AddSectionButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Add Section", "Enter section name:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                try
                {
                    _dataManager.AddSection(dialog.ResponseText);
                    RefreshSections();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddAccountButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAddAccountDialog();
        }

        private void ShowAddAccountDialog()
        {
            var dialog = new AccountDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _dataManager.AddAccount(_currentSection, dialog.AccountType, dialog.Email, dialog.Password);
                    RefreshAccounts();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShowEditAccountDialog(Account account)
        {
            var dialog = new AccountDialog(account);
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _dataManager.UpdateAccount(account, dialog.AccountType, dialog.Email, dialog.Password);
                    RefreshAccounts();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteAccount(Account account)
        {
            var result = MessageBox.Show($"Delete account '{account.Email}'?",
                "Delete Account", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _dataManager.RemoveAccount(_currentSection, account);
                RefreshAccounts();
            }
        }

        private void LoadThemeButtons()
        {
            ThemesPanel.Children.Clear();
            var themes = _themeManager.GetThemes();

            foreach (var theme in themes)
            {
                var button = new Button
                {
                    Content = theme.Value.Name,
                    Height = 40,
                    Margin = new Thickness(0, 0, 0, 8),
                    Background = new SolidColorBrush(theme.Value.ButtonColor),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 13,
                    Cursor = Cursors.Hand,
                    Tag = theme.Key
                };

                button.Click += ThemePopupButton_Click;
                ThemesPanel.Children.Add(button);
            }

            // Add music enable checkbox
            var checkBox = new CheckBox
            {
                Content = "Enable Theme Music",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 10, 0, 0),
                IsChecked = _dataManager.Settings.MusicEnabled
            };
            checkBox.Checked += MusicEnabledCheckBox_Changed;
            checkBox.Unchecked += MusicEnabledCheckBox_Changed;
            ThemesPanel.Children.Add(checkBox);
        }

        private void ThemePopupButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string themeKey = button.Tag.ToString();

            // Remember if music was playing
            bool wasPlaying = _audioManager.IsPlaying;

            _themeManager.ApplyTheme(themeKey);
            _dataManager.Settings.Theme = themeKey;
            _dataManager.SaveSettings();

            // Update volume menu visibility
            UpdateVolumeMenuVisibility();

            // Refresh account cards to update button colors
            if (_currentSection != null)
            {
                RefreshAccounts();
            }

            // Handle music
            if (_dataManager.Settings.MusicEnabled)
            {
                string musicFile = _themeManager.GetMusicFile(themeKey);
                if (!string.IsNullOrEmpty(musicFile))
                {
                    _audioManager.LoadMusic(musicFile);
                    if (wasPlaying || _dataManager.Settings.MusicPlaying)
                    {
                        _audioManager.Play();
                    }
                }
                else
                {
                    _audioManager.Stop();
                }
            }

            // Close the theme popup after selection
            ThemePopup.IsOpen = false;
        }

        private void MusicEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            _dataManager.Settings.MusicEnabled = checkBox.IsChecked == true;
            _dataManager.SaveSettings();

            UpdateVolumeMenuVisibility();

            if (_dataManager.Settings.MusicEnabled)
            {
                string musicFile = _themeManager.GetMusicFile(_dataManager.Settings.Theme);
                if (!string.IsNullOrEmpty(musicFile))
                {
                    _audioManager.LoadMusic(musicFile);
                    if (_dataManager.Settings.MusicPlaying)
                    {
                        _audioManager.Play();
                    }
                }
            }
            else
            {
                _audioManager.Stop();
            }
        }

        private void ThemeMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle popup open/closed
            if (ThemePopup.IsOpen)
            {
                ThemePopup.IsOpen = false;
            }
            else
            {
                LoadThemeButtons();
                ThemePopup.IsOpen = true;
            }
        }

        private async void VolumeMenuContainer_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseInVolumeArea = true;

            // Cancel any pending close operation
            _volumeMenuCancellation?.Cancel();
            _volumeMenuCancellation = new System.Threading.CancellationTokenSource();

            // Show loading circle
            LoadingCircle.Visibility = Visibility.Visible;
            var loadingStoryboard = (Storyboard)FindResource("LoadingCircleAnimation");
            loadingStoryboard.Begin(this);

            try
            {
                // Wait 1 second before opening
                await System.Threading.Tasks.Task.Delay(1000, _volumeMenuCancellation.Token);

                // Hide loading circle
                LoadingCircle.Visibility = Visibility.Collapsed;
                loadingStoryboard.Stop(this);

                // Open popup
                VolumePopup.IsOpen = true;
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // User moved mouse away before 1 second elapsed
                LoadingCircle.Visibility = Visibility.Collapsed;
                loadingStoryboard.Stop(this);
            }
        }

        private void VolumeMenuContainer_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseInVolumeArea = false;

            // Cancel the opening animation if still pending
            _volumeMenuCancellation?.Cancel();

            // Hide loading circle
            LoadingCircle.Visibility = Visibility.Collapsed;
            var loadingStoryboard = (Storyboard)FindResource("LoadingCircleAnimation");
            loadingStoryboard.Stop(this);

            // Delay closing to check if mouse entered popup
            System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!_isMouseInVolumeArea)
                    {
                        VolumePopup.IsOpen = false;
                    }
                });
            });
        }

        private void VolumePopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseInVolumeArea = true;
        }

        private void VolumePopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseInVolumeArea = false;
            VolumePopup.IsOpen = false;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear current section
            _currentSection = null;

            // Reset accounts panel
            AccountsListPanel.Children.Clear();
            AccountsHeaderText.Text = "Select a section";
            AddAccountButton.IsEnabled = false;
            AccountSearchBox.IsEnabled = false;
            AccountSearchBox.Text = "";
            BackButton.Visibility = Visibility.Collapsed;
            NoAccountsTutorial.Visibility = Visibility.Collapsed;
        }

        // ===== Update System (Exact copy from Sonic Racing structure) =====

        // Update-related fields
        private string _latestVersion = string.Empty;
        private string _updateFileName = string.Empty;
        private string _updateFileSize = string.Empty;
        private bool _isUpdateAvailable;
        private bool _isDownloading;
        private int _downloadProgress;
        private string _downloadProgressTextValue = string.Empty;
        private string _changelog = string.Empty;
        private bool _updateDeclinedThisSession = false;

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide main app, show updates panel
            MainAppPanel.Visibility = Visibility.Collapsed;
            UpdatesPanel.Visibility = Visibility.Visible;

            // Set current version
            CurrentVersion.Text = AppVersion.GetDisplayVersion();

            // Reset declined flag since user is manually checking
            _updateDeclinedThisSession = false;

            // Auto-check for updates
            _ = CheckForUpdatesAsync(silent: true);
        }

        private void BackFromUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide updates panel, show main app
            UpdatesPanel.Visibility = Visibility.Collapsed;
            MainAppPanel.Visibility = Visibility.Visible;

            // Reset installation view
            UpdatesMainView.Visibility = Visibility.Visible;
            UpdatesInstallationView.Visibility = Visibility.Collapsed;
        }

        // Exact copy of Sonic Racing's CheckForUpdatesBanner_Click
        private void CheckForUpdatesBanner_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _ = CheckForUpdatesAsync(silent: false);
        }

        // Exact copy of Sonic Racing's UpdateStatusButton_Click
        private void UpdateStatusButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isUpdateAvailable && !_isDownloading)
            {
                ShowInstallationView();
            }
        }

        // Update-related storage
        private UpdateInfo _currentUpdateInfo;

        // Check for updates on startup (after login)
        private async Task CheckForUpdatesOnStartupAsync()
        {
            // Don't check if user already declined this session
            if (_updateDeclinedThisSession)
                return;

            try
            {
                var updateInfo = await _updateService.CheckForUpdatesAsync();

                if (updateInfo.IsUpdateAvailable)
                {
                    _currentUpdateInfo = updateInfo;
                    _isUpdateAvailable = true;
                    _latestVersion = updateInfo.LatestVersion;
                    _changelog = updateInfo.Changelog;
                    _updateFileName = updateInfo.FileName;

                    if (updateInfo.FileSize > 0)
                    {
                        var sizeInMB = updateInfo.FileSize / (1024.0 * 1024.0);
                        _updateFileSize = $"{sizeInMB:F2} MB";
                    }
                    else
                    {
                        _updateFileSize = "Unknown";
                    }

                    // Show update notification dialog
                    ShowUpdateNotificationDialog();
                }
            }
            catch
            {
                // Silently fail - don't bother user with update check errors on startup
            }
        }

        private void ShowUpdateNotificationDialog()
        {
            var latestVersionFormatted = _latestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? _latestVersion
                : $"v{_latestVersion}";

            var message = $"A new version of PassManager is available!\n\n" +
                         $"Current Version: {AppVersion.GetDisplayVersion()}\n" +
                         $"Latest Version: {latestVersionFormatted}\n" +
                         $"File Size: {_updateFileSize}\n\n" +
                         $"Would you like to view the update details and install it now?";

            var result = MessageBox.Show(message, "Update Available",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                // Navigate to updates panel
                SettingsButton_Click(null, null);
            }
            else
            {
                // User declined, don't show again this session
                _updateDeclinedThisSession = true;
            }
        }

        // Exact copy of Sonic Racing's CheckForUpdatesAsync logic
        private async Task CheckForUpdatesAsync(bool silent = false)
        {
            try
            {
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                _currentUpdateInfo = updateInfo;

                _isUpdateAvailable = updateInfo.IsUpdateAvailable;

                if (updateInfo.IsUpdateAvailable)
                {
                    _latestVersion = updateInfo.LatestVersion;
                    _changelog = updateInfo.Changelog;

                    var latestVersionFormatted = _latestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                        ? _latestVersion
                        : $"v{_latestVersion}";

                    _updateFileName = updateInfo.FileName;

                    if (updateInfo.FileSize > 0)
                    {
                        var sizeInMB = updateInfo.FileSize / (1024.0 * 1024.0);
                        _updateFileSize = $"{sizeInMB:F2} MB";
                    }
                    else
                    {
                        _updateFileSize = "Unknown";
                    }

                    // Update UI - Update available (green banner)
                    UpdateStatusBorder.Visibility = Visibility.Visible;
                    UpdateStatusBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                    UpdateStatusIcon.Text = "☁️";
                    UpdateStatusTitle.Text = "Update Available!";
                    UpdateStatusMessage.Text = $"A new version ({latestVersionFormatted}) is available for download. Click here to update!";
                    UpdateStatusBorder.Cursor = System.Windows.Input.Cursors.Hand;

                    // Show update details
                    UpdateDetailsPanel.Visibility = Visibility.Visible;
                    LatestVersion.Text = latestVersionFormatted;
                    UpdateFileName.Text = _updateFileName;
                    UpdateFileSize.Text = _updateFileSize;

                    // Show hint
                    UpdateHintText.Visibility = Visibility.Visible;
                }
                else
                {
                    // Update UI - Up to date (blue banner)
                    UpdateStatusBorder.Visibility = Visibility.Visible;
                    UpdateStatusBorder.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
                    UpdateStatusIcon.Text = "✓";
                    UpdateStatusTitle.Text = "You're up to date!";
                    UpdateStatusMessage.Text = $"You have the latest version ({AppVersion.GetDisplayVersion()}).";
                    UpdateStatusBorder.Cursor = System.Windows.Input.Cursors.Arrow;

                    // Hide details
                    UpdateDetailsPanel.Visibility = Visibility.Collapsed;
                    UpdateHintText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                // Update UI - Error (orange banner)
                UpdateStatusBorder.Visibility = Visibility.Visible;
                UpdateStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                UpdateStatusIcon.Text = "⚠️";
                UpdateStatusTitle.Text = "Update Check Failed";
                UpdateStatusMessage.Text = $"Could not check for updates. Error: {ex.Message}";
                UpdateStatusBorder.Cursor = System.Windows.Input.Cursors.Arrow;

                UpdateDetailsPanel.Visibility = Visibility.Collapsed;
                UpdateHintText.Visibility = Visibility.Collapsed;

                if (!silent)
                {
                    MessageBox.Show($"Failed to check for updates:\n{ex.Message}", "Update Check Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // Exact copy of Sonic Racing's ShowInstallationView
        private void ShowInstallationView()
        {
            Changelog.Text = _changelog ?? "No changelog available.";

            var latestVersionFormatted = _latestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? _latestVersion
                : $"v{_latestVersion}";

            // Update installation view
            InstallLatestVersion.Text = latestVersionFormatted;
            InstallUpdateFileName.Text = _updateFileName;
            InstallUpdateFileSize.Text = _updateFileSize;

            // Switch views
            UpdatesMainView.Visibility = Visibility.Collapsed;
            UpdatesInstallationView.Visibility = Visibility.Visible;
        }

        // Exact copy of Sonic Racing's CancelInstallation
        private void CancelInstallation_Click(object sender, RoutedEventArgs e)
        {
            UpdatesInstallationView.Visibility = Visibility.Collapsed;
            UpdatesMainView.Visibility = Visibility.Visible;
        }

        // Exact copy of Sonic Racing's AcceptInstallation
        private async void AcceptInstallation_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading || _currentUpdateInfo == null) return;

            _isDownloading = true;

            // Show progress, hide buttons
            InstallDownloadProgressPanel.Visibility = Visibility.Visible;
            InstallButtonsPanel.Visibility = Visibility.Collapsed;

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _downloadProgress = percent;
                        InstallDownloadProgressBar.Value = percent;
                        InstallDownloadProgress.Text = percent.ToString();

                        if (percent < 100)
                        {
                            _downloadProgressTextValue = $"Downloading... {percent}%";
                            InstallDownloadProgressText.Text = _downloadProgressTextValue;
                        }
                        else
                        {
                            _downloadProgressTextValue = "Installing...";
                            InstallDownloadProgressText.Text = _downloadProgressTextValue;
                        }
                    });
                });

                // Use the UpdateService to download and install
                var success = await _updateService.DownloadAndInstallUpdateAsync(_currentUpdateInfo, progress);

                if (!success)
                {
                    MessageBox.Show("Failed to install the update. Please try again or download manually from GitHub.",
                        "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Reset UI
                    InstallDownloadProgressPanel.Visibility = Visibility.Collapsed;
                    InstallButtonsPanel.Visibility = Visibility.Visible;
                    _isDownloading = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading update:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Reset UI
                InstallDownloadProgressPanel.Visibility = Visibility.Collapsed;
                InstallButtonsPanel.Visibility = Visibility.Visible;
                _isDownloading = false;
            }
        }

        private void ApplyButtonStyle(Button button, Color? customColor = null)
        {
            // Set base button style - use current theme's button color if no custom color specified
            Color baseColor = customColor ?? _themeManager.GetButtonColor(_dataManager.Settings.Theme);
            Color hoverColor = Color.FromRgb(
                (byte)Math.Min(255, baseColor.R + 20),
                (byte)Math.Min(255, baseColor.G + 20),
                (byte)Math.Min(255, baseColor.B + 20)
            );
            Color pressColor = Color.FromRgb(
                (byte)Math.Max(0, baseColor.R - 20),
                (byte)Math.Max(0, baseColor.G - 20),
                (byte)Math.Max(0, baseColor.B - 20)
            );

            // Apply base style
            button.Background = new SolidColorBrush(baseColor);
            button.Foreground = Brushes.White;
            button.BorderThickness = new Thickness(0);
            button.FontWeight = FontWeights.SemiBold;

            // Apply border radius
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = button.Background,
                Child = new ContentPresenter
                {
                    Content = button.Content,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // Store original background for restore
            var originalBrush = new SolidColorBrush(baseColor);

            // Hover effect
            button.MouseEnter += (s, e) =>
            {
                if (!button.IsPressed)
                {
                    button.Background = new SolidColorBrush(hoverColor);
                    // Scale up slightly
                    var scaleTransform = new ScaleTransform(1.05, 1.05);
                    button.RenderTransform = scaleTransform;
                    button.RenderTransformOrigin = new Point(0.5, 0.5);
                }
            };

            button.MouseLeave += (s, e) =>
            {
                button.Background = originalBrush;
                // Reset scale
                button.RenderTransform = new ScaleTransform(1.0, 1.0);
            };

            // Press effect
            button.PreviewMouseLeftButtonDown += (s, e) =>
            {
                button.Background = new SolidColorBrush(pressColor);
                // Scale down
                var scaleTransform = new ScaleTransform(0.95, 0.95);
                button.RenderTransform = scaleTransform;
                button.RenderTransformOrigin = new Point(0.5, 0.5);
            };

            button.PreviewMouseLeftButtonUp += (s, e) =>
            {
                // Return to hover state if still hovering
                if (button.IsMouseOver)
                {
                    button.Background = new SolidColorBrush(hoverColor);
                    button.RenderTransform = new ScaleTransform(1.05, 1.05);
                }
                else
                {
                    button.Background = originalBrush;
                    button.RenderTransform = new ScaleTransform(1.0, 1.0);
                }
            };
        }


        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioManager.IsPlaying)
            {
                // Currently playing -> Pause
                _audioManager.Pause();
                PlayPauseButton.Content = "▶️"; // Play icon
                _dataManager.Settings.MusicPlaying = false;
            }
            else
            {
                // Currently paused -> Play
                _audioManager.Play();
                PlayPauseButton.Content = "⏸️"; // Pause icon
                _dataManager.Settings.MusicPlaying = true;
            }

            // Save state
            _dataManager.SaveSettings();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioManager != null)
            {
                _audioManager.SetVolume((float)(e.NewValue / 100.0));
                VolumeText.Text = $"{(int)e.NewValue}%";
                _dataManager.Settings.Volume = (int)e.NewValue;
                _dataManager.SaveSettings();
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var importer = new CsvImporter(_dataManager);
            try
            {
                importer.Import();
                RefreshSections();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var exporter = new CsvExporter(_dataManager);
            try
            {
                exporter.Export();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _audioManager?.Dispose();
            base.OnClosing(e);
        }

        /// <summary>
        /// Calculate password strength (0-100)
        /// </summary>
        private int CalculatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password)) return 0;

            int strength = 0;

            // Length (up to 40 points)
            strength += Math.Min(password.Length * 4, 40);

            // Has lowercase
            if (password.Any(char.IsLower)) strength += 10;

            // Has uppercase
            if (password.Any(char.IsUpper)) strength += 10;

            // Has digits
            if (password.Any(char.IsDigit)) strength += 10;

            // Has special characters
            if (password.Any(c => !char.IsLetterOrDigit(c))) strength += 15;

            // Variety bonus
            var uniqueChars = password.Distinct().Count();
            strength += Math.Min(uniqueChars * 2, 15);

            return Math.Min(strength, 100);
        }

        /// <summary>
        /// Create a circular arc for password strength (Zelda-style)
        /// </summary>
        private System.Windows.Shapes.Path CreateStrengthArc(int strength, double angle)
        {
            // Determine color based on strength
            Color arcColor;
            if (strength < 33)
                arcColor = Color.FromRgb(255, 80, 80);      // Red (weak)
            else if (strength < 66)
                arcColor = Color.FromRgb(255, 200, 80);     // Yellow/Orange (medium)
            else
                arcColor = Color.FromRgb(100, 255, 100);    // Green (strong)

            var path = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(arcColor),
                StrokeThickness = 3,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure();

            // Circle center and radius
            double centerX = 14;
            double centerY = 14;
            double radius = 12;

            // Start at top (270 degrees)
            double startAngle = 270;
            double endAngle = startAngle + angle;

            // Convert to radians
            double startRad = startAngle * Math.PI / 180;
            double endRad = endAngle * Math.PI / 180;

            // Calculate start point
            figure.StartPoint = new Point(
                centerX + radius * Math.Cos(startRad),
                centerY + radius * Math.Sin(startRad)
            );

            // Create arc
            var arc = new ArcSegment
            {
                Point = new Point(
                    centerX + radius * Math.Cos(endRad),
                    centerY + radius * Math.Sin(endRad)
                ),
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = angle > 180
            };

            figure.Segments.Add(arc);
            geometry.Figures.Add(figure);
            path.Data = geometry;

            return path;
        }
    }
}
