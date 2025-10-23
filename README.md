# Steam Persona Switcher

Automatically change your Steam profile name (persona name) based on which game you're currently playing.

## Features

- 🎮 Monitors running processes to detect games
- 🔄 Automatically updates your Steam persona name
- ⚙️ Configurable via simple JSON file
- 🔐 Supports Steam Guard authentication
- 🔁 Auto-reconnect on disconnect
- 📝 Customizable check intervals

## How It Works

This application uses [SteamKit2](https://github.com/SteamRE/SteamKit) to maintain a connection to Steam and monitor your local processes. When it detects a game from your configured list is running, it automatically updates your Steam profile name to the corresponding persona name.

## Prerequisites

- .NET 8.0 or higher
- A Steam account
- Windows OS (for process monitoring)

## Installation

### Option 1: Download Pre-built Release
1. Download the latest release from the [Releases](https://github.com/JamesDBartlett3/SteamPersonaChanger/releases) page
2. Extract the files
3. Edit `config.json` with your details
4. Run `SteamPersonaChanger.exe`

### Option 2: Build from Source
```bash
git clone https://github.com/JamesDBartlett3/SteamPersonaChanger.git
cd SteamPersonaChanger
dotnet build
dotnet run
```

## Configuration

On first run, a default `config.json` file will be created. Edit it with your settings:

```json
{
  "username": "your_steam_username",
  "password": "your_steam_password",
  "checkIntervalSeconds": 10,
  "gamePersonaNames": {
    "hl2.exe": "Playing Half-Life 2",
    "csgo.exe": "Pwning noobs in CS:GO",
    "dota2.exe": "Feeding in Dota 2",
    "EldenRing.exe": "Getting wrecked in Elden Ring"
  },
  "defaultPersonaName": "Not Gaming"
}
```

### Finding Process Names

Use Windows Task Manager to find the exact process name for your games:
1. Open Task Manager (Ctrl+Shift+Esc)
2. Go to the "Details" tab
3. Find your game's process name (e.g., "RocketLeague.exe")
4. Add it to the `gamePersonaNames` dictionary in config.json

## Security Notes

⚠️ **Important:** Your Steam credentials are stored in plain text in `config.json`. Please ensure:
- Keep `config.json` secure and never commit it to version control
- Consider using a Steam account with limited access
- The application supports Steam Guard 2FA for additional security

## Usage

1. Start the application
2. Enter your Steam Guard code if prompted
3. The app will monitor for configured games and update your persona name automatically
4. Press Ctrl+C to exit gracefully

## Building as a Single Executable

To create a standalone executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in `bin/Release/net8.0/win-x64/publish/`

## Troubleshooting

**Application won't connect to Steam**
- Verify your credentials in config.json
- Check your internet connection
- Ensure Steam servers are online

**Persona name not changing**
- Verify the process name matches exactly (case-sensitive)
- Check the console for error messages
- Steam may rate-limit frequent name changes

**Steam Guard issues**
- The app will prompt you for 2FA codes
- Make sure you have access to your Steam Guard codes

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [SteamKit2](https://github.com/SteamRE/SteamKit)
- Inspired by the need to show off what game you're playing

## Disclaimer

This tool is not affiliated with or endorsed by Valve Corporation or Steam. Use at your own risk.
