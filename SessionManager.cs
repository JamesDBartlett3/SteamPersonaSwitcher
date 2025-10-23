using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SteamPersonaSwitcher;

/// <summary>
/// Manages secure storage of Steam session tokens (refresh tokens) using Windows DPAPI.
/// This allows persistent sessions without requiring Steam Guard on every login.
/// </summary>
public class SessionManager
{
    private readonly string _sessionFilePath;

    public SessionManager(string appDataDirectory)
    {
        _sessionFilePath = Path.Combine(appDataDirectory, "session.dat");
    }

    /// <summary>
    /// Saves the refresh token securely using Windows DPAPI encryption.
    /// </summary>
    public void SaveSession(string username, string refreshToken)
    {
        try
        {
            // Combine username and refresh token with a separator
            var combinedData = $"{username}|||{refreshToken}";
            var dataBytes = Encoding.UTF8.GetBytes(combinedData);

            // Encrypt using DPAPI (current user scope)
            var encryptedData = ProtectedData.Protect(
                dataBytes,
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            // Save encrypted data to file
            File.WriteAllBytes(_sessionFilePath, encryptedData);
            Console.WriteLine($"[SESSION] Session saved for user: {username}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save session: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads and decrypts the saved session. Returns null if no session is saved.
    /// </summary>
    public (string Username, string RefreshToken)? LoadSession()
    {
        try
        {
            if (!File.Exists(_sessionFilePath))
            {
                Console.WriteLine("[SESSION] No saved session found");
                return null;
            }

            // Read encrypted data
            var encryptedData = File.ReadAllBytes(_sessionFilePath);

            // Decrypt using DPAPI
            var decryptedData = ProtectedData.Unprotect(
                encryptedData,
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            var combinedData = Encoding.UTF8.GetString(decryptedData);
            var parts = combinedData.Split(new[] { "|||" }, StringSplitOptions.None);

            if (parts.Length != 2)
            {
                throw new Exception("Invalid session format");
            }

            Console.WriteLine($"[SESSION] Loaded session for user: {parts[0]}");
            return (parts[0], parts[1]);
        }
        catch (CryptographicException)
        {
            // Data was encrypted by a different user or machine
            Console.WriteLine("[SESSION] Cannot decrypt session - may be from different user");
            throw new Exception("Cannot decrypt session. It may have been saved by a different user.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SESSION] Failed to load session: {ex.Message}");
            throw new Exception($"Failed to load session: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes the saved session file.
    /// </summary>
    public void DeleteSession()
    {
        try
        {
            if (File.Exists(_sessionFilePath))
            {
                File.Delete(_sessionFilePath);
                Console.WriteLine("[SESSION] Session deleted");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete session: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if a saved session exists.
    /// </summary>
    public bool HasSavedSession()
    {
        return File.Exists(_sessionFilePath);
    }
}
