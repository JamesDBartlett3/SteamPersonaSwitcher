using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace SteamPersonaSwitcher;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class LogEntry : INotifyPropertyChanged
{
    private string _timestamp = string.Empty;
    private string _level = string.Empty;
    private string _message = string.Empty;
    private Brush _color = Brushes.Black;
    private string _icon = string.Empty;

    public string Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); }
    }

    public string Level
    {
        get => _level;
        set { _level = value; OnPropertyChanged(nameof(Level)); }
    }

    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(nameof(Message)); }
    }

    public Brush Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(nameof(Color)); }
    }

    public string Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(nameof(Icon)); }
    }

    public string FullText => $"[{Timestamp}] {Icon} {Level}: {Message}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DebugLogger
{
    private static DebugLogger? _instance;
    private static readonly object _lock = new object();
    private readonly ObservableCollection<LogEntry> _logEntries;
    private const int MaxLogEntries = 1000; // Prevent memory issues
    private bool _autoScroll = true;

    public static DebugLogger Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new DebugLogger();
                }
            }
            return _instance;
        }
    }

    private DebugLogger()
    {
        _logEntries = new ObservableCollection<LogEntry>();
    }

    public ObservableCollection<LogEntry> LogEntries => _logEntries;

    public bool AutoScroll
    {
        get => _autoScroll;
        set => _autoScroll = value;
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        // Fallback for cases where dispatcher isn't available
        try
        {
            if (Application.Current?.Dispatcher == null || !Application.Current.Dispatcher.CheckAccess())
            {
                // Not on UI thread or app not initialized, queue for later
                return;
            }
        }
        catch
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                Level = level.ToString().ToUpper(),
                Message = message,
                Color = GetColorForLevel(level),
                Icon = GetIconForLevel(level)
            };

            _logEntries.Add(entry);

            // Trim old entries if we exceed max
            while (_logEntries.Count > MaxLogEntries)
            {
                _logEntries.RemoveAt(0);
            }
        });
    }

    public void Debug(string message) => Log(message, LogLevel.Debug);
    public void Info(string message) => Log(message, LogLevel.Info);
    public void Warning(string message) => Log(message, LogLevel.Warning);
    public void Error(string message) => Log(message, LogLevel.Error);

    public void Clear()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _logEntries.Clear();
            Log("Debug log cleared", LogLevel.Info);
        });
    }

    private static Brush GetColorForLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => new SolidColorBrush(Color.FromRgb(128, 128, 128)),   // Gray
            LogLevel.Info => new SolidColorBrush(Color.FromRgb(220, 220, 220)),    // Light Gray (changed from black)
            LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 200, 100)), // Light Orange
            LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 100, 100)),   // Light Red
            _ => new SolidColorBrush(Color.FromRgb(220, 220, 220))
        };
    }

    private static string GetIconForLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => "🔧",
            LogLevel.Info => "ℹ️",
            LogLevel.Warning => "⚠️",
            LogLevel.Error => "❌",
            _ => "•"
        };
    }

    public string GetAllLogsAsText()
    {
        var logs = new System.Text.StringBuilder();
        foreach (var entry in _logEntries)
        {
            logs.AppendLine(entry.FullText);
        }
        return logs.ToString();
    }
}
