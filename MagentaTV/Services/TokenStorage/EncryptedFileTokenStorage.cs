// MagentaTV/Services/TokenStorage/EncryptedFileTokenStorage.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MagentaTV.Configuration;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Implementace token storage s šifrovaným ukládáním na disk
/// </summary>
public class EncryptedFileTokenStorage : ITokenStorage
{
    private readonly string _filePath;
    private readonly byte[] _key;
    private readonly ILogger<EncryptedFileTokenStorage> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public EncryptedFileTokenStorage(
        ILogger<EncryptedFileTokenStorage> logger,
        IOptions<TokenStorageOptions> options)
    {
        _logger = logger;
        var config = options.Value;

        _filePath = Path.Combine(config.StoragePath, "tokens.enc");

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        // Generate or load encryption key
        _key = GetOrCreateEncryptionKey(config.KeyFilePath);

        _logger.LogInformation("EncryptedFileTokenStorage initialized. Storage path: {StoragePath}", config.StoragePath);
    }

    /// <summary>
    /// Uloží tokeny do šifrovaného souboru
    /// </summary>
    public async Task SaveTokensAsync(TokenData tokens)
    {
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var encryptedData = Encrypt(json);
            await File.WriteAllBytesAsync(_filePath, encryptedData);

            _logger.LogDebug("Tokens saved successfully for user: {Username}, expires: {ExpiresAt}",
                tokens.Username, tokens.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tokens for user: {Username}", tokens.Username);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Načte tokeny ze šifrovaného souboru
    /// </summary>
    public async Task<TokenData?> LoadTokensAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("Token file does not exist: {FilePath}", _filePath);
                return null;
            }

            var encryptedData = await File.ReadAllBytesAsync(_filePath);
            var json = Decrypt(encryptedData);

            var tokens = JsonSerializer.Deserialize<TokenData>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (tokens != null)
            {
                _logger.LogDebug("Tokens loaded for user: {Username}, valid: {IsValid}, expires: {ExpiresAt}",
                    tokens.Username, tokens.IsValid, tokens.ExpiresAt);
            }

            return tokens;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt tokens - file may be corrupted or key changed");
            // If we can't decrypt, clear the corrupted file
            await ClearTokensAsync();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tokens");
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Vymaže token soubor
    /// </summary>
    public async Task ClearTokensAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                _logger.LogDebug("Tokens cleared successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear tokens");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Zkontroluje, jestli jsou dostupné platné tokeny
    /// </summary>
    public async Task<bool> HasValidTokensAsync()
    {
        var tokens = await LoadTokensAsync();
        return tokens?.IsValid == true;
    }

    /// <summary>
    /// Získá nebo vytvoří encryption key
    /// </summary>
    private byte[] GetOrCreateEncryptionKey(string keyFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath)!);

        if (File.Exists(keyFilePath))
        {
            try
            {
                var keyData = File.ReadAllBytes(keyFilePath);
                if (keyData.Length == 32) // 256-bit key
                {
                    _logger.LogDebug("Using existing encryption key");
                    return keyData;
                }
                else
                {
                    _logger.LogWarning("Existing key file has invalid length ({Length} bytes), generating new key", keyData.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing encryption key, generating new one");
            }
        }

        // Generate new 256-bit key
        var newKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(newKey);
        }

        try
        {
            File.WriteAllBytes(keyFilePath, newKey);
            _logger.LogInformation("Generated new encryption key at: {KeyFilePath}", keyFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save encryption key to: {KeyFilePath}", keyFilePath);
            throw;
        }

        return newKey;
    }

    /// <summary>
    /// Zašifruje text pomocí AES-256
    /// </summary>
    private byte[] Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();

        // Write IV first (16 bytes)
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);

        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt, Encoding.UTF8))
        {
            swEncrypt.Write(plainText);
        }

        return msEncrypt.ToArray();
    }

    /// <summary>
    /// Dešifruje data pomocí AES-256
    /// </summary>
    private string Decrypt(byte[] cipherData)
    {
        if (cipherData.Length < 16)
        {
            throw new CryptographicException("Cipher data is too short to contain a valid IV");
        }

        using var aes = Aes.Create();
        aes.Key = _key;

        // Extract IV from the beginning (16 bytes)
        var iv = new byte[16];
        Array.Copy(cipherData, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(cipherData, 16, cipherData.Length - 16);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8);

        return srDecrypt.ReadToEnd();
    }

    /// <summary>
    /// Dispose pattern pro cleanup
    /// </summary>
    public void Dispose()
    {
        _fileLock?.Dispose();
    }
}