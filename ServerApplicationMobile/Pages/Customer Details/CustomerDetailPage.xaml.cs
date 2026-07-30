using ServerApplicationMobile.Pages.Customer_Details;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class CustomerDetailPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly AuthenticationService _authenticationService;
    public Customer SelectedCustomer { get; private set; }

    private List<Job> jobs = new();
    private List<Product> products = new();
    private bool _isOpeningChildPage;

    public CustomerDetailPage(
        Customer customer,
        DatabaseService databaseService,
        AuthenticationService authenticationService)
    {
        InitializeComponent();

        _databaseService = databaseService;
        _authenticationService = authenticationService;
        SelectedCustomer = customer;
        Title = customer.CustomerName;

        // Load data from the ATEK server API.
        LoadDataAsync();
    }

    private async void LoadDataAsync()
    {
        try
        {
            // Show loading indicator if you have one
            // LoadingIndicator.IsVisible = true;

            jobs = await _databaseService.GetJobsForCustomerAsync(SelectedCustomer);
            products = await _databaseService.GetProductsForCustomerAsync(SelectedCustomer);

            // Update UI if needed
            // If you have CollectionViews or ListViews bound to these, they'll update automatically
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load customer data: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading indicator
            // LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnJobClicked(object sender, EventArgs e)
    {
        if (_isOpeningChildPage)
            return;

        _isOpeningChildPage = true;
        try
        {
            await Navigation.PushAsync(new JobsPage(jobs));
        }
        finally
        {
            _isOpeningChildPage = false;
        }
    }
    
    private void OnProductClicked(object sender, EventArgs e)
    {
        Navigation.PushAsync(new ProductsPage(products));
    }
    
    private async void OnProductTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is Product tappedProduct)
        {
            // Animate
            await frame.ScaleTo(0.97, 75, Easing.CubicInOut);
            await frame.ScaleTo(1.0, 75, Easing.CubicInOut);

            // Navigate to detail page
            await Navigation.PushAsync(new ProductDetailPage(tappedProduct));
        }
    }
    
    private void OnCustomerInfoClicked(object sender, EventArgs e)
    {
        Navigation.PushAsync(new CustomerInfoPage(
            SelectedCustomer,
            _databaseService,
            _authenticationService));
    }
    
    private async void OnGetDirectionsClicked(object sender, EventArgs e)
    {
        string fullAddress = $"{SelectedCustomer.Address1}, {SelectedCustomer.City}, {SelectedCustomer.State}";

        if (string.IsNullOrWhiteSpace(SelectedCustomer.Address1) ||
            string.IsNullOrWhiteSpace(SelectedCustomer.City) ||
            string.IsNullOrWhiteSpace(SelectedCustomer.State))
        {
            await DisplayAlert("Error", "Customer address is incomplete.", "OK");
            return;
        } 
        
        try
        { 
            var encodedAddress = Uri.EscapeDataString(fullAddress);

#if ANDROID
            var uri = $"geo:0,0?q={encodedAddress}";
#elif IOS
            var uri = $"http://maps.apple.com/?daddr={encodedAddress}";
#else
            var uri = $"https://www.google.com/maps/dir/?api=1&destination={encodedAddress}";
#endif

            await Launcher.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Unable to open maps: {ex.Message}", "OK");
        }
    }
}

public class Job
{
    public string JobNumber { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string OEMNumber { get; set; } = string.Empty;
    public string InstallDate { get; set; } = string.Empty;
    public string MachineType { get; set; } = string.Empty;
}

public class Product
{
    public string Type { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Available { get; set; }
}
