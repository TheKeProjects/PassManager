using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PassManager.UI
{
    public class ThemeManager
    {
        private readonly Window _window;
        private Dictionary<string, ThemeData> _themes;

        public ThemeManager(Window window)
        {
            _window = window;
            InitializeThemes();
        }

        private void InitializeThemes()
        {
            // Main themes (displayed directly in theme menu)
            _themes = new Dictionary<string, ThemeData>
            {
                ["default"] = new ThemeData
                {
                    Name = "Default",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(255, 182, 193), // Pink
                        Color.FromRgb(147, 112, 219), // Purple
                        90),
                    ButtonColor = Color.FromRgb(107, 155, 209),
                    MusicFile = null
                },
                ["dark"] = new ThemeData
                {
                    Name = "Dark Mode",
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    ButtonColor = Color.FromRgb(70, 70, 70),
                    MusicFile = null
                },
                ["purple"] = new ThemeData
                {
                    Name = "Purple",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(138, 43, 226), // BlueViolet
                        Color.FromRgb(75, 0, 130),   // Indigo
                        90),
                    ButtonColor = Color.FromRgb(138, 43, 226),
                    MusicFile = null
                },
                ["ultrakill"] = new ThemeData
                {
                    Name = "Ultrakill",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(200, 0, 0),    // Bright blood red
                        Color.FromRgb(40, 0, 0),     // Very dark red/black
                        90),
                    ButtonColor = Color.FromRgb(200, 0, 0),
                    MusicFile = "musica/ultrakill_theme.mp3"
                },
                ["sonic"] = new ThemeData
                {
                    Name = "Sonic",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(29, 83, 165),  // Sonic blue (darker)
                        Color.FromRgb(0, 123, 255),  // Bright blue
                        90),
                    ButtonColor = Color.FromRgb(29, 83, 165),
                    MusicFile = "musica/sonic_theme.mp3"
                },
                ["zelda"] = new ThemeData
                {
                    Name = "Zelda",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(40, 120, 40),  // Link's green tunic
                        Color.FromRgb(20, 60, 20),   // Dark forest green
                        90),
                    ButtonColor = Color.FromRgb(40, 120, 40),
                    MusicFile = "musica/zelda_theme.mp3"
                },
                ["mario"] = new ThemeData
                {
                    Name = "Mario",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(227, 38, 54),  // Mario red (cap & shirt)
                        Color.FromRgb(32, 100, 210), // Mario blue (overalls)
                        90),
                    ButtonColor = Color.FromRgb(227, 38, 54),
                    MusicFile = "musica/mario_theme.mp3"
                },
                ["pokemon"] = new ThemeData
                {
                    Name = "Pokemon",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(255, 203, 5),  // Pikachu yellow
                        Color.FromRgb(200, 160, 0),  // Darker yellow/gold
                        90),
                    ButtonColor = Color.FromRgb(255, 203, 5), // Pikachu yellow
                    MusicFile = "musica/pokemon_theme.mp3"
                },
                ["minecraft"] = new ThemeData
                {
                    Name = "Minecraft",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(83, 163, 64),  // Creeper green
                        Color.FromRgb(48, 95, 37),   // Dark creeper green
                        90),
                    ButtonColor = Color.FromRgb(83, 163, 64),
                    MusicFile = "musica/minecraft_theme.mp3"
                },
                ["inazuma_eleven"] = new ThemeData
                {
                    Name = "Inazuma Eleven",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(255, 203, 5),  // Mario red (cap & shirt)
                        Color.FromRgb(32, 100, 210), // Mario blue (overalls)
                        90),
                    ButtonColor = Color.FromRgb(255, 203, 5), // Yellow
                    MusicFile = "musica/inazuma_theme.mp3"
                },
                ["axel_blaze"] = new ThemeData
                {
                    Name = "Axel Blaze",
                    Background = new LinearGradientBrush(
                        Color.FromRgb(255, 140, 50),   // Bright orange (headband)
                        Color.FromRgb(139, 69, 19),    // Dark brown/saddle brown
                        90),
                    ButtonColor = Color.FromRgb(255, 120, 30),  // Orange button
                    MusicFile = "musica/axel_theme.mp3"
                }
            };
        }

        public void ApplyTheme(string themeKey)
        {
            if (!_themes.ContainsKey(themeKey))
                themeKey = "default";

            var theme = _themes[themeKey];

            // Apply background
            var mainGrid = _window.FindName("MainGrid") as System.Windows.Controls.Grid;
            if (mainGrid != null)
            {
                mainGrid.Background = theme.Background;
            }

            // Update button styles
            var modernButtonStyle = _window.FindResource("ModernButton") as Style;
            if (modernButtonStyle != null)
            {
                var newStyle = new Style(typeof(System.Windows.Controls.Button), modernButtonStyle);
                newStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty,
                    new SolidColorBrush(theme.ButtonColor)));
                _window.Resources["ModernButton"] = newStyle;
            }

            // Update animated background if this is the MainWindow
            var mainWindow = _window as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.UpdateAnimatedBackground();
            }
        }

        public Dictionary<string, ThemeData> GetThemes()
        {
            return _themes;
        }

        public Dictionary<string, ThemeData> GetMainThemes()
        {
            return _themes;
        }

        public string GetMusicFile(string themeKey)
        {
            return _themes.ContainsKey(themeKey) ? _themes[themeKey].MusicFile : null;
        }

        public Color GetButtonColor(string themeKey)
        {
            return _themes.ContainsKey(themeKey) ? _themes[themeKey].ButtonColor : Color.FromRgb(107, 155, 209);
        }
    }

    public class ThemeData
    {
        public string Name { get; set; }
        public Brush Background { get; set; }
        public Color ButtonColor { get; set; }
        public string MusicFile { get; set; }
    }
}
