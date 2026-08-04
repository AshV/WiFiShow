Write-Host "Building Neutralino application..."
npx @neutralinojs/neu build

Write-Host "Renaming executables to remove architecture suffixes..."

$distPath = "dist\wifi-show"

# Windows
if (Test-Path "$distPath\wifi-show-win_x64.exe") {
    Rename-Item -Path "$distPath\wifi-show-win_x64.exe" -NewName "WiFiShow.exe" -Force
}

# Mac
if (Test-Path "$distPath\wifi-show-mac_universal") {
    Rename-Item -Path "$distPath\wifi-show-mac_universal" -NewName "WiFiShow-Mac" -Force
}

Write-Host "Build and rename complete! Your clean executables are in the dist\wifi-show folder."
