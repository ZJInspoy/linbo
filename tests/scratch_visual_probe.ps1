param(
    [string]$Root
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Split-Path -Parent $PSScriptRoot }
$env:LINBO_DATA_DIR = Join-Path $Root "artifacts\test-data\scratch-visual"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path (Join-Path $Root "artifacts\Linbo.exe")))
$winType = $asm.GetType("LinboNative.LinboWindow")
$itemType = $asm.GetType("LinboNative.ScratchItem")
$controlType = $asm.GetType("LinboNative.ScratchItemControl")

$bmp = New-Object System.Drawing.Bitmap 40, 24
for ($x = 0; $x -lt 40; $x++) {
    for ($y = 0; $y -lt 24; $y++) {
        $bmp.SetPixel($x, $y, [System.Drawing.Color]::DodgerBlue)
    }
}
$stream = New-Object IO.MemoryStream
$bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$stream.Position = 0
$decoder = [System.Windows.Media.Imaging.PngBitmapDecoder]::new($stream, [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$image = $decoder.Frames[0]

$win = [Activator]::CreateInstance($winType)
$item = [Activator]::CreateInstance($itemType)
$item.id = "probe"
$item.kind = "image"
$item.x = 0
$item.y = 0
$item.w = 100
$item.h = 60
$item.aspect = 1.666
$item.createdAt = 1
$item.image = $image

$control = [Activator]::CreateInstance($controlType, @($win, $item))
$imageField = $controlType.GetField("imageElement", [Reflection.BindingFlags]"Instance,NonPublic")
$inkField = $controlType.GetField("ink", [Reflection.BindingFlags]"Instance,NonPublic")

$item.w = 180
$item.h = 108
$control.UpdateSize()
$imageElement = $imageField.GetValue($control)

$control.SetDrawing($true, $false)
$ink = $inkField.GetValue($control)
$inkMode = $ink.EditingMode.ToString()
$hit = $ink.IsHitTestVisible

$control.SetDrawing($true, $true)
$eraseMode = $ink.EditingMode.ToString()

$control.SetDrawingAttributes([System.Windows.Media.Colors]::Red, 8)
$startField = $controlType.GetField("straightLineStart", [Reflection.BindingFlags]"Instance,NonPublic")
$lineMethod = $controlType.GetMethod("CreateStraightLineStroke", [Reflection.BindingFlags]"Instance,NonPublic")
$startField.SetValue($control, [System.Windows.Point]::new(4, 5))
$line = $lineMethod.Invoke($control, @([System.Windows.Point]::new(40, 30)))

$win.Close()
[PSCustomObject]@{
    ImageWidth = $imageElement.Width
    ImageHeight = $imageElement.Height
    InkMode = $inkMode
    InkHitTest = $hit
    EraseMode = $eraseMode
    LinePoints = $line.StylusPoints.Count
    LineWidth = $line.DrawingAttributes.Width
}
