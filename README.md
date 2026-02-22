# ToyBattles Launcher (WPF)

A modern, dark-themed game launcher built with WPF and .NET 8. Features automatic update checking, patching, file repair, and game launching.

---

## Quick Start

```powershell
# Build
dotnet build ToyBattlesLauncher.slnx

# Run
dotnet run --project src/Launcher.App

# Test
dotnet test ToyBattlesLauncher.slnx

# Release build
dotnet publish src/Launcher.App -c Release -o publish/
```

---

## How to Use This Launcher for Your Own Game

### 1. Set Up Your Game Folder Structure

The launcher expects your game to be installed in a folder like this:

```
MyGame/
├── Bin/
│   └── MyGame.exe         ← your game executable
├── patch.ini              ← version list (local copy)
├── patchLauncher.ini      ← launcher version
├── updateinfo.ini         ← update server URL
└── Launcher.App.exe       ← this launcher (published)
```

### 2. Configure `updateinfo.ini`

This tells the launcher where to download updates from:

```ini
[CONFIG]
UpdateAddress=http://your-cdn.com/updates
FullDownloadAddress=http://your-cdn.com/full
```

Replace the URLs with your own CDN or file server.

### 3. Configure `patch.ini` (on your server)

This file lists all available versions. Put it on your update server so the launcher can fetch it. The launcher compares the local version against the remote version list.

```ini
[VERSION]
ENG_1.0.0.1=1
ENG_1.0.0.2=2
ENG_1.0.0.3=3
```

Each line is `VersionString=OrderNumber`. The highest number is the latest version.

### 4. Host Patch Files on Your Server

For each version step, create a `.cab` archive containing the changed files:

```
http://your-cdn.com/updates/ENG_1.0.0.1_ENG_1.0.0.2.cab
http://your-cdn.com/updates/ENG_1.0.0.2_ENG_1.0.0.3.cab
```

The launcher downloads and extracts these sequentially to upgrade from any version to the latest.

### 5. Customize the Launcher

#### Change the game name and branding:
- **Window title**: `MainWindow.xaml` → `Title="..."`
- **Logo text**: `MainWindow.xaml` → search for `TOY` and `BATTLES` TextBlocks
- **Banner text**: `HomeView.xaml` → search for `TOYBATTLES` and `MicroVolts Recharged`
- **Links**: `MainViewModel.cs` → update Discord/website URLs in `NavigateNewsCommand` and `NavigateDiscordCommand`

#### Change the game executable:
- **`LaunchService.cs`** → change `Bin\\MicroVolts.exe` to your game's exe path

#### Change theme colors:
- **`Dark.xaml`** → edit the color palette at the top (AccentGold, GlowPurple, etc.)

### 6. Publish and Distribute

```powershell
# Create a self-contained single-file executable
dotnet publish src/Launcher.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

This creates a single `Launcher.App.exe` you can ship alongside your game. Place it in the game root folder with the INI config files.

---

## Project Structure

```
ToyBattlesLauncher.slnx
├── src/
│   ├── Launcher.Core/          ← Core logic (no UI dependency)
│   │   ├── Config/             ← INI parsers (patch.ini, updateinfo.ini)
│   │   ├── Models/             ← GameVersion, LocalState
│   │   └── Services/           ← Download, Patch, Repair, Launch, Log
│   └── Launcher.App/           ← WPF UI
│       ├── Themes/Dark.xaml    ← Color palette and control styles
│       ├── Views/              ← XAML pages (Home, Settings, Repair)
│       ├── ViewModels/         ← MVVM ViewModels
│       └── Converters/         ← XAML value converters
└── tests/
    └── Launcher.Core.Tests/    ← 33 xUnit tests
```

## Config Files (Examples)

| File | Purpose |
|------|---------|
| `updateinfo.ini` | Base URL for downloading updates |
| `patch.ini` | Version list — local copy shows installed version, remote copy shows latest |
| `patchLauncher.ini` | Launcher's own version string |

## Features

- **Auto-update**: Compares local vs remote versions, downloads `.cab` patches, applies them
- **File repair**: Verifies critical files and re-downloads corrupted ones
- **Modern UI**: Dark theme, gold accents, purple glow effects, fade-in animations
- **MVVM architecture**: Clean separation between UI and logic
- **Logging**: Daily rotating log files in `logs/` folder

## Requirements

- .NET 8 SDK (for building)
- Windows 10/11 (WPF is Windows-only)
