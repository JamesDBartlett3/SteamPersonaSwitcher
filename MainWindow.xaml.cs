using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SteamPersonaSwitcher;

public partial class MainWindow : Window
{
    private readonly SteamPersonaService _service;
    private ObservableCollection<GamePersonaMapping> _gamePersonaMappings;
    private bool _isClosingToTray = false;
    private bool _isShuttingDown = false;
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly string _trayPreferencesPath;
    private readonly CredentialManager _credentialManager;
    private readonly SessionManager _sessionManager;

    private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        
        Console.WriteLine("[UI] MainWindow initializing...");
        
        // Set up AppData directory
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamPersonaSwitcher");
        _configFilePath = Path.Combine(_configDirectory, "config.yaml");
        _trayPreferencesPath = Path.Combine(_configDirectory, "tray_preferences.yaml");
        
        Console.WriteLine($"[UI] Config directory: {_configDirectory}");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
            Console.WriteLine("[UI] Created config directory");
        }
        
        // Initialize credential manager
        _credentialManager = new CredentialManager(_configDirectory);
        Console.WriteLine("[UI] Credential manager initialized");
        
        // Initialize session manager
        _sessionManager = new SessionManager(_configDirectory);
        Console.WriteLine("[UI] Session manager initialized");
        
        // Get tray icon from resources
        _trayIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)FindResource("TrayIcon");
        
        _service = new SteamPersonaService();
        _service.SetSessionManager(_sessionManager);
        _gamePersonaMappings = new ObservableCollection<GamePersonaMapping>();
        
        GamePersonaGrid.ItemsSource = _gamePersonaMappings;
        Console.WriteLine("[UI] Game persona grid initialized");
        
        // Subscribe to service events
        _service.StatusChanged += OnStatusChanged;
        _service.PersonaChanged += OnPersonaChanged;
        _service.ErrorOccurred += OnErrorOccurred;
        _service.ConnectionStateChanged += OnConnectionStateChanged;
        
        // Load configuration on startup
        LoadConfiguration();
        
        // Check if should start minimized
        if (StartMinimizedCheckBox.IsChecked == true)
        {
            WindowState = WindowState.Minimized;
            Hide();
        }
    }

    private void OnStatusChanged(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine($"[UI EVENT] Status: {message}");
            AppendStatus($"[{DateTime.Now:HH:mm:ss}] {message}");
        });
    }

    private void OnPersonaChanged(object? sender, string personaName)
    {
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine($"[UI EVENT] Persona changed to: {personaName}");
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
            Console.WriteLine($"[UI EVENT] ERROR: {error}");
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
        Console.WriteLine("[UI] Start button clicked");
        
        // Validate inputs
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            Console.WriteLine("[UI] Validation failed: No username");
            AppendStatus("⚠️ Please enter your Steam username.");
            return;
        }

        if (string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            Console.WriteLine("[UI] Validation failed: No password");
            AppendStatus("⚠️ Please enter your Steam password.");
            return;
        }

        if (!int.TryParse(CheckIntervalTextBox.Text, out int interval) || interval < 1)
        {
            Console.WriteLine("[UI] Validation failed: Invalid interval");
            AppendStatus("⚠️ Please enter a valid check interval (minimum 1 second).");
            return;
        }

        Console.WriteLine($"[UI] Starting with username: {UsernameTextBox.Text.Trim()}, interval: {interval}s");

        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        // Save or delete credentials based on Remember Me checkbox
        try
        {
            if (RememberMeCheckBox.IsChecked == true)
            {
                Console.WriteLine("[UI] Saving credentials (Remember Me is checked)");
                _credentialManager.SaveCredentials(username, password);
                AppendStatus("Credentials saved securely.");
            }
            else
            {
                // If Remember Me is unchecked, delete any saved credentials
                if (_credentialManager.HasSavedCredentials())
                {
                    Console.WriteLine("[UI] Deleting saved credentials (Remember Me is unchecked)");
                    _credentialManager.DeleteCredentials();
                    AppendStatus("Saved credentials removed.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UI] Failed to save credentials: {ex.Message}");
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
        
        Console.WriteLine($"[UI] Starting with {_gamePersonaMappings.Count} game mappings");

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
        Console.WriteLine("[UI] Stop button clicked");
        
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
                Console.WriteLine("[UI] Stop operation timed out");
                AppendStatus("Warning: Stop operation timed out. The service may still be running.");
            }
            else
            {
                Console.WriteLine("[UI] Service stopped successfully");
                AppendStatus("Service stopped.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UI] Stop error: {ex.Message}");
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

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var processName = NewGameProcessTextBox.Text.Trim();
        var personaName = NewPersonaNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(personaName))
        {
            AppendStatus("⚠️ Please enter both process name and persona name.");
            return;
        }

        Console.WriteLine($"[UI] Adding game mapping: {processName} -> {personaName}");

        // Check if already exists
        if (_gamePersonaMappings.Any(m => m.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"[UI] Duplicate mapping detected for {processName}");
            AppendStatus($"⚠️ A mapping for '{processName}' already exists.");
            return;
        }

        _gamePersonaMappings.Add(new GamePersonaMapping 
        { 
            ProcessName = processName, 
            PersonaName = personaName 
        });
        
        Console.WriteLine($"[UI] Game mapping added. Total mappings: {_gamePersonaMappings.Count}");
        
        // Clear inputs
        NewGameProcessTextBox.Text = "game.exe";
        NewPersonaNameTextBox.Text = "Playing Game";
        
        AppendStatus($"Added mapping: {processName} → {personaName}");
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string processName)
        {
            Console.WriteLine($"[UI] Removing game mapping: {processName}");
            var item = _gamePersonaMappings.FirstOrDefault(m => m.ProcessName == processName);
            if (item != null)
            {
                _gamePersonaMappings.Remove(item);
                Console.WriteLine($"[UI] Game mapping removed. Total mappings: {_gamePersonaMappings.Count}");
                AppendStatus($"Removed mapping: {processName}");
            }
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("[UI] Save Config clicked");
        try
        {
            var config = new Config
            {
                Username = UsernameTextBox.Text.Trim(),
                CheckIntervalSeconds = int.TryParse(CheckIntervalTextBox.Text, out int interval) ? interval : 10,
                DefaultPersonaName = DefaultPersonaTextBox.Text.Trim(),
                GamePersonaNames = _gamePersonaMappings.ToDictionary(m => m.ProcessName, m => m.PersonaName)
            };
            
            Console.WriteLine($"[Config] Saving {_gamePersonaMappings.Count} game persona mappings");

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
        Console.WriteLine("[UI] Load Config clicked");
        LoadConfiguration();
    }

    private void ClearCredentials_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("[UI] Clear Credentials clicked");
        try
        {
            if (!_credentialManager.HasSavedCredentials())
            {
                AppendStatus("ℹ️ No saved credentials found.");
                return;
            }

            AppendStatus("⚠️ Delete saved credentials? This will require re-entering username/password and Steam Guard on next login.");
            Console.WriteLine("[UI] Deleting credentials...");
            
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
            Console.WriteLine("[UI] Credentials and session deleted successfully");
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
            Console.WriteLine("[Config] LoadConfiguration called");
            if (File.Exists(_configFilePath))
            {
                Console.WriteLine($"[Config] Loading config from: {_configFilePath}");
                var yaml = File.ReadAllText(_configFilePath);
                Console.WriteLine($"[Config] YAML content length: {yaml.Length}");
                
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var config = deserializer.Deserialize<Config>(yaml);

                if (config != null)
                {
                    Console.WriteLine($"[Config] Config deserialized. GamePersonaNames count: {config.GamePersonaNames?.Count ?? 0}");
                    
                    UsernameTextBox.Text = config.Username;
                    CheckIntervalTextBox.Text = config.CheckIntervalSeconds.ToString();
                    DefaultPersonaTextBox.Text = config.DefaultPersonaName;

                    _gamePersonaMappings.Clear();
                    Console.WriteLine("[Config] Cleared existing mappings");
                    
                    if (config.GamePersonaNames != null)
                    {
                        foreach (var kvp in config.GamePersonaNames)
                        {
                            Console.WriteLine($"[Config] Adding mapping: {kvp.Key} -> {kvp.Value}");
                            _gamePersonaMappings.Add(new GamePersonaMapping
                            {
                                ProcessName = kvp.Key,
                                PersonaName = kvp.Value
                            });
                        }
                    }
                    
                    Console.WriteLine($"[Config] Loaded {_gamePersonaMappings.Count} game persona mappings");
                    Console.WriteLine($"[Config] GamePersonaGrid.Items.Count: {GamePersonaGrid.Items.Count}");

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
                    PersonaName = "Playing Half-Life 2" 
                });
                _gamePersonaMappings.Add(new GamePersonaMapping 
                { 
                    ProcessName = "csgo.exe", 
                    PersonaName = "Playing CS:GO" 
                });
                Console.WriteLine($"[Config] Added {_gamePersonaMappings.Count} default game persona mappings");
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

    private void LoadSavedCredentials()
    {
        try
        {
            Console.WriteLine("[UI] Checking for saved credentials...");
            if (_credentialManager.HasSavedCredentials())
            {
                Console.WriteLine("[UI] Saved credentials found, loading...");
                var credentials = _credentialManager.LoadCredentials();
                if (credentials.HasValue)
                {
                    UsernameTextBox.Text = credentials.Value.Username;
                    PasswordBox.Password = credentials.Value.Password;
                    RememberMeCheckBox.IsChecked = true;
                    AppendStatus("Saved credentials loaded securely.");
                    Console.WriteLine($"[UI] Credentials loaded for user: {credentials.Value.Username}");
                }
            }
            else
            {
                Console.WriteLine("[UI] No saved credentials found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UI] Failed to load saved credentials: {ex.Message}");
            AppendStatus($"⚠️ Could not load saved credentials: {ex.Message}. Please re-enter your credentials.");
        }
    }

    private void SaveTrayPreferences()
    {
        try
        {
            var prefs = new Dictionary<string, bool>
            {
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
                    if (prefs.TryGetValue("StartMinimized", out bool startMin))
                        StartMinimizedCheckBox.IsChecked = startMin;
                    if (prefs.TryGetValue("MinimizeToTray", out bool minToTray))
                        MinimizeToTrayCheckBox.IsChecked = minToTray;
                    if (prefs.TryGetValue("CloseToTray", out bool closeToTray))
                        CloseToTrayCheckBox.IsChecked = closeToTray;
                }
            }
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
            _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                "Application minimized to tray", 
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (CloseToTrayCheckBox.IsChecked == true && !_isClosingToTray)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
            _trayIcon?.ShowBalloonTip("Steam Persona Switcher", 
                "Application minimized to tray. Right-click the tray icon to exit.", 
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
        else
        {
            // Actually closing - cleanup asynchronously to avoid UI freeze
            if (!_isShuttingDown)
            {
                e.Cancel = true; // Cancel the first close attempt
                _isShuttingDown = true;
                
                Console.WriteLine("[UI] Window closing, stopping service...");
                
                // Stop service asynchronously
                await _service.StopAsync();
                
                _trayIcon?.Dispose();
                
                Console.WriteLine("[UI] Service stopped, closing application");
                
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

    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _isClosingToTray = true;
        Close();
    }
}
