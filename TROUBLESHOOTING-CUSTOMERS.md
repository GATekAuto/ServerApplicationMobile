# ?? Troubleshooting: Customers Not Loading

## Diagnostic Steps

When customers aren't loading, check the **Output window** in Visual Studio for diagnostic messages.

### What to Look For:

#### 1. **Connection Status**
Look for these messages:
```
DatabaseService: Initialized with secure credentials
DatabaseService: Connecting to database...
DatabaseService: ? Connected! Server: ... Database: ...
```

? **If you see these**: Connection is working  
? **If not**: Connection failed - check credentials

#### 2. **Table Detection**
Look for:
```
Available Customer tables:
  - Customers
  - ATekCustomer
  (or other table names)
```

? **If you see table names**: Tables exist  
? **If empty**: No customer tables found

#### 3. **Query Execution**
Look for:
```
Trying query: SELECT TOP 10 * FROM Customers ...
? Query succeeded!
```

? **If succeeded**: Query worked  
? **If failed**: Table name or columns incorrect

#### 4. **Column Information**
Look for:
```
Columns found:
  [0] CustomerID (Int32)
  [1] CustomerName (String)
  ...
```

This shows what columns exist in the table.

#### 5. **Customer Data**
Look for:
```
Sample customer 1: ABC Company
Sample customer 2: XYZ Corp
...
DatabaseService: Loaded 150 customers
```

? **If you see customers**: Data is being read  
? **If 0 customers**: Table is empty

#### 6. **UI Update**
Look for:
```
CustomersPage: Received 150 customers
CustomersPage: Setting ItemsSource with 150 customers
CustomersPage: ItemsSource set. CollectionView has 150 items
```

? **If counts match**: UI should show customers  
? **If counts don't match**: UI binding issue

## Common Issues & Solutions

### Issue 1: "Server not found"
**Output shows:**
```
DatabaseService: ? Connection test failed!
Error Number: 53
? Server not found or not accessible
```

**Solutions:**
1. Check internet connection
2. Verify server address: `198.12.230.220`
3. Check firewall allows port 1433
4. Try: `ping 198.12.230.220`

### Issue 2: "Login failed"
**Output shows:**
```
Error Number: 18456
? Login failed - check credentials
```

**Solutions:**
1. Verify credentials in DatabaseConfig.cs:
   - Username: `ATekUserInfo`
   - Password: `ATekAuto222!`
2. Check SQL Server authentication is enabled
3. Verify user has permissions

### Issue 3: "Database not found"
**Output shows:**
```
Error Number: 4060
? Database not found
```

**Solutions:**
1. Verify database name: `ATekUserData`
2. Check database exists on server
3. Verify user has access to database

### Issue 4: "Table not found"
**Output shows:**
```
Available Customer tables:
(empty - no tables listed)
```

**Solutions:**
1. Table might have different name
2. Check what tables exist:
   ```sql
   SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
   WHERE TABLE_NAME LIKE '%Customer%'
   ```
3. Update query in DatabaseService if needed

### Issue 5: "0 customers loaded"
**Output shows:**
```
DatabaseService: Loaded 0 customers
```

**Solutions:**
1. Table is empty - no data
2. Add test data to database
3. Check query filters/conditions

### Issue 6: "Customers load but don't display"
**Output shows:**
```
DatabaseService: Loaded 150 customers
CustomersPage: Received 150 customers
CustomersPage: ItemsSource set. CollectionView has 0 items
```

**Solutions:**
1. UI binding issue
2. Check XAML CollectionView binding
3. Verify Customer class properties match
4. Check if CollectionView is visible

## Quick Diagnostic Test

Add this button to your XAML for testing:

```xml
<Button Text="Test Connection" 
        Clicked="OnTestConnectionClicked"/>
```

Add this to CustomersPage.xaml.cs:

```csharp
private async void OnTestConnectionClicked(object sender, EventArgs e)
{
    try
    {
        var result = await _databaseService.TestConnectionAsync();
        await DisplayAlert("Connection Test", 
            result ? "? Connection works!" : "? Connection failed", 
            "OK");
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", ex.Message, "OK");
    }
}
```

## What the Enhanced Code Does

### DatabaseService Improvements:
1. ? **Tries multiple table names**:
   - `Customers`
   - `ATekCustomer`
   - `dbo.Customers`

2. ? **Shows all available tables**:
   - Lists tables that contain "Customer"

3. ? **Logs all columns**:
   - Shows column names and types

4. ? **Tries alternate column names**:
   - `CustomerID` or `ID` or `CustomerId`
   - `CustomerName` or `Name`
   - `Address1` or `Address` or `Street`

5. ? **Shows sample data**:
   - Displays first 3 customers

### CustomersPage Improvements:
1. ? **Detailed logging** at each step
2. ? **Prevents multiple loads**
3. ? **Shows helpful error messages**
4. ? **Ensures UI updates on main thread**

## Next Steps

1. **Run the app** in Debug mode
2. **Open Output window** (View ? Output)
3. **Navigate to Customers tab**
4. **Read the diagnostic messages**
5. **Share the output** if you need help

The Output window will tell you exactly:
- ? Which step succeeded
- ? Which step failed
- ?? What data was found
- ?? What to check next

## Expected Output (Success)

```
DatabaseService: Initialized with secure credentials
DatabaseService: Connecting to database...
DatabaseService: ? Connected! Server: ... Database: ATekUserData
Available Customer tables:
  - Customers
Trying query: SELECT TOP 10 * FROM Customers ORDER BY CustomerName
? Query succeeded!
Columns found:
  [0] CustomerID (Int32)
  [1] CustomerName (String)
  [2] Address1 (String)
  ...
Sample customer 1: ABC Company
Sample customer 2: XYZ Corp
Sample customer 3: 123 Industries
DatabaseService: Loaded 150 customers
CustomersPage: Starting to load customers...
CustomersPage: Calling GetCustomersAsync...
CustomersPage: Received 150 customers
CustomersPage: Setting ItemsSource with 150 customers
  Customer 1: ID=1, Name=ABC Company
  Customer 2: ID=2, Name=XYZ Corp
  Customer 3: ID=3, Name=123 Industries
CustomersPage: ItemsSource set. CollectionView has 150 items
CustomersPage: Load customers completed
```

If you see this output, customers should be visible in the UI!

---

**Run the app and check the Output window - it will tell you exactly what's happening!** ??
