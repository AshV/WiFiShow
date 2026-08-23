using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
        public bool IsAvailable { get; set; }
        public bool IsSaved { get; set; } = true;
        public int? SignalQuality { get; set; }
        public string Band { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string RadioType { get; set; } = string.Empty;
        public DateTime? LastConnectedTime { get; set; }
    }

    public static class WiFiManager
    {
        private const uint WLAN_CLIENT_VERSION_VISTA = 2;
        private const uint WLAN_PROFILE_GET_PLAINTEXT_KEY = 4;
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

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanScan(
            IntPtr hClientHandle,
            ref Guid pInterfaceGuid,
            IntPtr pDot11Ssid,
            IntPtr pIeData,
            IntPtr pReserved);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern uint WlanGetAvailableNetworkList(
            IntPtr hClientHandle,
            ref Guid pInterfaceGuid,
            uint dwFlags,
            IntPtr pReserved,
            out IntPtr ppAvailableNetworkList);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_AVAILABLE_NETWORK
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public DOT11_SSID dot11Ssid;
            public uint dot11BssType;
            public uint uNumberOfBssids;
            public bool bNetworkConnectable;
            public uint wlanNotConnectableReason;
            public uint uNumberOfPhyTypes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] dot11PhyTypes;
            public bool bMorePhyTypes;
            public uint wlanSignalQuality;
            public bool bSecurityEnabled;
            public uint dot11DefaultAuthAlgorithm;
            public uint dot11DefaultCipherAlgorithm;
            public uint dwFlags;
            public uint dwReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_AVAILABLE_NETWORK_LIST
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

        public class VisibleNetworkInfo
        {
            public string Ssid { get; set; } = string.Empty;
            public string AuthType { get; set; } = string.Empty;
            public string Encryption { get; set; } = string.Empty;
            public int SignalQuality { get; set; }
            public string RadioType { get; set; } = string.Empty;
            public string Band { get; set; } = string.Empty;
            public string Channel { get; set; } = string.Empty;
            public string Bssid { get; set; } = string.Empty;
        }

        private static string MapAuthAlgorithm(uint authAlgo)
        {
            return authAlgo switch
            {
                1 => "Open",
                2 => "WEP",
                3 => "None",
                4 => "WPA2-Enterprise",
                5 => "WPA2-Personal",
                6 => "WPA-Enterprise",
                7 => "WPA-Personal",
                9 => "WPA3-Personal",
                10 => "OWE",
                _ => "WPA2-Personal"
            };
        }

        public static Dictionary<string, VisibleNetworkInfo> GetVisibleNetworks()
        {
            var networks = new Dictionary<string, VisibleNetworkInfo>(StringComparer.OrdinalIgnoreCase);

            // 1. Query Native Win32 WLAN API directly with WlanScan and WlanGetAvailableNetworkList
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

                                    // Request active probe scan
                                    WlanScan(clientHandle, ref ifGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                                    // Query available networks (flags 0x3: include adhoc + hidden)
                                    uint availStatus = WlanGetAvailableNetworkList(clientHandle, ref ifGuid, 0x3, IntPtr.Zero, out IntPtr pAvailableList);
                                    if (availStatus == ERROR_SUCCESS && pAvailableList != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            var availHeader = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK_LIST>(pAvailableList);
                                            int netSize = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
                                            IntPtr currentNetPtr = new IntPtr(pAvailableList.ToInt64() + 8);

                                            for (int j = 0; j < availHeader.dwNumberOfItems; j++)
                                            {
                                                var net = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(currentNetPtr);
                                                string ssid = string.Empty;
                                                if (net.dot11Ssid.uSSIDLength > 0 && net.dot11Ssid.ucSSID != null)
                                                {
                                                    ssid = Encoding.UTF8.GetString(net.dot11Ssid.ucSSID, 0, (int)Math.Min(net.dot11Ssid.uSSIDLength, 32));
                                                }

                                                if (!string.IsNullOrEmpty(ssid))
                                                {
                                                    if (!networks.TryGetValue(ssid, out var existing))
                                                    {
                                                        existing = new VisibleNetworkInfo
                                                        {
                                                            Ssid = ssid,
                                                            SignalQuality = (int)net.wlanSignalQuality,
                                                            AuthType = MapAuthAlgorithm(net.dot11DefaultAuthAlgorithm)
                                                        };
                                                        networks[ssid] = existing;
                                                    }
                                                    else
                                                    {
                                                        if ((int)net.wlanSignalQuality > existing.SignalQuality)
                                                        {
                                                            existing.SignalQuality = (int)net.wlanSignalQuality;
                                                        }
                                                        if (string.IsNullOrEmpty(existing.AuthType))
                                                        {
                                                            existing.AuthType = MapAuthAlgorithm(net.dot11DefaultAuthAlgorithm);
                                                        }
                                                    }
                                                }

                                                currentNetPtr = new IntPtr(currentNetPtr.ToInt64() + netSize);
                                            }
                                        }
                                        finally
                                        {
                                            WlanFreeMemory(pAvailableList);
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
            catch { }

            // 2. Also parse netsh wlan show networks mode=bssid to enrich band, channel, BSSID and additional networks
            try
            {
                string output = RunCommand("netsh", "wlan show networks mode=bssid");
                using var reader = new StringReader(output);
                string? line;
                VisibleNetworkInfo? currentNetwork = null;

                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmed.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            string ssid = parts[1].Trim();
                            if (!string.IsNullOrEmpty(ssid))
                            {
                                if (!networks.TryGetValue(ssid, out currentNetwork))
                                {
                                    currentNetwork = new VisibleNetworkInfo { Ssid = ssid };
                                    networks[ssid] = currentNetwork;
                                }
                            }
                            else
                            {
                                currentNetwork = null;
                            }
                        }
                    }
                    else if (currentNetwork != null)
                    {
                        var parts = trimmed.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string val = parts[1].Trim();

                            if (key.Equals("Authentication", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(currentNetwork.AuthType))
                                    currentNetwork.AuthType = val;
                            }
                            else if (key.Equals("Encryption", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(currentNetwork.Encryption))
                                    currentNetwork.Encryption = val;
                            }
                            else if (key.Equals("Radio type", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(currentNetwork.RadioType))
                                    currentNetwork.RadioType = val;
                            }
                            else if (key.Equals("Band", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(currentNetwork.Band))
                                    currentNetwork.Band = val;
                            }
                            else if (key.Equals("Channel", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(currentNetwork.Channel))
                                    currentNetwork.Channel = val;
                            }
                            else if (key.Equals("BSSID 1", StringComparison.OrdinalIgnoreCase) || (key.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(currentNetwork.Bssid)))
                            {
                                currentNetwork.Bssid = val;
                            }
                            else if (key.Equals("Signal", StringComparison.OrdinalIgnoreCase))
                            {
                                string sigStr = val.Replace("%", "").Trim();
                                if (int.TryParse(sigStr, out int sigVal))
                                {
                                    if (sigVal > currentNetwork.SignalQuality)
                                    {
                                        currentNetwork.SignalQuality = sigVal;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return networks;
        }

        public class ConnectedInfo
        {
            public string Profile { get; set; } = string.Empty;
            public string Ssid { get; set; } = string.Empty;
            public int SignalQuality { get; set; }
        }

        public static ConnectedInfo? GetCurrentlyConnectedInfo()
        {
            try
            {
                string output = RunCommand("netsh", "wlan show interfaces");
                using var reader = new StringReader(output);
                string? line;
                string? state = null;
                string? profile = null;
                string? ssid = null;
                int signal = 0;
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
                        else if (key.Equals("SSID", StringComparison.OrdinalIgnoreCase))
                            ssid = val;
                        else if (key.Equals("Signal", StringComparison.OrdinalIgnoreCase))
                        {
                            string sigStr = val.Replace("%", "").Trim();
                            if (int.TryParse(sigStr, out int sigVal))
                                signal = sigVal;
                        }
                    }
                }

                if (string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrEmpty(profile) || !string.IsNullOrEmpty(ssid)))
                {
                    return new ConnectedInfo
                    {
                        Profile = profile ?? string.Empty,
                        Ssid = ssid ?? string.Empty,
                        SignalQuality = signal
                    };
                }
            }
            catch { }
            return null;
        }

        private static void EnrichAndSortProfiles(List<WiFiProfile> profiles)
        {
            try
            {
                var connInfo = GetCurrentlyConnectedInfo();
                var visibleNetworks = GetVisibleNetworks();
                var lastConnectedMap = GetLastConnectedHistory();

                var matchedVisibleSsids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var profile in profiles)
                {
                    profile.IsSaved = true;

                    if (connInfo != null &&
                        ((!string.IsNullOrEmpty(connInfo.Profile) && string.Equals(profile.Name, connInfo.Profile, StringComparison.OrdinalIgnoreCase)) ||
                         (!string.IsNullOrEmpty(connInfo.Ssid) && string.Equals(profile.Ssid, connInfo.Ssid, StringComparison.OrdinalIgnoreCase))))
                    {
                        profile.IsConnected = true;
                        profile.IsAvailable = true;
                        if (connInfo.SignalQuality > 0)
                        {
                            profile.SignalQuality = connInfo.SignalQuality;
                        }
                    }

                    if (visibleNetworks.TryGetValue(profile.Ssid, out var vis) ||
                        visibleNetworks.TryGetValue(profile.Name, out vis))
                    {
                        profile.IsAvailable = true;
                        matchedVisibleSsids.Add(vis.Ssid);
                        if (vis.SignalQuality > 0 || !profile.SignalQuality.HasValue)
                        {
                            profile.SignalQuality = vis.SignalQuality;
                        }
                        if (string.IsNullOrEmpty(profile.Band))
                            profile.Band = vis.Band;
                        if (string.IsNullOrEmpty(profile.Channel))
                            profile.Channel = vis.Channel;
                        if (string.IsNullOrEmpty(profile.RadioType))
                            profile.RadioType = vis.RadioType;
                        if (string.IsNullOrEmpty(profile.AuthType) && !string.IsNullOrEmpty(vis.AuthType))
                            profile.AuthType = vis.AuthType;
                    }

                    if (lastConnectedMap.TryGetValue(profile.Name, out DateTime lastTime) ||
                        lastConnectedMap.TryGetValue(profile.Ssid, out lastTime))
                    {
                        profile.LastConnectedTime = lastTime;
                    }
                }

                // Add any nearby broadcasting SSIDs that are NOT saved
                foreach (var kvp in visibleNetworks)
                {
                    var vis = kvp.Value;
                    if (!string.IsNullOrEmpty(vis.Ssid) && !matchedVisibleSsids.Contains(vis.Ssid))
                    {
                        profiles.Add(new WiFiProfile
                        {
                            Name = vis.Ssid,
                            Ssid = vis.Ssid,
                            Password = string.Empty,
                            AuthType = !string.IsNullOrEmpty(vis.AuthType) ? vis.AuthType : "Unknown",
                            IsSaved = false,
                            IsAvailable = true,
                            IsConnected = false,
                            SignalQuality = vis.SignalQuality,
                            Band = vis.Band,
                            Channel = vis.Channel,
                            RadioType = vis.RadioType
                        });
                    }
                }

                // Sort: 
                // 1. Currently Connected on top
                // 2. In Range Saved networks (by signal quality descending)
                // 3. In Range Unsaved / Nearby networks (by signal quality descending)
                // 4. Out-of-range Saved networks (by LastConnectedTime descending, then alphabetical)
                profiles.Sort((a, b) =>
                {
                    if (a.IsConnected != b.IsConnected)
                        return b.IsConnected.CompareTo(a.IsConnected);

                    if (a.IsAvailable != b.IsAvailable)
                        return b.IsAvailable.CompareTo(a.IsAvailable);

                    if (a.IsAvailable && b.IsAvailable)
                    {
                        if (a.IsSaved != b.IsSaved)
                            return b.IsSaved.CompareTo(a.IsSaved); // Saved in-range first

                        int aSig = a.SignalQuality ?? 0;
                        int bSig = b.SignalQuality ?? 0;
                        if (aSig != bSig)
                            return bSig.CompareTo(aSig); // Higher signal first
                    }

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
            return GetCurrentlyConnectedInfo()?.Profile;
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
                    bool isProtected = string.Equals(sharedKey.Element(ns + "protected")?.Value, "true", StringComparison.OrdinalIgnoreCase);
                    password = sharedKey.Element(ns + "keyMaterial")?.Value ?? string.Empty;

                    if (isProtected || password.StartsWith("01000000D08C9DDF", StringComparison.OrdinalIgnoreCase) || password.Length > 64)
                    {
                        password = GetClearTextPasswordViaNetsh(name);
                    }
                }
                else if (string.IsNullOrEmpty(password) && !string.Equals(authType, "open", StringComparison.OrdinalIgnoreCase))
                {
                    password = GetClearTextPasswordViaNetsh(name);
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

        private static string GetClearTextPasswordViaNetsh(string profileName)
        {
            try
            {
                string output = RunCommand("netsh", $"wlan show profile name=\"{profileName}\" key=clear");
                using var reader = new StringReader(output);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("Key Content", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmed.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
            catch { }
            return string.Empty;
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
