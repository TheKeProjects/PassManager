using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;

namespace PassManager.UI
{
    public class AudioManager : IDisposable
    {
        private IWavePlayer _wavePlayer;
        private AudioFileReader _audioFileReader;
        private string _currentFile;
        private float _volume = 0.5f;
        private string _tempMusicFile;

        public bool IsPlaying => _wavePlayer?.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => _wavePlayer?.PlaybackState == PlaybackState.Paused;

        public void LoadMusic(string musicFile)
        {
            if (string.IsNullOrWhiteSpace(musicFile))
            {
                Stop();
                return;
            }

            // Stop current playback
            Stop();

            try
            {
                string actualPath = musicFile;

                // Try to load from embedded resources first
                if (!File.Exists(musicFile))
                {
                    actualPath = ExtractEmbeddedResource(musicFile);
                    if (actualPath == null)
                    {
                        Console.WriteLine($"Music file not found: {musicFile}");
                        return;
                    }
                }

                _currentFile = actualPath;
                _audioFileReader = new AudioFileReader(actualPath);
                _audioFileReader.Volume = _volume;

                _wavePlayer = new WaveOutEvent();
                _wavePlayer.Init(_audioFileReader);

                // Add event handler for looping
                _wavePlayer.PlaybackStopped += OnPlaybackStopped;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading music: {ex.Message}");
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Only loop if it was not manually stopped and there's a current file
            if (_audioFileReader != null && _currentFile != null)
            {
                try
                {
                    // Reset position to beginning and play again
                    _audioFileReader.Position = 0;
                    _wavePlayer?.Play();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error looping music: {ex.Message}");
                }
            }
        }

        private string ExtractEmbeddedResource(string resourcePath)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // Convert path to resource name (replace slashes with dots)
                string resourceName = "PassManager." + resourcePath.Replace("/", ".").Replace("\\", ".");

                // Find the resource (case-insensitive)
                var matchingResource = Array.Find(assembly.GetManifestResourceNames(),
                    name => name.Equals(resourceName, StringComparison.OrdinalIgnoreCase));

                if (matchingResource == null)
                {
                    Console.WriteLine($"Embedded resource not found: {resourceName}");
                    Console.WriteLine("Available resources:");
                    foreach (var res in assembly.GetManifestResourceNames())
                    {
                        if (res.Contains("musica"))
                            Console.WriteLine($"  {res}");
                    }
                    return null;
                }

                // Extract to temp file
                using (Stream resourceStream = assembly.GetManifestResourceStream(matchingResource))
                {
                    if (resourceStream == null)
                        return null;

                    // Clean up old temp file if exists
                    if (_tempMusicFile != null && File.Exists(_tempMusicFile))
                    {
                        try { File.Delete(_tempMusicFile); } catch { }
                    }

                    // Create new temp file
                    string fileName = Path.GetFileName(resourcePath);
                    _tempMusicFile = Path.Combine(Path.GetTempPath(), $"PassManager_{fileName}");

                    using (FileStream fileStream = File.Create(_tempMusicFile))
                    {
                        resourceStream.CopyTo(fileStream);
                    }

                    return _tempMusicFile;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting embedded resource: {ex.Message}");
                return null;
            }
        }

        public void Play()
        {
            try
            {
                if (_wavePlayer == null || _audioFileReader == null)
                    return;

                if (_wavePlayer.PlaybackState == PlaybackState.Paused)
                {
                    _wavePlayer.Play();
                }
                else if (_wavePlayer.PlaybackState == PlaybackState.Stopped)
                {
                    _audioFileReader.Position = 0;
                    _wavePlayer.Play();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing music: {ex.Message}");
            }
        }

        public void Pause()
        {
            try
            {
                if (_wavePlayer?.PlaybackState == PlaybackState.Playing)
                {
                    _wavePlayer.Pause();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pausing music: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                if (_wavePlayer != null)
                {
                    _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
                    _wavePlayer.Stop();
                    _wavePlayer.Dispose();
                    _wavePlayer = null;
                }

                _audioFileReader?.Dispose();
                _audioFileReader = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping music: {ex.Message}");
            }
        }

        public void SetVolume(float volume)
        {
            _volume = Math.Clamp(volume, 0f, 1f);

            if (_audioFileReader != null)
            {
                _audioFileReader.Volume = _volume;
            }
        }

        public void Dispose()
        {
            Stop();

            // Clean up temp music file
            if (_tempMusicFile != null && File.Exists(_tempMusicFile))
            {
                try
                {
                    File.Delete(_tempMusicFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
