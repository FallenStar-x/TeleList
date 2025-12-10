# TeleList

A Windows desktop application for browsing game entities and quickly updating teleport coordinates. Built with C# and WPF.

## What It Does

TeleList works alongside Speeder to streamline the teleportation workflow:

1. **Read** entity data from dump files (entities.txt)
2. **Browse** entities with filtering, sorting, and search
3. **Select** a target entity
4. **Auto-update** INI file with coordinates for teleportation
5. **Track** skipped treasure (ones you couldn't reach)

## Features

- **Entity Browsing**: View entities with type, location, and distance information
- **Filtering & Sorting**: Search by name, filter by type, sort by various criteria
- **Distance Calculation**: Set a reference entity and calculate distances from it
- **Skip Tracking**: Mark treasure as skipped (displayed in red) to track ones you couldn't reach
- **INI Integration**: Automatically update teleport coordinates in INI files
- **Global Hotkeys**: Navigate and mark entities without focusing the app
- **Auto-Refresh**: Automatically reload when the entity file changes
- **Dark Theme**: Modern dark UI

## Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 SDK (for building)

## Building

### Prerequisites

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build Commands

```bash
# Clone the repository
git clone https://github.com/FallenStar-x/TeleList.git
cd TeleList

# Build
dotnet build

# Run
dotnet run

# Publish self-contained executable
dotnet publish -c Release -p:PublishSingleFile=true --self-contained true -r win-x64
```

The published executable will be in `bin/Release/net8.0-windows/win-x64/publish/`

## Usage

### First Run

1. Launch `TeleList.exe`
2. Click **Load File** to select your entities file
3. Optionally configure an INI file for coordinate updates

### Entity File Format

The application expects entity files with blocks separated by `----------------`:

```
Entity type: Npc_Guard_001
Location: -123.45,67.89,-234.56
Distance: 45.67
----------------
Entity type: Treasurebox_42
Location: -200.00,50.00,-100.00
Distance: 120.50
----------------
```

### INI File Format

For coordinate updates, the INI file should contain:

```ini
[fRandomLocation]
keys=store -v TELEPORTX -w "-123.456"
keys2=store -v TELEPORTY -w "67.89"
keys3=store -v TELEPORTZ -w "-234.56"
```

### Hotkeys

Default global hotkeys (configurable in the app):

| Action | Default Key |
|--------|-------------|
| Next Entity | Right Arrow |
| Previous Entity | Left Arrow |
| Toggle Skip | Backslash (\\) |
| Reload File | Ctrl+R |
| Update INI | Ctrl+U |
| Clear Entities | Ctrl+Delete |

## Project Structure

```
TeleList/
├── App.xaml                    # Application entry point
├── MainWindow.xaml/.cs         # Main window UI and logic
├── Models/
│   └── Entity.cs               # Entity data model
├── ViewModels/
│   └── EntityViewModel.cs      # Entity view model for DataGrid
├── Services/
│   ├── EntityParser.cs         # Parses entity files
│   ├── GlobalHotkeyManager.cs  # Win32 global hotkey handling
│   ├── INICoordinateUpdater.cs # Updates INI coordinates
│   └── SettingsManager.cs      # JSON settings persistence
├── Dialogs/
│   └── HotkeyConfigDialog.xaml # Hotkey configuration dialog
└── Themes/
    └── DarkTheme.xaml          # Dark theme resources
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -am 'Add your feature'`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Create a Pull Request

## License

This project is open source. Feel free to use and modify as needed.
