using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace CanaryLauncherUpdate
{
    public static class LauncherUtils
    {
        /// <summary>
        /// Gets the launcher directory. If a client folder is specified and not overridden, returns a combined path.
        /// </summary>
        public static string GetLauncherPath(string clientFolder, bool onlyBaseDirectory = false)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return (string.IsNullOrEmpty(clientFolder) || onlyBaseDirectory)
                ? baseDir
                : System.IO.Path.Combine(baseDir, clientFolder);
        }

        /// <summary>
        /// Reads the client version from launcher_config.json located in the provided launcherPath.
        /// </summary>
        public static string GetClientVersion(string launcherPath)
        {
            string jsonPath = System.IO.Path.Combine(launcherPath, "launcher_config.json");
            if (!File.Exists(jsonPath))
                return "";

            try
            {
                string json = File.ReadAllText(jsonPath);
                dynamic jsonObj = JsonConvert.DeserializeObject(json);
                // Assumes the JSON contains a "clientVersion" property.
                return jsonObj.clientVersion ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Creates a desktop shortcut pointing to the specified executable.
        /// </summary>
        public static void CreateShortcut(string shortcutName, string executablePath)
        {
            try
            {
                // Get the desktop folder path.
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                // Build the full shortcut path using the provided shortcut name.
                string shortcutPath = System.IO.Path.Combine(desktopPath, shortcutName + ".lnk");
                // Use WScript.Shell to create the shortcut.
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var lnk = shell.CreateShortcut(shortcutPath);
                lnk.TargetPath = executablePath;
                lnk.Description = shortcutName;
                lnk.Save();
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
            }
            catch (Exception ex)
            {
                // Log or handle error as needed.
                Console.WriteLine("Error creating shortcut: " + ex.Message);
            }
        }

        public static void CreateLauncherShortcut(string shortcutName)
        {
            try
            {
                // Get the path to the desktop.
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                // Build the full shortcut path using the provided shortcut name.
                string shortcutPath = Path.Combine(desktopPath, shortcutName + ".lnk");
                // Create a WScript.Shell instance to create the shortcut.
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                // Create the shortcut.
                var lnk = shell.CreateShortcut(shortcutPath);
                // Set the target to the launcher executable (the running application).
                lnk.TargetPath = Assembly.GetEntryAssembly().Location;
                lnk.Description = "Shortcut for the Culling Launcher";
                lnk.Save();
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
            }
            catch (Exception ex)
            {
                // Handle any errors as needed.
                Console.WriteLine("Error creating launcher shortcut: " + ex.Message);
            }
        }
    }
}
