using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Authentication;

namespace SteamPersonaSwitcher;

public class SteamPersonaService
{
    private SteamClient? _steamClient;
    private CallbackManager? _manager;
    private SteamUser? _steamUser;
    private SteamFriends? _steamFriends;
    private Timer? _gameCheckTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _callbackTask;
    private readonly SemaphoreSlim _authenticationLock = new SemaphoreSlim(1, 1);
    
    private bool _isRunning = false;
    private bool _isLoggedIn = false;
    private bool _isAuthenticating = false;
    private string _currentPersonaName = string.Empty;
    
    private Config? _config;

    // Events for UI updates
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? PersonaChanged;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<bool>? ConnectionStateChanged;

    public bool IsRunning => _isRunning;
    public bool IsLoggedIn => _isLoggedIn;
    public string CurrentPersonaName => _currentPersonaName;

    public Task StartAsync(Config config)
    {
        if (_isRunning)
        {
            RaiseStatus("Service is already running.");
            return Task.CompletedTask;
        }

        // Ensure we're in a clean state before starting
        if (_steamClient != null)
        {
            try
            {
                if (_steamClient.IsConnected)
                {
                    _steamClient.Disconnect();
                }
            }
            catch { /* Ignore errors during cleanup */ }
        }

        _config = config;
        _isRunning = true;
        _isLoggedIn = false;
        _currentPersonaName = string.Empty;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Initialize SteamKit
            _steamClient = new SteamClient();
            _manager = new CallbackManager(_steamClient);
            _steamUser = _steamClient.GetHandler<SteamUser>();
            _steamFriends = _steamClient.GetHandler<SteamFriends>();

            // Register callbacks
            _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            _manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

            RaiseStatus("Connecting to Steam...");
            _steamClient.Connect();

            // Start callback loop on background thread
            _callbackTask = Task.Run(() => CallbackLoop(_cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            RaiseError($"Failed to start service: {ex.Message}");
            _isRunning = false;
        }
        
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        RaiseStatus("Stopping service...");
        _isRunning = false;
        
        // Cancel any ongoing operations (including authentication)
        _cancellationTokenSource?.Cancel();
        
        // Stop game monitoring
        _gameCheckTimer?.Dispose();
        _gameCheckTimer = null;

        // Wait for authentication to complete if in progress
        if (_isAuthenticating)
        {
            RaiseStatus("Waiting for authentication to complete...");
            try
            {
                // Wait up to 3 seconds for authentication to finish
                var waitTask = Task.Run(async () =>
                {
                    var maxWait = TimeSpan.FromSeconds(3);
                    var start = DateTime.Now;
                    while (_isAuthenticating && (DateTime.Now - start) < maxWait)
                    {
                        await Task.Delay(100);
                    }
                });
                await waitTask;
            }
            catch { /* Ignore timeout */ }
        }

        // Log off from Steam if connected
        if (_isLoggedIn && _steamUser != null)
        {
            try
            {
                _steamUser.LogOff();
                await Task.Delay(500); // Give it a moment to log off gracefully
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error during logoff: {ex.Message}");
            }
        }

        // Disconnect from Steam
        if (_steamClient != null && _steamClient.IsConnected)
        {
            try
            {
                _steamClient.Disconnect();
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error during disconnect: {ex.Message}");
            }
        }
        
        // Wait for callback task to complete
        if (_callbackTask != null)
        {
            try
            {
                await _callbackTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error stopping callback loop: {ex.Message}");
            }
        }

        // Cleanup
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _callbackTask = null;
        _isLoggedIn = false;
        
        RaiseStatus("Service stopped.");
        ConnectionStateChanged?.Invoke(this, false);
    }

    private void CallbackLoop(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            _manager?.RunWaitCallbacks(TimeSpan.FromSeconds(1));
        }
    }

    private async void OnConnected(SteamClient.ConnectedCallback callback)
    {
        // Check if we're still supposed to be running
        if (!_isRunning)
        {
            RaiseStatus("Connection received but service is stopping.");
            return;
        }

        // Prevent multiple simultaneous authentication attempts
        if (_isAuthenticating)
        {
            RaiseStatus("Authentication already in progress, ignoring duplicate connection.");
            return;
        }

        // Try to acquire the authentication lock
        if (!await _authenticationLock.WaitAsync(0))
        {
            RaiseStatus("Authentication already in progress.");
            return;
        }

        try
        {
            _isAuthenticating = true;
            RaiseStatus($"Connected to Steam! Logging in as '{_config!.Username}'...");
            ConnectionStateChanged?.Invoke(this, true);

            // Use modern authentication flow with cancellation support
            var authSession = await _steamClient!.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = _config.Username,
                    Password = _config.Password,
                    IsPersistentSession = false,
                    Authenticator = new UserConsoleAuthenticator(),
                });

            // Check again before waiting for result
            if (!_isRunning)
            {
                RaiseStatus("Authentication started but service is stopping.");
                return;
            }

            // Pass cancellation token to polling
            var pollResponse = await authSession.PollingWaitForResultAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

            // Final check before logging on
            if (!_isRunning)
            {
                RaiseStatus("Authentication completed but service is stopping.");
                return;
            }

            _steamUser!.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken,
            });
        }
        catch (TaskCanceledException)
        {
            RaiseStatus("Authentication cancelled.");
        }
        catch (OperationCanceledException)
        {
            RaiseStatus("Authentication cancelled.");
        }
        catch (Exception ex)
        {
            if (!_isRunning)
            {
                // Service is stopping, this is expected
                RaiseStatus("Authentication interrupted due to service stop.");
            }
            else
            {
                RaiseError($"Authentication failed: {ex.Message}");
                _ = Task.Run(async () => await StopAsync());
            }
        }
        finally
        {
            _isAuthenticating = false;
            _authenticationLock.Release();
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        _isLoggedIn = false;
        ConnectionStateChanged?.Invoke(this, false);
        
        // Only attempt to reconnect if the service is still supposed to be running
        // and the disconnect wasn't initiated by the user
        if (_isRunning && !callback.UserInitiated)
        {
            RaiseStatus("Disconnected from Steam. Attempting reconnect in 5 seconds...");
            // Schedule reconnection on a separate task to avoid blocking the callback thread
            _ = HandleReconnectAsync();
        }
        else if (callback.UserInitiated)
        {
            RaiseStatus("Disconnected from Steam (user initiated).");
        }
        else
        {
            RaiseStatus("Disconnected from Steam.");
        }
    }

    private async Task HandleReconnectAsync()
    {
        await Task.Delay(5000);
        
        // Check again after delay in case the service was stopped
        if (_isRunning)
        {
            RaiseStatus("Reconnecting...");
            _steamClient?.Connect();
        }
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            RaiseError($"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");
            _ = StopAsync();
            return;
        }

        RaiseStatus("Successfully logged on!");
        _isLoggedIn = true;
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        RaiseStatus($"Logged off of Steam: {callback.Result}");
        _isLoggedIn = false;
        ConnectionStateChanged?.Invoke(this, false);
    }

    private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
    {
        // Set initial persona state to Online
        _steamFriends!.SetPersonaState(EPersonaState.Online);
        
        RaiseStatus($"Account info received. Current name: {callback.PersonaName}");
        RaiseStatus($"Starting game monitoring (checking every {_config!.CheckIntervalSeconds} seconds)...");

        // Start monitoring games
        _gameCheckTimer = new Timer(CheckForRunningGames, null, 
            TimeSpan.Zero, 
            TimeSpan.FromSeconds(_config!.CheckIntervalSeconds));
    }

    private void CheckForRunningGames(object? state)
    {
        if (!_isLoggedIn || _config == null)
            return;

        try
        {
            var processes = Process.GetProcesses();
            string? newPersonaName = null;

            // Check if any configured game is running
            foreach (var process in processes)
            {
                try
                {
                    var processName = process.ProcessName + ".exe";
                    
                    if (_config.GamePersonaNames.ContainsKey(processName))
                    {
                        newPersonaName = _config.GamePersonaNames[processName];
                        break;
                    }
                }
                catch
                {
                    // Ignore processes we can't access
                }
            }

            // Use default name if no game is running
            newPersonaName ??= _config.DefaultPersonaName;

            // Only update if the name has changed
            if (newPersonaName != _currentPersonaName)
            {
                RaiseStatus($"Changing persona name to: {newPersonaName}");
                _steamFriends?.SetPersonaName(newPersonaName);
                _currentPersonaName = newPersonaName;
                PersonaChanged?.Invoke(this, newPersonaName);
            }
        }
        catch (Exception ex)
        {
            RaiseError($"Error checking games: {ex.Message}");
        }
    }

    private void RaiseStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }

    private void RaiseError(string message)
    {
        ErrorOccurred?.Invoke(this, message);
    }
}
