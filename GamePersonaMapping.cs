using System.ComponentModel;

namespace SteamPersonaSwitcher;

/// <summary>
/// Represents a mapping between a game process name and a Steam persona name
/// </summary>
public class GamePersonaMapping : INotifyPropertyChanged
{
    private string _processName = string.Empty;
    private string _personaName = string.Empty;
    private bool _isCommitted = false;

    public string ProcessName
    {
        get => _processName;
        set
        {
            if (_processName != value)
            {
                _processName = value;
                OnPropertyChanged(nameof(ProcessName));
                OnPropertyChanged(nameof(IsNotEmpty));
                OnPropertyChanged(nameof(ShowRemoveButton));
            }
        }
    }

    public string PersonaName
    {
        get => _personaName;
        set
        {
            if (_personaName != value)
            {
                _personaName = value;
                OnPropertyChanged(nameof(PersonaName));
                OnPropertyChanged(nameof(IsNotEmpty));
                OnPropertyChanged(nameof(ShowRemoveButton));
            }
        }
    }

    /// <summary>
    /// Returns true if both ProcessName and PersonaName are not empty
    /// </summary>
    public bool IsNotEmpty => !string.IsNullOrWhiteSpace(_processName) && !string.IsNullOrWhiteSpace(_personaName);

    /// <summary>
    /// Returns true if this item has been committed to the collection (not a new placeholder row)
    /// </summary>
    public bool IsCommitted
    {
        get => _isCommitted;
        set
        {
            if (_isCommitted != value)
            {
                _isCommitted = value;
                OnPropertyChanged(nameof(IsCommitted));
                OnPropertyChanged(nameof(ShowRemoveButton));
            }
        }
    }

    /// <summary>
    /// Returns true if the Remove button should be visible (committed AND not empty)
    /// </summary>
    public bool ShowRemoveButton => _isCommitted && IsNotEmpty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
