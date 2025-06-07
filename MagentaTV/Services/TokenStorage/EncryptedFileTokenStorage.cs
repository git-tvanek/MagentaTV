using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MagentaTV.Configuration;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Implementace token storage s šifrovaným ukládáním na disk - plně asynchronní verze
/// </summary>
public class EncryptedFileTokenStorage : ITokenStorage, IAsyncDisposable
{
    private readonly string _filePath;
    private byte[]? _key;
    private readonly ILogger<EncryptedFileTokenStorage> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly TokenStorageOptions _options;
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private const string DefaultSessionId = "default";
    private const int BufferSize = 4096;

    public EncryptedFileTokenStorage(
        ILogger<EncryptedFileTokenStorage> logger,
        IOptions<TokenStorageOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _filePath = Path.Combine(_options.StoragePath, "tokens.enc");

        // Directory creation remains synchronous as there's no async alternative
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        _logger.LogInformation("EncryptedFileTokenStorage created. Storage path: {StoragePath}", _options.StoragePath);
    }

    /// <summary>
    /// Ensures the storage is initialized with encryption key
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized && _key != null)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized && _key != null)
                return;

            _key = await GetOrCreateEncryptionKeyAsync(_options.KeyFilePath);
            _initialized = true;
            _logger.LogInformation("EncryptedFileTokenStorage initialized successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string GetSessionFilePath(string sessionId)
    {
        if (sessionId == DefaultSessionId)
            return _filePath;

        var safeId = Convert.ToBase64String(Encoding.UTF8.GetBytes(sessionId))
            .Replace("+", "-").Replace("/", "_");
        return Path.Combine(Path.GetDirectoryName(_filePath)!, $"tokens_{safeId}.enc");
    }

    /// <summary>
    /// Asynchronně zkontroluje existenci souboru
    /// </summary>
    private static async Task<bool> FileExistsAsync(string path)
    {
        return await Task.Run(() => File.Exists(path));
    }

    /// <summary>
    /// Uloží tokeny do šifrovaného souboru (výchozí session)
    /// </summary>
    public Task SaveTokensAsync(TokenData tokens) => SaveTokensAsync(DefaultSessionId, tokens);

    /// <summary>
    /// Uloží tokeny pro danou session
    /// </summary>
    public async Task SaveTokensAsync(string sessionId, TokenData tokens)
    {
        await EnsureInitializedAsync();

        var path = GetSessionFilePath(sessionId);
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var encryptedData = await EncryptAsync(json);

            // Use FileStream with async operations
            await using var fileStream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            await fileStream.WriteAsync(encryptedData);
            await fileStream.FlushAsync();

            _logger.LogDebug("Tokens saved successfully for session {SessionId}, user: {Username}, expires: {ExpiresAt}",
                sessionId, tokens.Username, tokens.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tokens for session {SessionId}", sessionId);
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
    public Task<TokenData?> LoadTokensAsync() => LoadTokensAsync(DefaultSessionId);

    public async Task<TokenData?> LoadTokensAsync(string sessionId)
    {
        await EnsureInitializedAsync();

        var path = GetSessionFilePath(sessionId);
        await _fileLock.WaitAsync();
        try
        {
            if (!await FileExistsAsync(path))
            {
                _logger.LogDebug("Token file does not exist: {FilePath}", path);
                return null;
            }

            // Use FileStream with async operations
            await using var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);

            var encryptedData = new byte[fileStream.Length];
            await fileStream.ReadAsync(encryptedData);

            var json = await DecryptAsync(encryptedData);

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
            await ClearTokensAsync(sessionId);
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
    public Task ClearTokensAsync() => ClearTokensAsync(DefaultSessionId);

    public async Task ClearTokensAsync(string sessionId)
    {
        var path = GetSessionFilePath(sessionId);
        await _fileLock.WaitAsync();
        try
        {
            if (await FileExistsAsync(path))
            {
                // Async file deletion
                await Task.Run(() => File.Delete(path));
                _logger.LogDebug("Tokens cleared successfully for session {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear tokens for session {SessionId}", sessionId);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Zkontroluje, jestli jsou dostupné platné tokeny
    /// </summary>
    public Task<bool> HasValidTokensAsync() => HasValidTokensAsync(DefaultSessionId);

    public async Task<bool> HasValidTokensAsync(string sessionId)
    {
        var tokens = await LoadTokensAsync(sessionId);
        return tokens?.IsValid == true;
    }

    /// <summary>
    /// Získá nebo vytvoří encryption key - plně asynchronní
    /// </summary>
    private async Task<byte[]> GetOrCreateEncryptionKeyAsync(string keyFilePath)
    {
        // Directory creation remains synchronous
        Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath)!);

        if (await FileExistsAsync(keyFilePath))
        {
            try
            {
                await using var fileStream = new FileStream(
                    keyFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    useAsync: true);

                var keyData = new byte[fileStream.Length];
                await fileStream.ReadAsync(keyData);

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
            await using var fileStream = new FileStream(
                keyFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            await fileStream.WriteAsync(newKey);
            await fileStream.FlushAsync();

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
    /// Asynchronně zašifruje text pomocí AES-256
    /// </summary>
    private async Task<byte[]> EncryptAsync(string plainText)
    {
        if (_key == null)
            throw new InvalidOperationException("Encryption key not initialized");

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        await using var msEncrypt = new MemoryStream();

        // Write IV first (16 bytes)
        await msEncrypt.WriteAsync(aes.IV);

        await using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        await using (var swEncrypt = new StreamWriter(csEncrypt, Encoding.UTF8))
        {
            await swEncrypt.WriteAsync(plainText);
            await swEncrypt.FlushAsync();
        }

        return msEncrypt.ToArray();
    }

    /// <summary>
    /// Asynchronně dešifruje data pomocí AES-256
    /// </summary>
    private async Task<string> DecryptAsync(byte[] cipherData)
    {
        if (_key == null)
            throw new InvalidOperationException("Encryption key not initialized");

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
        await using var msDecrypt = new MemoryStream(cipherData, 16, cipherData.Length - 16);
        await using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8);

        return await srDecrypt.ReadToEndAsync();
    }

    /// <summary>
    /// Dispose pattern pro cleanup
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Async dispose pattern
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_fileLock != null)
        {
            await _fileLock.WaitAsync();
            try
            {
                _fileLock.Dispose();
            }
            catch { }
        }

        _initLock?.Dispose();

        // Clear sensitive data
        if (_key != null)
        {
            Array.Clear(_key, 0, _key.Length);
            _key = null;
        }
    }
}