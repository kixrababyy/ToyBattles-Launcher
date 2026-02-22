# ToyBattles Launcher

A modern WPF game launcher for **ToyBattles (MicroVolts Recharged)** with dark theme, MVVM architecture, and a clean update/patch pipeline.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer)
- Windows 10/11

## Quick Start

```powershell
# Build the solution
dotnet build ToyBattlesLauncher.slnx -c Release

# Run the launcher
dotnet run --project src/Launcher.App

# Run unit tests
dotnet test ToyBattlesLauncher.slnx
```

## Project Structure

```
ToyBattlesLauncher.slnx
├── src/
│   ├── Launcher.Core/         # Class library — all update/patch/repair/launch logic
│   │   ├── Config/            # INI parsing (updateinfo.ini, patch.ini, patchLauncher.ini)
│   │   ├── Models/            # GameVersion, LocalState
│   │   └── Services/          # DownloadService, PatchService, RepairService, LaunchService, LogService
│   │
│   └── Launcher.App/          # WPF application — MVVM dark-themed UI
│       ├── Themes/Dark.xaml   # Color palette, styled controls, rounded corners
│       ├── ViewModels/        # MainViewModel, HomeViewModel, SettingsViewModel, RepairViewModel
│       ├── Views/             # MainWindow, HomeView, SettingsView, RepairView
│       └── Converters/        # XAML value converters
│
├── tests/
│   └── Launcher.Core.Tests/   # xUnit tests
│
├── updateinfo.ini             # CDN/update URL config (do not change format)
├── patch.ini                  # Version manifest (do not change format)
└── patchLauncher.ini          # Launcher version (do not change format)
```

## INI File Formats

### updateinfo.ini
```ini
[update]
addr = http://cdn.toybattles.net/ENG

[FullFile]
addr = http://cdn.toybattles.net/update/ENG/Full/
```

### patch.ini
```ini
[patch]
version = ENG_2.0.4.3          # Latest version
version1 = ENG_2.0.4.2         # Known versions (unordered)
version2 = ENG_2.0.4.1
exe = bin/MicroVolts.exe        # Game executable path
```

### patchLauncher.ini
```ini
[patch]
version = ENG_2.0.1.2
```

## Expected Game Folder Layout

```
GameRoot/
├── Bin/
│   └── MicroVolts.exe          # Game executable
├── data/
│   └── cgd.dip                 # Game data file
├── updateinfo.ini
├── patch.ini
└── patchLauncher.ini
```

## Key Entry Points

| Feature         | File                                      |
|-----------------|-------------------------------------------|
| Update pipeline | `src/Launcher.Core/Services/PatchService.cs` |
| Repair pipeline | `src/Launcher.Core/Services/RepairService.cs` |
| Game launch     | `src/Launcher.Core/Services/LaunchService.cs` |
| Home UI + logic | `src/Launcher.App/ViewModels/HomeViewModel.cs` |
| Settings UI     | `src/Launcher.App/ViewModels/SettingsViewModel.cs` |
| Main shell      | `src/Launcher.App/Views/MainWindow.xaml`  |

## Release Build

```powershell
dotnet build ToyBattlesLauncher.slnx -c Release

# Output: src/Launcher.App/bin/Release/net8.0-windows/
```

## Logs

Logs are written to:
```
%LOCALAPPDATA%\ToyBattlesLauncher\logs\
```

State file (game root, installed version, settings):
```
%LOCALAPPDATA%\ToyBattlesLauncher\state.json
```
