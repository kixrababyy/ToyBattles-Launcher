# ToyBattles Launcher

A modern, dark-themed WPF launcher for **ToyBattles**. Handles full game installation, step-by-step patching, file repair, server switching, playtime tracking, and self-updating from GitHub Releases.

---

## Quick Start

```powershell
# Build
dotnet build ToyBattlesLauncher.slnx

# Run
dotnet run --project src/Launcher.App

# Publish (single-file .exe — no .NET runtime required)
dotnet publish src/Launcher.App/Launcher.App.csproj --configuration Release --output "publish/single-file"
```

---

## Distribution

Publishing produces a **self-contained single-file exe** (~109 MB). Users don't need .NET installed.

Ship these files together:

| File | Purpose |
|------|---------|
| `Launcher.exe` | The launcher (everything bundled) |
| `Assets/` | Logo + banner images (user-swappable) |
| `wallpapers/` | Background slideshow images (user-swappable) |

The `.pdb` files in the publish output are debug symbols — don't ship them.

---

## Features

- **Full game install** — downloads and extracts the complete game archive (ZIP/CAB) for first-time users
- **Step-by-step patching** — compares local vs remote versions, downloads `.zip` patches, applies them sequentially
- **File verify & repair** — verifies critical game files and re-downloads missing or corrupt ones
- **Full reinstall** — re-downloads and re-extracts the complete game archive to fix broken installs
- **Multiple server profiles** — Main Build, SEA Server, Test Server; remembers per-server install paths
- **Self-update from GitHub** — checks `kixrababyy/ToyBattles-Launcher` releases on startup; downloads and hot-swaps the exe
- **Playtime tracking** — accumulates session time; recovers time if launcher was closed while game was running
- **System tray** — minimises to tray when game launches (if Keep Launcher Open is on); balloon notification on update
- **cgd.dip sync** — downloads the latest `cgd.dip` from CDN on startup and after fresh install
- **Wallpaper slideshow** — rotates background images with crossfade transitions
- **Dark theme** — deep navy/blue palette with sky-blue and gold accents
- **MVVM architecture** — clean separation between WPF UI and core logic
- **Structured logging** — daily rotating log files in `%LOCALAPPDATA%\ToyBattlesLauncher\logs\`

---

## Project Structure

```
ToyBattlesLauncher.slnx
├── src/
│   ├── Launcher.Core/              ← Core logic (no UI dependency)
│   │   ├── Config/
│   │   │   ├── IniParser.cs        ← Generic [section] key=value parser
│   │   │   ├── PatchConfig.cs      ← Parses patch.ini version list
│   │   │   ├── PatchLauncherConfig.cs
│   │   │   └── UpdateInfoConfig.cs ← Parses updateinfo.ini server URLs
│   │   ├── Models/
│   │   │   ├── GameVersion.cs      ← ENG_X.Y.Z.W version parsing & comparison
│   │   │   └── LocalState.cs       ← Persisted JSON state (paths, version, flags)
│   │   └── Services/
│   │       ├── DownloadService.cs      ← HTTP downloads with retry, progress, speed/ETA
│   │       ├── InstallService.cs       ← Full game install (download + extract ZIP/CAB)
│   │       ├── LaunchService.cs        ← Launches MicroVolts.exe with correct working dir
│   │       ├── LauncherUpdateService.cs← GitHub release check + exe hot-swap
│   │       ├── LogService.cs           ← Thread-safe daily rotating logger
│   │       ├── PatchService.cs         ← Step-by-step patching pipeline
│   │       └── RepairService.cs        ← File verification and re-download
│   └── Launcher.App/               ← WPF UI
│       ├── Assets/                  ← logo.png, banner.png, icon.ico (user-swappable)
│       ├── Themes/Dark.xaml         ← Color palette, button/progress bar styles
│       ├── Views/
│       │   ├── MainWindow.xaml      ← Shell: nav sidebar, wallpaper slideshow,
│       │   │                            ambient orbs, tray icon, rounded window
│       │   ├── HomeView.xaml        ← Play button, download card, version badges,
│       │   │                            server selector, playtime display
│       │   ├── SettingsView.xaml    ← Game path, server IP, behaviour flags,
│       │   │                            download speed cap, shortcut creator
│       │   ├── RepairView.xaml      ← Verify & repair, full reinstall
│       │   └── ParticleCanvas.cs    ← Floating particle renderer
│       ├── ViewModels/
│       │   ├── MainViewModel.cs     ← Navigation, window commands
│       │   ├── HomeViewModel.cs     ← Update check, download, install, launch,
│       │   │                            server switching, playtime, cgd.dip sync
│       │   ├── SettingsViewModel.cs ← Settings load/save, auto-save on change
│       │   └── RepairViewModel.cs   ← Verify & repair, full reinstall
│       ├── Converters/              ← BoolToVisibility, NavSelection, etc.
│       └── wallpapers/              ← Embedded background images
└── tests/
    └── Launcher.Core.Tests/
        ├── GameVersionTests.cs
        ├── IniParserTests.cs
        └── PatchServiceTests.cs
```

---

## Pages

### Home
The main screen. Shows the action button (INSTALL / CHECKING / UPDATE / PLAY), download progress card, version badges, server selector, and playtime.

### Settings
- Game Directory — path + Browse / Open Folder buttons
- Server IP Address — saved to `state.json` for use by the game client
- Max Download Speed — throttle in MB/s (0 = unlimited)
- Launch Arguments — extra CLI args passed to the game exe
- Keep launcher open after launching game (checkbox)
- Buttons: Open Logs Folder, Create Desktop Shortcut, Save, Discard

### Repair
- **Verify & Repair** — checks critical game files against the CDN and re-downloads any that are missing or corrupt
- **Full Reinstall** — re-downloads the complete game archive and extracts it over the existing install; works even if `updateinfo.ini` or other files are missing

---

## Config Files

| File | Format | Purpose |
|------|--------|---------|
| `updateinfo.ini` | INI | Update server base URL; full-file address (used as fallback only) |
| `patch.ini` | INI | Version list — `version` = latest, `version1..N` = known versions |

### `updateinfo.ini` Example

```ini
[update]
addr = http://cdn.toybattles.net/ENG

[FullFile]
addr = http://cdn.toybattles.net/update/ENG/Full/
```

> The launcher derives the full game archive URL from `[update] addr` as:
> `{UpdateAddress}/microvolts/Full/Full.zip`
> The `[FullFile] addr` value is not used directly.

---

## Persisted State (`state.json`)

Saved to `%LOCALAPPDATA%\ToyBattlesLauncher\state.json`.

| Field | Type | Purpose |
|-------|------|---------|
| `GameRootPath` | string | Active game installation folder |
| `InstalledVersion` | string | Currently installed game version |
| `ServerProfile` | string | Selected server (`Main Build`, `SEA Server`, `Test Server`) |
| `ServerGameRoots` | object | Per-server install paths |
| `ServerIp` | string | Custom server IP address |
| `CheckUpdatesOnStartup` | bool | Auto-check on launch (default: true) |
| `KeepLauncherOpen` | bool | Keep launcher open after game starts |
| `MaxDownloadSpeedMBps` | int | Throttle cap in MB/s (0 = unlimited) |
| `CustomUpdateUrl` | string | Override update server URL |
| `LaunchArguments` | string | Extra CLI args passed to the game exe |
| `TotalPlaytimeSeconds` | long | Cumulative playtime in seconds |
| `PendingSessionStartUtc` | datetime | Set when game launches; recovered on next startup |

---

## How the Install/Update Pipeline Works

1. **First launch (no game found)** → shows **INSTALL** button
2. User picks install folder
3. Launcher derives archive URL: `{UpdateAddress}/microvolts/Full/Full.zip`
4. Downloads and extracts the full game archive to the chosen folder
5. Syncs `cgd.dip` from CDN (ensures the game can connect immediately)
6. Checks remote `patch.ini` — applies any pending patches automatically
7. Shows **PLAY** when up to date

On subsequent launches, only steps 6–7 run (no re-download needed).

---

## Self-Update (GitHub Releases)

The launcher checks `kixrababyy/ToyBattles-Launcher` for a newer release on every startup.

To push a launcher update:
1. Build and publish `Launcher.exe`
2. Increment `<Version>` in `Launcher.App.csproj` (e.g. `1.0.1`)
3. Create a GitHub Release tagged `v1.0.1`
4. Upload `Launcher.exe` as a release asset (must be named exactly `Launcher.exe`)

Users will be prompted on next launch and the exe is replaced automatically.

---

## Customization

| What | Where |
|------|-------|
| Logo | Drop `logo.png` into `Assets/` next to the exe |
| Banner | Drop `banner.png` into `Assets/` |
| Wallpapers | Drop `.png`/`.jpg` files into `wallpapers/` — auto-rotated |
| Theme colors | `Themes/Dark.xaml` — edit `AccentBlue`, `AccentGold`, etc. |
| Discord / website links | `MainViewModel.cs` |
| Game executable path | `LaunchService.cs` → `Bin\\MicroVolts.exe` |
| GitHub update repo | `LauncherUpdateService.cs` → `GitHubOwner` / `GitHubRepo` |

---

## Requirements

- **.NET 8 SDK** — for building only (published exe is self-contained, users need nothing)
- **Windows 10/11** — WPF is Windows-only
