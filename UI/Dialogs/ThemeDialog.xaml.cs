using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PassManager.Models;

namespace PassManager.UI
{
    public partial class ThemeDialog : Window
    {
        private ThemeManager _themeManager;
        private AudioManager _audioManager;
        private Settings _settings;

        public ThemeDialog(ThemeManager themeManager, AudioManager audioManager, Settings settings)
        {
            InitializeComponent();

            _themeManager = themeManager;
            _audioManager = audioManager;
            _settings = settings;

            MusicEnabledCheckBox.IsChecked = _settings.MusicEnabled;

            LoadThemes();
        }

        private void LoadThemes()
        {
            var themes = _themeManager.GetMainThemes();

            foreach (var theme in themes)
            {
                var button = new Button
                {
                    Content = theme.Value.Name,
                    Height = 40,
                    Margin = new Thickness(0, 0, 0, 10),
                    Background = new SolidColorBrush(theme.Value.ButtonColor),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 14,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = theme.Key
                };

                button.Click += ThemeButton_Click;
                ThemesPanel.Children.Add(button);
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string themeKey = button.Tag.ToString();

            // Remember if music was playing before theme change
            bool wasPlaying = _audioManager.IsPlaying;

            _themeManager.ApplyTheme(themeKey);
            _settings.Theme = themeKey;
            DataManager.Instance.SaveSettings();

            // Load music if enabled and theme has music
            if (_settings.MusicEnabled)
            {
                string musicFile = _themeManager.GetMusicFile(themeKey);
                if (!string.IsNullOrEmpty(musicFile))
                {
                    _audioManager.LoadMusic(musicFile);
                    // Only play if music was already playing
                    if (wasPlaying || _settings.MusicPlaying)
                    {
                        _audioManager.Play();
                    }
                }
                else
                {
                    // Theme has no music, stop playback
                    _audioManager.Stop();
                }
            }
        }

        private void MusicEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _settings.MusicEnabled = MusicEnabledCheckBox.IsChecked == true;
            DataManager.Instance.SaveSettings();

            if (_settings.MusicEnabled)
            {
                string musicFile = _themeManager.GetMusicFile(_settings.Theme);
                _audioManager.LoadMusic(musicFile);
            }
            else
            {
                _audioManager.Stop();
            }
        }
    }
}
