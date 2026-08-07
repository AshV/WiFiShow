$ErrorActionPreference = "Stop"

# Create Output dir
if (!(Test-Path "Output")) { New-Item -ItemType Directory "Output" | Out-Null }

# Recreate MsixPackage dir
if (Test-Path "MsixPackage") { Remove-Item -Recurse -Force "MsixPackage" }
New-Item -ItemType Directory "MsixPackage\Assets" | Out-Null

# Copy binaries
Copy-Item "bin\Release\net481\*" "MsixPackage\" -Recurse -Force

# Generate Images
Add-Type -AssemblyName System.Drawing

Function Generate-Icon($src, $dst, $w, $h) {
    $img = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    
    # Calculate aspect ratio preserving center
    $scale = [math]::Min($w / $img.Width, $h / $img.Height)
    $newW = [int]($img.Width * $scale)
    $newH = [int]($img.Height * $scale)
    $x = ($w - $newW) / 2
    $y = ($h - $newH) / 2
    
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($img, $x, $y, $newW, $newH)
    
    $bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    $img.Dispose()
}

$iconSrc = "$PWD\appIcon.png"
Generate-Icon $iconSrc "$PWD\MsixPackage\Assets\Square150x150Logo.png" 150 150
Generate-Icon $iconSrc "$PWD\MsixPackage\Assets\Square44x44Logo.png" 44 44
Generate-Icon $iconSrc "$PWD\MsixPackage\Assets\StoreLogo.png" 50 50
Generate-Icon $iconSrc "$PWD\MsixPackage\Assets\Wide310x150Logo.png" 310 150

# Create AppxManifest.xml
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  
  <Identity Name="AshishVishwakarma.WiFiShow"
    Publisher="CN=Ashish Vishwakarma"
    Version="2.1.0.0" 
    ProcessorArchitecture="x64" />
    
  <Properties>
    <DisplayName>Wi-Fi Show</DisplayName>
    <PublisherDisplayName>Ashish Vishwakarma</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Resources>
    <Resource Language="en-us" />
  </Resources>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.14393.0" MaxVersionTested="10.0.19041.0" />
  </Dependencies>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>

  <Applications>
    <Application Id="WiFiShow"
      Executable="WiFiShow.Fluent.exe"
      EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="Wi-Fi Show"
        Description="Manage your Wi-Fi profiles and passwords"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
</Package>
"@

Set-Content -Path "MsixPackage\AppxManifest.xml" -Value $manifest -Encoding UTF8

# Pack MSIX
$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
& $makeappx pack /v /d "$PWD\MsixPackage" /p "$PWD\Output\WiFiShow_2.1.msix" /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }

Write-Host "MSIX generation successful!"
