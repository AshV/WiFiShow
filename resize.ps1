Add-Type -AssemblyName System.Drawing
function Resize-Image {
    param([string]$in, [string]$out, [int]$w, [int]$h, [switch]$center)
    $img = [System.Drawing.Image]::FromFile($in)
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    
    if ($center) {
        # Keep aspect ratio and center
        $ratio = [Math]::Min($w / $img.Width, $h / $img.Height)
        $newW = [int]($img.Width * $ratio)
        $newH = [int]($img.Height * $ratio)
        $x = ($w - $newW) / 2
        $y = ($h - $newH) / 2
        $g.DrawImage($img, $x, $y, $newW, $newH)
    } else {
        $g.DrawImage($img, 0, 0, $w, $h)
    }
    
    $g.Dispose()
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $img.Dispose()
}

$icon = "a:\GitHub\WiFiShow\wifi-manager-neu\resources\icons\appIcon.png"
$outDir = "a:\GitHub\WiFiShow\package_staging\Assets"

Resize-Image -in $icon -out "$outDir\Square150x150Logo.png" -w 150 -h 150
Resize-Image -in $icon -out "$outDir\Square44x44Logo.png" -w 44 -h 44
Resize-Image -in $icon -out "$outDir\StoreLogo.png" -w 50 -h 50
Resize-Image -in $icon -out "$outDir\SplashScreen.png" -w 620 -h 300 -center
