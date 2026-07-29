using ConAuto.CADSEncryptDecryptLib;
using Microsoft.Data.SqlClient;

namespace ServerApplicationMobile.Services;

public static class DatabaseConfig
{
    private const string ServerKey = "atek_sql_server";
    private const string DatabaseKey = "atek_sql_database";
    private const string UserKey = "atek_sql_user";
    private const string PasswordKey = "atek_sql_password";

    private const string DefaultDatabase = "ATekUserInfo";
    private const string EncryptedDefaultServer = "M+qshmRMv+MR9GUPbtYizA==";
    private const string EncryptedDefaultUser = "l96zTIBoFs8Ae8RCIMKWlA==";
    private const string EncryptedDefaultPassword = "/F6JtXURx0+E/i5+zjWeFw==";
    private const string EncryptionKey = "ATekJob";

    public static async Task<string> GetConnectionStringAsync()
    {
        var server = await GetValueAsync(ServerKey) ?? Decrypt(EncryptedDefaultServer);
        var database = await GetValueAsync(DatabaseKey) ?? DefaultDatabase;
        var user = await GetValueAsync(UserKey) ?? Decrypt(EncryptedDefaultUser);
        var password = await GetValueAsync(PasswordKey) ?? Decrypt(EncryptedDefaultPassword);

        if (string.IsNullOrWhiteSpace(server) ||
            string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("SQL Server connection settings are incomplete.");
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = user,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30,
            ApplicationName = "ATek Server Application Mobile"
        }.ConnectionString;
    }

    public static async Task SetConnectionAsync(string server, string database, string user, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await SecureStorage.SetAsync(ServerKey, server.Trim());
        await SecureStorage.SetAsync(DatabaseKey, database.Trim());
        await SecureStorage.SetAsync(UserKey, user.Trim());
        await SecureStorage.SetAsync(PasswordKey, password);
    }

    public static void ClearConnection()
    {
        SecureStorage.Remove(ServerKey);
        SecureStorage.Remove(DatabaseKey);
        SecureStorage.Remove(UserKey);
        SecureStorage.Remove(PasswordKey);
    }

    private static async Task<string> GetValueAsync(string key)
    {
        try
        {
            var value = await SecureStorage.GetAsync(key);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DatabaseConfig: Secure storage read failed for {key}: {ex.Message}");
            return null;
        }
    }

    private static string Decrypt(string encryptedValue)
    {
        return CTripleDESCryptoService.DecryptData(encryptedValue, EncryptionKey);
    }
}
