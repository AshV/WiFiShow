# Wi-Fi Show Commands

The application uses the built-in Windows `netsh` (Network Shell) utility to interact with the system's Wi-Fi profiles. Below is a list of all the terminal commands executed behind each UI action.

## 1. Refreshing / Loading Networks
**UI Action**: App startup or clicking the **"Refresh"** button.
**Command**:
```powershell
netsh wlan export profile folder="[temp_folder]" key=clear
```
*Purpose*: Extracts all saved Wi-Fi network profiles (SSIDs) on the current system into XML files simultaneously. The app then parses these XML files using .NET's LINQ-to-XML API to gather names, connection modes, auth types, and clear-text passwords. This is massively faster than running individual `netsh show` commands sequentially.

## 2. Viewing All Details (ℹ️)
**UI Action**: Clicking the **ℹ️** (Details) button on a network card.
**Command**:
```powershell
netsh wlan show profile name="{profile_name}" key=clear
```
*Purpose*: Fetches the complete, raw profile configuration (including Authentication type, Cipher, Radio type, MAC randomization, etc.) and displays the full text in a new modal window.

## 3. Toggling Auto-Connect
**UI Action**: Toggling the Auto-Connect slider on a network card.
**Command**:
```powershell
# To Enable Auto-Connect:
netsh wlan set profileparameter name="{profile_name}" connectionmode=auto

# To Disable Auto-Connect (Manual Mode):
netsh wlan set profileparameter name="{profile_name}" connectionmode=manual
```
*Purpose*: Modifies the Windows connection behavior for the specified Wi-Fi profile.

## 4. Forgetting a Network (🗑️)
**UI Action**: Clicking the **🗑️** (Trash) button and confirming the prompt.
**Command**:
```powershell
netsh wlan delete profile name="{profile_name}"
```
*Purpose*: Completely removes the saved Wi-Fi profile, its password, and settings from Windows.
