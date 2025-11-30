using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SteamPersonaSwitcher;

public partial class MainWindow : Window
{
    private readonly SteamPersonaService _service;
    private ObservableCollection<GamePersonaMapping> _gamePersonaMappings;
    private bool _forceActualClose = false;
    private bool _isShuttingDown = false;
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly string _trayPreferencesPath;
    private readonly CredentialManager _credentialManager;
    private readonly SessionManager _sessionManager;
    private readonly DebugLogger _debugLogger;
    private bool _isDebugPanelVisible = false;
    private double _debugPanelWidth = 350; // Default width
    private DebugPanelWindow? _debugPanelWindow;

    private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;
    
    // Track whether we've shown the tray notification this session to avoid spam
    private bool _hasShownTrayNotification = false;
    
    // Track the last persona name to detect actual changes (not initial set)
    private string? _lastPersonaName = null;
    
    // Track if we're waiting to update the start-minimized notification when connected
    private bool _waitingForConnectionToShowNotification = false;

    // Clear credentials countdown
    private System.Windows.Threading.DispatcherTimer? _clearCredentialsTimer;
    private int _clearCredentialsCountdown = 0;
    private bool _isClearCredentialsPending = false;

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize debug logger
        _debugLogger = DebugLogger.Instance;
        
        // Track window position and size changes to update debug panel position
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        
        _debugLogger.Info("MainWindow initializing...");
        
        // Set up AppData directory
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamPersonaSwitcher");
        _configFilePath = Path.Combine(_configDirectory, "config.yaml");
        _trayPreferencesPath = Path.Combine(_configDirectory, "tray_preferences.yaml");
        
        _debugLogger.Info($"Config directory: {_configDirectory}");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
            _debugLogger.Info("Created config directory");
        }
        
        // Initialize credential manager
        _credentialManager = new CredentialManager(_configDirectory);
        _debugLogger.Info("Credential manager initialized");
        
        // Initialize session manager
        _sessionManager = new SessionManager(_configDirectory);
        _debugLogger.Info("Session manager initialized");
        
        // Get tray icon from resources
        _trayIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)FindResource("TrayIcon");
        
        _service = new SteamPersonaService();
        _service.SetSessionManager(_sessionManager);
        _gamePersonaMappings = new ObservableCollection<GamePersonaMapping>();
        
        GamePersonaGrid.ItemsSource = _gamePersonaMappings;
        _debugLogger.Info("Game persona grid initialized");
        
        // Subscribe to service events
        _service.StatusChanged += OnStatusChanged;
        _service.PersonaChanged += OnPersonaChanged;
        _service.ErrorOccurred += OnErrorOccurred;
        _service.ConnectionStateChanged += OnConnectionStateChanged;
        
        // Load configuration on startup
        LoadConfiguration();
        LoadDebugPanelPreferences();
        
        // Check if should start minimized (only if parent "Minimize to tray" is also enabled)
        if (MinimizeToTrayCheckBox.IsChecked == true && 
            StartMinimizedCheckBox.IsChecked == true)
        {
            WindowState = WindowState.Minimized;
            Hide();
            
            // Show the start-minimized notification after hiding
            // Use Dispatcher to ensure it runs after the window is actually hidden
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowStartMinimizedNotification();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        // After content is rendered, set MinWidth to initial width and MinHeight to actual height plus buffer
        UpdateLayout();
        MinWidth = 800; // Set minimum width to the initial width
        MinHeight = ActualHeight + 20; // Add 20px buffer for padding
        SizeToContent = SizeToContent.Manual; // Disable auto-sizing after initial render
    }

    private async void ShowStartMinimizedNotification()
    {
        // Set flag to suppress regular connection notifications during startup
        _waitingForConnectionToShowNotification = true;
        
        // Wait briefly to allow auto-connect to establish (if enabled)
        await Task.Delay(1200); // 1.2 seconds - enough time for most connections
        
        string status = _service.IsRunning 
            ? (_service.IsLoggedIn ? "Connected!" : "Connecting...") 
            : "Disconnected";
        
        string message = $"Status: {status}\n\n" +
                        "Double-click the tray icon or right-click → Show Window to restore.";
        
        _trayIcon?.ShowBalloonTip(
            "Steam Persona Switcher Running", 
            message, 
            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        
        _debugLogger.Info($"Start minimized notification shown - Status: {status}");
        _hasShownTrayNotification = true;
        
        // If already connected, clear the flag now
        if (_service.IsLoggedIn)
        {
            _waitingForConnectionToShowNotification = false;
            _debugLogger.Debug("Already connected - clearing notification flag");
        }
        // Otherwise keep the flag set to update when connection completes
        else if (_service.IsRunning)
        {
            _debugLogger.Debug("Still connecting after delay - will update notification when connected");
        }
        else
        {
            _waitingForConnectionToShowNotification = false;
        }
    }

    private void OnStatusChanged(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            _debugLogger.Info($"Status: {message}");
            AppendStatus($"[{DateTime.Now:HH:mm:ss}] {message}");
        });
    }

    private void OnPersonaChanged(object? sender, string personaName)
    {
        Dispatcher.Invoke(() =>
        {
            _debugLogger.Info($"Persona changed to: {personaName}");
            AppendStatus($"[{DateTime.Now:HH:mm:ss}] ✓ Persona changed to: {personaName}");
            
            // Only show notification if this is an actual change (not the first time it's set)
            if (_lastPersonaName != null && _lastPersonaName != personaName)
            {
                _trayIcon?.ShowBalloonTip("Persona Changed", 
                    $"Now: {personaName}", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                _debugLogger.Debug($"Persona changed notification shown: {_lastPersonaName} → {personaName}");
            }
            else if (_lastPersonaName == null)
            {
                _debugLogger.Debug($"Persona set for first time: {personaName} (notification suppressed)");
            }
            
            // Update last known persona name
            _lastPersonaName = personaName;
        });
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        Dispatcher.Invoke(() =>
        {
            _debugLogger.Info($"ERROR: {error}");
            AppendStatus($"[{DateTime.Now:HH:mm:ss}] ❌ ERROR: {error}");
        });
    }

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine($"[UI EVENT] Connection state changed: {(isConnected ? "CONNECTED" : "DISCONNECTED")}");
            _debugLogger.Info($"[UI EVENT] Connection state changed: {(isConnected ? "CONNECTED" : "DISCONNECTED")}");
            _debugLogger.Debug($"_waitingForConnectionToShowNotification = {_waitingForConnectionToShowNotification}");
            
            Title = isConnected 
                ? "Steam Persona Switcher - Connected" 
                : "Steam Persona Switcher - Disconnected";
            
            // Update tray menu to reflect current state
            UpdateTrayContextMenu();
            
            // If we're waiting to update the start-minimized notification, do it now
            if (_waitingForConnectionToShowNotification && isConnected)
            {
                _waitingForConnectionToShowNotification = false;
                _debugLogger.Info("Connection established - updating start-minimized notification");
                
                // Close any existing balloon (this doesn't actually work reliably in Windows)
                // but showing a new one should replace it
                string connectedMessage = "Status: Connected!\n\n" +
                                         "Double-click the tray icon or right-click → Show Window to restore.";
                
                _debugLogger.Debug($"Showing 'Connected!' balloon notification");
                _trayIcon?.ShowBalloonTip(
                    "Steam Persona Switcher", 
                    connectedMessage, 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                
                _debugLogger.Info("Start-minimized notification updated to 'Connected!'");
            }
            // If window is hidden (in tray), show connection status notifications
            else if (!IsVisible && !_waitingForConnectionToShowNotification)
            {
                if (isConnected)
                {
                    _trayIcon?.ShowBalloonTip(
                        "Steam Persona Switcher", 
                        "Connected to Steam!", 
                        Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                    _debugLogger.Debug("Showed 'Connected' notification (window hidden)");
                }
                // Don't show disconnect notification - the "Service stopping..." notification already showed
                // and the delay can be long, making it confusing
            }
        });
    }

    private void AppendStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusTextBox.AppendText(message + Environment.NewLine);
            StatusTextBox.ScrollToEnd();
        });
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Info("Start button clicked");
        
        // Validate inputs
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            _debugLogger.Info("Validation failed: No username");
            AppendStatus("⚠️ Please enter your Steam username.");
            return;
        }

        if (string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            _debugLogger.Info("Validation failed: No password");
            AppendStatus("⚠️ Please enter your Steam password.");
            return;
        }

        if (!int.TryParse(CheckIntervalTextBox.Text, out int interval) || interval < 1)
        {
            _debugLogger.Info("Validation failed: Invalid interval");
            AppendStatus("⚠️ Please enter a valid check interval (minimum 1 second).");
            return;
        }

        _debugLogger.Info($"Starting with username: {UsernameTextBox.Text.Trim()}, interval: {interval}s");

        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        // Save or delete credentials based on Remember Me checkbox
        try
        {
            if (RememberMeCheckBox.IsChecked == true)
            {
                _debugLogger.Info("Saving credentials (Remember Me is checked)");
                _credentialManager.SaveCredentials(username, password);
                AppendStatus("Credentials saved securely.");
            }
            else
            {
                // If Remember Me is unchecked, delete any saved credentials
                if (_credentialManager.HasSavedCredentials())
                {
                    _debugLogger.Info("Deleting saved credentials (Remember Me is unchecked)");
                    _credentialManager.DeleteCredentials();
                    AppendStatus("Saved credentials removed.");
                }
            }
        }
        catch (Exception ex)
        {
            _debugLogger.Info($"Failed to save credentials: {ex.Message}");
            AppendStatus($"Warning: Failed to save credentials: {ex.Message}");
        }

        // Create config from UI
        var config = new Config
        {
            Username = username,
            Password = password,
            CheckIntervalSeconds = interval,
            DefaultPersonaName = DefaultPersonaTextBox.Text.Trim(),
            GamePersonaNames = _gamePersonaMappings.ToDictionary(m => m.ProcessName, m => m.PersonaName)
        };
        
        _debugLogger.Info($"Starting with {_gamePersonaMappings.Count} game mappings");

        // Disable controls
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        UsernameTextBox.IsEnabled = false;
        PasswordBox.IsEnabled = false;
        RememberMeCheckBox.IsEnabled = false;

        AppendStatus("Starting Steam Persona Switcher...");
        
        await _service.StartAsync(config);
        
        // Update tray menu to reflect service started
        UpdateTrayContextMenu();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Info("Stop button clicked");
        
        StopButton.IsEnabled = false;
        AppendStatus("Stopping service...");

        try
        {
            // Stop with a timeout
            var stopTask = _service.StopAsync();
            var timeoutTask = Task.Delay(5000); // 5 second timeout
            
            var completedTask = await Task.WhenAny(stopTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _debugLogger.Info("Stop operation timed out");
                AppendStatus("Warning: Stop operation timed out. The service may still be running.");
            }
            else
            {
                _debugLogger.Info("Service stopped successfully");
                AppendStatus("Service stopped.");
            }
        }
        catch (Exception ex)
        {
            _debugLogger.Info($"Stop error: {ex.Message}");
            AppendStatus($"Error stopping service: {ex.Message}");
        }
        finally
        {
            StartButton.IsEnabled = true;
            UsernameTextBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            RememberMeCheckBox.IsEnabled = true;
            
            // Update tray menu to reflect service stopped
            UpdateTrayContextMenu();
        }
    }

    private void GamePersonaGrid_InitializingNewItem(object sender, InitializingNewItemEventArgs e)
    {
        if (e.NewItem is GamePersonaMapping mapping)
        {
            // New items start as not committed - explicitly set to false
            mapping.IsCommitted = false;
            _debugLogger.Info($"Initializing new game persona mapping row - IsCommitted: {mapping.IsCommitted}, IsNotEmpty: {mapping.IsNotEmpty}, ShowRemoveButton: {mapping.ShowRemoveButton}");
        }
    }

    private void GamePersonaGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit)
        {
            if (e.Row.Item is GamePersonaMapping mapping)
            {
                // If both fields are empty, schedule removal of the empty row
                if (string.IsNullOrWhiteSpace(mapping.ProcessName) && string.IsNullOrWhiteSpace(mapping.PersonaName))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_gamePersonaMappings.Contains(mapping))
                        {
                            _gamePersonaMappings.Remove(mapping);
                            _debugLogger.Info("Removed empty game mapping row");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                // If only one field is filled, warn and remove
                else if (string.IsNullOrWhiteSpace(mapping.ProcessName) || string.IsNullOrWhiteSpace(mapping.PersonaName))
                {
                    AppendStatus($"⚠️ Both process name and persona name are required.");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_gamePersonaMappings.Contains(mapping))
                        {
                            _gamePersonaMappings.Remove(mapping);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                // Both fields are filled - validate for duplicates
                else
                {
                    // Check for duplicates (excluding the current item)
                    var duplicate = _gamePersonaMappings.FirstOrDefault(m => 
                        m != mapping && 
                        m.ProcessName.Equals(mapping.ProcessName, StringComparison.OrdinalIgnoreCase));
                    
                    if (duplicate != null)
                    {
                        AppendStatus($"⚠️ A mapping for '{mapping.ProcessName}' already exists.");
                        // Remove the duplicate entry
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (_gamePersonaMappings.Contains(mapping))
                            {
                                _gamePersonaMappings.Remove(mapping);
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    else
                    {
                        _debugLogger.Info($"Game mapping added/updated: {mapping.ProcessName} -> {mapping.PersonaName}");
                        AppendStatus($"✓ Added mapping: {mapping.ProcessName} → {mapping.PersonaName}");
                        // Mark as committed so the Remove button appears
                        mapping.IsCommitted = true;
                    }
                }
            }
        }
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is GamePersonaMapping mapping)
        {
            _debugLogger.Info($"Removing game mapping: {mapping.ProcessName}");
            _gamePersonaMappings.Remove(mapping);
            _debugLogger.Info($"Game mapping removed. Total mappings: {_gamePersonaMappings.Count}");
            AppendStatus($"Removed mapping: {mapping.ProcessName}");
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        // Check if the dragged data contains files
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            // Only accept .exe files
            if (files != null && files.Any(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null)
            return;

        foreach (var file in files)
        {
            if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                _debugLogger.Debug($"Skipping non-exe file: {file}");
                continue;
            }

            var exeName = Path.GetFileName(file);
            _debugLogger.Info($"Dropped exe file: {exeName}");

            // Check if this exe is already in the mapping list
            var existingMapping = _gamePersonaMappings.FirstOrDefault(m => 
                m.ProcessName.Equals(exeName, StringComparison.OrdinalIgnoreCase));

            if (existingMapping != null)
            {
                // Highlight the existing entry instead of adding a duplicate
                _debugLogger.Info($"Exe already exists in mapping list: {exeName}");
                HighlightExistingMapping(existingMapping);
                AppendStatus($"ℹ️ '{exeName}' is already in the mapping list - highlighted existing entry.");
            }
            else
            {
                // Add new mapping with empty persona name (user will fill it in)
                var newMapping = new GamePersonaMapping
                {
                    ProcessName = exeName,
                    PersonaName = string.Empty,
                    IsCommitted = false
                };
                _gamePersonaMappings.Add(newMapping);
                _debugLogger.Info($"Added new mapping for: {exeName}");
                AppendStatus($"✓ Added '{exeName}' to mapping list - please enter a persona name.");

                // Select the new row and focus the persona name cell for editing
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    GamePersonaGrid.SelectedItem = newMapping;
                    GamePersonaGrid.ScrollIntoView(newMapping);
                    
                    // Begin edit on the persona name column (column index 1)
                    var row = GamePersonaGrid.ItemContainerGenerator.ContainerFromItem(newMapping) as DataGridRow;
                    if (row != null)
                    {
                        GamePersonaGrid.CurrentCell = new DataGridCellInfo(newMapping, GamePersonaGrid.Columns[1]);
                        GamePersonaGrid.BeginEdit();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
    }

    private void HighlightExistingMapping(GamePersonaMapping mapping)
    {
        // Select the existing row
        GamePersonaGrid.SelectedItem = mapping;
        GamePersonaGrid.ScrollIntoView(mapping);

        // Flash highlight effect to draw attention to the row
        var row = GamePersonaGrid.ItemContainerGenerator.ContainerFromItem(mapping) as DataGridRow;
        if (row != null)
        {
            var originalBackground = row.Background;
            
            // Create a flash animation
            var flashAnimation = new System.Windows.Media.Animation.ColorAnimation
            {
                From = Colors.Yellow,
                To = (originalBackground as SolidColorBrush)?.Color ?? Colors.White,
                Duration = TimeSpan.FromMilliseconds(1000),
                AutoReverse = false
            };

            var brush = new SolidColorBrush(Colors.Yellow);
            row.Background = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, flashAnimation);
        }
    }

    private void DiscoverRunningGames_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Info("Discover Running Games clicked");
        
        try
        {
            var discoveredGames = DiscoverPotentialGames();
            
            if (discoveredGames.Count == 0)
            {
                AppendStatus("ℹ️ No potential games found running. Try launching a game first.");
                return;
            }

            // Show a dialog to let the user select which games to add
            var dialog = new GameDiscoveryDialog(discoveredGames, _gamePersonaMappings);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                var selectedGames = dialog.SelectedGames;
                int addedCount = 0;
                
                foreach (var game in selectedGames)
                {
                    // Check if already exists
                    var existing = _gamePersonaMappings.FirstOrDefault(m => 
                        m.ProcessName.Equals(game.ProcessName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existing != null)
                    {
                        HighlightExistingMapping(existing);
                        _debugLogger.Info($"Game already exists: {game.ProcessName}");
                    }
                    else
                    {
                        var newMapping = new GamePersonaMapping
                        {
                            ProcessName = game.ProcessName,
                            PersonaName = string.Empty,
                            IsCommitted = false
                        };
                        _gamePersonaMappings.Add(newMapping);
                        addedCount++;
                        _debugLogger.Info($"Added discovered game: {game.ProcessName}");
                    }
                }
                
                if (addedCount > 0)
                {
                    AppendStatus($"✓ Added {addedCount} game(s) to the mapping list. Please set persona names.");
                }
            }
        }
        catch (Exception ex)
        {
            _debugLogger.Error($"Error discovering games: {ex.Message}");
            AppendStatus($"❌ Error discovering games: {ex.Message}");
        }
    }

    /// <summary>
    /// Discovers running processes that are likely to be games using various heuristics.
    /// </summary>
    private List<DiscoveredGame> DiscoverPotentialGames()
    {
        var potentialGames = new List<DiscoveredGame>();
        var processes = Process.GetProcesses();
        
        // Known system/utility processes to exclude
        var excludedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "svchost", "csrss", "wininit", "services", "lsass", "smss", "winlogon",
            "dwm", "explorer", "taskhostw", "sihost", "fontdrvhost", "ctfmon",
            "conhost", "RuntimeBroker", "SearchHost", "StartMenuExperienceHost",
            "ShellExperienceHost", "TextInputHost", "SystemSettings", "ApplicationFrameHost",
            "WindowsTerminal", "powershell", "pwsh", "cmd", "Code", "devenv",
            "msedge", "chrome", "firefox", "opera", "brave", "spotify", "discord",
            "slack", "teams", "zoom", "skype", "steam", "steamwebhelper",
            "EpicGamesLauncher", "Origin", "GOGGalaxy", "UbisoftConnect", "Battle.net",
            "SecurityHealthSystray", "SecurityHealthService", "MsMpEng", "NisSrv",
            "OneDrive", "Dropbox", "GoogleDriveSync", "iCloudServices",
            "NVIDIA Web Helper", "nvcontainer", "NVDisplay.Container",
            "AMD Radeon Software", "RadeonSoftware", "amdow", "amdfendrsr",
            "Realtek", "audiodg", "IAStorDataMgrSvc", "IntelAudioService",
            "SearchIndexer", "SearchProtocolHost", "SearchFilterHost",
            "WmiPrvSE", "dllhost", "msiexec", "TrustedInstaller",
            "SteamPersonaSwitcher", "notepad", "calc", "mspaint"
        };

        // Known game-related keywords in process names or paths
        var gameKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "game", "play", "steam", "unity", "unreal", "engine", "client",
            "launcher", "64", "x64", "dx11", "dx12", "vulkan", "opengl"
        };

        // Common game install paths
        var gamePathIndicators = new string[]
        {
            @"\steamapps\common\",
            @"\Epic Games\",
            @"\GOG Galaxy\Games\",
            @"\Origin Games\",
            @"\Ubisoft Game Launcher\games\",
            @"\Games\",
            @"\Program Files\Steam\",
            @"\Program Files (x86)\Steam\"
        };

        foreach (var process in processes)
        {
            try
            {
                // Skip excluded processes
                if (excludedProcesses.Contains(process.ProcessName))
                    continue;

                // Skip processes with no main window (likely background services)
                // But don't skip fullscreen games which may report no window handle
                string? mainWindowTitle = null;
                IntPtr mainWindowHandle = IntPtr.Zero;
                
                try
                {
                    mainWindowHandle = process.MainWindowHandle;
                    mainWindowTitle = process.MainWindowTitle;
                }
                catch
                {
                    // Some processes don't allow access to window info
                }

                string? processPath = null;
                try
                {
                    processPath = process.MainModule?.FileName;
                }
                catch
                {
                    // Access denied for some system processes
                }

                // Calculate a "game likelihood" score
                int score = 0;
                var reasons = new List<string>();

                // Check if process has a visible window with a title
                if (mainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(mainWindowTitle))
                {
                    score += 20;
                    reasons.Add("Has visible window");
                }

                // Check process path for game-related indicators
                if (!string.IsNullOrEmpty(processPath))
                {
                    foreach (var indicator in gamePathIndicators)
                    {
                        if (processPath.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                        {
                            score += 50;
                            reasons.Add($"In game directory");
                            break;
                        }
                    }
                }

                // Check process name for game-related keywords
                foreach (var keyword in gameKeywords)
                {
                    if (process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 10;
                        reasons.Add($"Name contains '{keyword}'");
                        break;
                    }
                }

                // Check memory usage (games typically use more memory)
                try
                {
                    var workingSet = process.WorkingSet64 / (1024 * 1024); // MB
                    if (workingSet > 500)
                    {
                        score += 15;
                        reasons.Add($"High memory ({workingSet}MB)");
                    }
                    else if (workingSet > 200)
                    {
                        score += 5;
                        reasons.Add($"Medium memory ({workingSet}MB)");
                    }
                }
                catch { }

                // Check if process is using significant CPU (might indicate active game)
                // Note: This is a snapshot, not continuous monitoring
                try
                {
                    // Check thread count - games often have many threads
                    if (process.Threads.Count > 20)
                    {
                        score += 10;
                        reasons.Add($"Many threads ({process.Threads.Count})");
                    }
                }
                catch { }

                // Only include if score is high enough
                if (score >= 30)
                {
                    var exeName = process.ProcessName + ".exe";
                    
                    // Don't add duplicates
                    if (!potentialGames.Any(g => g.ProcessName.Equals(exeName, StringComparison.OrdinalIgnoreCase)))
                    {
                        potentialGames.Add(new DiscoveredGame
                        {
                            ProcessName = exeName,
                            WindowTitle = mainWindowTitle ?? "",
                            ProcessPath = processPath ?? "",
                            Score = score,
                            Reasons = reasons
                        });
                    }
                }
            }
            catch
            {
                // Skip processes we can't access
            }
        }

        // Sort by score (highest first)
        return potentialGames.OrderByDescending(g => g.Score).ToList();
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Info("Save Config clicked");
        try
        {
            var config = new Config
            {
                Username = UsernameTextBox.Text.Trim(),
                CheckIntervalSeconds = int.TryParse(CheckIntervalTextBox.Text, out int interval) ? interval : 10,
                DefaultPersonaName = DefaultPersonaTextBox.Text.Trim(),
                GamePersonaNames = _gamePersonaMappings.ToDictionary(m => m.ProcessName, m => m.PersonaName)
            };
            
            _debugLogger.Info($"Saving {_gamePersonaMappings.Count} game persona mappings");

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = serializer.Serialize(config);
            File.WriteAllText(_configFilePath, yaml);
            
            // Save tray preferences
            SaveTrayPreferences();
            
            AppendStatus($"✓ Configuration saved successfully to {_configFilePath}");
        }
        catch (Exception ex)
        {
            AppendStatus($"❌ Failed to save config: {ex.Message}");
        }
    }

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Info("Load Config clicked");
        LoadConfiguration();
    }

    private void ClearCredentials_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Info("Clear Credentials clicked");
        
        // If countdown is already in progress, cancel it
        if (_isClearCredentialsPending)
        {
            CancelClearCredentials();
            return;
        }
        
        try
        {
            if (!_credentialManager.HasSavedCredentials())
            {
                AppendStatus("ℹ️ No saved credentials found.");
                return;
            }

            // Start the countdown
            StartClearCredentialsCountdown();
        }
        catch (Exception ex)
        {
            AppendStatus($"❌ Failed to initiate credential deletion: {ex.Message}");
        }
    }

    private void StartClearCredentialsCountdown()
    {
        _isClearCredentialsPending = true;
        _clearCredentialsCountdown = 10;
        
        // Update button appearance
        UpdateClearCredentialsButton();
        
        AppendStatus("⚠️ Credentials will be deleted in 10 seconds. Click the button again to cancel.");
        _debugLogger.Info("Clear credentials countdown started");
        
        // Create and start the timer
        _clearCredentialsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clearCredentialsTimer.Tick += ClearCredentialsTimer_Tick;
        _clearCredentialsTimer.Start();
    }

    private void ClearCredentialsTimer_Tick(object? sender, EventArgs e)
    {
        _clearCredentialsCountdown--;
        
        if (_clearCredentialsCountdown <= 0)
        {
            // Time's up - execute the deletion
            _clearCredentialsTimer?.Stop();
            _clearCredentialsTimer = null;
            _isClearCredentialsPending = false;
            
            ExecuteClearCredentials();
            ResetClearCredentialsButton();
        }
        else
        {
            // Update the button with remaining time
            UpdateClearCredentialsButton();
        }
    }

    private void UpdateClearCredentialsButton()
    {
        ClearCredentialsButton.Content = $"UNDO ({_clearCredentialsCountdown}...)";
        ClearCredentialsButton.Foreground = new SolidColorBrush(Colors.Red);
        ClearCredentialsButton.FontWeight = FontWeights.Bold;
        ClearCredentialsButton.ToolTip = "Click to cancel credential deletion";
    }

    private void ResetClearCredentialsButton()
    {
        ClearCredentialsButton.Content = "Clear Credentials";
        ClearCredentialsButton.Foreground = new SolidColorBrush(Colors.Black);
        ClearCredentialsButton.FontWeight = FontWeights.Normal;
        ClearCredentialsButton.ToolTip = "Delete saved login credentials";
    }

    private void CancelClearCredentials()
    {
        _clearCredentialsTimer?.Stop();
        _clearCredentialsTimer = null;
        _isClearCredentialsPending = false;
        
        ResetClearCredentialsButton();
        
        AppendStatus("✓ Credential deletion cancelled.");
        _debugLogger.Info("Clear credentials countdown cancelled by user");
    }

    private void ExecuteClearCredentials()
    {
        try
        {
            _debugLogger.Info("Executing credential deletion...");
            
            _credentialManager.DeleteCredentials();
            
            // Also delete saved session
            if (_sessionManager.HasSavedSession())
            {
                _sessionManager.DeleteSession();
                AppendStatus("Saved session also deleted.");
            }
            
            RememberMeCheckBox.IsChecked = false;
            PasswordBox.Password = string.Empty;
            AppendStatus("✓ Saved credentials and session deleted. Steam Guard will be required on next login.");
            _debugLogger.Info("Credentials and session deleted successfully");
        }
        catch (Exception ex)
        {
            AppendStatus($"❌ Failed to delete credentials: {ex.Message}");
            _debugLogger.Error($"Failed to delete credentials: {ex.Message}");
        }
    }

    private void LoadConfiguration()
    {
        try
        {
            _debugLogger.Info("LoadConfiguration called");
            if (File.Exists(_configFilePath))
            {
                _debugLogger.Info($"Loading config from: {_configFilePath}");
                var yaml = File.ReadAllText(_configFilePath);
                _debugLogger.Info($"YAML content length: {yaml.Length}");
                
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var config = deserializer.Deserialize<Config>(yaml);

                if (config != null)
                {
                    _debugLogger.Info($"Config deserialized. GamePersonaNames count: {config.GamePersonaNames?.Count ?? 0}");
                    
                    UsernameTextBox.Text = config.Username;
                    CheckIntervalTextBox.Text = config.CheckIntervalSeconds.ToString();
                    DefaultPersonaTextBox.Text = config.DefaultPersonaName;

                    _gamePersonaMappings.Clear();
                    _debugLogger.Info("Cleared existing mappings");
                    
                    if (config.GamePersonaNames != null)
                    {
                        foreach (var kvp in config.GamePersonaNames)
                        {
                            _debugLogger.Info($"Adding mapping: {kvp.Key} -> {kvp.Value}");
                            _gamePersonaMappings.Add(new GamePersonaMapping
                            {
                                ProcessName = kvp.Key,
                                PersonaName = kvp.Value,
                                IsCommitted = true
                            });
                        }
                    }
                    
                    _debugLogger.Info($"Loaded {_gamePersonaMappings.Count} game persona mappings");
                    _debugLogger.Info($"GamePersonaGrid.Items.Count: {GamePersonaGrid.Items.Count}");

                    AppendStatus($"Configuration loaded from {_configFilePath}");
                }
            }
            else
            {
                AppendStatus($"No configuration file found at {_configFilePath}. Using default settings.");
                
                // Add some default game mappings
                _gamePersonaMappings.Add(new GamePersonaMapping 
                { 
                    ProcessName = "hl2.exe", 
                    PersonaName = "Playing Half-Life 2",
                    IsCommitted = true
                });
                _gamePersonaMappings.Add(new GamePersonaMapping 
                { 
                    ProcessName = "csgo.exe", 
                    PersonaName = "Playing CS:GO",
                    IsCommitted = true
                });
                _debugLogger.Info($"Added {_gamePersonaMappings.Count} default game persona mappings");
            }
            
            // Load tray preferences FIRST (before loading credentials/auto-starting)
            LoadTrayPreferences();
            
            // Load saved credentials if they exist (and possibly auto-start service)
            LoadSavedCredentials();
        }
        catch (Exception ex)
        {
            AppendStatus($"Failed to load config: {ex.Message}");
        }
    }

    private async void LoadSavedCredentials()
    {
        try
        {
            _debugLogger.Info("Checking for saved credentials...");
            if (_credentialManager.HasSavedCredentials())
            {
                _debugLogger.Info("Saved credentials found, loading...");
                var credentials = _credentialManager.LoadCredentials();
                if (credentials.HasValue)
                {
                    UsernameTextBox.Text = credentials.Value.Username;
                    PasswordBox.Password = credentials.Value.Password;
                    RememberMeCheckBox.IsChecked = true;
                    AppendStatus("Saved credentials loaded securely.");
                    _debugLogger.Info($"Credentials loaded for user: {credentials.Value.Username}");
                    
                    // Auto-start the service if enabled
                    if (AutoStartServiceCheckBox.IsChecked == true)
                    {
                        _debugLogger.Info("Auto-starting service with saved credentials...");
                        AppendStatus("🚀 Auto-starting service with saved credentials...");
                        await AutoStartService();
                    }
                    else
                    {
                        _debugLogger.Info("Auto-start disabled in settings");
                    }
                }
            }
            else
            {
                _debugLogger.Info("No saved credentials found");
            }
        }
        catch (Exception ex)
        {
            _debugLogger.Info($"Failed to load saved credentials: {ex.Message}");
            AppendStatus($"⚠️ Could not load saved credentials: {ex.Message}. Please re-enter your credentials.");
        }
    }

    private async Task AutoStartService()
    {
        try
        {
            // Validate that we have the necessary inputs
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text) || 
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                _debugLogger.Info("Auto-start skipped: Missing credentials");
                return;
            }

            if (!int.TryParse(CheckIntervalTextBox.Text, out int interval) || interval < 1)
            {
                _debugLogger.Info("Auto-start skipped: Invalid interval, using default 5 seconds");
                CheckIntervalTextBox.Text = "5";
                interval = 5;
            }

            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            // Create config from UI
            var config = new Config
            {
                Username = username,
                Password = password,
                CheckIntervalSeconds = interval,
                DefaultPersonaName = DefaultPersonaTextBox.Text.Trim(),
                GamePersonaNames = _gamePersonaMappings.ToDictionary(
                    m => m.ProcessName,
                    m => m.PersonaName
                )
            };

            // Update button states before starting
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            UsernameTextBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            RememberMeCheckBox.IsEnabled = false;

            // Start the service
            _debugLogger.Info($"Auto-starting service for user: {username}");
            await _service.StartAsync(config);
        }
        catch (Exception ex)
        {
            _debugLogger.Info($"Auto-start failed: {ex.Message}");
            AppendStatus($"❌ Auto-start failed: {ex.Message}");
            
            // Reset button states on error
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            UsernameTextBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            RememberMeCheckBox.IsEnabled = true;
        }
    }

    private void SaveTrayPreferences()
    {
        try
        {
            var prefs = new Dictionary<string, bool>
            {
                ["AutoStartService"] = AutoStartServiceCheckBox.IsChecked ?? false,
                ["StartMinimized"] = StartMinimizedCheckBox.IsChecked ?? false,
                ["MinimizeToTray"] = MinimizeToTrayCheckBox.IsChecked ?? false,
                ["CloseToTray"] = CloseToTrayCheckBox.IsChecked ?? false
            };
            
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = serializer.Serialize(prefs);
            File.WriteAllText(_trayPreferencesPath, yaml);
        }
        catch (Exception ex)
        {
            AppendStatus($"Failed to save tray preferences: {ex.Message}");
        }
    }

    private void LoadTrayPreferences()
    {
        try
        {
            if (File.Exists(_trayPreferencesPath))
            {
                var yaml = File.ReadAllText(_trayPreferencesPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var prefs = deserializer.Deserialize<Dictionary<string, bool>>(yaml);
                
                if (prefs != null)
                {
                    if (prefs.TryGetValue("AutoStartService", out bool autoStart))
                        AutoStartServiceCheckBox.IsChecked = autoStart;
                    if (prefs.TryGetValue("StartMinimized", out bool startMin))
                        StartMinimizedCheckBox.IsChecked = startMin;
                    if (prefs.TryGetValue("MinimizeToTray", out bool minToTray))
                        MinimizeToTrayCheckBox.IsChecked = minToTray;
                    if (prefs.TryGetValue("CloseToTray", out bool closeToTray))
                        CloseToTrayCheckBox.IsChecked = closeToTray;
                }
            }
            
            // Apply parent-child hierarchy after loading values
            bool isMinimizeToTrayEnabled = MinimizeToTrayCheckBox.IsChecked == true;
            StartMinimizedCheckBox.IsEnabled = isMinimizeToTrayEnabled;
            CloseToTrayCheckBox.IsEnabled = isMinimizeToTrayEnabled;
            StartMinimizedCheckBox.Opacity = isMinimizeToTrayEnabled ? 1.0 : 0.5;
            CloseToTrayCheckBox.Opacity = isMinimizeToTrayEnabled ? 1.0 : 0.5;
            
            // Load Run at Startup preference from registry
            LoadRunAtStartupPreference();
        }
        catch
        {
            // Silently fail - use defaults
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        
        if (WindowState == WindowState.Minimized && MinimizeToTrayCheckBox.IsChecked == true)
        {
            Hide();
            
            // Only show notification the first time per session (regardless of minimize or close)
            if (!_hasShownTrayNotification)
            {
                _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                    "Application minimized to tray", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                _hasShownTrayNotification = true;
                _debugLogger.Info("Sent to tray via minimize - notification shown");
            }
            else
            {
                _debugLogger.Info("Sent to tray via minimize - notification suppressed (already shown this session)");
            }
        }
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        // If "Close to tray" is enabled AND parent "Minimize to tray" is enabled AND we're not forcing an actual close
        if (MinimizeToTrayCheckBox.IsChecked == true && 
            CloseToTrayCheckBox.IsChecked == true && 
            !_forceActualClose)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
            
            // Only show notification the first time per session (regardless of minimize or close)
            if (!_hasShownTrayNotification)
            {
                _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                    "Application minimized to tray. Right-click the tray icon to exit.", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                _hasShownTrayNotification = true;
                _debugLogger.Info("Sent to tray via close - notification shown");
            }
            else
            {
                _debugLogger.Info("Sent to tray via close - notification suppressed (already shown this session)");
            }
        }
        else
        {
            // Actually closing - cleanup asynchronously to avoid UI freeze
            if (!_isShuttingDown)
            {
                e.Cancel = true; // Cancel the first close attempt
                _isShuttingDown = true;
                
                _debugLogger.Info("Window closing, stopping service...");
                
                // Stop service asynchronously
                await _service.StopAsync();
                
                _trayIcon?.Dispose();
                
                _debugLogger.Info("Service stopped, closing application");
                
                // Now actually close
                Application.Current.Shutdown();
            }
        }
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayIcon_TrayContextMenuOpen(object sender, RoutedEventArgs e)
    {
        // Update menu items based on current state
        UpdateTrayContextMenu();
        
        // Handle DPI-aware positioning of the context menu
        if (_trayIcon?.ContextMenu != null)
        {
            _trayIcon.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            
            // Get the current mouse position using P/Invoke
            if (GetCursorPos(out POINT cursorPos))
            {
                // Convert to WPF coordinates with DPI awareness
                var dpiScale = VisualTreeHelper.GetDpi(this);
                var scaledX = cursorPos.X / dpiScale.DpiScaleX;
                var scaledY = cursorPos.Y / dpiScale.DpiScaleY;
                
                _trayIcon.ContextMenu.HorizontalOffset = scaledX;
                _trayIcon.ContextMenu.VerticalOffset = scaledY;
                
                _debugLogger.Info($"Context menu positioning - Cursor: ({cursorPos.X}, {cursorPos.Y}), DPI Scale: {dpiScale.DpiScaleX}x{dpiScale.DpiScaleY}, Scaled: ({scaledX}, {scaledY})");
            }
        }
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _forceActualClose = true;
        Close();
    }

    private void UpdateTrayContextMenu()
    {
        // Access the menu item from the tray icon's context menu
        if (_trayIcon?.ContextMenu != null && 
            _trayIcon.ContextMenu.Items.Count > 2 && 
            _trayIcon.ContextMenu.Items[2] is MenuItem startStopMenuItem)
        {
            bool isRunning = _service.IsRunning;
            
            if (isRunning)
            {
                startStopMenuItem.Header = "Stop Service";
                _debugLogger.Debug("Tray menu: Set to 'Stop Service'");
            }
            else
            {
                startStopMenuItem.Header = "Start Service";
                _debugLogger.Debug("Tray menu: Set to 'Start Service'");
            }
        }
    }

    private void TrayStartStop_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Info("Tray Start/Stop menu item clicked");
        
        if (_service.IsRunning)
        {
            // Stop the service - call Stop_Click directly (it's async void, so we can't await it)
            _debugLogger.Info("Stopping service via tray menu");
            Stop_Click(sender, e);
            
            // Show notification that service is stopping
            _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                "Service stopping...", 
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
        else
        {
            // Start the service
            _debugLogger.Info("Starting service via tray menu");
            
            // Check if we have saved credentials
            if (_credentialManager.HasSavedCredentials())
            {
                var credentials = _credentialManager.LoadCredentials();
                if (credentials.HasValue)
                {
                    var (savedUsername, savedPassword) = credentials.Value;
                    if (!string.IsNullOrWhiteSpace(savedUsername) && !string.IsNullOrWhiteSpace(savedPassword))
                    {
                        // Auto-populate credentials
                        UsernameTextBox.Text = savedUsername;
                        PasswordBox.Password = savedPassword;
                        RememberMeCheckBox.IsChecked = true;
                        
                        _debugLogger.Info("Loaded saved credentials for tray start");
                        
                        // Show notification that service is starting
                        _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                            "Starting service...", 
                            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                        
                        // Trigger the start (it's async void, so we can't await it)
                        Start_Click(sender, e);
                    }
                    else
                    {
                        _debugLogger.Warning("No valid saved credentials found for tray start");
                        _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                            "Please open the window and enter your credentials to start the service.", 
                            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Warning);
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                    }
                }
                else
                {
                    _debugLogger.Info("Could not load saved credentials for tray start - showing window");
                    _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                        "Please open the window and enter your credentials to start the service.", 
                        Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                }
            }
            else
            {
                _debugLogger.Info("No saved credentials for tray start - showing window");
                _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                    "Please open the window and enter your credentials to start the service.", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }
        
        // Menu will be updated via OnConnectionStateChanged event
    }

    private void RunAtStartup_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            var runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (runKey == null)
            {
                AppendStatus("⚠️ Could not access Windows startup registry key.");
                return;
            }

            const string appName = "SteamPersonaSwitcher";
            
            if (RunAtStartupCheckBox.IsChecked == true)
            {
                // Add to startup
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    runKey.SetValue(appName, $"\"{exePath}\"");
                    _debugLogger.Info($"Added to Windows startup: {exePath}");
                    AppendStatus("✓ App will run when Windows starts.");
                }
            }
            else
            {
                // Remove from startup
                if (runKey.GetValue(appName) != null)
                {
                    runKey.DeleteValue(appName);
                    _debugLogger.Info("Removed from Windows startup");
                    AppendStatus("✓ App removed from Windows startup.");
                }
            }
            
            runKey.Close();
        }
        catch (Exception ex)
        {
            _debugLogger.Info($"Failed to update startup setting: {ex.Message}");
            AppendStatus($"❌ Failed to update startup setting: {ex.Message}");
        }
    }

    private void MinimizeToTray_Changed(object sender, RoutedEventArgs e)
    {
        bool isEnabled = MinimizeToTrayCheckBox.IsChecked == true;
        
        // Update child checkbox states
        StartMinimizedCheckBox.IsEnabled = isEnabled;
        CloseToTrayCheckBox.IsEnabled = isEnabled;
        
        // Visual feedback - reduce opacity when disabled
        StartMinimizedCheckBox.Opacity = isEnabled ? 1.0 : 0.5;
        CloseToTrayCheckBox.Opacity = isEnabled ? 1.0 : 0.5;
        
        _debugLogger.Info($"Minimize to tray {(isEnabled ? "enabled" : "disabled")}");
        
        // Save preferences immediately
        SaveTrayPreferences();
    }

    private void LoadRunAtStartupPreference()
    {
        try
        {
            var runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            if (runKey != null)
            {
                const string appName = "SteamPersonaSwitcher";
                var value = runKey.GetValue(appName);
                RunAtStartupCheckBox.IsChecked = value != null;
                runKey.Close();
                
                if (value != null)
                {
                    _debugLogger.Info("App is set to run at Windows startup");
                }
            }
        }
        catch (Exception ex)
        {
            _debugLogger.Info($"Failed to check startup setting: {ex.Message}");
        }
    }

    // Debug Panel Event Handlers
    private void DebugToggle_Click(object sender, RoutedEventArgs e)
    {
        _isDebugPanelVisible = !_isDebugPanelVisible;
        
        if (_isDebugPanelVisible)
        {
            ShowDebugPanel();
        }
        else
        {
            HideDebugPanel();
        }
        
        // Update button text
        DebugToggleButton.Content = _isDebugPanelVisible ? "Hide Debug Log" : "Show Debug Log";
        
        SaveDebugPanelPreferences();
    }

    private void ShowDebugPanel()
    {
        if (_debugPanelWindow == null)
        {
            _debugPanelWindow = new DebugPanelWindow
            {
                Owner = this,
                Width = _debugPanelWidth
            };
        }
        
        _debugPanelWindow.Show();
        _debugPanelWindow.UpdatePosition();
        _debugLogger.Info("Debug panel opened");
    }

    private void HideDebugPanel()
    {
        if (_debugPanelWindow != null)
        {
            // Save current width before hiding
            _debugPanelWidth = _debugPanelWindow.Width;
            _debugPanelWindow.Hide();
        }
        _debugLogger.Info("Debug panel closed");
    }

    public void OnDebugPanelClosed()
    {
        _isDebugPanelVisible = false;
        DebugToggleButton.Content = "Show Debug Log";
        SaveDebugPanelPreferences();
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        _debugPanelWindow?.UpdatePosition();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _debugPanelWindow?.UpdatePosition();
    }

    private void SaveDebugPanelPreferences()
    {
        try
        {
            var preferences = new
            {
                IsDebugPanelVisible = _isDebugPanelVisible,
                DebugPanelWidth = _debugPanelWidth
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            
            var yaml = serializer.Serialize(preferences);
            var debugPrefsPath = Path.Combine(_configDirectory, "debug_preferences.yaml");
            File.WriteAllText(debugPrefsPath, yaml);
        }
        catch (Exception ex)
        {
            _debugLogger.Error($"Failed to save debug panel preferences: {ex.Message}");
        }
    }

    private void LoadDebugPanelPreferences()
    {
        try
        {
            var debugPrefsPath = Path.Combine(_configDirectory, "debug_preferences.yaml");
            if (File.Exists(debugPrefsPath))
            {
                var yaml = File.ReadAllText(debugPrefsPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                
                var preferences = deserializer.Deserialize<Dictionary<string, object>>(yaml);
                
                if (preferences.TryGetValue("isDebugPanelVisible", out var visible))
                {
                    _isDebugPanelVisible = Convert.ToBoolean(visible);
                }
                
                if (preferences.TryGetValue("debugPanelWidth", out var width))
                {
                    _debugPanelWidth = Convert.ToDouble(width);
                }

                // Apply visibility preference on startup
                if (_isDebugPanelVisible)
                {
                    ShowDebugPanel();
                }

                // Update button text to match state
                DebugToggleButton.Content = _isDebugPanelVisible ? "Hide Debug Log" : "Show Debug Log";

                _debugLogger.Info($"Debug panel preferences loaded (Visible: {_isDebugPanelVisible}, Width: {_debugPanelWidth})");
            }
        }
        catch (Exception ex)
        {
            _debugLogger.Warning($"Failed to load debug panel preferences: {ex.Message}");
        }
    }

    // P/Invoke for getting cursor position
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}

