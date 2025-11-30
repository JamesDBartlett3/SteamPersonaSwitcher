using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SteamPersonaSwitcher;

public partial class GameDiscoveryDialog : Window
{
    private readonly ObservableCollection<DiscoveredGame> _discoveredGames;
    private readonly ObservableCollection<GamePersonaMapping> _existingMappings;

    public List<DiscoveredGame> SelectedGames => _discoveredGames.Where(g => g.IsSelected).ToList();

    public GameDiscoveryDialog(List<DiscoveredGame> discoveredGames, ObservableCollection<GamePersonaMapping> existingMappings)
    {
        InitializeComponent();
        
        _existingMappings = existingMappings;
        
        // Mark games that already exist in the mapping list
        foreach (var game in discoveredGames)
        {
            var alreadyExists = existingMappings.Any(m => 
                m.ProcessName.Equals(game.ProcessName, System.StringComparison.OrdinalIgnoreCase));
            
            if (alreadyExists)
            {
                game.IsSelected = false;
                game.Reasons.Insert(0, "Already in list");
            }
        }
        
        _discoveredGames = new ObservableCollection<DiscoveredGame>(discoveredGames);
        GamesListView.ItemsSource = _discoveredGames;
        
        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        var selectedCount = _discoveredGames.Count(g => g.IsSelected);
        var totalCount = _discoveredGames.Count;
        SelectionCountText.Text = $"{selectedCount} of {totalCount} selected";
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectionCount();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var game in _discoveredGames)
        {
            game.IsSelected = true;
        }
        GamesListView.Items.Refresh();
        UpdateSelectionCount();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var game in _discoveredGames)
        {
            game.IsSelected = false;
        }
        GamesListView.Items.Refresh();
        UpdateSelectionCount();
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        if (!SelectedGames.Any())
        {
            MessageBox.Show("Please select at least one game to add.", "No Selection", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
