# TeleList

A Windows companion app for Speeder that simplifies teleporting to game entities/treasures by automatically updating INI coordinates.

## How It Works

1. **Speeder** dumps nearby entities to `entities.txt`
2. **TeleList** loads the file and displays all entities in a browsable list
3. **Select** an entity and TeleList updates your Speeder INI file with the coordinates
4. **Speeder** reads the INI and teleports you to that location
5. **Mark** unreachable treasures to revisit later

## Setup

### 1. Speeder Script

Your Speeder script must include this variable to dump entities:

```
EntityFileName=entities.txt
```

### 2. Speeder INI File

Your Speeder INI must contain this function for TeleList to update:

```ini
[fRandomLocation]
keys=store -v TELEPORTX -w "-3508.010000"
keys2=store -v TELEPORTY -w "190.278000"
keys3=store -v TELEPORTZ -w "-1201.190000"
```

### 3. TeleList Configuration

1. Launch `TeleList.exe`
2. Click **Load File** and select your `entities.txt`
3. Click **Browse** next to INI Path and select your Speeder INI file
4. Enable **Auto Update INI** to automatically update coordinates when selecting entities

## Usage

### Basic Workflow

1. In-game, use Speeder to dump entities to `entities.txt`
2. TeleList auto-reloads the file (if Auto Refresh is enabled)
3. Browse/search the entity list
4. Click an entity or use hotkeys to navigate
5. Use Speeder's teleport function to teleport to the coordinates
6. Mark treasures you couldn't reach with the Skip hotkey
7. Move to a new location and repeat

### Sharing Entity Files

You can load entity files created by other players containing pre-mapped coordinates instead of dumping your own.

### Hotkeys

Default global hotkeys (configurable):

| Action | Default Key |
|--------|-------------|
| Next Entity | Right Arrow |
| Previous Entity | Left Arrow |
| Toggle Skip | Backslash (\\) |
| Reload File | Ctrl+R |
| Update INI | Ctrl+U |
| Clear Entities | Ctrl+Delete |

## Features

- **Auto Refresh**: Automatically reloads when `entities.txt` changes
- **Skip Tracking**: Mark treasures as skipped (red) to track unreachable ones
- **Distance Sorting**: Sort by distance from a reference entity
- **Search & Filter**: Find specific entity types quickly
- **Global Hotkeys**: Control the app without switching windows

## Requirements

- Windows 10/11 (64-bit)

## Building from Source

```bash
git clone https://github.com/FallenStar-x/TeleList.git
cd TeleList
dotnet build
dotnet run
```

## License

Open source. Feel free to use and modify.
