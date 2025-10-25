using System;
using System.Threading.Tasks;
using SteamKit2.Authentication;

namespace SteamPersonaSwitcher;

/// <summary>
/// Custom Steam authenticator that logs messages to console instead of showing GUI prompts
/// Since we're using persistent sessions, authentication should rarely be needed
/// </summary>
public class GuiSteamAuthenticator : IAuthenticator
{
    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        var message = previousCodeWasIncorrect
            ? "⚠️ Previous code was incorrect. Please check your Steam Mobile App for the new code."
            : "🔐 Steam Guard required: Please open your Steam Mobile App and enter the code shown.";
        
        Console.WriteLine($"[STEAM GUARD] {message}");
        
        // Return empty string - this will fail authentication and trigger the persistent session to be cleared
        // User will need to re-authenticate properly next time
        return Task.FromResult(string.Empty);
    }

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
    {
        var message = previousCodeWasIncorrect
            ? $"⚠️ Previous code was incorrect. Please check your email ({email}) for a new code."
            : $"📧 Steam Guard code sent to {email}. Please check your email and enter the code.";
        
        Console.WriteLine($"[STEAM GUARD] {message}");
        
        // Return empty string - this will fail authentication and trigger retry
        return Task.FromResult(string.Empty);
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        Console.WriteLine("[STEAM GUARD] 📱 Please confirm this login in your Steam Mobile App.");
        Console.WriteLine("[STEAM GUARD] Waiting for mobile confirmation...");
        
        // Return true to indicate we're waiting - Steam will poll for confirmation
        return Task.FromResult(true);
    }
}
