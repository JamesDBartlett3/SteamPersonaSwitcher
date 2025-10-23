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
    private ObservableCollection<KeyValuePair<string, string>> _gamePersonaMappings;
    private bool _isClosingToTray = false;
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly string _trayPreferencesPath;
    private readonly CredentialManager _credentialManager;

    private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        
        // Set up AppData directory
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamPersonaSwitcher");
        _configFilePath = Path.Combine(_configDirectory, "config.yaml");
        _trayPreferencesPath = Path.Combine(_configDirectory, "tray_preferences.yaml");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
        
        // Initialize credential manager
        _credentialManager = new CredentialManager(_configDirectory);
        
        // Get tray icon from resources
        _trayIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)FindResource("TrayIcon");
        
        _service = new SteamPersonaService();
        _gamePersonaMappings = new ObservableCollection<KeyValuePair<string, string>>();
        
        GamePersonaGrid.ItemsSource = _gamePersonaMappings;
        
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
            AppendStatus($"[{DateTime.Now:HH:mm:ss}] {message}");
        });
    }

    private void OnPersonaChanged(object? sender, string personaName)
    {
        Dispatcher.Invoke(() =>
        {
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
            AppendStatus($"[{DateTime.Now:HH:mm:ss}] ❌ ERROR: {error}");
            MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            Title = isConnected 
                ? "Steam Persona Switcher - Connected" 
                : "Steam Persona Switcher - Disconnected";
        });
    }

    private void AppendStatus(string message)
    {
        StatusTextBox.AppendText(message + Environment.NewLine);
        StatusTextBox.ScrollToEnd();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            MessageBox.Show("Please enter your Steam username.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            MessageBox.Show("Please enter your Steam password.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(CheckIntervalTextBox.Text, out int interval) || interval < 1)
        {
            MessageBox.Show("Please enter a valid check interval (minimum 1 second).", 
                "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        // Save or delete credentials based on Remember Me checkbox
        try
        {
            if (RememberMeCheckBox.IsChecked == true)
            {
                _credentialManager.SaveCredentials(username, password);
                AppendStatus("Credentials saved securely.");
            }
            else
            {
                // If Remember Me is unchecked, delete any saved credentials
                if (_credentialManager.HasSavedCredentials())
                {
                    _credentialManager.DeleteCredentials();
                    AppendStatus("Saved credentials removed.");
                }
            }
        }
        catch (Exception ex)
        {
            AppendStatus($"Warning: Failed to save credentials: {ex.Message}");
        }

        // Create config from UI
        var config = new Config
        {
            Username = username,
            Password = password,
            CheckIntervalSeconds = interval,
            DefaultPersonaName = DefaultPersonaTextBox.Text.Trim(),
            GamePersonaNames = _gamePersonaMappings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

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
        StopButton.IsEnabled = false;
        StartButton.IsEnabled = true;
        UsernameTextBox.IsEnabled = true;
        PasswordBox.IsEnabled = true;
        RememberMeCheckBox.IsEnabled = true;

        await _service.StopAsync();
    }

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var processName = NewGameProcessTextBox.Text.Trim();
        var personaName = NewPersonaNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(personaName))
        {
            MessageBox.Show("Please enter both process name and persona name.", 
                "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if already exists
        if (_gamePersonaMappings.Any(kvp => kvp.Key.Equals(processName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"A mapping for '{processName}' already exists.", 
                "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _gamePersonaMappings.Add(new KeyValuePair<string, string>(processName, personaName));
        
        // Clear inputs
        NewGameProcessTextBox.Text = string.Empty;
        NewPersonaNameTextBox.Text = string.Empty;
        
        AppendStatus($"Added mapping: {processName} → {personaName}");
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string processName)
        {
            var item = _gamePersonaMappings.FirstOrDefault(kvp => kvp.Key == processName);
            if (!item.Equals(default(KeyValuePair<string, string>)))
            {
                _gamePersonaMappings.Remove(item);
                AppendStatus($"Removed mapping: {processName}");
            }
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = new Config
            {
                Username = UsernameTextBox.Text.Trim(),
                CheckIntervalSeconds = int.TryParse(CheckIntervalTextBox.Text, out int interval) ? interval : 10,
                DefaultPersonaName = DefaultPersonaTextBox.Text.Trim(),
                GamePersonaNames = _gamePersonaMappings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = serializer.Serialize(config);
            File.WriteAllText(_configFilePath, yaml);
            
            // Save tray preferences
            SaveTrayPreferences();
            
            AppendStatus($"Configuration saved to {_configFilePath}");
            MessageBox.Show($"Configuration saved successfully!\n\nLocation: {_configFilePath}", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppendStatus($"Failed to save config: {ex.Message}");
            MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        LoadConfiguration();
    }

    private void ClearCredentials_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_credentialManager.HasSavedCredentials())
            {
                MessageBox.Show("No saved credentials found.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "Are you sure you want to delete your saved credentials?\n\n" +
                "You will need to re-enter your username and password next time.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _credentialManager.DeleteCredentials();
                RememberMeCheckBox.IsChecked = false;
                PasswordBox.Password = string.Empty;
                AppendStatus("Saved credentials deleted.");
                MessageBox.Show("Saved credentials have been deleted.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            AppendStatus($"Failed to delete credentials: {ex.Message}");
            MessageBox.Show($"Failed to delete credentials: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var yaml = File.ReadAllText(_configFilePath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var config = deserializer.Deserialize<Config>(yaml);

                if (config != null)
                {
                    UsernameTextBox.Text = config.Username;
                    CheckIntervalTextBox.Text = config.CheckIntervalSeconds.ToString();
                    DefaultPersonaTextBox.Text = config.DefaultPersonaName;

                    _gamePersonaMappings.Clear();
                    foreach (var kvp in config.GamePersonaNames)
                    {
                        _gamePersonaMappings.Add(kvp);
                    }

                    AppendStatus($"Configuration loaded from {_configFilePath}");
                }
            }
            else
            {
                AppendStatus($"No configuration file found at {_configFilePath}. Using default settings.");
                
                // Add some default game mappings
                _gamePersonaMappings.Add(new KeyValuePair<string, string>("hl2.exe", "Playing Half-Life 2"));
                _gamePersonaMappings.Add(new KeyValuePair<string, string>("csgo.exe", "Playing CS:GO"));
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
            if (_credentialManager.HasSavedCredentials())
            {
                var credentials = _credentialManager.LoadCredentials();
                if (credentials.HasValue)
                {
                    UsernameTextBox.Text = credentials.Value.Username;
                    PasswordBox.Password = credentials.Value.Password;
                    RememberMeCheckBox.IsChecked = true;
                    AppendStatus("Saved credentials loaded securely.");
                }
            }
        }
        catch (Exception ex)
        {
            AppendStatus($"Failed to load saved credentials: {ex.Message}");
            MessageBox.Show(
                $"Could not load saved credentials: {ex.Message}\n\nPlease re-enter your credentials.",
                "Credential Load Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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

    private void Window_Closing(object sender, CancelEventArgs e)
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
            // Actually closing - cleanup
            _service.StopAsync().Wait();
            _trayIcon?.Dispose();
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
