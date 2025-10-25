using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamPersonaSwitcher;

public partial class DebugPanelWindow : Window
{
    private readonly DebugLogger _debugLogger;
    private Window? _owner;

    public DebugPanelWindow()
    {
        InitializeComponent();
        
        _debugLogger = DebugLogger.Instance;
        DebugLogItems.ItemsSource = _debugLogger.LogEntries;
        _debugLogger.LogEntries.CollectionChanged += DebugLogEntries_CollectionChanged;
        
        Loaded += DebugPanelWindow_Loaded;
    }

    private void DebugPanelWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _owner = Owner;
        if (_owner != null)
        {
            // Position the panel to the right of the owner window
            UpdatePosition();
            
            // Track owner window changes
            _owner.LocationChanged += Owner_LocationChanged;
            _owner.SizeChanged += Owner_SizeChanged;
            _owner.StateChanged += Owner_StateChanged;
        }
    }

    public void UpdatePosition()
    {
        if (_owner != null)
        {
            // Position at the right edge of the owner window
            Left = _owner.Left + _owner.ActualWidth;
            Top = _owner.Top;
            Height = _owner.ActualHeight;
        }
    }

    private void Owner_LocationChanged(object? sender, EventArgs e)
    {
        UpdatePosition();
    }

    private void Owner_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePosition();
    }

    private void Owner_StateChanged(object? sender, EventArgs e)
    {
        if (_owner != null)
        {
            // Hide when owner is minimized, show when restored
            if (_owner.WindowState == WindowState.Minimized)
            {
                Hide();
            }
            else if (WindowState != _owner.WindowState)
            {
                Show();
                UpdatePosition();
            }
        }
    }

    private void ClearDebugLog_Click(object sender, RoutedEventArgs e)
    {
        _debugLogger.Clear();
    }

    private void CopyDebugLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var allLogs = _debugLogger.GetAllLogsAsText();
            Clipboard.SetText(allLogs);
            _debugLogger.Info("Debug log copied to clipboard");
        }
        catch (Exception ex)
        {
            _debugLogger.Error($"Failed to copy logs: {ex.Message}");
        }
    }

    private void DebugLogEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll to bottom if enabled
        if (AutoScrollCheckBox.IsChecked == true && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            Dispatcher.InvokeAsync(() =>
            {
                DebugScrollViewer.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void WordWrapCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Update the TextWrapping for all TextBlocks in the ItemsControl
        if (DebugLogItems != null && WordWrapCheckBox != null)
        {
            var isWrapping = WordWrapCheckBox.IsChecked == true;
            var wrapping = isWrapping ? TextWrapping.Wrap : TextWrapping.NoWrap;

            // Find all TextBlocks in the visual tree
            var itemsPresenter = FindVisualChild<ItemsPresenter>(DebugLogItems);
            if (itemsPresenter != null)
            {
                var panel = FindVisualChild<StackPanel>(itemsPresenter);
                if (panel != null)
                {
                    for (int i = 0; i < panel.Children.Count; i++)
                    {
                        var container = panel.Children[i] as ContentPresenter;
                        if (container != null)
                        {
                            var textBlock = FindVisualChild<TextBlock>(container);
                            if (textBlock != null)
                            {
                                textBlock.TextWrapping = wrapping;
                            }
                        }
                    }
                }
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Prevent closing, just hide instead
        e.Cancel = true;
        Hide();
        
        // Notify owner to update button state
        if (_owner is MainWindow mainWindow)
        {
            mainWindow.OnDebugPanelClosed();
        }
    }
}
