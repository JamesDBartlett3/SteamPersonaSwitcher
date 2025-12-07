using System.Collections.Generic;

namespace SteamPersonaSwitcher;

/// <summary>
/// Represents a game process discovered by the game detection system.
/// </summary>
public class DiscoveredGame
{
    /// <summary>
    /// The process name including .exe extension (e.g., "game.exe").
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// The main window title of the process, if available.
    /// </summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// The full path to the process executable, if accessible.
    /// </summary>
    public string ProcessPath { get; set; } = string.Empty;

    /// <summary>
    /// A score indicating how likely this process is to be a game (higher = more likely).
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// List of reasons why this process was flagged as a potential game.
    /// </summary>
    public List<string> Reasons { get; set; } = new();

    /// <summary>
    /// Whether the user has selected this game for adding to the mapping list.
    /// </summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// Gets a display-friendly name for the game.
    /// </summary>
    public string DisplayName => !string.IsNullOrEmpty(WindowTitle) ? WindowTitle : ProcessName;

    /// <summary>
    /// Gets a summary of detection reasons for tooltip display.
    /// </summary>
    public string ReasonsSummary => string.Join(", ", Reasons);
}
