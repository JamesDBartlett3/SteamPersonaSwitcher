using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Authentication;

class Program
{
    static Config? config;
    static SteamClient? steamClient;
    static CallbackManager? manager;
    static SteamUser? steamUser;
    static SteamFriends? steamFriends;
    
    static bool isRunning = true;
    static bool isLoggedIn = false;
    static string currentPersonaName = string.Empty;
    static Timer? gameCheckTimer;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Steam Persona Changer v1.0");
        Console.WriteLine("==========================\n");

        // Load configuration
        if (!File.Exists("config.json"))
        {
            Console.WriteLine("config.json not found! Creating default config...");
            CreateDefaultConfig();
            Console.WriteLine("Please edit config.json with your Steam credentials and game list.");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        try
        {
            var configJson = File.ReadAllText("config.json");
            config = JsonSerializer.Deserialize<Config>(configJson);

            if (config == null || string.IsNullOrEmpty(config.Username))
            {
                Console.WriteLine("Invalid configuration! Please check config.json");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading config.json: {ex.Message}");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        // Initialize SteamKit
        steamClient = new SteamClient();
        manager = new CallbackManager(steamClient);
        steamUser = steamClient.GetHandler<SteamUser>();
        steamFriends = steamClient.GetHandler<SteamFriends>();

        // Register callbacks
        manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nShutting down gracefully...");
            isRunning = false;
            gameCheckTimer?.Dispose();
            steamUser?.LogOff();
        };

        Console.WriteLine("Connecting to Steam...");
        steamClient.Connect();

        // Main callback loop
        while (isRunning)
        {
            manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
        }

        gameCheckTimer?.Dispose();
        Console.WriteLine("Goodbye!");
    }

    static void CreateDefaultConfig()
    {
        var defaultConfig = new Config
        {
            Username = "your_username_here",
            Password = "your_password_here",
            CheckIntervalSeconds = 10,
            GamePersonaNames = new Dictionary<string, string>
            {
                { "hl2.exe", "Playing Half-Life 2" },
                { "csgo.exe", "Playing CS:GO" },
                { "dota2.exe", "Playing Dota 2" },
                { "EldenRing.exe", "Getting wrecked in Elden Ring" }
            },
            DefaultPersonaName = "Not Gaming"
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText("config.json", json);
    }

    static async void OnConnected(SteamClient.ConnectedCallback callback)
    {
        Console.WriteLine($"Connected to Steam! Logging in as '{config!.Username}'...");

        try
        {
            // Use modern authentication flow
            var authSession = await steamClient!.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = config.Username,
                    Password = config.Password,
                    IsPersistentSession = false,
                    Authenticator = new UserConsoleAuthenticator(),
                });

            var pollResponse = await authSession.PollingWaitForResultAsync();

            steamUser!.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
            isRunning = false;
        }
    }

    static void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Console.WriteLine("Disconnected from Steam. Attempting reconnect in 5 seconds...");
        isLoggedIn = false;
        
        Thread.Sleep(5000);
        
        if (isRunning)
        {
            Console.WriteLine("Reconnecting...");
            steamClient?.Connect();
        }
    }

    static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            Console.WriteLine($"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");
            isRunning = false;
            return;
        }

        Console.WriteLine("Successfully logged on!");
        isLoggedIn = true;
    }

    static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        Console.WriteLine($"Logged off of Steam: {callback.Result}");
        isLoggedIn = false;
    }

    static void OnAccountInfo(SteamUser.AccountInfoCallback callback)
    {
        // Set initial persona state to Online
        steamFriends!.SetPersonaState(EPersonaState.Online);
        
        Console.WriteLine($"Account info received. Current name: {callback.PersonaName}");
        Console.WriteLine($"Starting game monitoring (checking every {config!.CheckIntervalSeconds} seconds)...");
        Console.WriteLine("Press Ctrl+C to exit.\n");

        // Start monitoring games
        gameCheckTimer = new Timer(CheckForRunningGames, null, 
            TimeSpan.Zero, 
            TimeSpan.FromSeconds(config!.CheckIntervalSeconds));
    }

    static void CheckForRunningGames(object? state)
    {
        if (!isLoggedIn || config == null)
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
                    
                    if (config.GamePersonaNames.ContainsKey(processName))
                    {
                        newPersonaName = config.GamePersonaNames[processName];
                        break;
                    }
                }
                catch
                {
                    // Ignore processes we can't access
                }
            }

            // Use default name if no game is running
            newPersonaName ??= config.DefaultPersonaName;

            // Only update if the name has changed
            if (newPersonaName != currentPersonaName)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Changing persona name to: {newPersonaName}");
                steamFriends?.SetPersonaName(newPersonaName);
                currentPersonaName = newPersonaName;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking games: {ex.Message}");
        }
    }
}
