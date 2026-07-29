# Finding Your SQL Server Address

## What We Know from ATekServerApplication

From analyzing the code in `D:\ATek Software v18.0\Code\ATekCommon\DBLib\CDatabaseLib.cs`:

**? Confirmed Database Settings:**
- **Database Name**: `ATekUserData`
- **Username**: `ATekUser`  
- **Password**: `ATekAuto99`
- **Server**: ? **NEED TO FIND THIS**

## How to Find Your Server Address

### Method 1: Check SQL Server Management Studio
1. Open **SQL Server Management Studio (SSMS)**
2. Look at what server name you connect with when using ATekServerApplication
3. Common formats:
   - `localhost`
   - `(local)`
   - `.`
   - `localhost\SQLEXPRESS`
   - `COMPUTERNAME\SQLEXPRESS`
   - `192.168.x.x` (IP address)

### Method 2: Check Windows Services
1. Press `Win + R`, type `services.msc`
2. Look for services starting with "SQL Server"
3. The instance name is in parentheses: `SQL Server (MSSQLSERVER)` or `SQL Server (SQLEXPRESS)`

### Method 3: Check ATekServerApplication Runtime
1. Run ATekServerApplication
2. It successfully connects to the database
3. The server must be accessible from your machine

### Method 4: Check Registry
SQL Server instances are registered in:
```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL
```

## Common Server Addresses to Try

Update `Services/DatabaseConfig.cs` with one of these:

### Local SQL Server (Default Instance)
```csharp
private const string SERVER = "localhost";
// OR
private const string SERVER = "(local)";
// OR  
private const string SERVER = ".";
```

### Local SQL Server Express
```csharp
private const string SERVER = "localhost\\SQLEXPRESS";
// OR
private const string SERVER = ".\\SQLEXPRESS";
```

### Network SQL Server
```csharp
private const string SERVER = "192.168.1.100";  // Replace with actual IP
// OR
private const string SERVER = "SERVERNAME";      // Replace with actual computer name
```

### SQL Server with Port
```csharp
private const string SERVER = "localhost,1433";
// OR
private const string SERVER = "192.168.1.100,1433";
```

## Testing Your Connection

### Using the App
1. Run the MAUI app in Debug mode
2. Navigate to Customers tab
3. Check **Output window** in Visual Studio for messages like:
   ```
   DatabaseService: Testing connection...
   DatabaseService: ? Connection test successful!
   ```

### Using SQL Server Management Studio
1. Open SSMS
2. Try connecting with:
   - **Server**: Your server address
   - **Authentication**: SQL Server Authentication
   - **Login**: `ATekUser`
   - **Password**: `ATekAuto99`
3. If it connects, your server address is correct!
4. Verify database `ATekUserData` exists

### Using Command Line
```powershell
# Test SQL Server connectivity
sqlcmd -S localhost -U ATekUser -P ATekAuto99 -d ATekUserData -Q "SELECT @@SERVERNAME"
```

## What the Output Window Shows

When you run the app, look for these messages:

### ? Success:
```
DatabaseService: Using connection string: Server=localhost;Database=ATekUserData;User Id=ATekUser;Password=****;...
DatabaseService: Attempting to connect...
DatabaseService: Connected successfully! Server: Microsoft SQL Server 2019
DatabaseService: Loaded 150 customers
```

### ? Server Not Found:
```
DatabaseService: ? Connection test failed!
Error: A network-related or instance-specific error occurred
Error Number: 53
  ? Server not found or not accessible
  ? Check server name/IP address
  ? Verify SQL Server is running
```

### ? Login Failed:
```
DatabaseService: ? Connection test failed!
Error: Login failed for user 'ATekUser'
Error Number: 18456
  ? Login failed
  ? Check username and password
  ? Verify SQL Server authentication mode
```

### ? Database Not Found:
```
DatabaseService: ? Connection test failed!
Error: Cannot open database "ATekUserData"
Error Number: 4060
  ? Database not found
  ? Check database name
```

## Quick Troubleshooting

### Error: "Server not found"
**Try these SERVER values in order:**
1. `localhost`
2. `.`
3. `(local)`
4. `localhost\\SQLEXPRESS`
5. `.\\SQLEXPRESS`
6. Your computer name (check in System Properties)
7. `127.0.0.1`

### Error: "Login failed"
1. Verify user `ATekUser` exists in SQL Server
2. Check password is `ATekAuto99`
3. Ensure SQL Server is in "Mixed Mode" authentication
4. Grant `ATekUser` access to `ATekUserData` database

### Error: "Database does not exist"
1. Check if database `ATekUserData` exists in SSMS
2. Verify name is spelled correctly (case-sensitive on Linux)
3. Ensure `ATekUser` has permissions

## Most Likely Scenarios

Based on ATekServerApplication setup:

### Scenario 1: Local Development Machine
```csharp
private const string SERVER = "localhost";
```

### Scenario 2: Local SQL Server Express
```csharp
private const string SERVER = "localhost\\SQLEXPRESS";
```

### Scenario 3: Network Database Server
Ask your DBA or check where ATekServerApplication connects to.

## Next Steps

1. ? Check SQL Server Management Studio for server name
2. ? Update `SERVER` in `DatabaseConfig.cs`
3. ? Run the app and check Output window
4. ? Navigate to Customers tab
5. ? Customers should load from database!

---

**Still stuck?** Share the error message from the Output window and I can help identify the issue!
