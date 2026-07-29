# ?? Database Connection Configuration

## Current Issue
The app shows "Unable to connect to database" because the connection string needs to be configured.

## ?? Quick Fix Steps

### 1. Open `Services/DatabaseConfig.cs`

### 2. Update these 4 values:

```csharp
private const string SERVER = "YOUR_SQL_SERVER";        // ? Change this
private const string DATABASE = "YOUR_DATABASE_NAME";   // ? Change this
private const string USER = "YOUR_USERNAME";            // ? Change this
private const string PASSWORD = "YOUR_PASSWORD";        // ? Change this
```

---

## ?? Common Configuration Examples

### **Example 1: Local SQL Server (Default Instance)**
```csharp
private const string SERVER = "localhost";
private const string DATABASE = "ATekDatabase";
private const string USER = "sa";
private const string PASSWORD = "MyPassword123";
```

### **Example 2: Local SQL Server Express**
```csharp
private const string SERVER = "localhost\\SQLEXPRESS";  // Note the double backslash
private const string DATABASE = "ATekDatabase";
private const string USER = "sa";
private const string PASSWORD = "MyPassword123";
```

### **Example 3: Network SQL Server (by IP)**
```csharp
private const string SERVER = "192.168.1.100";
private const string DATABASE = "ATekDatabase";
private const string USER = "sqluser";
private const string PASSWORD = "SecurePass123";
```

### **Example 4: Network SQL Server with Port**
```csharp
private const string SERVER = "192.168.1.100,1433";
private const string DATABASE = "ATekDatabase";
private const string USER = "sqluser";
private const string PASSWORD = "SecurePass123";
```

### **Example 5: Named SQL Server Instance**
```csharp
private const string SERVER = "MYCOMPUTER\\SQLEXPRESS";
private const string DATABASE = "ATekDatabase";
private const string USER = "sa";
private const string PASSWORD = "MyPassword123";
```

---

## ?? How to Find Your SQL Server Details

### Find Server Name:
1. Open **SQL Server Management Studio (SSMS)**
2. Look at the **Connect to Server** dialog
3. The server name is shown (e.g., `localhost`, `(local)`, `DESKTOP-ABC\SQLEXPRESS`)

### Find Database Name:
1. In SSMS, expand **Databases** folder
2. Look for your database (e.g., `ATekDatabase`, `ATekWebDB`)

### Username/Password:
- If using **SQL Server Authentication**: Use your SQL login (e.g., `sa`)
- If using **Windows Authentication**: See alternative method below

---

## ?? Alternative: Windows Authentication

If you use Windows Authentication (no username/password):

In `DatabaseConfig.cs`, use this connection string instead:

```csharp
public static string GetConnectionString()
{
    return $"Server={SERVER};Database={DATABASE};Integrated Security=True;TrustServerCertificate=True;";
}
```

Example:
```csharp
private const string SERVER = "localhost";
private const string DATABASE = "ATekDatabase";
// No USER or PASSWORD needed

public static string GetConnectionString()
{
    return $"Server={SERVER};Database={DATABASE};Integrated Security=True;TrustServerCertificate=True;";
}
```

---

## ?? Testing Your Connection

### Method 1: Check Output Window
1. Run your app in Debug mode
2. Open **Output** window in Visual Studio
3. Look for messages like:
   ```
   DatabaseService: Testing connection...
   DatabaseService: ? Connection test successful!
   ```

### Method 2: Use DatabaseTestPage
1. Navigate to the test page (if implemented)
2. Click "Test Connection" button
3. See if connection succeeds

---

## ? Common Errors & Solutions

### Error: "Server not found"
**Cause:** Wrong server name or SQL Server not running

**Solutions:**
- Verify SQL Server is running (check Services in Windows)
- Check server name/IP is correct
- Try `localhost` or `(local)` for local server
- For SQL Express, use `localhost\\SQLEXPRESS`

### Error: "Login failed"
**Cause:** Wrong username/password or authentication mode issue

**Solutions:**
- Verify username and password are correct
- Check SQL Server is in "Mixed Mode" authentication:
  1. Open SSMS
  2. Right-click server ? Properties
  3. Security ? Check "SQL Server and Windows Authentication mode"
  4. Restart SQL Server service

### Error: "Database not found"
**Cause:** Database name is wrong or doesn't exist

**Solutions:**
- Verify database name in SSMS
- Check spelling and capitalization
- Ensure database exists

### Error: "Network-related error"
**Cause:** Firewall blocking connection or TCP/IP disabled

**Solutions:**
- Check Windows Firewall allows SQL Server (port 1433)
- Enable TCP/IP in SQL Server Configuration Manager
- For remote connections, enable "Allow remote connections" in SQL Server

---

## ?? Quick Validation Checklist

Before running the app, verify:

- [ ] SQL Server is running
- [ ] Server name is correct
- [ ] Database exists and name is correct
- [ ] Username and password are correct
- [ ] SQL Server allows SQL Authentication (if not using Windows Auth)
- [ ] Firewall allows SQL Server connections (if remote)
- [ ] Connection string format is correct (note double backslashes `\\`)

---

## ?? Platform-Specific Notes

### Windows
- Full SQL Server support
- Can use Windows Authentication or SQL Authentication

### Android
- Requires SQL Authentication (username/password)
- Requires network permissions in AndroidManifest.xml
- Server must allow remote connections

### iOS
- Requires SQL Authentication (username/password)
- Server must allow remote connections
- May need to allow cleartext traffic for local networks

---

## ?? Example Configuration for Testing

**For quick testing with local SQL Server Express:**

```csharp
namespace ServerApplicationMobile.Services;

public static class DatabaseConfig
{
    // Local SQL Server Express example
    private const string SERVER = "localhost\\SQLEXPRESS";
    private const string DATABASE = "ATekDatabase";
    private const string USER = "sa";
    private const string PASSWORD = "YourSaPassword";

    public static string GetConnectionString()
    {
        return $"Server={SERVER};Database={DATABASE};User Id={USER};Password={PASSWORD};TrustServerCertificate=True;Encrypt=True;";
    }

    public static string GetDevelopmentConnectionString()
    {
        return GetConnectionString(); // Use same for development
    }

    public static bool IsDevelopment()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    public static string GetCurrentConnectionString()
    {
        return GetConnectionString();
    }
}
```

---

## ?? Still Having Issues?

Check the **Output window** in Visual Studio for detailed error messages. The DatabaseService now logs:
- Connection attempts
- Specific error codes
- Helpful diagnostic suggestions

The error messages will tell you exactly what's wrong!

---

**Next Step:** Update `DatabaseConfig.cs` with your SQL Server details and run the app again! ??
