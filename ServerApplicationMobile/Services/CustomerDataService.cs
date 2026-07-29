namespace ServerApplicationMobile.Services;

/// <summary>
/// Loads the customer list once and shares the same in-flight task and result
/// with every page for the lifetime of the application.
/// </summary>
public sealed class CustomerDataService
{
    private readonly DatabaseService _databaseService;
    private readonly object _sync = new();
    private Task<IReadOnlyList<Customer>> _loadTask;

    public CustomerDataService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task<IReadOnlyList<Customer>> GetCustomersAsync()
    {
        lock (_sync)
        {
            return _loadTask ??= LoadCustomersAsync();
        }
    }

    public void StartLoading()
    {
        _ = ObserveStartupLoadAsync(GetCustomersAsync());
    }

    private async Task<IReadOnlyList<Customer>> LoadCustomersAsync()
    {
        return await _databaseService.GetCustomersAsync();
    }

    private async Task ObserveStartupLoadAsync(Task<IReadOnlyList<Customer>> loadTask)
    {
        try
        {
            await loadTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"CustomerDataService: Startup load failed: {ex.Message}");

            lock (_sync)
            {
                if (ReferenceEquals(_loadTask, loadTask))
                    _loadTask = null;
            }
        }
    }
}
