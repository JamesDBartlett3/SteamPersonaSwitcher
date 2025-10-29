using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        
        // Check if should start minimized
        if (StartMinimizedCheckBox.IsChecked == true)
        {
            WindowState = WindowState.Minimized;
            Hide();
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
            
            // Show tray notification
            _trayIcon?.ShowBalloonTip("Persona Changed", 
                $"Now: {personaName}", 
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
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
            Title = isConnected 
                ? "Steam Persona Switcher - Connected" 
                : "Steam Persona Switcher - Disconnected";
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
        try
        {
            if (!_credentialManager.HasSavedCredentials())
            {
                AppendStatus("ℹ️ No saved credentials found.");
                return;
            }

            AppendStatus("⚠️ Delete saved credentials? This will require re-entering username/password and Steam Guard on next login.");
            _debugLogger.Info("Deleting credentials...");
            
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
            
            // Load saved credentials if they exist
            LoadSavedCredentials();
            
            // Load tray preferences
            LoadTrayPreferences();
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
        // If "Close to tray" is enabled AND we're not forcing an actual close, minimize to tray instead
        if (CloseToTrayCheckBox.IsChecked == true && !_forceActualClose)
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

