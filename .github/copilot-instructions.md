# Steam Persona Switcher - Copilot Instructions

## Project Overview

Steam Persona Switcher is a WPF desktop application that automatically changes your Steam profile name (persona) based on which game you're currently playing. It uses SteamKit2 for Steam integration and runs on Windows.

## Technology Stack

- **Framework**: .NET 8.0 with Windows-specific features
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Target Platform**: Windows (win-x64)
- **Key Dependencies**:
  - SteamKit2 (3.3.1) - Steam client protocol library
  - Hardcodet.NotifyIcon.Wpf (2.0.0) - System tray functionality
  - YamlDotNet (16.2.1) - Configuration file handling

## Architecture & Design Patterns

### Core Components

1. **MainWindow** - Primary UI and application orchestration
2. **SteamPersonaService** - Steam connection and persona management logic
3. **CredentialManager** - Secure credential storage using Windows DPAPI
4. **SessionManager** - Persistent Steam session management
5. **DebugLogger** - Singleton logger for debugging and diagnostics
6. **DebugPanelWindow** - Dockable debug log viewer

### Data Flow

```
User Input (MainWindow)
    ↓
Config (YAML) + Credentials (Encrypted)
    ↓
SteamPersonaService
    ↓
SteamKit2 → Steam Servers
    ↓
Process Monitoring → Persona Name Changes
```

## Coding Standards & Conventions

### General Guidelines

- **Null Safety**: Enable nullable reference types (`<Nullable>enable</Nullable>`)
- **Async/Await**: Use async patterns for I/O operations and Steam communication
- **Error Handling**: Log errors to DebugLogger and show user-friendly messages
- **XAML**: Follow WPF best practices with proper data binding and MVVM-like patterns

### Naming Conventions

- **Private Fields**: Use `_camelCase` with underscore prefix (e.g., `_steamClient`)
- **Public Properties**: Use `PascalCase` (e.g., `IsRunning`)
- **Events**: Use `PascalCase` with event-style naming (e.g., `StatusChanged`)
- **Methods**: Use `PascalCase` with verb-noun pattern (e.g., `LoadConfiguration`)
- **Constants**: Use `PascalCase` (e.g., `MaxLogEntries`)

### Code Organization

- Keep UI logic in code-behind files (`.xaml.cs`)
- Separate business logic into service classes
- Use INotifyPropertyChanged for data binding
- Event handlers follow pattern: `ElementName_EventName` (e.g., `Start_Click`)

## Security Best Practices

### Credential Management

- **NEVER** store passwords in plain text
- Use Windows DPAPI (`ProtectedData.Protect/Unprotect`) for credential encryption
- Store encrypted credentials in user's AppData folder
- Session tokens use same encryption approach
- Configuration files (YAML) should NOT contain sensitive data

### Steam Authentication

- Use SteamKit2's modern authentication flow (`BeginAuthSessionViaCredentialsAsync`)
- Support Steam Guard via mobile app (device confirmation)
- Implement persistent sessions to minimize re-authentication
- Handle token expiration gracefully (delete expired sessions and re-authenticate)

## WPF & UI Guidelines

### XAML Practices

- Use consistent styling with `<Window.Resources>` for shared styles
- Implement proper grid layouts with appropriate `RowDefinitions` and `ColumnDefinitions`
- Use `SizeToContent` where appropriate for initial window sizing
- Set `MinWidth` and `MinHeight` to prevent UI from becoming unusable

### Data Binding

- Use `ObservableCollection<T>` for collections displayed in UI
- Implement `INotifyPropertyChanged` for bindable models
- Use `UpdateSourceTrigger=PropertyChanged` for immediate updates
- Create custom converters (like `StringNotEmptyConverter`) for complex binding logic

### System Tray Integration

- Use Hardcodet.NotifyIcon.Wpf for tray functionality
- Provide context menu for common actions
- Show balloon tips for important notifications
- Handle minimize-to-tray and close-to-tray separately

## Steam Integration

### Connection Management

- Initialize: `SteamClient` → `CallbackManager` → `SteamUser` → `SteamFriends`
- Register callbacks before connecting
- Run callback loop on background thread with cancellation token
- Implement auto-reconnect with delays on disconnect
- Handle concurrent authentication attempts with semaphore locks

### Authentication Flow

1. Check for saved session (refresh token)
2. If valid session exists, use it (no Steam Guard needed)
3. If no session or expired, do full authentication
4. Save new refresh token for future use
5. Handle Steam Guard challenges via `IAuthenticator` implementation

### Persona Management

- Monitor running processes with `Process.GetProcesses()`
- Match process names against configured game list (case-sensitive `.exe` names)
- Only update persona when it changes (avoid spam)
- Use `SteamFriends.SetPersonaName()` for updates
- Respect Steam's rate limiting

## Configuration & Persistence

### YAML Configuration

- Use YamlDotNet with `CamelCaseNamingConvention`
- Store in `%AppData%\SteamPersonaSwitcher\config.yaml`
- Structure:
  ```yaml
  username: "steamusername"
  checkIntervalSeconds: 10
  defaultPersonaName: "Not Gaming"
  gamePersonaNames:
    game.exe: "Persona Name"
  ```

### Encrypted Data

- Credentials: `credentials.dat` (DPAPI encrypted)
- Session tokens: `session.dat` (DPAPI encrypted)
- Format: `username|||data` (triple pipe separator)
- User-scoped encryption (won't decrypt on different machines/users)

### Preferences

- Tray preferences: `tray_preferences.yaml`
- Debug panel state: `debug_preferences.yaml`
- Registry for "Run at startup": `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

## Debugging & Logging

### DebugLogger Usage

```csharp
DebugLogger.Instance.Info("Information message");
DebugLogger.Instance.Warning("Warning message");
DebugLogger.Instance.Error("Error message");
DebugLogger.Instance.Debug("Debug details");
```

### Debug Panel Features

- Color-coded messages by severity
- Timestamps with milliseconds
- Auto-scroll option
- Word wrap toggle
- Copy all logs to clipboard
- Clear log history
- Docked to right side of main window

## Common Tasks & Patterns

### Building and Running in Development

- Always use this command to test build and run locally:
  ```powershell
  dotnet build; dotnet run -c Debug -r win-x64
  ```
- After implementing new code, provide the user with a list of tests to verify functionality.
- Do not proceed until user has confirmed that all tests pass.
- Do not mark the task as complete until user confirms that all tests pass.

### Adding New Features

1. Update TODO.md with task details
2. Implement business logic in service classes
3. Add UI elements in XAML
4. Wire up event handlers in code-behind
5. Add debug logging at key points
6. Update README.md with user-facing changes
7. Test thoroughly (see Tests section in TODO.md)

### Event Handling Pattern

```csharp
private void ElementName_EventName(object sender, EventArgs e)
{
    DebugLogger.Instance.Info("Action description");

    // Validation
    if (!ValidateInput())
    {
        AppendStatus("⚠️ Validation message");
        return;
    }

    try
    {
        // Action logic
        AppendStatus("✓ Success message");
    }
    catch (Exception ex)
    {
        DebugLogger.Instance.Error($"Error: {ex.Message}");
        AppendStatus($"❌ Error message: {ex.Message}");
    }
}
```

### Async Service Operations

```csharp
private async void Button_Click(object sender, RoutedEventArgs e)
{
    Button.IsEnabled = false;

    try
    {
        await _service.PerformActionAsync();
    }
    catch (Exception ex)
    {
        HandleError(ex);
    }
    finally
    {
        Button.IsEnabled = true;
    }
}
```

## Testing Considerations

- Test Steam connection with valid/invalid credentials
- Test Steam Guard authentication flows
- Test session persistence across app restarts
- Test process monitoring with various games
- Test all tray functionality (minimize, restore, close)
- Test startup behaviors
- Test configuration save/load
- Test credential encryption/decryption
- Test UI state persistence

## Known Limitations & Future Work

See TODO.md for:

- Enhancement ideas
- Bug fixes in progress
- Tests to perform
- Cleanup tasks

## Common Pitfalls to Avoid

1. **Don't** store passwords in plain text anywhere
2. **Don't** block the UI thread with long-running operations
3. **Don't** forget to dispose of resources (timers, connections)
4. **Don't** ignore cancellation tokens in async operations
5. **Don't** assume process monitoring will work for all games (some run with different exe names)
6. **Don't** update persona too frequently (Steam may rate-limit)
7. **Don't** forget to log important events for debugging
8. **Don't** hard-code file paths (use AppData)

## Deployment

### Single-File Executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Configuration

- `PublishSingleFile=true` - Creates single executable
- `SelfContained=true` - Includes .NET runtime
- `RuntimeIdentifier=win-x64` - Windows 64-bit target
- `IncludeNativeLibrariesForSelfExtract=true` - Extracts native dependencies
- `DebugType=none` and `DebugSymbols=false` - No debug symbols in release

## License

This project uses GPL-3.0 License. Any contributions must comply with GPL-3.0 requirements.
