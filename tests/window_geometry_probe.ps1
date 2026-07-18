param(
    [string]$Root
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Split-Path -Parent $PSScriptRoot }
$env:LINBO_DATA_DIR = Join-Path $Root "artifacts\test-data\window-geometry"
if (Test-Path -LiteralPath $env:LINBO_DATA_DIR) { Remove-Item -LiteralPath $env:LINBO_DATA_DIR -Recurse -Force }

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path (Join-Path $Root "artifacts\Linbo.exe")))
$winType = $asm.GetType("LinboNative.LinboWindow")
$win = [Activator]::CreateInstance($winType)
$flags = [Reflection.BindingFlags]"Instance,NonPublic"

$mainField = $winType.GetField("mainPaneWidth", $flags)
$scratchField = $winType.GetField("scratchPaneWidth", $flags)
$modeField = $winType.GetField("windowMode", $flags)
$stateField = $winType.GetField("state", $flags)

$toggleScratch = $winType.GetMethod("ToggleScratchCanvas", $flags)
$cycleMode = $winType.GetMethod("CycleWindowMaxMode", $flags)
$savePlacement = $winType.GetMethod("SaveWindowPlacementToState", $flags)

$mainField.SetValue($win, 720.0)
$win.Width = 720
$win.Height = 900
$toggleScratch.Invoke($win, @())
$scratchAfterOpen = [double]$scratchField.GetValue($win)
$widthAfterOpen = [double]$win.Width

$cycleMode.Invoke($win, @())
$mode1 = [string]$modeField.GetValue($win)
$cycleMode.Invoke($win, @())
$mode2 = [string]$modeField.GetValue($win)
$cycleMode.Invoke($win, @())
$mode3 = [string]$modeField.GetValue($win)

$savePlacement.Invoke($win, @())
$state = $stateField.GetValue($win)

$result = [PSCustomObject]@{
    ScratchEqualsMain = [Math]::Abs($scratchAfterOpen - 720) -lt 0.01
    WidthAfterOpen = $widthAfterOpen
    ModeSequence = "$mode1,$mode2,$mode3"
    PlacementSaved = $state.windowPlacementSaved
    SavedScratchOpen = $state.scratchOpen
    SavedMainWidth = $state.mainPaneWidth
    SavedScratchWidth = $state.scratchPaneWidth
}

$win.Close()
$result
