using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WiFiShow.Fluent
{
    public class WiFiProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Ssid { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string AuthType { get; set; } = string.Empty;
        public bool IsAutoConnect { get; set; }
        public string ConnectionMode { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public DateTime? LastConnectedTime { get; set; }
    }

    public static class WiFiManager
    {
        private const uint WLAN_CLIENT_VERSION_VISTA = 2;
        private const uint WLAN_PROFILE_GET_PLAINTEXT_KEY = 1;
        private const uint ERROR_SUCCESS = 0;

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanOpenHandle(
            uint dwClientVersion,
            IntPtr pReserved,
            out uint pdwNegotiatedVersion,
            out IntPtr phClientHandle);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanCloseHandle(
            IntPtr hClientHandle,
            IntPtr pReserved);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanEnumInterfaces(
            IntPtr hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint WlanGetProfileList(
            IntPtr hClientHandle,
            ref Guid pInterfaceGuid,
            IntPtr pReserved,
            out IntPtr ppProfileList);

        [DllImport("wlanapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint WlanGetProfile(
            IntPtr hClientHandle,
            ref Guid pInterfaceGuid,
            string strProfileName,
            IntPtr pReserved,
            out IntPtr pstrProfileXml,
            ref uint pdwFlags,
            out uint pdwGrantedAccess);

        [DllImport("wlanapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint WlanDeleteProfile(
            IntPtr hClientHandle,
            ref Guid pInterfaceGuid,
            string strProfileName,
            IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public int isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_INTERFACE_INFO_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_PROFILE_INFO
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_PROFILE_INFO_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
        }

        public static async Task<List<WiFiProfile>> GetWiFiProfilesAsync()
        {
            return await Task.Run(() =>
            {
                var profiles = new List<WiFiProfile>();
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    uint status = WlanOpenHandle(WLAN_CLIENT_VERSION_VISTA, IntPtr.Zero, out _, out IntPtr clientHandle);
                    if (status == ERROR_SUCCESS)
                    {
                        try
                        {
                            status = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out IntPtr pInterfaceList);
                            if (status == ERROR_SUCCESS && pInterfaceList != IntPtr.Zero)
                            {
                                try
                                {
                                    var ifListHeader = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(pInterfaceList);
                                    int ifInfoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                                    IntPtr currentIfPtr = new IntPtr(pInterfaceList.ToInt64() + 8);

                                    for (int i = 0; i < ifListHeader.dwNumberOfItems; i++)
                                    {
                                        var ifInfo = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(currentIfPtr);
                                        Guid ifGuid = ifInfo.InterfaceGuid;

                                        uint profileStatus = WlanGetProfileList(clientHandle, ref ifGuid, IntPtr.Zero, out IntPtr pProfileList);
                                        if (profileStatus == ERROR_SUCCESS && pProfileList != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                var profListHeader = Marshal.PtrToStructure<WLAN_PROFILE_INFO_LIST>(pProfileList);
                                                int profInfoSize = Marshal.SizeOf<WLAN_PROFILE_INFO>();
                                                IntPtr currentProfPtr = new IntPtr(pProfileList.ToInt64() + 8);

                                                for (int j = 0; j < profListHeader.dwNumberOfItems; j++)
                                                {
                                                    var profInfo = Marshal.PtrToStructure<WLAN_PROFILE_INFO>(currentProfPtr);
                                                    string profileName = profInfo.strProfileName;

                                                    if (!string.IsNullOrEmpty(profileName) && seenNames.Add(profileName))
                                                    {
                                                        uint flags = WLAN_PROFILE_GET_PLAINTEXT_KEY;
                                                        uint getProfStatus = WlanGetProfile(
                                                            clientHandle,
                                                            ref ifGuid,
                                                            profileName,
                                                            IntPtr.Zero,
                                                            out IntPtr pXml,
                                                            ref flags,
                                                            out _);

                                                        if (getProfStatus == ERROR_SUCCESS && pXml != IntPtr.Zero)
                                                        {
                                                            try
                                                            {
                                                                string? xmlContent = Marshal.PtrToStringUni(pXml);
                                                                if (!string.IsNullOrEmpty(xmlContent))
                                                                {
                                                                    var profile = ParseProfileXml(xmlContent, profileName);
                                                                    if (profile != null)
                                                                    {
                                                                        profiles.Add(profile);
                                                                    }
                                                                }
                                                            }
                                                            finally
                                                            {
                                                                WlanFreeMemory(pXml);
                                                            }
                                                        }
                                                    }

                                                    currentProfPtr = new IntPtr(currentProfPtr.ToInt64() + profInfoSize);
                                                }
                                            }
                                            finally
                                            {
                                                WlanFreeMemory(pProfileList);
                                            }
                                        }

                                        currentIfPtr = new IntPtr(currentIfPtr.ToInt64() + ifInfoSize);
                                    }
                                }
                                finally
                                {
                                    WlanFreeMemory(pInterfaceList);
                                }
                            }
                        }
                        finally
                        {
                            WlanCloseHandle(clientHandle, IntPtr.Zero);
                        }
                    }
                }
                catch
                {
                    // Fallback to netsh if native API fails
                    profiles = GetWiFiProfilesNetshFallback();
                }

                if (profiles.Count == 0)
                {
                    profiles = GetWiFiProfilesNetshFallback();
                }

                EnrichAndSortProfiles(profiles);
                return profiles;
            });
        }

        private static void EnrichAndSortProfiles(List<WiFiProfile> profiles)
        {
            try
            {
                string? connectedProfile = GetCurrentlyConnectedProfileName();
                var lastConnectedMap = GetLastConnectedHistory();

                foreach (var profile in profiles)
                {
                    if (!string.IsNullOrEmpty(connectedProfile) &&
                        (string.Equals(profile.Name, connectedProfile, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(profile.Ssid, connectedProfile, StringComparison.OrdinalIgnoreCase)))
                    {
                        profile.IsConnected = true;
                    }

                    if (lastConnectedMap.TryGetValue(profile.Name, out DateTime lastTime) ||
                        lastConnectedMap.TryGetValue(profile.Ssid, out lastTime))
                    {
                        profile.LastConnectedTime = lastTime;
                    }
                }

                // Sort: Currently connected first, then by LastConnectedTime descending, then alphabetical
                profiles.Sort((a, b) =>
                {
                    if (a.IsConnected != b.IsConnected)
                        return b.IsConnected.CompareTo(a.IsConnected);

                    if (a.LastConnectedTime.HasValue && b.LastConnectedTime.HasValue)
                        return b.LastConnectedTime.Value.CompareTo(a.LastConnectedTime.Value);

                    if (a.LastConnectedTime.HasValue)
                        return -1;

                    if (b.LastConnectedTime.HasValue)
                        return 1;

                    return string.Compare(a.Ssid, b.Ssid, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch { }
        }

        public static string? GetCurrentlyConnectedProfileName()
        {
            try
            {
                string output = RunCommand("netsh", "wlan show interfaces");
                using var reader = new StringReader(output);
                string? line;
                string? state = null;
                string? profile = null;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();
                        if (key.Equals("State", StringComparison.OrdinalIgnoreCase))
                            state = val;
                        else if (key.Equals("Profile", StringComparison.OrdinalIgnoreCase))
                            profile = val;
                    }
                }

                if (string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(profile))
                {
                    return profile;
                }
            }
            catch { }
            return null;
        }

        public static Dictionary<string, DateTime> GetLastConnectedHistory()
        {
            var history = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string query = "*[System[(EventID=8001)]]";
                var eventQuery = new EventLogQuery("Microsoft-Windows-WLAN-AutoConfig/Operational", PathType.LogName, query)
                {
                    ReverseDirection = true
                };

                using var reader = new EventLogReader(eventQuery);
                EventRecord? record;
                int count = 0;
                while ((record = reader.ReadEvent()) != null && count < 500)
                {
                    using (record)
                    {
                        count++;
                        if (record.TimeCreated.HasValue && record.Properties != null && record.Properties.Count > 4)
                        {
                            string? profileName = record.Properties[3]?.Value?.ToString();
                            string? ssid = record.Properties[4]?.Value?.ToString();
                            DateTime time = record.TimeCreated.Value;

                            if (!string.IsNullOrEmpty(profileName) && !history.ContainsKey(profileName))
                            {
                                history[profileName] = time;
                            }
                            if (!string.IsNullOrEmpty(ssid) && !history.ContainsKey(ssid))
                            {
                                history[ssid] = time;
                            }
                        }
                    }
                }
            }
            catch { }
            return history;
        }

        private static WiFiProfile? ParseProfileXml(string xmlContent, string fallbackName)
        {
            try
            {
                var xml = XDocument.Parse(xmlContent);
                XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";

                string name = xml.Root?.Element(ns + "name")?.Value ?? fallbackName;
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

                return new WiFiProfile
                {
                    Name = name,
                    Ssid = ssid,
                    Password = password,
                    AuthType = authType,
                    IsAutoConnect = isAuto,
                    ConnectionMode = connectionMode
                };
            }
            catch
            {
                return null;
            }
        }

        private static List<WiFiProfile> GetWiFiProfilesNetshFallback()
        {
            var profiles = new List<WiFiProfile>();
            string tempFolder = Path.Combine(Path.GetTempPath(), "WiFiShowExport_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tempFolder);
                RunCommand("netsh", $"wlan export profile folder=\"{tempFolder}\" key=clear");

                foreach (var file in Directory.GetFiles(tempFolder, "*.xml"))
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        var profile = ParseProfileXml(content, Path.GetFileNameWithoutExtension(file));
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }

            return profiles;
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
                try
                {
                    uint status = WlanOpenHandle(WLAN_CLIENT_VERSION_VISTA, IntPtr.Zero, out _, out IntPtr clientHandle);
                    if (status == ERROR_SUCCESS)
                    {
                        try
                        {
                            status = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out IntPtr pInterfaceList);
                            if (status == ERROR_SUCCESS && pInterfaceList != IntPtr.Zero)
                            {
                                try
                                {
                                    var ifListHeader = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(pInterfaceList);
                                    int ifInfoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                                    IntPtr currentIfPtr = new IntPtr(pInterfaceList.ToInt64() + 8);

                                    for (int i = 0; i < ifListHeader.dwNumberOfItems; i++)
                                    {
                                        var ifInfo = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(currentIfPtr);
                                        Guid ifGuid = ifInfo.InterfaceGuid;
                                        WlanDeleteProfile(clientHandle, ref ifGuid, profileName, IntPtr.Zero);
                                        currentIfPtr = new IntPtr(currentIfPtr.ToInt64() + ifInfoSize);
                                    }
                                }
                                finally
                                {
                                    WlanFreeMemory(pInterfaceList);
                                }
                            }
                        }
                        finally
                        {
                            WlanCloseHandle(clientHandle, IntPtr.Zero);
                        }
                    }
                }
                catch
                {
                    RunCommand("netsh", $"wlan delete profile name=\"{profileName}\"");
                }
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
