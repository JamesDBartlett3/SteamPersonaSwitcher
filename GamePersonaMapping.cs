using System.ComponentModel;

namespace SteamPersonaSwitcher;

/// <summary>
/// Represents a mapping between a game process name and a Steam persona name
/// </summary>
public class GamePersonaMapping : INotifyPropertyChanged
{
    private string _processName = string.Empty;
    private string _personaName = string.Empty;

    public string ProcessName
    {
        get => _processName;
        set
        {
            if (_processName != value)
            {
                _processName = value;
                OnPropertyChanged(nameof(ProcessName));
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
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
