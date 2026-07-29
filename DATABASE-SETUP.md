# Database Integration Setup Guide

## ? What Was Added

1. **DatabaseService.cs** - Direct SQL Server database access
2. **DatabaseConfig.cs** - Connection string configuration
3. **Updated CustomersPage** - Now loads customers from database
4. **Updated CustomerDetailPage** - Loads jobs and products from database
5. **Updated MauiProgram.cs** - Registered DatabaseService in DI

## ?? Configuration Steps

### 1. Update Database Connection String

Edit `Services/DatabaseConfig.cs` and replace:

```csharp
private const string SERVER = "YOUR_SQL_SERVER";
private const string DATABASE = "YOUR_DATABASE_NAME";
private const string USER = "YOUR_USERNAME";
private const string PASSWORD = "YOUR_PASSWORD";
```

With your actual SQL Server details.

### 2. Verify Table Names and Columns

The queries in `DatabaseService.cs` assume these tables:

**Customers Table:**
- CustomerID (int)
- CustomerName (string)
- Address1, Address2 (string)
- City, State, ZipCode (string)
- Country, Phone, Email (string)
- ContactPerson (string)

**Jobs Table:**
- CustomerID (int, foreign key)
- JobNumber, SerialNumber, OEMNumber (string)
- InstallDate (datetime)
- MachineType (string)

**Products Table:**
- CustomerID (int, foreign key)
- Type, ProductName, Version (string)
- Quantity, Available (int)

**If your table/column names are different**, update the SQL queries in `DatabaseService.cs`.

### 3. Update Page Registrations

Since CustomersPage now requires DatabaseService via constructor injection, you need to register it properly. Update your navigation code:

**Option 1: Register CustomersPage in DI (Recommended)**

In `MauiProgram.cs`:
```csharp
builder.Services.AddTransient<CustomersPage>();
```

Then navigate using DI:
```csharp
var customersPage = serviceProvider.GetService<CustomersPage>();
await Navigation.PushAsync(customersPage);
```

**Option 2: Pass DatabaseService manually**
```csharp
var dbService = serviceProvider.GetService<DatabaseService>();
await Navigation.PushAsync(new CustomersPage(dbService));
```

## ?? How It Works

### Loading Customers
When CustomersPage appears:
1. Calls `DatabaseService.GetCustomersAsync()`
2. Executes SQL query to get all customers
3. Populates CustomerStore and displays in CollectionView

### Loading Customer Details
When user taps a customer:
1. Navigates to CustomerDetailPage with selected customer
2. Calls `GetJobsForCustomerAsync(customerID)`
3. Calls `GetProductsForCustomerAsync(customerID)`
4. Loads jobs and products for that specific customer

## ?? Security Notes

**Current Implementation:**
- Connection string is hardcoded in DatabaseConfig.cs
- ?? This is fine for development but NOT for production

**For Production:**
1. **Use SecureStorage API:**
```csharp
await SecureStorage.SetAsync("db_connection", connectionString);
var conn = await SecureStorage.GetAsync("db_connection");
```

2. **Or use Azure App Configuration:**
```csharp
var config = new ConfigurationBuilder()
    .AddAzureAppConfiguration(options => {
        options.Connect(connectionString);
    })
    .Build();
```

## ?? Testing

### Test Database Connection

Add this to a test page/button:

```csharp
private async void OnTestConnectionClicked(object sender, EventArgs e)
{
    var dbService = Handler.MauiContext.Services.GetService<DatabaseService>();
    var isConnected = await dbService.TestConnectionAsync();
    
    await DisplayAlert("Database Test", 
        isConnected ? "? Connected successfully!" : "? Connection failed",
        "OK");
}
```

### Debug Output

The DatabaseService writes debug output. Check Output window for:
- `Error getting customers: [message]`
- `Error getting jobs: [message]`
- `Error getting products: [message]`

## ?? Platform Considerations

**Windows:** ? Full SQL Server connectivity
**Android:** ? Works with Microsoft.Data.SqlClient (ensure network permissions)
**iOS:** ? Works with Microsoft.Data.SqlClient
**Mac Catalyst:** ? Should work

**Android Manifest** - Add if needed:
```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```

## ?? Troubleshooting

### "Cannot connect to SQL Server"
1. Check connection string in DatabaseConfig.cs
2. Ensure SQL Server allows remote connections
3. Check firewall settings
4. Verify SQL Server is running

### "Login failed for user"
1. Verify username/password
2. Check SQL Server authentication mode (Mixed mode)
3. Ensure user has appropriate permissions

### "Table does not exist"
1. Verify table names in SQL queries
2. Check database name in connection string
3. Ensure user has SELECT permissions

### App builds but crashes when loading customers
1. Check Output window for exception details
2. Verify SQL query matches your table structure
3. Test connection using TestConnectionAsync()

## ?? Next Steps

1. **Update DatabaseConfig.cs** with your connection details
2. **Verify table names** in DatabaseService queries
3. **Test the connection** using test button
4. **Run the app** and navigate to Customers page
5. **Check debug output** for any errors

## ?? Related Files

- `Services/DatabaseService.cs` - Main database operations
- `Services/DatabaseConfig.cs` - Connection string config
- `Pages/Tabs/CustomersPage.xaml.cs` - Customers list
- `Pages/Customer Details/CustomerDetailPage.xaml.cs` - Customer details
- `MauiProgram.cs` - Service registration

---

**Need help?** Check the debug output or add breakpoints in DatabaseService methods to see what's happening.
