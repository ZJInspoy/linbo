<p align="center">
  <img src="docs/linbo3-hero.png" alt="Linbo 3.0" width="760">
</p>

<h1 align="center">Linbo</h1>

<p align="center">A native Windows infinite-canvas prompt manager for AIGC workflows.</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows-111111">
  <img alt="UI" src="https://img.shields.io/badge/UI-native%20WPF-111111">
  <img alt="License" src="https://img.shields.io/badge/license-GPL--3.0-111111">
</p>

Linbo organizes prompts, reference material, and temporary images on a local infinite canvas. Each card displays a title while keeping its full text hidden and ready to copy. Persistent data stays on your computer by default, with no account or cloud service required.

## Features

- **Infinite canvas:** Pan, zoom, navigate with a minimap, create cards by drawing a region, and arrange cards in a two-column masonry layout.
- **Prompt cards:** Store a title and hidden body, copy content in one click, assign color labels, pin important cards, archive cards, and persist everything locally.
- **Document import:** Drag in Word or TXT files to use the file name as the card title and the document body as its hidden content.
- **Archive canvas:** Switch with a card-flip transition, restore archived cards, delete individual cards, or clear the archive.
- **Temporary reference canvas:** Paste or drag in images, resize them proportionally, draw directly on an image, mirror prompt cards, and save individual items locally.
- **Window workflow:** Use a borderless window with five adjustable boundaries, two-stage maximize behavior, and restored window position between sessions.

## Download

Download the Windows installer from [GitHub Releases](../../releases/latest). Compiled executables are not committed to the source repository.

## Local Data

Linbo does not upload card content. Its persistent state is stored at:

```text
%APPDATA%\Linbo\linbo-native-state.json
```

Installing a newer version preserves existing local data. During uninstallation, you can export cards to Word before the local data is removed. Regular backups are still recommended for important content.

## Build From Source

Requirements:

- Windows 10 or Windows 11
- PowerShell 5.1 or later
- .NET Framework 4.8 Developer Pack (recommended)

Run the following command from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Build output is written to `artifacts/`:

```text
Linbo.exe
LinboUninstall.exe
Linbo-3.0-Setup.exe
SHA256SUMS.txt
```

## Validation

After building, run the three lightweight validation scripts in STA mode:

```powershell
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\tests\clipboard_probe.ps1
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\tests\scratch_visual_probe.ps1
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\tests\window_geometry_probe.ps1
```

## Contributing

Reproducible bug reports and focused pull requests are welcome. For interaction changes, include the steps, expected result, and actual result. For data-handling changes, also describe upgrade behavior and unexpected-exit scenarios.

## License

Linbo's source code and image assets are released under the [GNU General Public License v3.0](LICENSE). You may use, study, modify, and redistribute the project. Distributed modifications must remain under the same license and include the corresponding source code.
