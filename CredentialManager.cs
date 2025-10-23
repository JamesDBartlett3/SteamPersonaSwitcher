using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SteamPersonaSwitcher;

/// <summary>
/// Manages secure storage of Steam credentials using Windows DPAPI (Data Protection API).
/// Credentials are encrypted for the current user and stored in the application's AppData folder.
/// </summary>
public class CredentialManager
{
    private readonly string _credentialFilePath;

    public CredentialManager(string appDataDirectory)
    {
        _credentialFilePath = Path.Combine(appDataDirectory, "credentials.dat");
    }

    /// <summary>
    /// Saves the username and password securely using Windows DPAPI encryption.
    /// </summary>
    public void SaveCredentials(string username, string password)
    {
        try
        {
            // Combine username and password with a separator
            var combinedData = $"{username}|||{password}";
            var dataBytes = Encoding.UTF8.GetBytes(combinedData);

            // Encrypt using DPAPI (current user scope)
            var encryptedData = ProtectedData.Protect(
                dataBytes,
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            // Save encrypted data to file
            File.WriteAllBytes(_credentialFilePath, encryptedData);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save credentials: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads and decrypts the saved credentials. Returns null if no credentials are saved.
    /// </summary>
    public (string Username, string Password)? LoadCredentials()
    {
        try
        {
            if (!File.Exists(_credentialFilePath))
            {
                return null;
            }

            // Read encrypted data
            var encryptedData = File.ReadAllBytes(_credentialFilePath);

            // Decrypt using DPAPI
            var decryptedData = ProtectedData.Unprotect(
                encryptedData,
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            var combinedData = Encoding.UTF8.GetString(decryptedData);
            var parts = combinedData.Split(new[] { "|||" }, StringSplitOptions.None);

            if (parts.Length != 2)
            {
                throw new Exception("Invalid credential format");
            }

            return (parts[0], parts[1]);
        }
        catch (CryptographicException)
        {
            // Data was encrypted by a different user or machine
            throw new Exception("Cannot decrypt credentials. They may have been saved by a different user.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load credentials: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes the saved credentials file.
    /// </summary>
    public void DeleteCredentials()
    {
        try
        {
            if (File.Exists(_credentialFilePath))
            {
                File.Delete(_credentialFilePath);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete credentials: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if saved credentials exist.
    /// </summary>
    public bool HasSavedCredentials()
    {
        return File.Exists(_credentialFilePath);
    }
}
