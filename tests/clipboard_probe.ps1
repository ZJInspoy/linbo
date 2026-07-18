param(
    [string]$Root
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Split-Path -Parent $PSScriptRoot }
$env:LINBO_DATA_DIR = Join-Path $Root "artifacts\test-data\clipboard"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$bmp = New-Object System.Drawing.Bitmap 40, 24
for ($x = 0; $x -lt 40; $x++) {
    for ($y = 0; $y -lt 24; $y++) {
        if ($x -lt 20) {
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::Red)
        } else {
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::DodgerBlue)
        }
    }
}

[System.Windows.Forms.Clipboard]::SetImage($bmp)

$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path (Join-Path $Root "artifacts\Linbo.exe")))
$type = $asm.GetType("LinboNative.LinboWindow")
$win = [Activator]::CreateInstance($type)
$method = $type.GetMethod("GetClipboardBitmap", [Reflection.BindingFlags]"Instance,NonPublic")
$img = $method.Invoke($win, @())

$stride = $img.PixelWidth * 4
$pixels = New-Object byte[] ($stride * $img.PixelHeight)
$img.CopyPixels($pixels, $stride, 0)

$leftIndex = (12 * $stride) + (10 * 4)
$rightIndex = (12 * $stride) + (30 * 4)
$left = "{0},{1},{2},{3}" -f $pixels[$leftIndex + 2], $pixels[$leftIndex + 1], $pixels[$leftIndex], $pixels[$leftIndex + 3]
$right = "{0},{1},{2},{3}" -f $pixels[$rightIndex + 2], $pixels[$rightIndex + 1], $pixels[$rightIndex], $pixels[$rightIndex + 3]

$win.Close()

$tempPng = Join-Path $env:LINBO_DATA_DIR "clipboard-filedrop.png"
New-Item -ItemType Directory -Path $env:LINBO_DATA_DIR -Force | Out-Null
$bmp.Save($tempPng, [System.Drawing.Imaging.ImageFormat]::Png)
$data = New-Object System.Windows.Forms.DataObject
$data.SetData([System.Windows.Forms.DataFormats]::FileDrop, [string[]]@($tempPng))
[System.Windows.Forms.Clipboard]::SetDataObject($data, $true)

$win2 = [Activator]::CreateInstance($type)
$img2 = $method.Invoke($win2, @())
$win2.Close()

[PSCustomObject]@{
    Width = $img.PixelWidth
    Height = $img.PixelHeight
    LeftRGBA = $left
    RightRGBA = $right
    FileDropWidth = if ($img2) { $img2.PixelWidth } else { 0 }
    FileDropHeight = if ($img2) { $img2.PixelHeight } else { 0 }
}
