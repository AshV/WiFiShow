using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WiFiShow.WPF
{
    public class WiFiProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Ssid { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string AuthType { get; set; } = string.Empty;
        public bool IsAutoConnect { get; set; }
        public string ConnectionMode { get; set; } = string.Empty;
    }

    public static class WiFiManager
    {
        public static async Task<List<WiFiProfile>> GetWiFiProfilesAsync()
        {
            return await Task.Run(() =>
            {
                var profiles = new List<WiFiProfile>();
                string tempFolder = Path.Combine(Path.GetTempPath(), "WiFiShowExport");

                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }
                else
                {
                    // Clean up old files
                    foreach (var file in Directory.GetFiles(tempFolder))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }

                // Export profiles
                RunCommand("netsh", $"wlan export profile folder=\"{tempFolder}\" key=clear");

                // Parse XML files
                foreach (var file in Directory.GetFiles(tempFolder, "*.xml"))
                {
                    try
                    {
                        var xml = XDocument.Load(file);
                        XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";

                        string name = xml.Root?.Element(ns + "name")?.Value ?? string.Empty;
                        string ssid = xml.Root?.Element(ns + "SSIDConfig")?.Element(ns + "SSID")?.Element(ns + "name")?.Value ?? name;
                        string connectionMode = xml.Root?.Element(ns + "connectionMode")?.Value ?? "manual";
                        bool isAuto = connectionMode.Equals("auto", StringComparison.OrdinalIgnoreCase);
                        
                        var security = xml.Root?.Element(ns + "MSM")?.Element(ns + "security");
                        string authType = security?.Element(ns + "authEncryption")?.Element(ns + "authentication")?.Value ?? string.Empty;
                        
                        string password = string.Empty;
                        var sharedKey = security?.Element(ns + "sharedKey");
                        if (sharedKey != null)
                        {
                            password = sharedKey.Element(ns + "keyMaterial")?.Value ?? string.Empty;
                        }

                        profiles.Add(new WiFiProfile
                        {
                            Name = name,
                            Ssid = ssid,
                            Password = password,
                            AuthType = authType,
                            IsAutoConnect = isAuto,
                            ConnectionMode = connectionMode
                        });
                    }
                    catch
                    {
                        // Ignore parsing errors for individual files
                    }
                }

                // Clean up
                try { Directory.Delete(tempFolder, true); } catch { }

                return profiles;
            });
        }

        public static async Task ToggleAutoConnectAsync(string profileName, bool autoConnect)
        {
            await Task.Run(() =>
            {
                string mode = autoConnect ? "auto" : "manual";
                RunCommand("netsh", $"wlan set profileparameter name=\"{profileName}\" connectionmode={mode}");
            });
        }

        public static async Task DeleteProfileAsync(string profileName)
        {
            await Task.Run(() =>
            {
                RunCommand("netsh", $"wlan delete profile name=\"{profileName}\"");
            });
        }

        public static async Task<string> GetProfileDetailsAsync(string profileName)
        {
            return await Task.Run(() =>
            {
                return RunCommand("netsh", $"wlan show profile name=\"{profileName}\" key=clear");
            });
        }

        private static string RunCommand(string fileName, string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
