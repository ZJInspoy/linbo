param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "artifacts")
)

$ErrorActionPreference = "Stop"

$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $framework "csc.exe"
$assemblyRoot = Join-Path $env:WINDIR "Microsoft.NET\assembly"
$presentationCore = Join-Path $assemblyRoot "GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll"
$presentationFramework = Join-Path $assemblyRoot "GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll"
$windowsBase = Join-Path $assemblyRoot "GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll"
$systemXaml = Join-Path $assemblyRoot "GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"

$required = @($csc, $presentationCore, $presentationFramework, $windowsBase, $systemXaml)
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath $_) })
if ($missing.Count -gt 0) {
    throw "缺少 .NET Framework WPF 构建组件。请安装 .NET Framework 4.8 Developer Pack。`n$($missing -join "`n")"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$assetOutput = Join-Path $OutputDirectory "assets"
New-Item -ItemType Directory -Force -Path $assetOutput | Out-Null

$sourceRoot = Join-Path $PSScriptRoot "src"
$assetRoot = Join-Path $PSScriptRoot "assets"
$icon = Join-Path $assetRoot "linbo-icon.ico"
$app = Join-Path $OutputDirectory "Linbo.exe"
$uninstaller = Join-Path $OutputDirectory "LinboUninstall.exe"
$installer = Join-Path $OutputDirectory "Linbo-3.0-Setup.exe"

function Invoke-Compiler([string[]]$CompilerArguments) {
    & $csc $CompilerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "C# 编译失败，退出代码：$LASTEXITCODE"
    }
}

$frameworkReferences = @(
    "/reference:$presentationCore",
    "/reference:$presentationFramework",
    "/reference:$windowsBase",
    "/reference:$systemXaml",
    "/reference:$(Join-Path $framework 'System.Web.Extensions.dll')",
    "/reference:$(Join-Path $framework 'System.IO.Compression.dll')",
    "/reference:$(Join-Path $framework 'System.IO.Compression.FileSystem.dll')",
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')",
    "/reference:$(Join-Path $framework 'System.Drawing.dll')"
)

Invoke-Compiler (@(
    "/nologo",
    "/target:winexe",
    "/out:$app",
    "/win32icon:$icon"
) + $frameworkReferences + (Join-Path $sourceRoot "LinboApp.cs"))

Invoke-Compiler (@(
    "/nologo",
    "/target:winexe",
    "/out:$uninstaller",
    "/win32icon:$icon",
    "/reference:$(Join-Path $framework 'System.Web.Extensions.dll')",
    "/reference:$(Join-Path $framework 'System.IO.Compression.dll')",
    "/reference:$(Join-Path $framework 'System.IO.Compression.FileSystem.dll')",
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')"
    (Join-Path $sourceRoot "LinboUninstaller.cs")
))

Invoke-Compiler (@(
    "/nologo",
    "/target:winexe",
    "/out:$installer",
    "/win32icon:$icon",
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')",
    "/reference:$(Join-Path $framework 'System.Drawing.dll')",
    "/resource:$app,LinboAppPayload",
    "/resource:$uninstaller,LinboUninstallPayload",
    "/resource:$(Join-Path $assetRoot 'linbo-icon.png'),LinboIconPngPayload",
    "/resource:$icon,LinboIconIcoPayload",
    "/resource:$(Join-Path $assetRoot 'mist-spirit.jpg'),MistSpiritPayload"
    (Join-Path $sourceRoot "LinboInstaller.cs")
))

Copy-Item -LiteralPath (Join-Path $assetRoot "linbo-icon.png") -Destination $assetOutput -Force
Copy-Item -LiteralPath $icon -Destination $assetOutput -Force
Copy-Item -LiteralPath (Join-Path $assetRoot "mist-spirit.jpg") -Destination $assetOutput -Force

$checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $installer).Hash.ToLowerInvariant()
"$checksum  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath (Join-Path $OutputDirectory "SHA256SUMS.txt") -Encoding Ascii

Get-ChildItem -File -LiteralPath $OutputDirectory | Select-Object Name, Length, LastWriteTime
