# Wi-Fi Manager Commands

The application uses the built-in Windows `netsh` (Network Shell) utility to interact with the system's Wi-Fi profiles. Below is a list of all the terminal commands executed behind each UI action.

## 1. Refreshing / Loading Networks
**UI Action**: App startup or clicking the **"🔄 Refresh"** button.
**Command**:
```powershell
netsh wlan show profiles
```
*Purpose*: Retrieves a list of all saved Wi-Fi network profiles (SSIDs) on the current system.

## 2. Retrieving Passwords
**UI Action**: Happens automatically in the background for each loaded network to populate the password field (whether it's masked or unmasked).
**Command**:
```powershell
netsh wlan show profile name="{profile_name}" key=clear
```
*Purpose*: Extracts the clear-text password from the "Key Content" field in the output. This command is also parsed to determine if the network is an "Open Network" or if the password "Requires Admin Rights".

## 3. Viewing All Details (ℹ️)
**UI Action**: Clicking the **ℹ️** (Details) button on a network row.
**Command**:
```powershell
netsh wlan show profile name="{profile_name}" key=clear
```
*Purpose*: Fetches the complete, raw profile configuration (including Authentication type, Cipher, Radio type, MAC randomization, etc.) and displays the full text in a new window.

## 4. Toggling Auto-Connect (⚡)
**UI Action**: Clicking the **⚡** (Auto-Connect) button and selecting "Enable Auto-Connect" or "Disable Auto-Connect".
**Command**:
```powershell
# To Enable Auto-Connect:
netsh wlan set profileparameter name="{profile_name}" connectionmode=auto

# To Disable Auto-Connect (Manual Mode):
netsh wlan set profileparameter name="{profile_name}" connectionmode=manual
```
*Purpose*: Modifies the Windows connection behavior for the specified Wi-Fi profile.

## 5. Forgetting a Network (🗑️)
**UI Action**: Clicking the **🗑️** (Trash) button and confirming the prompt.
**Command**:
```powershell
netsh wlan delete profile name="{profile_name}"
```
*Purpose*: Completely removes the saved Wi-Fi profile, its password, and settings from Windows.
