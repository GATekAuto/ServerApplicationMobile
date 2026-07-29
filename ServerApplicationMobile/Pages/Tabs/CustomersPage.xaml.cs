using System.Net.Http;
using System.Net.Http.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class CustomersPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly CustomerDataService _customerDataService;
    private readonly AuthenticationService _authenticationService;
    private IReadOnlyList<Customer> _customers = Array.Empty<Customer>();
    private bool _isLoading = false;
    private bool _hasLoaded;
    private bool _isOpeningCustomer;

    public CustomersPage(
        DatabaseService databaseService,
        CustomerDataService customerDataService,
        AuthenticationService authenticationService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _customerDataService = customerDataService;
        _authenticationService = authenticationService;
        this.Appearing += CustomersPage_Appearing;
    }

    private async void CustomersPage_Appearing(object sender, EventArgs e)
    {
        if (!_isLoading && !_hasLoaded)
        {
            await LoadCustomersAsync();
        }
    }

    private async Task LoadCustomersAsync()
    {
        if (_isLoading) return;
        
        _isLoading = true;
        
        try
        {
            System.Diagnostics.Debug.WriteLine("CustomersPage: Starting to load customers...");
            
            // Show loading state (if you have a loading indicator in XAML)
            // LoadingIndicator.IsVisible = true;
            // LoadingIndicator.IsRunning = true;
            
            // Load customers from database
            System.Diagnostics.Debug.WriteLine("CustomersPage: Calling GetCustomersAsync...");
            _customers = await _customerDataService.GetCustomersAsync();
            
            System.Diagnostics.Debug.WriteLine($"CustomersPage: Received {_customers.Count} customers");
            
            if (_customers.Any())
            {
                System.Diagnostics.Debug.WriteLine($"CustomersPage: Setting ItemsSource with {_customers.Count} customers");
                
                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CustomerCollectionView.ItemsSource = _customers;
                    _hasLoaded = true;
                    System.Diagnostics.Debug.WriteLine($"CustomersPage: ItemsSource set. CollectionView has {CustomerCollectionView.ItemsSource?.Cast<object>()?.Count() ?? 0} items");
                });
                
                // Log first few customers for verification
                int count = 0;
                foreach (var customer in _customers.Take(3))
                {
                    count++;
                    System.Diagnostics.Debug.WriteLine($"  Customer {count}: ID={customer.CustomerID}, Name={customer.CustomerName}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CustomersPage: No customers returned from database");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CustomerCollectionView.ItemsSource = new List<Customer>();
                    _hasLoaded = true;
                });
                await DisplayAlert("No Data", "No customers found in database.", "OK");
            }
        }
        catch (Exception ex)
        {
            // Log detailed error
            System.Diagnostics.Debug.WriteLine($"CustomersPage ERROR: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"  Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"  Stack: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
            }
            
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                CustomerCollectionView.ItemsSource = new List<Customer>();
                await DisplayAlert("Error", $"Failed to load customers: {ex.Message}\n\nCheck Output window for details.", "OK");
            });
        }
        finally
        {
            _isLoading = false;
            
            // Hide loading state
            // LoadingIndicator.IsVisible = false;
            // LoadingIndicator.IsRunning = false;
            
            System.Diagnostics.Debug.WriteLine("CustomersPage: Load customers completed");
        }
    }

    private async void OnCustomerTapped(object sender, TappedEventArgs e)
    {
        if (_isOpeningCustomer || e.Parameter is not Customer selectedCustomer)
            return;

        _isOpeningCustomer = true;
        try
        {
            if (sender is TapGestureRecognizer { Parent: VisualElement customerCard })
            {
                await customerCard.ScaleToAsync(0.97, 70, Easing.CubicOut);
                await customerCard.ScaleToAsync(1.0, 90, Easing.CubicIn);
            }

            await Navigation.PushAsync(new CustomerDetailPage(
                selectedCustomer,
                _databaseService,
                _authenticationService));
        }
        finally
        {
            _isOpeningCustomer = false;
        }
    }

    private void OnPageLoaded(object sender, EventArgs e)
    {
        CustomerCollectionView.InvalidateMeasure();
        this.ForceLayout();
    }

    private void OnSearchSubmitted(object sender, EventArgs e)
    {
        DismissCustomerSearchKeyboard();
        string filter = CustomerSearchBar.Text?.ToLowerInvariant().Trim();

        if (string.IsNullOrEmpty(filter))
        {
            CustomerCollectionView.ItemsSource = _customers;
            return;
        }

        var filtered = _customers
            .Where(c =>
                (!string.IsNullOrEmpty(c.CustomerName) && c.CustomerName.ToLowerInvariant().Contains(filter)) ||
                (!string.IsNullOrEmpty(c.OEM) && c.OEM.ToLowerInvariant().Contains(filter)) ||
                (!string.IsNullOrEmpty(c.City) && c.City.ToLowerInvariant().Contains(filter)) ||
                (!string.IsNullOrEmpty(c.State) && c.State.ToLowerInvariant().Contains(filter)) ||
                (!string.IsNullOrEmpty(c.Email) && c.Email.ToLowerInvariant().Contains(filter))
            )
            .ToList();

        CustomerCollectionView.ItemsSource = filtered;
    }

    private void OnCustomerSearchFocused(object sender, FocusEventArgs e)
    {
        DismissSearchButton.IsVisible = DeviceInfo.Platform == DevicePlatform.iOS;
    }

    private void OnCustomerSearchUnfocused(object sender, FocusEventArgs e)
    {
        DismissSearchButton.IsVisible = false;
    }

    private void OnDismissSearchClicked(object sender, EventArgs e)
    {
        DismissCustomerSearchKeyboard();
    }

    private void OnCustomerListScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        if (CustomerSearchBar.IsFocused)
            DismissCustomerSearchKeyboard();
    }

    private void DismissCustomerSearchKeyboard()
    {
        DismissSearchButton.IsVisible = false;
        CustomerSearchBar.Unfocus();
#if IOS
        if (CustomerSearchBar.Handler?.PlatformView is UIKit.UISearchBar nativeSearchBar)
            nativeSearchBar.ResignFirstResponder();
#endif
    }

    private void CustomerSearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            CustomerCollectionView.ItemsSource = _customers;
        }
    }
}

public class Customer : INotifyPropertyChanged
{
    private string _contact = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _address1 = string.Empty;
    private string _address2 = string.Empty;
    private string _city = string.Empty;
    private string _state = string.Empty;
    private string _zip = string.Empty;
    private string _country = string.Empty;
    private string _shippingAddress1 = string.Empty;
    private string _shippingAddress2 = string.Empty;
    private string _shippingCity = string.Empty;
    private string _shippingState = string.Empty;
    private string _shippingZip = string.Empty;
    private string _shippingCountry = string.Empty;

    public event PropertyChangedEventHandler PropertyChanged;

    public int CustomerID { get; set; }
    public string TicketStatus { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string OEM { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = string.Empty;
    public string Address1 { get => _address1; set => SetField(ref _address1, value); }
    public string Address2 { get => _address2; set => SetField(ref _address2, value); }
    public string City { get => _city; set => SetField(ref _city, value); }
    public string State { get => _state; set => SetField(ref _state, value); }
    public string ZipCode { get => _zip; set => SetZip(value); }
    public string Zip { get => _zip; set => SetZip(value); }
    public string Country { get => _country; set => SetField(ref _country, value); }
    public string Email { get => _email; set => SetField(ref _email, value); }
    public string Phone { get => _phone; set => SetField(ref _phone, value); }
    public string ContactPerson { get => _contact; set => SetContact(value); }
    public string Contact { get => _contact; set => SetContact(value); }
    public string ShippingAddress1 { get => _shippingAddress1; set => SetField(ref _shippingAddress1, value); }
    public string ShippingAddress2 { get => _shippingAddress2; set => SetField(ref _shippingAddress2, value); }
    public string ShippingCity { get => _shippingCity; set => SetField(ref _shippingCity, value); }
    public string ShippingState { get => _shippingState; set => SetField(ref _shippingState, value); }
    public string ShippingZip { get => _shippingZip; set => SetField(ref _shippingZip, value); }
    public string ShippingCountry { get => _shippingCountry; set => SetField(ref _shippingCountry, value); }
    public bool HasMaintenancePackage { get; set; }
    public bool IsInactive { get; set; }
    public string InactiveReason { get; set; } = string.Empty;
    public bool IsAccountCreated { get; set; }
    public double DiscountPercent { get; set; }
    public string LastOrderDataBlob { get; set; } = string.Empty;
    public int SalesBranch { get; set; }

    public Customer CreateCopy()
    {
        var copy = (Customer)MemberwiseClone();
        copy.PropertyChanged = null;
        return copy;
    }

    public void CopyFrom(Customer source)
    {
        CompanyName = source.CompanyName;
        Contact = source.Contact;
        Email = source.Email;
        Phone = source.Phone;
        Address1 = source.Address1;
        Address2 = source.Address2;
        City = source.City;
        State = source.State;
        Zip = source.Zip;
        Country = source.Country;
        ShippingAddress1 = source.ShippingAddress1;
        ShippingAddress2 = source.ShippingAddress2;
        ShippingCity = source.ShippingCity;
        ShippingState = source.ShippingState;
        ShippingZip = source.ShippingZip;
        ShippingCountry = source.ShippingCountry;
        HasMaintenancePackage = source.HasMaintenancePackage;
        IsInactive = source.IsInactive;
        InactiveReason = source.InactiveReason;
        IsAccountCreated = source.IsAccountCreated;
        DiscountPercent = source.DiscountPercent;
        LastOrderDataBlob = source.LastOrderDataBlob;
        SalesBranch = source.SalesBranch;
        TicketStatus = source.TicketStatus;
    }

    private void SetContact(string value)
    {
        if (!SetField(ref _contact, value, nameof(Contact)))
            return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContactPerson)));
    }

    private void SetZip(string value)
    {
        if (!SetField(ref _zip, value, nameof(Zip)))
            return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ZipCode)));
    }

    private bool SetField(ref string field, string value, [CallerMemberName] string propertyName = null)
    {
        value ??= string.Empty;
        if (string.Equals(field, value, StringComparison.Ordinal))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
