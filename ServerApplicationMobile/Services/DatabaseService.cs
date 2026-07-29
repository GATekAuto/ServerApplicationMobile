using System.Data;
using ConAuto.CADSEncryptDecryptLib;
using ConAuto.SharedEnums;
using Microsoft.Data.SqlClient;

namespace ServerApplicationMobile.Services;

/// <summary>
/// Read-only access to the ATek SQL Server data used by the customer screens.
/// </summary>
public sealed class DatabaseService
{
    private const int CommandTimeoutSeconds = 60;

    public async Task<List<Customer>> GetCustomersAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "Customer_GetAllCustomersData");
        await using var reader = await command.ExecuteReaderAsync();

        var customers = new List<Customer>();
        while (await reader.ReadAsync())
        {
            customers.Add(new Customer
            {
                CustomerID = customers.Count + 1,
                CustomerName = GetString(reader, 0),
                OEM = GetString(reader, 1),
                CompanyName = GetString(reader, 3),
                ContactPerson = GetString(reader, 4),
                Contact = GetString(reader, 4),
                Email = GetString(reader, 5),
                Phone = GetString(reader, 6),
                Address1 = GetString(reader, 7),
                Address2 = GetString(reader, 8),
                City = GetString(reader, 9),
                State = GetString(reader, 10),
                ZipCode = GetString(reader, 11),
                Zip = GetString(reader, 11),
                Country = GetString(reader, 12),
                TicketStatus = GetBoolean(reader, 15) ? "Inactive" : "Active",
                HasMaintenancePackage = GetBoolean(reader, 13),
                IsInactive = GetBoolean(reader, 15),
                InactiveReason = GetString(reader, 16),
                IsAccountCreated = GetBoolean(reader, 17),
                ShippingAddress1 = GetString(reader, 18),
                ShippingAddress2 = GetString(reader, 19),
                ShippingCity = GetString(reader, 20),
                ShippingState = GetString(reader, 21),
                ShippingZip = GetString(reader, 22),
                ShippingCountry = GetString(reader, 23),
                DiscountPercent = GetDouble(reader, 24),
                LastOrderDataBlob = GetString(reader, 25),
                SalesBranch = GetInt32(reader, 26)
            });
        }

        return customers;
    }

    public async Task<Customer> GetCustomerAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "Customer_GetCustomerInfo");
        AddTextParameter(command, "@CustomerName", customer.CustomerName);
        AddTextParameter(command, "@OEMName", customer.OEM);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new InvalidOperationException("The customer could not be found in the database.");

        var isInactive = GetBoolean(reader, 15);
        return new Customer
        {
            CustomerID = customer.CustomerID,
            CustomerName = GetString(reader, 0),
            OEM = GetString(reader, 1),
            CompanyName = GetString(reader, 3),
            ContactPerson = GetString(reader, 4),
            Contact = GetString(reader, 4),
            Email = GetString(reader, 5),
            Phone = GetString(reader, 6),
            Address1 = GetString(reader, 7),
            Address2 = GetString(reader, 8),
            City = GetString(reader, 9),
            State = GetString(reader, 10),
            ZipCode = GetString(reader, 11),
            Zip = GetString(reader, 11),
            Country = GetString(reader, 12),
            HasMaintenancePackage = GetBoolean(reader, 13),
            IsInactive = isInactive,
            TicketStatus = isInactive ? "Inactive" : "Active",
            InactiveReason = GetString(reader, 16),
            IsAccountCreated = GetBoolean(reader, 17),
            ShippingAddress1 = GetString(reader, 18),
            ShippingAddress2 = GetString(reader, 19),
            ShippingCity = GetString(reader, 20),
            ShippingState = GetString(reader, 21),
            ShippingZip = GetString(reader, 22),
            ShippingCountry = GetString(reader, 23),
            DiscountPercent = GetDouble(reader, 24),
            LastOrderDataBlob = GetString(reader, 25),
            SalesBranch = GetInt32(reader, 26),
            ProductVersion = customer.ProductVersion
        };
    }

    public async Task UpdateCustomerAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (string.IsNullOrWhiteSpace(customer.CustomerName) || string.IsNullOrWhiteSpace(customer.OEM))
            throw new InvalidOperationException("Customer Name and OEM are required.");

        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "Customer_UpdateCustomer");

        AddParameter(command, "@CustomerName", SqlDbType.VarChar, 255, customer.CustomerName);
        AddParameter(command, "@OEM", SqlDbType.VarChar, 255, customer.OEM);
        AddParameter(command, "@CompanyName", SqlDbType.VarChar, 255,
            string.IsNullOrWhiteSpace(customer.CompanyName) ? customer.CustomerName : customer.CompanyName);
        AddParameter(command, "@ContactName", SqlDbType.VarChar, 255, customer.Contact);
        AddParameter(command, "@Email", SqlDbType.VarChar, 255, customer.Email);
        AddParameter(command, "@Phone", SqlDbType.VarChar, 255, customer.Phone);
        AddParameter(command, "@Address1", SqlDbType.VarChar, 255, customer.Address1);
        AddParameter(command, "@Address2", SqlDbType.VarChar, 255, customer.Address2);
        AddParameter(command, "@City", SqlDbType.VarChar, 100, customer.City);
        AddParameter(command, "@State", SqlDbType.VarChar, 100, customer.State);
        AddParameter(command, "@Zip", SqlDbType.VarChar, 50, customer.Zip);
        AddParameter(command, "@Country", SqlDbType.VarChar, 255, customer.Country);
        AddParameter(command, "@HasMaintenancePackage", SqlDbType.SmallInt, 2,
            Convert.ToInt16(customer.HasMaintenancePackage));
        AddParameter(command, "@SpareData", SqlDbType.Text, -1, null);
        AddParameter(command, "@IsInactive", SqlDbType.SmallInt, 2,
            Convert.ToInt16(customer.IsInactive));
        AddParameter(command, "@InactiveReason", SqlDbType.Text, 500, customer.InactiveReason);
        AddParameter(command, "@IsAccountCreated", SqlDbType.SmallInt, 2,
            Convert.ToInt16(customer.IsAccountCreated));
        AddParameter(command, "@SAddress1", SqlDbType.VarChar, 255, customer.ShippingAddress1);
        AddParameter(command, "@SAddress2", SqlDbType.VarChar, 255, customer.ShippingAddress2);
        AddParameter(command, "@SCity", SqlDbType.VarChar, 100, customer.ShippingCity);
        AddParameter(command, "@SState", SqlDbType.VarChar, 100, customer.ShippingState);
        AddParameter(command, "@SZip", SqlDbType.VarChar, 50, customer.ShippingZip);
        AddParameter(command, "@SCountry", SqlDbType.VarChar, 255, customer.ShippingCountry);
        AddParameter(command, "@Discount", SqlDbType.Float, 8, customer.DiscountPercent);
        AddParameter(command, "@LastData", SqlDbType.Text, -1,
            string.IsNullOrEmpty(customer.LastOrderDataBlob) ? null : customer.LastOrderDataBlob);
        AddParameter(command, "@SalesBranch", SqlDbType.SmallInt, 2, customer.SalesBranch);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<AuthenticatedUser> AuthenticateAsync(string userId, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return null;

        var encryptedPassword = CTripleDESCryptoService.EncryptData(password, "ATekOEM");
        await using var connection = await OpenConnectionAsync();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            await using var command = CreateStoredProcedure(connection, "OEMUSER_GetUser");
            AddTextParameter(command, "@USER", userId);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync() &&
                string.Equals(GetString(reader, 1), encryptedPassword, StringComparison.Ordinal))
            {
                return new AuthenticatedUser
                {
                    UserID = GetString(reader, 0),
                    OEMName = GetString(reader, 2),
                    DisplayName = GetString(reader, 3),
                    Role = (enumOEMUserRole)GetInt32(reader, 4),
                    IsOemAdministrator = false
                };
            }
        }

        await using (var command = CreateStoredProcedure(connection, "OEM_GetAllOEMsData"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (!string.Equals(GetString(reader, 1), encryptedPassword, StringComparison.Ordinal))
                    continue;

                var oemName = GetString(reader, 0);
                // A phone model is not a useful chat identity. For password-only
                // OEM administrator accounts, use the name entered on the login
                // screen as the service technician identity.
                var serviceTechName = userId.Trim();
                return new AuthenticatedUser
                {
                    UserID = serviceTechName,
                    OEMName = oemName,
                    DisplayName = serviceTechName,
                    Role = enumOEMUserRole.Admin,
                    IsOemAdministrator = true
                };
            }
        }

        return null;
    }

    public async Task<List<Job>> GetJobsForCustomerAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "Job_GetCustomerJobInfo");
        AddTextParameter(command, "@CustomerName", customer.CustomerName);
        AddTextParameter(command, "@OEMName", customer.OEM);

        await using var reader = await command.ExecuteReaderAsync();
        var jobs = new List<Job>();

        while (await reader.ReadAsync())
        {
            jobs.Add(new Job
            {
                JobNumber = GetString(reader, 0),
                SerialNumber = GetString(reader, 3),
                OEMNumber = GetString(reader, 4),
                InstallDate = GetDate(reader, 12)?.ToString("MM/dd/yyyy") ?? string.Empty,
                MachineType = GetString(reader, 13)
            });
        }

        return jobs;
    }

    public async Task<List<Product>> GetProductsForCustomerAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var jobs = await GetJobsForCustomerAsync(customer);
        if (jobs.Count == 0)
            return new List<Product>();

        await using var connection = await OpenConnectionAsync();
        var products = new List<Product>();

        foreach (var job in jobs)
        {
            await using var command = CreateStoredProcedure(connection, "JobProduct_GetJobProductInfo");
            AddTextParameter(command, "@JobNumber", job.JobNumber);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var quantity = GetInt32(reader, 6);
                products.Add(new Product
                {
                    ProductName = GetString(reader, 0),
                    Type = GetBoolean(reader, 7) ? "Trial" : "License",
                    Version = JoinVersion(GetString(reader, 4), GetString(reader, 5)),
                    Quantity = quantity,
                    Available = quantity
                });
            }
        }

        return products;
    }

    public async Task<List<ServiceTicket>> GetServiceTicketsAsync(
        string searchText,
        DateTime? startDate,
        DateTime? endDate,
        bool openOnly)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "ServiceCall_SearchEx");
        AddNullableParameter(command, "@CustomerName", SqlDbType.VarChar, 255,
            string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim());
        AddNullableParameter(command, "@CallDateStart", SqlDbType.DateTime, 0, startDate?.Date);
        AddNullableParameter(command, "@CallDateEnd", SqlDbType.DateTime, 0,
            endDate?.Date.AddDays(1).AddTicks(-1));
        AddNullableParameter(command, "@IsTicketClosed", SqlDbType.SmallInt, 0,
            openOnly ? (short)0 : null);

        await using var reader = await command.ExecuteReaderAsync();
        var tickets = new List<ServiceTicket>();
        while (await reader.ReadAsync())
            tickets.Add(ReadServiceTicket(reader, compact: true));

        return tickets.OrderBy(ticket => ticket.IsClosed).ThenByDescending(ticket => ticket.CallDate).ToList();
    }

    public async Task<ServiceTicket> GetServiceTicketAsync(string ticketNumber)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "ServiceCall_GetServiceCall");
        AddParameter(command, "@TicketNumber", SqlDbType.VarChar, 50, ticketNumber);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new InvalidOperationException("The service ticket could not be found.");
        return ReadServiceTicket(reader, compact: false);
    }

    public async Task<List<SoftwareLog>> GetSoftwareLogsAsync(
        string searchText,
        DateTime? startDate,
        DateTime? endDate,
        enumSoftwareLogType? logType)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "SoftwareVersionLog_SearchEx");
        AddNullableParameter(command, "@LogBy", SqlDbType.VarChar, 255,
            string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim());
        AddNullableParameter(command, "@CallDateStart", SqlDbType.DateTime, 0, startDate?.Date);
        AddNullableParameter(command, "@CallDateEnd", SqlDbType.DateTime, 0,
            endDate?.Date.AddDays(1).AddTicks(-1));
        AddNullableParameter(command, "@LogType", SqlDbType.SmallInt, 0,
            logType.HasValue ? Convert.ToInt16(logType.Value) : null);

        await using var reader = await command.ExecuteReaderAsync();
        var logs = new List<SoftwareLog>();
        while (await reader.ReadAsync())
            logs.Add(ReadSoftwareLog(reader, compact: true));

        return logs.OrderByDescending(log => log.LogDate).ToList();
    }

    public async Task<SoftwareLog> GetSoftwareLogAsync(long id)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "SoftwareVersionLog_GetLog");
        AddParameter(command, "@ID", SqlDbType.BigInt, 0, id);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new InvalidOperationException("The software log could not be found.");
        return ReadSoftwareLog(reader, compact: false);
    }

    public async Task<List<ChatLog>> GetChatLogsAsync(
        string searchText,
        DateTime? startDate,
        DateTime? endDate)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = CreateStoredProcedure(connection, "Chat_Search");
        AddNullableParameter(command, "@CustomerName", SqlDbType.VarChar, 255, null);
        AddNullableParameter(command, "@OEMName", SqlDbType.VarChar, 255, null);
        AddNullableParameter(command, "@JobNumber", SqlDbType.VarChar, 50, null);
        AddNullableParameter(command, "@CallDateStart", SqlDbType.DateTime, 0, startDate?.Date);
        AddNullableParameter(command, "@CallDateEnd", SqlDbType.DateTime, 0,
            endDate?.Date.AddDays(1).AddTicks(-1));
        AddNullableParameter(command, "@Problem", SqlDbType.VarChar, 255, null);

        await using var reader = await command.ExecuteReaderAsync();
        var logs = new List<ChatLog>();
        while (await reader.ReadAsync())
        {
            logs.Add(new ChatLog
            {
                ID = GetInt64(reader, 0),
                LogDate = GetDate(reader, 1),
                JobNumber = GetString(reader, 2),
                CustomerName = GetString(reader, 3),
                OEMName = GetString(reader, 4),
                UserName = GetString(reader, 5),
                PhoneNumber = GetString(reader, 6),
                Message1 = GetRawString(reader, 7),
                Message2 = GetRawString(reader, 8),
                ChatID = GetString(reader, 9),
                StartTime = GetDate(reader, 10),
                AcceptedTime = GetDate(reader, 11)
            });
        }

        var filter = searchText?.Trim();
        IEnumerable<ChatLog> result = logs;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            result = result.Where(log =>
                Contains(log.CustomerName, filter) ||
                Contains(log.OEMName, filter) ||
                Contains(log.JobNumber, filter) ||
                Contains(log.UserName, filter) ||
                Contains(log.PhoneNumber, filter) ||
                Contains(log.Message, filter) ||
                Contains(log.ChatID, filter));
        }

        return result.OrderByDescending(log => log.LogDate).ToList();
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var connection = await OpenConnectionAsync();
            await using var command = new SqlCommand("SELECT 1", connection)
            {
                CommandTimeout = CommandTimeoutSeconds
            };

            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DatabaseService: SQL connection test failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<SqlConnection> OpenConnectionAsync()
    {
        var connectionString = await DatabaseConfig.GetConnectionStringAsync();
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static SqlCommand CreateStoredProcedure(SqlConnection connection, string name)
    {
        return new SqlCommand(name, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds
        };
    }

    private static void AddTextParameter(SqlCommand command, string name, string value)
    {
        command.Parameters.Add(name, SqlDbType.VarChar, 255).Value =
            value?.Trim() ?? string.Empty;
    }

    private static void AddParameter(
        SqlCommand command,
        string name,
        SqlDbType type,
        int size,
        object value)
    {
        var parameter = size > 0
            ? command.Parameters.Add(name, type, size)
            : command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
    }

    private static void AddNullableParameter(
        SqlCommand command,
        string name,
        SqlDbType type,
        int size,
        object value)
    {
        AddParameter(command, name, type, size, value);
    }

    private static ServiceTicket ReadServiceTicket(SqlDataReader reader, bool compact)
    {
        if (compact)
        {
            return new ServiceTicket
            {
                TicketNumber = GetString(reader, 0),
                CustomerName = GetString(reader, 1),
                OEM = GetString(reader, 2),
                CallDate = GetDate(reader, 3) ?? default,
                ProblemType = (enumATekServiceCallProblemType)GetInt32(reader, 4),
                TicketCreatedBy = GetString(reader, 5),
                JobNumber = GetString(reader, 6),
                IsClosed = GetBoolean(reader, 7),
                TicketClosedDate = GetDate(reader, 8),
                TicketClosedBy = GetString(reader, 9),
                IsNeedToSendTech = GetBoolean(reader, 10),
                SoftwareType = GetString(reader, 11),
                SoftwareVersion = SplitVersion(GetString(reader, 12)).Major,
                SoftwareMinorVersion = SplitVersion(GetString(reader, 12)).Minor,
                MachineArea = GetString(reader, 13),
                MachineItem = GetString(reader, 14),
                TroubleShootingSteps = GetString(reader, 15)
            };
        }

        var version = SplitVersion(GetString(reader, 15));
        return new ServiceTicket
        {
            TicketNumber = GetString(reader, 0),
            CustomerName = GetString(reader, 1),
            OEM = GetString(reader, 2),
            CallDate = GetDate(reader, 3) ?? default,
            ProblemType = (enumATekServiceCallProblemType)GetInt32(reader, 4),
            TicketCreatedBy = GetString(reader, 5),
            JobNumber = GetString(reader, 6),
            ProblemInfo = GetString(reader, 7),
            ProblemSolution = GetString(reader, 8),
            IsClosed = GetBoolean(reader, 9),
            TicketClosedDate = GetDate(reader, 10),
            TicketClosedBy = GetString(reader, 11),
            IsNeedToSendTech = GetBoolean(reader, 12),
            Remarks = GetString(reader, 13),
            SoftwareType = GetString(reader, 14),
            SoftwareVersion = version.Major,
            SoftwareMinorVersion = version.Minor,
            MachineArea = GetString(reader, 16),
            MachineItem = GetString(reader, 17),
            TroubleShootingSteps = GetString(reader, 19)
        };
    }

    private static SoftwareLog ReadSoftwareLog(SqlDataReader reader, bool compact)
    {
        return new SoftwareLog
        {
            ID = GetInt64(reader, 0),
            LogDate = GetDate(reader, 1) ?? default,
            MajorVersion = GetInt32(reader, 2),
            MinorVersion = GetInt32(reader, 3),
            Build = GetInt32(reader, 4),
            SoftwareType = GetString(reader, 5),
            LogType = (enumSoftwareLogType)GetInt32(reader, 6),
            Description = GetString(reader, 7),
            InternalRemarks = compact ? string.Empty : GetString(reader, 8),
            LogBy = GetString(reader, compact ? 8 : 9),
            IsHidden = GetBoolean(reader, compact ? 9 : 11)
        };
    }

    private static (string Major, string Minor) SplitVersion(string value)
    {
        var parts = value?.Split(';', 2) ?? Array.Empty<string>();
        return (parts.ElementAtOrDefault(0) ?? string.Empty, parts.ElementAtOrDefault(1) ?? string.Empty);
    }

    private static bool Contains(string value, string searchText) =>
        value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;

    private static string GetString(SqlDataReader reader, int ordinal)
    {
        if (ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
            return string.Empty;

        return Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? string.Empty;
    }

    private static string GetRawString(SqlDataReader reader, int ordinal)
    {
        if (ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
            return string.Empty;

        return Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }

    private static bool GetBoolean(SqlDataReader reader, int ordinal)
    {
        if (ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
            return false;

        return Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static int GetInt32(SqlDataReader reader, int ordinal)
    {
        if (ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
            return 0;

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static long GetInt64(SqlDataReader reader, int ordinal)
    {
        if (ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
            return 0;

        return Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static double GetDouble(SqlDataReader reader, int ordinal)
    {
        if (ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
            return 0;

        return Convert.ToDouble(reader.GetValue(ordinal));
    }

    private static DateTime? GetDate(SqlDataReader reader, int ordinal)
    {
        if (ordinal >= reader.FieldCount || reader.IsDBNull(ordinal))
            return null;

        return Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static string JoinVersion(string major, string minor)
    {
        return string.Join('.', new[] { major, minor }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
