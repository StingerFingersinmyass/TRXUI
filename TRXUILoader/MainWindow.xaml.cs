using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SourceChord.FluentWPF;

namespace TRXLoader
{
    public partial class MainWindow : AcrylicWindow
    {
        private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = true });
        private readonly CultureInfo ci = CultureInfo.InstalledUICulture;

        private IEasingFunction Smooth { get; set; } = new QuarticEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        public MainWindow()
        {
            Log("Constructor - Start");
            try
            {
                if (Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName.Contains(Path.GetTempPath()))
                {
                    Log("Constructor - Running in temp path, showing message and exiting.");
                    MessageBox.Show("You cannot run TRX from within an archive. Please extract it first.", "TRX", MessageBoxButton.OK, MessageBoxImage.Hand);
                    Application.Current.Shutdown();
                }
                else
                {
                    Log("Constructor - Initializing components.");
                    ResourceDictionaryEx.GlobalTheme = ElementTheme.Dark;
                    InitializeComponent();
                    Log("Constructor - InitializeComponent finished.");
                }
            }
            catch (Exception ex)
            {
                Log($"Constructor - CRITICAL ERROR: {ex}");
                MessageBox.Show("A critical error occurred during initialization. Please send the log file to support.", "TRX Loader Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
            Log("Constructor - End");
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Log("MainWindow_Loaded - Start");
            try
            {
                Log("MainWindow_Loaded - Calling StepOne");
                await StepOne();
            }
            catch (Exception ex)
            {
                Log($"MainWindow_Loaded - CRITICAL ERROR: {ex}");
                MessageBox.Show("A critical error occurred on startup. Details: " + ex.ToString(), "TRX Loader Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
            Log("MainWindow_Loaded - End");
        }

        public async Task StepOne()
        {
            Log("StepOne - Start");
            try
            {
                CleanUpDirectories();

                Log("StepOne - Updating UI to 'Checking'");
                headerLbl.Content = "Checking";
                FadeIn(headerLbl);
                await Task.Delay(500);

                string binPath = ".\\Bin";
                Log($"StepOne - Ensuring directory '{binPath}' exists.");
                Directory.CreateDirectory(binPath);

                Log("StepOne - Reading current version");
                string verPath = Path.Combine(binPath, "ver.bin");
                string currentVer = File.Exists(verPath) ? File.ReadAllText(verPath) : "0";
                Log($"StepOne - Current version is: {currentVer}");

                // Integrity Check
                string coreExePath = Path.Combine(binPath, "TRX.exe");
                if (!File.Exists(coreExePath))
                {
                    Log("StepOne - INTEGRITY CHECK FAILED: TRX.exe is missing. Forcing update.");
                    currentVer = "0"; // Force update by treating current version as non-existent
                }

                Log("StepOne - Sending request to GitHub API");
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                string releaseInfo = await httpClient.GetStringAsync("https://api.github.com/repos/StingerFingersinmyass/TRXUI.INSIDES/releases/latest");
                Log("StepOne - GitHub API response received.");

                string ver = ParseJsonValue(releaseInfo, "tag_name");
                if (string.IsNullOrEmpty(ver)) throw new Exception("Could not parse version from GitHub response.");

                if (ver != currentVer)
                {
                    Log($"StepOne - New version found: {ver}. Current version: {currentVer}. Starting download.");
                    headerLbl.Content = "Downloading";
                    statusLbl.Content = "Please wait...";
                    FadeIn(headerLbl);
                    FadeIn(statusLbl);

                    string assetsUrl = ParseJsonValue(releaseInfo, "assets_url");
                    if (string.IsNullOrEmpty(assetsUrl)) throw new Exception("Could not parse assets_url from GitHub response.");

                    string assetsInfo = await httpClient.GetStringAsync(assetsUrl);
                    string downloadUrl = ParseJsonValue(assetsInfo, "browser_download_url");

                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        Log($"StepOne - Download URL found: {downloadUrl}");
                        await DownloadAndExtract(downloadUrl, ver, binPath);
                    }
                    else
                    {
                        throw new Exception("Could not find download URL in assets info.");
                    }
                }
                else
                {
                    Log("StepOne - Already latest version. No update needed.");
                }
            }
            catch (Exception ex)
            {
                Log($"StepOne - CRITICAL ERROR: {ex}");
                MessageBox.Show("An error occurred during the update process. Please check the log file.", "TRX Loader Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }
            
            await StepTwo();
        }

        private async Task DownloadAndExtract(string downloadUrl, string newVersion, string destinationPath)
        {
            string tempZipPath = Path.GetTempFileName();
            Log($"DownloadAndExtract - Downloading to temp file: {tempZipPath}");
            const int maxRetries = 3;

            try
            {
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        Log($"DownloadAndExtract - Attempt {attempt}/{maxRetries} to download file.");
                        using (var response = await httpClient.GetAsync(new Uri(downloadUrl), HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
                            using (var streamToWriteTo = File.Open(tempZipPath, FileMode.Create))
                            {
                                await streamToReadFrom.CopyToAsync(streamToWriteTo);
                            }
                        }
                        Log("DownloadAndExtract - Download complete.");
                        goto DownloadSuccess;
                    }
                    catch (Exception ex)
                    {
                        Log($"DownloadAndExtract - Attempt {attempt} failed: {ex.Message}");
                        if (attempt == maxRetries) throw;
                        await Task.Delay(2000);
                    }
                }

            DownloadSuccess:
                await Task.Run(() =>
                {
                    Log("DownloadAndExtract - Preparing for smart update.");
                    string tempBackupPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "temp_backup_data");

                    string[] foldersToKeep = { "AutoExec", "DLLs", "Icons", "SavedTabs", "Scripts", "TRXLogos" };
                    string[] filesToKeep = { "SeliwareAPI.dll", "TRX_Settings.json", "key.dat" };

                    // 1. Backup
                    if (Directory.Exists(tempBackupPath)) Directory.Delete(tempBackupPath, true);
                    Directory.CreateDirectory(tempBackupPath);

                    foreach (var folder in foldersToKeep)
                    {
                        string source = Path.Combine(destinationPath, folder);
                        if (Directory.Exists(source)) Directory.Move(source, Path.Combine(tempBackupPath, folder));
                    }
                    foreach (var file in filesToKeep)
                    {
                        string source = Path.Combine(destinationPath, file);
                        if (File.Exists(source)) File.Move(source, Path.Combine(tempBackupPath, file));
                    }
                    Log("DownloadAndExtract - User data backed up.");

                    // 2. Wipe Bin
                    Log($"DownloadAndExtract - Cleaning directory '{destinationPath}'.");
                    DirectoryInfo directoryInfo = new DirectoryInfo(destinationPath);
                    if (directoryInfo.Exists)
                    {
                        foreach (FileInfo file in directoryInfo.GetFiles()) file.Delete();
                        foreach (DirectoryInfo dir in directoryInfo.GetDirectories()) dir.Delete(true);
                    }
                    else
                    {
                        directoryInfo.Create();
                    }

                    // 3. Extract
                    Log("DownloadAndExtract - Starting extraction.");
                    ZipFile.ExtractToDirectory(tempZipPath, destinationPath);
                    Log("DownloadAndExtract - Extraction complete.");

                    // 4. Restore
                    Log("DownloadAndExtract - Restoring user data.");
                    foreach (var folder in foldersToKeep)
                    {
                        string source = Path.Combine(tempBackupPath, folder);
                        string dest = Path.Combine(destinationPath, folder);
                        if (Directory.Exists(source))
                        {
                            MergeDirectory(source, dest);
                        }
                    }
                    foreach (var file in filesToKeep)
                    {
                        string source = Path.Combine(tempBackupPath, file);
                        string dest = Path.Combine(destinationPath, file);
                        if (File.Exists(source))
                        {
                            File.Copy(source, dest, true); // Overwrite
                        }
                    }
                    Log("DownloadAndExtract - User data restored.");

                    // 5. Cleanup
                    if (Directory.Exists(tempBackupPath)) Directory.Delete(tempBackupPath, true);
                });

                string verPath = Path.Combine(destinationPath, "ver.bin");
                File.WriteAllText(verPath, newVersion);
                Log("DownloadAndExtract - Update process complete.");
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                    Log($"DownloadAndExtract - Deleted temp file: {tempZipPath}");
                }
            }
        }

        public async Task StepTwo()
        {
            Log("StepTwo - Start");
            await Task.Delay(500);
            UFade(headerLbl);
            UFade(statusLbl);
            await Task.Delay(500);
            headerLbl.Content = "Starting";
            statusLbl.Content = "Please wait";
            FadeIn(headerLbl);
            FadeIn(statusLbl);
            ObjectWidthShift(this, 350, 700);
            await Task.Delay(4280);
            
            string exePath = ".\\Bin\\TRX.exe";
            Log($"StepTwo - Starting '{exePath}'");
            Process.Start(exePath);
            
            await Task.Delay(500);
            Log("StepTwo - Exiting loader.");
            Application.Current.Shutdown();
        }

        private void CleanUpDirectories()
        {
            Log("CleanUpDirectories - Starting cleanup.");
            string rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string binPath = Path.Combine(rootPath, "Bin");
            if (!Directory.Exists(binPath)) Directory.CreateDirectory(binPath);

            string[] foldersToMove = { "AutoExec", "DLLs", "Icons", "SavedTabs", "Scripts", "TRXLogos" };

            foreach (var folder in foldersToMove)
            {
                string sourcePath = Path.Combine(rootPath, folder);
                string destPath = Path.Combine(binPath, folder);

                if (Directory.Exists(sourcePath) && !Directory.Exists(destPath))
                {
                    Log($"CleanUpDirectories - Moving '{sourcePath}' to '{destPath}'.");
                    Directory.Move(sourcePath, destPath);
                }
            }
            Log("CleanUpDirectories - Cleanup finished.");
        }

        private void MergeDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                MergeDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }

        public void FadeIn(DependencyObject Object)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation { From = 0.0, To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(500)) };
            Storyboard.SetTarget(animation, Object);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        public void UFade(DependencyObject Object)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation { From = 1.0, To = 0.0, Duration = new Duration(TimeSpan.FromMilliseconds(500)) };
            Storyboard.SetTarget(animation, Object);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        public void ObjectWidthShift(DependencyObject Object, int Get, int Set)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation { From = Get, To = Set, Duration = TimeSpan.FromSeconds(3.0), EasingFunction = Smooth };
            Storyboard.SetTarget(animation, Object);
            Storyboard.SetTargetProperty(animation, new PropertyPath(FrameworkElement.WidthProperty));
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText("loader_log.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { /* Ignore logging errors */ }
        }

        private string ParseJsonValue(string json, string key)
        {
            string keyPattern = $"\"{key}\":\"";
            int keyIndex = json.IndexOf(keyPattern);
            if (keyIndex == -1) return null;

            int valueIndex = keyIndex + keyPattern.Length;
            int valueEndIndex = json.IndexOf('"', valueIndex);
            if (valueEndIndex == -1) return null;

            return json.Substring(valueIndex, valueEndIndex - valueIndex);
        }
    }
}
