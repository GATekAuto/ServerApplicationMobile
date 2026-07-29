# ?? Database Integration - Ready to Use!

## ? Changes Made

### 1. Dependency Injection Setup
- **MauiProgram.cs**: Registered DatabaseService, CustomersPage, CustomerDetailPage, and AppTabbedPage
- **App.xaml.cs**: Updated to retrieve AppTabbedPage from DI container
- **AppTabbedPage**: Modified to accept DatabaseService and create CustomersPage programmatically

### 2. Database Connection
Your app is now configured to load customers, jobs, and products from your SQL Server database!

## ?? Configuration Required

### Update Connection String

Open `Services/DatabaseConfig.cs` and update these values:

```csharp
private const string SERVER = "YOUR_SQL_SERVER";        // e.g., "localhost" or "192.168.1.100"
private const string DATABASE = "YOUR_DATABASE_NAME";   // e.g., "ATekDatabase"
private const string USER = "YOUR_USERNAME";            // e.g., "sa"
private const string PASSWORD = "YOUR_PASSWORD";        // Your SQL Server password
```

### Example Configurations

**Local SQL Server:**
```csharp
private const string SERVER = "localhost";
private const string DATABASE = "ATekDatabase";
private const string USER = "sa";
private const string PASSWORD = "YourPassword123";
```

**Network SQL Server:**
```csharp
private const string SERVER = "192.168.1.100,1433";  // IP address with port
private const string DATABASE = "ATekDatabase";
private const string USER = "sqluser";
private const string PASSWORD = "SecurePassword123";
```

**SQL Server Express:**
```csharp
private const string SERVER = "(localdb)\\MSSQLLocalDB";
// OR
private const string SERVER = ".\\SQLEXPRESS";
```

## ?? Database Schema Expected

The app expects these tables with these columns:

### Customers Table
```sql
CREATE TABLE Customers (
    CustomerID INT PRIMARY KEY,
    CustomerName NVARCHAR(255),
    Address1 NVARCHAR(255),
    Address2 NVARCHAR(255),
    City NVARCHAR(100),
    State NVARCHAR(50),
    ZipCode NVARCHAR(20),
    Country NVARCHAR(100),
    Phone NVARCHAR(50),
    Email NVARCHAR(255),
    ContactPerson NVARCHAR(255)
);
```

### Jobs Table
```sql
CREATE TABLE Jobs (
    JobID INT PRIMARY KEY,
    CustomerID INT FOREIGN KEY REFERENCES Customers(CustomerID),
    JobNumber NVARCHAR(50),
    SerialNumber NVARCHAR(50),
    OEMNumber NVARCHAR(50),
    InstallDate DATETIME,
    MachineType NVARCHAR(100)
);
```

### Products Table
```sql
CREATE TABLE Products (
    ProductID INT PRIMARY KEY,
    CustomerID INT FOREIGN KEY REFERENCES Customers(CustomerID),
    Type NVARCHAR(50),
    ProductName NVARCHAR(255),
    Version NVARCHAR(50),
    Quantity INT,
    Available INT
);
```

**If your tables have different names or columns**, update the SQL queries in `Services/DatabaseService.cs`.

## ?? Testing

### 1. Build the App
```bash
dotnet build
```

### 2. Run the App
- The app will start with AppTabbedPage
- Navigate to the Customers tab
- Customers will load from your database automatically

### 3. Check Debug Output
If there are connection issues, check the Output window for:
```
Error loading customers: [error details]
```

## ?? Common Issues

### Issue: "Cannot connect to SQL Server"
**Solution:**
1. Verify SQL Server is running
2. Check firewall allows connections
3. For Android/iOS, ensure SQL Server allows remote connections
4. Test connection string using SQL Server Management Studio first

### Issue: "Login failed for user"
**Solution:**
1. Verify username/password in DatabaseConfig.cs
2. Ensure SQL Server uses "Mixed Mode" authentication
3. Check user has permissions on the database

### Issue: "Table 'Customers' does not exist"
**Solution:**
1. Verify database name in connection string
2. Check table names match your database
3. Update SQL queries in DatabaseService.cs if needed

### Issue: App crashes on startup
**Solution:**
1. Check Output window for exception details
2. Verify DatabaseConfig.cs has valid values
3. Try TestConnectionAsync() to isolate the issue

## ?? Platform-Specific Notes

### Android
Add to `AndroidManifest.xml` if needed:
```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```

### iOS
No special configuration needed - Microsoft.Data.SqlClient works out of the box.

### Windows
Full SQL Server connectivity supported.

## ?? Security Best Practices

**For Production:**
1. **Don't hardcode passwords** in source code
2. Use `SecureStorage` API:
```csharp
await SecureStorage.SetAsync("db_password", password);
var password = await SecureStorage.GetAsync("db_password");
```
3. Consider using Azure Key Vault or similar for connection strings
4. Use encrypted connections (`Encrypt=True`)

## ? What Happens Now

1. **App Starts** ? Creates AppTabbedPage with DatabaseService
2. **CustomersPage Loads** ? Calls `GetCustomersAsync()`
3. **SQL Query Executes** ? Retrieves all customers
4. **UI Updates** ? Displays customer list
5. **Tap Customer** ? Loads jobs and products for that customer

## ?? Next Steps

1. ? Update DatabaseConfig.cs with your connection details
2. ? Verify your database tables exist
3. ? Build and run the app
4. ? Navigate to Customers tab
5. ? See your data loaded from SQL Server!

---

**Need help?** Check `DATABASE-SETUP.md` for detailed troubleshooting or add a test button to verify database connectivity.
