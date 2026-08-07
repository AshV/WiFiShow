@echo off
echo Building Neutralino application as single files...
call npx @neutralinojs/neu build --embed-resources

echo Moving executables to Output folder...

set "distPath=dist\wifi-show-ashv"
set "outPath=Output"

if not exist "%outPath%" mkdir "%outPath%"

:: Windows
if exist "%distPath%\wifi-show-ashv-win_x64.exe" (
    move /Y "%distPath%\wifi-show-ashv-win_x64.exe" "%outPath%\WiFiShow.exe" >nul
    echo Created Output\WiFiShow.exe
)

:: Mac
if exist "%distPath%\wifi-show-ashv-mac_universal" (
    move /Y "%distPath%\wifi-show-ashv-mac_universal" "%outPath%\WiFiShow-Mac" >nul
    echo Created Output\WiFiShow-Mac
)

echo Build complete! Single file executables are in the Output folder.
