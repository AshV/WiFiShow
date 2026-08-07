Add-Type -AssemblyName System.Drawing
$src = "$PWD\appIcon.png"
$img = [System.Drawing.Image]::FromFile($src)
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($img, 0, 0, 256, 256)
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $ms.ToArray()
$g.Dispose()
$bmp.Dispose()
$img.Dispose()

$fs = [System.IO.File]::Create("$PWD\appIcon.ico")
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([int16]0) # reserved
$bw.Write([int16]1) # type 1
$bw.Write([int16]1) # count 1
$bw.Write([byte]0) # width 256 -> 0
$bw.Write([byte]0) # height 256 -> 0
$bw.Write([byte]0) # color count
$bw.Write([byte]0) # reserved
$bw.Write([int16]1) # planes
$bw.Write([int16]32) # bpp
$bw.Write([int]$pngBytes.Length) # size
$bw.Write([int]22) # offset
$bw.Write($pngBytes)
$bw.Close()
Write-Host "Created appIcon.ico"
