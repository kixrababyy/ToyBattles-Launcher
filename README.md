# ToyBattles Launcher

A modern, dark-themed WPF game launcher for **ToyBattles: MicroVolts Recharged**. Features automatic updates, full game installation, file repair, floating particle effects, and animated download UI.

# Valentine Theme

---

## Quick Start

```powershell
# Build
dotnet build ToyBattlesLauncher.slnx

# Run
dotnet run --project src/Launcher.App

# Test
dotnet test ToyBattlesLauncher.slnx

# Publish (single-file .exe, no .NET runtime needed)
dotnet publish src/Launcher.App/Launcher.App.csproj -c Release -o publish/single-file
```

---

## Distribution

Publishing produces a **self-contained single-file exe** — users don't need .NET installed.

Ship these files together:

| File | Size | Purpose |
|------|------|---------|
| `Launcher.exe` | ~103 MB | The launcher (everything bundled) |
| `Assets/` | ~2 MB | Logo + banner images (user-swappable) |
| `wallpapers/` | ~33 MB | Background slideshow images (user-swappable) |
| `updateinfo.ini` | <1 KB | Update server URLs |

The `.pdb` files in the publish output are debug symbols — don't ship them.

---

## Features

- **Full game install** — downloads and extracts the complete game archive for first-time users
- **Step-by-step patching** — compares local vs remote versions, downloads `.cab` patches, applies them sequentially
- **Inline settings & repair panel** — expandable panel on the home screen (no separate pages/tabs); toggle with the slider button next to PLAY
- **File repair** — verifies critical game files and re-downloads missing/corrupt ones; accessible from the inline panel
- **Auto-save settings** — all settings persist instantly on change (no Save button)
- **Floating particles** — 45 animated particles drift across the background in brand colors
- **Animated download card** — glassmorphic panel slides in with progress bar, speed, ETA, and percentage
- **Shimmer progress bar** — 6px gradient bar with animated light sweep effect
- **Wallpaper slideshow** — rotates background images with crossfade transitions every 9 seconds
- **Ambient orbs** — 4 breathing radial glow orbs that pulse and scale behind the content
- **Dark theme** — deep navy/blue palette with sky-blue and gold accents
- **MVVM architecture** — clean separation between WPF UI and core logic
- **Logging** — daily rotating log files in `%LOCALAPPDATA%\ToyBattlesLauncher\logs\`

---

## Project Structure

```
ToyBattlesLauncher.slnx
├── src/
│   ├── Launcher.Core/              ← Core logic (no UI dependency)
│   │   ├── Config/                 ← INI parsers (patch.ini, updateinfo.ini)
│   │   │   ├── IniParser.cs        ← Generic [section] key=value parser
│   │   │   ├── PatchConfig.cs      ← Parses patch.ini version list
│   │   │   ├── PatchLauncherConfig.cs
│   │   │   └── UpdateInfoConfig.cs ← Parses updateinfo.ini server URLs
│   │   ├── Models/
│   │   │   ├── GameVersion.cs      ← ENG_X.Y.Z.W version parsing & comparison
│   │   │   └── LocalState.cs       ← Persisted JSON state (install path, version,
│   │   │                               server IP, behaviour flags)
│   │   └── Services/
│   │       ├── DownloadService.cs   ← HTTP downloads with retry, progress, speed/ETA
│   │       ├── InstallService.cs    ← Full game install (download + extract ZIP/CAB)
│   │       ├── LaunchService.cs     ← Launches MicroVolts.exe with correct working dir
│   │       ├── LogService.cs        ← Thread-safe daily rotating file logger
│   │       ├── PatchService.cs      ← Step-by-step patching pipeline
│   │       └── RepairService.cs     ← File verification and re-download
│   └── Launcher.App/               ← WPF UI
│       ├── Assets/                  ← logo.png, banner.png (user-swappable)
│       ├── Themes/Dark.xaml         ← Color palette, button/progress bar styles
│       ├── Views/
│       │   ├── MainWindow.xaml      ← Shell: sidebar (Home only), wallpaper slideshow,
│       │   │                            ambient orbs, rounded window clip
│       │   ├── MainWindow.xaml.cs   ← Wallpaper slideshow, tray icon, folder dialogs,
│       │   │                            logo loading, round-corner clip logic
│       │   ├── HomeView.xaml        ← Main page: play button, download card, version
│       │   │                            badges, inline settings+repair expandable panel
│       │   └── ParticleCanvas.cs    ← Floating particle renderer (~60fps, 45 particles)
│       ├── ViewModels/
│       │   ├── MainViewModel.cs     ← Navigation, window commands (minimize/close)
│       │   ├── HomeViewModel.cs     ← Update check, download, install, launch logic;
│       │   │                            IsSettingsOpen / ToggleSettingsCommand
│       │   ├── SettingsViewModel.cs ← Game path, server IP, behaviour flags;
│       │   │                            auto-saves on every property change
│       │   └── RepairViewModel.cs   ← Verify & repair, full reinstall, cache clear
│       ├── Converters/              ← BoolToVisibility, StringToVisibility, NavSelection
│       └── wallpapers/              ← Embedded background images (user-swappable)
└── tests/
    └── Launcher.Core.Tests/         ← xUnit tests
        ├── GameVersionTests.cs
        ├── IniParserTests.cs
        └── PatchServiceTests.cs
```

---

## The Inline Settings & Repair Panel

There are no separate settings or repair pages. Everything is on the home screen.

**To open:** click the slider icon button (next to "Check for Updates" and PLAY).

The panel contains two sections:

**Verify & Repair** (top)
- Status text and progress bar while repair runs
- Results summary after completion
- Buttons: Verify & Repair Files, Full Reinstall, Clear Cache, Cancel

**Settings** (below a divider)
- Game Directory — file path + Browse / Open Folder buttons
- Server IP Address — saved to `state.json` for use by the game client
- Check for updates on startup (checkbox)
- Keep launcher open after launching game (checkbox)
- Buttons: Open Logs, Create Desktop Shortcut

All settings save automatically the moment they change — no Save button needed.

---

## Config Files

| File | Format | Purpose |
|------|--------|---------|
| `updateinfo.ini` | INI | Update server base URL + full file download URL |
| `patch.ini` | INI | Version list — `version` = latest, `version1..N` = known versions |
| `patchLauncher.ini` | INI | Launcher's own version string |

### `updateinfo.ini` Example

```ini
[update]
addr = http://cdn.toybattles.net/ENG

[FullFile]
addr = http://cdn.toybattles.net/update/ENG/Full/
```

### `patch.ini` Example

```ini
[patch]
version = ENG_2.0.4.3
version1 = ENG_2.0.4.2
version2 = ENG_2.0.4.1
exe = bin/MicroVolts.exe
```

---

## Persisted State (`state.json`)

Saved to `%LOCALAPPDATA%\ToyBattlesLauncher\state.json`.

| Field | Type | Purpose |
|-------|------|---------|
| `GameRootPath` | string | Path to the game installation folder |
| `InstalledVersion` | string | Currently installed game version |
| `ServerIp` | string | Custom server IP address |
| `CheckUpdatesOnStartup` | bool | Whether to auto-check on launch (default: true) |
| `KeepLauncherOpen` | bool | Whether to keep launcher open after game starts |
| `MaxDownloadSpeedMBps` | int | Throttle cap in MB/s (0 = unlimited) |
| `CustomUpdateUrl` | string | Override update server URL |
| `LaunchArguments` | string | Extra CLI args passed to the game exe |

---

## How the Install/Update Pipeline Works

1. **First launch (no game found)** → shows **INSTALL** button
2. Fetches `updateinfo.ini` → gets `FullFileAddress`
3. Downloads full game archive (ZIP or CAB) from the CDN
4. Extracts to the launcher's directory
5. Searches for `Bin/MicroVolts.exe` up to 2 levels deep
6. Copies `updateinfo.ini` into the game root for future launches
7. Transitions to **update check** → fetches remote `patch.ini`
8. Compares installed version vs latest → downloads step-by-step `.cab` patches if needed
9. Shows **PLAY** when up to date

---

## Customization

### Branding
- **Window title**: `MainWindow.xaml` → `Title="..."`
- **Logo**: drop `logo.png` into `Assets/` (46×46, shown in sidebar)
- **Banner**: drop `banner.png` into `Assets/` (shown on home page)
- **Text fallback**: `MainWindow.xaml` → `TOY` / `BATTLES` TextBlocks
- **Links**: `MainViewModel.cs` → Discord/website URLs

### Game Executable
- **`LaunchService.cs`** → change `Bin\\MicroVolts.exe` to your game's exe path

### Theme Colors
- **`Dark.xaml`** → edit the color palette at the top (`AccentBlue`, `AccentGold`, etc.)

### Backgrounds
- Drop `.png`/`.jpg` images into the `wallpapers/` folder — the launcher auto-rotates them

---

## Requirements

- **.NET 8 SDK** — for building (users don't need it — the published exe is self-contained)
- **Windows 10/11** — WPF is Windows-only
