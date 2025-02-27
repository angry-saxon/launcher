using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Ionic.Zip;
using LauncherConfig;

namespace CanaryLauncherUpdate
{
    public partial class MainWindow : Window
    {
        private const string LauncherConfigUrl = "http://51.81.154.175/updates/launcher_config.json";
        private ClientConfig clientConfig;
        private string clientExecutableName;
        private string urlClient;
        private string programVersion;
        private string newVersion = "";
        private bool clientDownloaded = false;
        private bool needUpdate = false;
        private HttpClient httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                clientConfig = await ClientConfig.LoadFromFileAsync(LauncherConfigUrl);
                clientExecutableName = clientConfig.clientExecutable;
                urlClient = clientConfig.newClientUrl;
                programVersion = clientConfig.launcherVersion;
                await LoadRemotePatchNotesAsync();

                // Initialize UI elements.
                ImageLogoServer.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png"));
                //ImageLogoCompany.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/logo_company.png"));

                newVersion = clientConfig.clientVersion;
                progressbarDownload.Visibility = Visibility.Collapsed;
                labelClientVersion.Visibility = Visibility.Collapsed;
                labelDownloadPercent.Visibility = Visibility.Collapsed;

                string launcherBasePath = LauncherUtils.GetLauncherPath(clientConfig.clientFolder, true);
                string launcherConfigPath = System.IO.Path.Combine(launcherBasePath, "launcher_config.json");

                if (File.Exists(launcherConfigPath))
                {
                    string actualVersion = LauncherUtils.GetClientVersion(launcherBasePath);
                    labelVersion.Text = "v" + programVersion;

                    if (newVersion != actualVersion)
                    {
                        buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_update.png")));
                        buttonPlayIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon_update.png"));
                        labelClientVersion.Content = newVersion;
                        labelClientVersion.Visibility = Visibility.Visible;
                        buttonPlay.Visibility = Visibility.Visible;
                        buttonPlay_tooltip.Text = "Update";
                        needUpdate = true;
                    }
                }

                string launcherPath = LauncherUtils.GetLauncherPath(clientConfig.clientFolder);
                if (!File.Exists(launcherConfigPath) || (Directory.Exists(launcherPath) &&
                    Directory.GetFiles(launcherPath).Length == 0 && Directory.GetDirectories(launcherPath).Length == 0))
                {
                    labelVersion.Text = "v" + programVersion;
                    buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_update.png")));
                    buttonPlayIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon_update.png"));
                    labelClientVersion.Content = "Download";
                    labelClientVersion.Visibility = Visibility.Visible;
                    buttonPlay.Visibility = Visibility.Visible;
                    buttonPlay_tooltip.Text = "Download";
                    needUpdate = true;
                }
            }
            catch (Exception ex)
            {
                labelVersion.Text = "Error loading config: " + ex.Message;
            }
        }

        private async Task UpdateClientAsync()
        {
            try
            {
                string launcherPath = LauncherUtils.GetLauncherPath(clientConfig.clientFolder, true);
                if (!Directory.Exists(launcherPath))
                {
                    Directory.CreateDirectory(launcherPath);
                }
                labelDownloadPercent.Visibility = Visibility.Visible;
                progressbarDownload.Visibility = Visibility.Visible;
                labelClientVersion.Visibility = Visibility.Collapsed;
                buttonPlay.Visibility = Visibility.Collapsed;

                // Download client file using HttpClient with progress reporting.
                using (HttpResponseMessage response = await httpClient.GetAsync(urlClient, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    bool canReportProgress = totalBytes != -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(System.IO.Path.Combine(launcherPath, "data.zip"), FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var totalRead = 0L;
                        var buffer = new byte[8192];
                        bool isMoreToRead = true;
                        while (isMoreToRead)
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                                continue;
                            }
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            if (canReportProgress)
                            {
                                int progressPercentage = (int)((totalRead * 100) / totalBytes);
                                progressbarDownload.Value = progressPercentage;
                                labelDownloadPercent.Content = SizeSuffix(totalRead) + " / " + SizeSuffix(totalBytes);
                            }
                        }
                    }
                }

                // Extract ZIP file
                await Task.Run(() =>
                {
                    try
                    {
                        using (ZipFile modZip = ZipFile.Read(System.IO.Path.Combine(launcherPath, "data.zip")))
                        {
                            foreach (ZipEntry zipEntry in modZip)
                            {
                                zipEntry.Extract(launcherPath, ExtractExistingFileAction.OverwriteSilently);
                            }
                        }
                        File.Delete(System.IO.Path.Combine(launcherPath, "data.zip"));
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => labelVersion.Text = "Extraction error: " + ex.Message);
                    }
                });

                progressbarDownload.Value = 100;

                // Download launcher_config.json using HttpClient
                string localConfigPath = System.IO.Path.Combine(launcherPath, "launcher_config.json");
                using (HttpResponseMessage configResponse = await httpClient.GetAsync(LauncherConfigUrl))
                {
                    configResponse.EnsureSuccessStatusCode();
                    string configContent = await configResponse.Content.ReadAsStringAsync();
                    File.WriteAllText(localConfigPath, configContent);
                }

                AddReadOnlyFiles();
                // Create desktop shortcut.
                string exePath = System.IO.Path.Combine(launcherPath, "", clientExecutableName);
                //LauncherUtils.CreateShortcut("The Culling", exePath); // for client shortcut
                LauncherUtils.CreateLauncherShortcut("The Culling");

                needUpdate = false;
                clientDownloaded = true;
                labelClientVersion.Content = LauncherUtils.GetClientVersion(launcherPath);
                buttonPlay_tooltip.Text = LauncherUtils.GetClientVersion(launcherPath);
                labelClientVersion.Visibility = Visibility.Visible;
                buttonPlay.Visibility = Visibility.Visible;
                progressbarDownload.Visibility = Visibility.Collapsed;
                labelDownloadPercent.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                labelVersion.Text = "Update error: " + ex.Message;
            }
        }

        private void AddReadOnlyFiles()
        {
            string launcherPath = LauncherUtils.GetLauncherPath(clientConfig.clientFolder);
            string eventSchedulePath = System.IO.Path.Combine(launcherPath, "cache", "eventschedule.json");
            if (File.Exists(eventSchedulePath))
            {
                File.SetAttributes(eventSchedulePath, FileAttributes.ReadOnly);
            }
            string boostedCreaturePath = System.IO.Path.Combine(launcherPath, "cache", "boostedcreature.json");
            if (File.Exists(boostedCreaturePath))
            {
                File.SetAttributes(boostedCreaturePath, FileAttributes.ReadOnly);
            }
            string onlineNumbersPath = System.IO.Path.Combine(launcherPath, "cache", "onlinenumbers.json");
            if (File.Exists(onlineNumbersPath))
            {
                File.SetAttributes(onlineNumbersPath, FileAttributes.ReadOnly);
            }
        }

        private bool _isUpdating = false;
        private async void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;  // Prevent multiple simultaneous updates
            _isUpdating = true;

            try
            {
                if (needUpdate || !Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig.clientFolder, true)))
                {
                    await UpdateClientAsync();
                }
                else
                {
                    string launcherPath = LauncherUtils.GetLauncherPath(clientConfig.clientFolder, true);
                    if (clientDownloaded || !Directory.Exists(launcherPath))
                    {
                        string exePath = System.IO.Path.Combine(launcherPath, "", clientExecutableName);
                        try
                        {
                            Process.Start(exePath);
                            this.Close();
                        }
                        catch (Exception ex)
                        {
                            labelVersion.Text = "Error starting client: " + ex.Message;
                        }
                    }
                    else
                    {
                        await UpdateClientAsync();
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
        {
            string launcherPath = LauncherUtils.GetLauncherPath(clientConfig?.clientFolder, true);
            if (File.Exists(System.IO.Path.Combine(launcherPath, "launcher_config.json")))
            {
                string actualVersion = LauncherUtils.GetClientVersion(launcherPath);
                if (newVersion != actualVersion)
                {
                    buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_hover_update.png")));
                }
                else
                {
                    buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_hover_play.png")));
                }
            }
            else
            {
                buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_hover_update.png")));
            }
        }

        private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
        {
            string launcherPath = LauncherUtils.GetLauncherPath(clientConfig?.clientFolder, true);
            if (File.Exists(System.IO.Path.Combine(launcherPath, "launcher_config.json")))
            {
                string actualVersion = LauncherUtils.GetClientVersion(launcherPath);
                if (newVersion != actualVersion)
                {
                    buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_update.png")));
                }
                else
                {
                    buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_play.png")));
                }
            }
            else
            {
                buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Assets/button_update.png")));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResizeMode != ResizeMode.NoResize)
                WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        private static string SizeSuffix(long value, int decimalPlaces = 1)
        {
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag++;
                adjustedSize /= 1024;
            }
            return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, SizeSuffixes[mag]);
        }

        private async Task LoadRemotePatchNotesAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    txtPatchNotes.Text = "Loading patch notes from server...";
                    string json = await client.GetStringAsync(LauncherConfigUrl);

                    // Deserialize the configuration JSON into your ClientConfig object.
                    ClientConfig serverConfig = JsonConvert.DeserializeObject<ClientConfig>(json);

                    // Update the TextBlock with the patch notes.
                    if (!string.IsNullOrWhiteSpace(serverConfig.patchNotes))
                    {
                        txtPatchNotes.Text = serverConfig.patchNotes;
                    }
                    else
                    {
                        txtPatchNotes.Text = "No patch notes available.";
                    }
                }
            }
            catch (TaskCanceledException)
            {
                txtPatchNotes.Text = "Connection timed out while loading patch notes.";
            }
            catch (Exception ex)
            {
                txtPatchNotes.Text = "Error loading patch notes: " + ex.Message;
            }
        }
    }
}

