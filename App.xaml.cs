using System;
using System.Windows;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Linq;
using System.Threading.Tasks;
using PassManager.Features.Updates.Services;

namespace PassManager
{
    public partial class App : Application
    {
        private const string TARGET_INSTALL_PATH = @"C:\Program Files\An-Average-Developer\PassManager";

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if we need to relocate to the fixed installation path
            bool shouldRelocate = await CheckAndRelocateIfNeededAsync();

            if (shouldRelocate)
            {
                // If relocation happened, the new process was started and we exit
                Shutdown();
                return;
            }

            // Initialize application data directory
            var dataManager = DataManager.Instance;

            // Manually create and show the main window (since we removed StartupUri)
            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        private async Task<bool> CheckAndRelocateIfNeededAsync()
        {
            InstallProgressWindow progressWindow = null;

            try
            {
                // Use Environment.ProcessPath for .NET 5+ compatibility with single-file executables
                string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    return false; // Can't determine exe path, run from current location
                }

                string currentDir = Path.GetDirectoryName(currentExePath);
                string exeFileName = Path.GetFileName(currentExePath);
                string targetExePath = Path.Combine(TARGET_INSTALL_PATH, exeFileName);

                // Check if we're already running from the target location
                if (string.Equals(currentDir, TARGET_INSTALL_PATH, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Already in the correct location
                }

                // Check if PassManager already exists in target location
                if (Directory.Exists(TARGET_INSTALL_PATH) && File.Exists(targetExePath))
                {
                    // Just launch from target location without showing progress window
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = targetExePath,
                        UseShellExecute = true,
                        WorkingDirectory = TARGET_INSTALL_PATH
                    };
                    Process.Start(startInfo);
                    return true; // Signal to shutdown current instance
                }

                // Target location doesn't have PassManager, need to download from GitHub
                // Check if we have admin rights
                bool isAdmin = IsRunningAsAdministrator();

                if (!isAdmin)
                {
                    // Restart with admin privileges
                    RestartAsAdministrator();
                    return true;
                }

                // Show progress window
                progressWindow = new InstallProgressWindow();
                progressWindow.Show();
                progressWindow.UpdateStatus("Checking latest version...", "Connecting to GitHub", 0);

                // Give the window time to render
                await Task.Delay(100);

                string downloadUrl = null;
                string fileName = null;

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "PassManager");
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    try
                    {
                        // Get latest version from version.txt (same as UpdateService)
                        var versionUrl = "https://raw.githubusercontent.com/TheKeProjects/PassManager/main/version.txt";
                        var latestVersion = await httpClient.GetStringAsync(versionUrl);
                        latestVersion = latestVersion?.Trim() ?? string.Empty;
                        latestVersion = latestVersion.Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();

                        if (string.IsNullOrWhiteSpace(latestVersion))
                        {
                            throw new Exception("Version.txt is empty or invalid");
                        }

                        progressWindow.UpdateStatus("Found version...", $"Version {latestVersion}", 5);

                        // Construct filename same as UpdateService
                        fileName = $"PassManager-v{latestVersion.Replace(".", "-")}.zip";
                        downloadUrl = $"https://github.com/TheKeProjects/PassManager/releases/latest/download/{fileName}";
                    }
                    catch (Exception ex)
                    {
                        progressWindow?.Close();
                        MessageBox.Show($"Could not get latest version from GitHub: {ex.Message}\n\nRunning from current location.",
                            "PassManager", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    progressWindow.UpdateStatus("Downloading PassManager...", $"Version {fileName}", 10);

                    // Download to temp directory
                    string tempDir = Path.Combine(Path.GetTempPath(), "PassManager_Install");
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                    Directory.CreateDirectory(tempDir);

                    string zipPath = Path.Combine(tempDir, fileName);

                    // Download the zip with progress
                    using (var response = await httpClient.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var buffer = new byte[8192];
                        var bytesRead = 0L;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            int read;
                            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                bytesRead += read;

                                if (totalBytes > 0)
                                {
                                    var progressPercentage = 10 + (int)((bytesRead * 70) / totalBytes);
                                    var downloadedMB = bytesRead / 1024.0 / 1024.0;
                                    var totalMB = totalBytes / 1024.0 / 1024.0;
                                    progressWindow.UpdateStatus("Downloading PassManager...",
                                        $"{downloadedMB:F1} MB / {totalMB:F1} MB", progressPercentage);
                                }
                            }
                        }
                    }

                    progressWindow.UpdateStatus("Extracting files...", "Installing to Program Files", 85);

                    // Create target directory
                    if (!Directory.Exists(TARGET_INSTALL_PATH))
                    {
                        Directory.CreateDirectory(TARGET_INSTALL_PATH);
                    }

                    // Extract the zip file to target location
                    ZipFile.ExtractToDirectory(zipPath, TARGET_INSTALL_PATH, true);

                    progressWindow.UpdateStatus("Finalizing installation...", "Cleaning up temporary files", 95);

                    // Clean up temp
                    try
                    {
                        await Task.Delay(500);
                        Directory.Delete(tempDir, true);
                    }
                    catch { }

                    progressWindow.UpdateStatus("Installation complete!", "Launching PassManager...", 100);
                    await Task.Delay(500);
                }

                // Start the relocated executable
                ProcessStartInfo launchInfo = new ProcessStartInfo
                {
                    FileName = targetExePath,
                    UseShellExecute = true,
                    WorkingDirectory = TARGET_INSTALL_PATH
                };
                Process.Start(launchInfo);

                // Keep progress window visible for a moment so user sees the app is launching
                if (progressWindow != null)
                {
                    progressWindow.UpdateStatus("PassManager is starting...", "Please wait", 100);
                    await Task.Delay(2000);
                }

                return true; // Signal to shutdown current instance
            }
            catch (Exception ex)
            {
                progressWindow?.Close();
                MessageBox.Show($"Failed to relocate application: {ex.Message}\n\nRunning from current location.",
                    "PassManager", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            finally
            {
                progressWindow?.Close();
            }
        }

        private bool IsRunningAsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void RestartAsAdministrator()
        {
            try
            {
                string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    MessageBox.Show("Failed to determine executable path.\n\nRunning from current location.",
                        "PassManager", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = currentExePath,
                    UseShellExecute = true,
                    Verb = "runas" // Request elevation
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart with administrator privileges: {ex.Message}\n\nRunning from current location.",
                    "PassManager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
