using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

/// <summary>
/// Optional test page to verify database connectivity
/// Add a button in MenuPage to navigate here for testing
/// </summary>
public partial class DatabaseTestPage : ContentPage
{
    private readonly DatabaseService _databaseService;

    public DatabaseTestPage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
    }

    private async void OnTestConnectionClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "Testing connection...";
        TestButton.IsEnabled = false;

        try
        {
            var isConnected = await _databaseService.TestConnectionAsync();

            if (isConnected)
            {
                StatusLabel.Text = "? Database connection successful!";
                StatusLabel.TextColor = Colors.Green;
                await LoadSampleDataButton.FadeTo(1, 500);
            }
            else
            {
                StatusLabel.Text = "? Connection failed. Check your settings.";
                StatusLabel.TextColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"? Error: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
            await DisplayAlert("Error", ex.ToString(), "OK");
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private async void OnLoadSampleDataClicked(object sender, EventArgs e)
    {
        LoadSampleDataButton.IsEnabled = false;
        ResultsLabel.Text = "Loading...";

        try
        {
            var customers = await _databaseService.GetCustomersAsync();
            
            ResultsLabel.Text = $"? Found {customers.Count} customers:\n\n";
            
            foreach (var customer in customers.Take(5))
            {
                ResultsLabel.Text += $"• {customer.CustomerName} ({customer.City}, {customer.State})\n";
            }

            if (customers.Count > 5)
            {
                ResultsLabel.Text += $"\n... and {customers.Count - 5} more";
            }

            ResultsLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            ResultsLabel.Text = $"? Error loading data: {ex.Message}";
            ResultsLabel.TextColor = Colors.Red;
            await DisplayAlert("Error", ex.ToString(), "OK");
        }
        finally
        {
            LoadSampleDataButton.IsEnabled = true;
        }
    }
}
