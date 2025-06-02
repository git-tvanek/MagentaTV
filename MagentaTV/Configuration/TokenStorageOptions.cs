using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

/// <summary>
/// Konfigurace pro token storage
/// </summary>
public class TokenStorageOptions
{
    public const string SectionName = "TokenStorage";

    /// <summary>
    /// Cesta k adresáři pro ukládání tokenů
    /// </summary>
    [Required]
    public string StoragePath { get; set; } = "data";

    /// <summary>
    /// Cesta k souboru s encryption key
    /// </summary>
    [Required]
    public string KeyFilePath { get; set; } = "data/token.key";

    /// <summary>
    /// Automaticky ukládat tokeny po přihlášení
    /// </summary>
    public bool AutoSave { get; set; } = true;

    /// <summary>
    /// Automaticky načítat tokeny při startu aplikace
    /// </summary>
    public bool AutoLoad { get; set; } = true;

    /// <summary>
    /// Doba platnosti tokenů v hodinách
    /// </summary>
    [Range(1, 168)] // 1 hour to 1 week
    public int TokenExpirationHours { get; set; } = 24;

    /// <summary>
    /// Vymazat tokeny při startu aplikace (pro testing)
    /// </summary>
    public bool ClearOnStartup { get; set; } = false;

    /// <summary>
    /// Interval pro automatické obnovení tokenů (v minutách)
    /// </summary>
    [Range(5, 1440)] // 5 minutes to 24 hours
    public int RefreshIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Počet minut před expirací kdy se pokusit o refresh
    /// </summary>
    [Range(1, 60)]
    public int RefreshBeforeExpiryMinutes { get; set; } = 5;

    /// <summary>
    /// Maximální počet pokusů o refresh tokenu
    /// </summary>
    [Range(1, 10)]
    public int MaxRefreshAttempts { get; set; } = 3;

    /// <summary>
    /// Povolit backup tokenů (vytvoří .backup kopii)
    /// </summary>
    public bool EnableBackup { get; set; } = true;

    /// <summary>
    /// Počet backup souborů k uchování
    /// </summary>
    [Range(0, 10)]
    public int BackupRetentionCount { get; set; } = 3;

    /// <summary>
    /// Validace konfigurace
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StoragePath))
            throw new ArgumentException("StoragePath cannot be empty", nameof(StoragePath));

        if (string.IsNullOrWhiteSpace(KeyFilePath))
            throw new ArgumentException("KeyFilePath cannot be empty", nameof(KeyFilePath));

        if (TokenExpirationHours < 1 || TokenExpirationHours > 168)
            throw new ArgumentException("TokenExpirationHours must be between 1 and 168", nameof(TokenExpirationHours));

        if (RefreshIntervalMinutes < 5 || RefreshIntervalMinutes > 1440)
            throw new ArgumentException("RefreshIntervalMinutes must be between 5 and 1440", nameof(RefreshIntervalMinutes));

        if (RefreshBeforeExpiryMinutes < 1 || RefreshBeforeExpiryMinutes > 60)
            throw new ArgumentException("RefreshBeforeExpiryMinutes must be between 1 and 60", nameof(RefreshBeforeExpiryMinutes));

        // Ensure storage path and key file path are valid
        try
        {
            Path.GetFullPath(StoragePath);
            Path.GetFullPath(KeyFilePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid path configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Získá celou cestu k backup souboru
    /// </summary>
    public string GetBackupFilePath(int backupIndex = 0)
    {
        var backupFileName = backupIndex == 0
            ? "tokens.backup"
            : $"tokens.backup.{backupIndex}";

        return Path.Combine(StoragePath, backupFileName);
    }

    /// <summary>
    /// Získá cestu k lock souboru (pro synchronizaci mezi procesy)
    /// </summary>
    public string GetLockFilePath()
    {
        return Path.Combine(StoragePath, "tokens.lock");
    }
}