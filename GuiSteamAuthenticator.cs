using System;
using System.Threading.Tasks;
using System.Windows;
using SteamKit2.Authentication;

namespace SteamPersonaSwitcher;

/// <summary>
/// Custom Steam authenticator that shows prompts in the GUI instead of console
/// </summary>
public class GuiSteamAuthenticator : IAuthenticator
{
    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        var tcs = new TaskCompletionSource<string>();
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            var message = previousCodeWasIncorrect
                ? "The previous code was incorrect. Please enter your Steam Guard code:"
                : "Please enter your Steam Guard code from the Steam Mobile App:";
            
            var dialog = new SteamGuardDialog(message);
            if (dialog.ShowDialog() == true)
            {
                tcs.SetResult(dialog.Code);
            }
            else
            {
                tcs.SetCanceled();
            }
        });
        
        return tcs.Task;
    }

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
    {
        var tcs = new TaskCompletionSource<string>();
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            var message = previousCodeWasIncorrect
                ? $"The previous code was incorrect. Please enter the code sent to {email}:"
                : $"Please enter the code sent to {email}:";
            
            var dialog = new SteamGuardDialog(message);
            if (dialog.ShowDialog() == true)
            {
                tcs.SetResult(dialog.Code);
            }
            else
            {
                tcs.SetCanceled();
            }
        });
        
        return tcs.Task;
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(
                "Please confirm this login in the Steam Mobile App.\n\n" +
                "Click OK after you've approved it, or Cancel to abort.",
                "Steam Guard - Mobile Confirmation Required",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            
            Console.WriteLine("[AUTHENTICATOR] Waiting for mobile confirmation...");
            tcs.SetResult(result == MessageBoxResult.OK);
        });
        
        return tcs.Task;
    }
}
