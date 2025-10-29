# Steam Persona Switcher

Automatically change your Steam profile name (persona name) based on which game you're currently playing.

<!-- Embed the logo here -->
![Steam Persona Switcher Logo](https://raw.githubusercontent.com/JamesDBartlett3/SteamPersonaSwitcher/main/SteamPersonaSwitcher_Transparent.png)

## Features

- 🎮 Monitors running processes to detect games
- 🔄 Automatically updates your Steam persona name
- 🖥️ Modern WPF GUI with system tray support
- 🐛 Built-in Debug Log panel for troubleshooting
- ⚙️ Configurable via GUI and YAML configuration files
- 🔐 Supports Steam Guard authentication with GUI dialogs
- 🔁 Auto-reconnect on disconnect
- 📝 Customizable check intervals
- 💾 Persistent session support to reduce re-authentication
- 🚀 Option to run on Windows startup
- 📋 Minimize to system tray

## How It Works

This application uses [SteamKit2](https://github.com/SteamRE/SteamKit) to maintain a connection to Steam and monitor your local processes. When it detects a game from your configured list is running, it automatically updates your Steam profile name to the corresponding persona name.

## Prerequisites

- .NET 8.0 or higher
- A Steam account
- Windows OS (for process monitoring)

## Installation

### Option 1: Download Pre-built Release

1. Download the latest release from the [Releases](https://github.com/JamesDBartlett3/SteamPersonaSwitcher/releases) page
2. Extract the files
3. Run `SteamPersonaSwitcher.exe`
4. Configure your settings via the GUI

### Option 2: Build from Source

```bash
git clone https://github.com/JamesDBartlett3/SteamPersonaSwitcher.git
cd SteamPersonaSwitcher
dotnet build
dotnet run
```

## Configuration

The application now uses a modern GUI for configuration. Settings are automatically saved to YAML files in your AppData folder.

### Main Settings

- **Steam Credentials**: Enter your Steam username and password
- **Default Persona Name**: The name to use when no configured game is running
- **Check Interval**: How often (in seconds) to check for running games

### Game-Persona Mappings

Add game executable names and their corresponding persona names:

1. Enter the game's .exe name in the "Game Process Name" column (e.g., "EldenRing.exe", "EliteDangerous64.exe", etc.)
2. Enter the persona name you want to use in the "Persona Name" column (e.g., "Elden Lord", "CMDR Zorthon Torgrim", etc.)
3. Press Tab, Enter, or click outside the grid to commit the entry
4. A new blank row will automatically appear for additional mappings
5. Click "Save Config" to save your changes

### Finding Process Names

Use Windows Task Manager to find the exact process name for your games:

1. Open Task Manager (Ctrl+Shift+Esc)
2. Go to the "Details" tab
3. Find your game's process name (e.g., "RocketLeague.exe")
4. Add it to your game-persona mappings in the application

## Debug Log Panel

The application includes a built-in Debug Log panel for troubleshooting:

- Click "Show Debug Log" to open the panel (docked to the right side of the window)
- The panel shows all application events with timestamps and color-coded severity levels
- Features include:
  - **Word Wrap**: Toggle to wrap long log messages
  - **Auto-scroll**: Automatically scroll to newest messages
  - **Copy**: Copy all logs to clipboard
  - **Clear**: Clear the log history
- Panel state (visible/hidden and width) is saved between sessions
- The panel is resizable by dragging its edge

## Security Notes

⚠️ **Important:** Your Steam credentials are stored securely:

- Passwords are encrypted using Windows Data Protection API (DPAPI)
- Session tokens are stored to eliminate the hassle of reauthenticating every time the application is launched
- Configuration files are stored in your user's AppData folder
- Never share your configuration files with others
- If you are concerned about the risk of entering your Steam credentials into an unofficial third party app, that's a good thing, because it means that you take your security seriously. Please feel free to review and/or compile the code yourself, for your own peace of mind.
- If you discover any security issues in this application, please report them to me immediately via the GitHub Issues feature in this repository, and I will address them as soon as possible.

## Usage

1. Launch the application
2. Enter your Steam credentials in the GUI
3. Configure your game-persona mappings
4. Click "Start Service" to begin monitoring
5. The app will automatically update your persona name when a configured game is detected
6. Use the system tray icon to access the application when minimized
7. Click "Show Debug Log" to view application events and troubleshoot issues

### System Tray

The application supports minimizing to the system tray:

- Click the tray icon to restore the window
- Right-click the tray icon for quick actions
- The application can start minimized to tray if configured

## Building as a Single Executable

To create a standalone executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in `bin/Release/net8.0/win-x64/publish/`

## Troubleshooting

**Application won't connect to Steam**

- Verify your credentials in the GUI settings
- Check your internet connection
- Ensure Steam servers are online
- Check the Debug Log panel for detailed error messages

**Persona name not changing**

- Verify the process name matches exactly (case-sensitive)
- Check the Debug Log panel for detection events
- Ensure the service is running
- Steam may rate-limit frequent name changes

**Steam Guard issues**

- The app will notify you in the Status area if Steam Guard authentication is required.
- Currently, the app only supports MFA via the Steam Mobile app. 
- Email and SMS codes are not supported at this time, but may be added in the future if enough people request them.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [SteamKit2](https://github.com/SteamRE/SteamKit)
- Inspired by [Foxhole](https://www.foxholegame.com/), which automatically sets the player's on-screen handle from their Steam persona name, and does not offer an option to customize it. If you play Foxhole (or other games which take the same approach), this tool allows you to set a different Steam persona name for when you play those games, and it will automatically switch back when you exit the game.

## Disclaimer

This tool is not affiliated with or endorsed by Valve Corporation or Steam. Use at your own risk.
