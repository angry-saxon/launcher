using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using LauncherConfig;

namespace CanaryLauncherUpdate
{
    public partial class SplashScreen : Window
    {
        private const string LauncherConfigUrl = "http://51.81.154.175/updates/launcher_config.json";
        private ClientConfig clientConfig;
        private string clientExecutableName;
        private string urlClient;
        private HttpClient httpClient;

        public SplashScreen()
        {
            InitializeComponent();
            // Increase the timeout to 20 seconds; adjust as needed.
            httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            this.Loaded += SplashScreen_Loaded;
        }

        private async void SplashScreen_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Content = "Loading configuration...";
                // Load configuration asynchronously.
                clientConfig = await ClientConfig.LoadFromFileAsync(LauncherConfigUrl);
                clientExecutableName = clientConfig.clientExecutable;
                urlClient = clientConfig.newClientUrl;
                txtStatus.Content = "Configuration loaded.";

                string launcherBasePath = LauncherUtils.GetLauncherPath(clientConfig.clientFolder, true);
                string launcherConfigPath = System.IO.Path.Combine(launcherBasePath, "launcher_config.json");

                txtStatus.Content = "Checking client version...";
                // Check if the client is up-to-date.
                bool isUpToDate = false;
                if (File.Exists(launcherConfigPath))
                {
                    string actualVersion = LauncherUtils.GetClientVersion(launcherBasePath);
                    if (clientConfig.clientVersion == actualVersion &&
                        Directory.Exists(LauncherUtils.GetLauncherPath(clientConfig.clientFolder)))
                    {
                        isUpToDate = true;
                    }
                }

                if (isUpToDate)
                {
                    txtStatus.Content = "Client is up-to-date. Launching client...";
                    StartClient();
                }
                else
                {
                    txtStatus.Content = "Client not up-to-date. Checking server connectivity...";
                    await CheckServerAndLaunchMainWindow();
                }
            }
            catch (Exception ex)
            {
                txtStatus.Content = "Error during startup: " + ex.Message;
                MessageBox.Show("Error during startup: " + ex.Message);
                this.Close();
            }
        }

        private async Task CheckServerAndLaunchMainWindow()
        {
            try
            {
                txtStatus.Content = "Connecting to server...";
                // Use a lightweight GET request to check the server response.
                HttpResponseMessage response = await httpClient.GetAsync(urlClient);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    txtStatus.Content = "Server not found. Launching in offline mode...";
                    MessageBox.Show("Server not found. Launching in offline mode.");
                    LaunchMainWindowOffline();
                    return;
                }
                txtStatus.Content = "Server responsive. Launching main window...";
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            catch (TaskCanceledException)
            {
                // The request timed out.
                txtStatus.Content = "Connection timed out. Launching in offline mode...";
                MessageBox.Show("Connection timed out. Launching in offline mode.");
                LaunchMainWindowOffline();
            }
            catch (Exception ex)
            {
                txtStatus.Content = "Error checking server: " + ex.Message + ". Launching in offline mode...";
                MessageBox.Show("Error checking server: " + ex.Message + ". Launching in offline mode.");
                LaunchMainWindowOffline();
            }
        }

        private void LaunchMainWindowOffline()
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        private void StartClient()
        {
            txtStatus.Content = "Starting client...";
            string launcherPath = LauncherUtils.GetLauncherPath(clientConfig.clientFolder, true);
            string exePath = System.IO.Path.Combine(launcherPath, "", clientExecutableName);
            try
            {
                Process.Start(exePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting client: " + ex.Message);
            }
            finally
            {
                this.Close();
            }
        }
    }
}
