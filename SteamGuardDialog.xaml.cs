using System;
using System.Windows;

namespace SteamPersonaSwitcher;

public partial class SteamGuardDialog : Window
{
    public string Code { get; private set; } = string.Empty;

    public SteamGuardDialog(string message)
    {
        InitializeComponent();
        MessageTextBlock.Text = message;
        CodeTextBox.Focus();
        
        DebugLogger.Instance.Info($"[AUTHENTICATOR] Showing Steam Guard dialog: {message}");
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CodeTextBox.Text))
        {
            MessageBox.Show("Please enter a code.", "Validation", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Code = CodeTextBox.Text.Trim();
        DebugLogger.Instance.Info($"[AUTHENTICATOR] User entered code: {Code}");
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DebugLogger.Instance.Info("[AUTHENTICATOR] User cancelled Steam Guard");
        DialogResult = false;
        Close();
    }
}

