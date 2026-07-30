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
            await LoadCustomersAsync();
    }

    private async Task LoadCustomersAsync(bool forceRefresh = false)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        var showInitialLoadingState = !_hasLoaded && !forceRefresh;
        SetInitialLoadingState(showInitialLoadingState);

        try
        {
            var customers = forceRefresh
                ? await _customerDataService.RefreshCustomersAsync()
                : await _customerDataService.GetCustomersAsync();

            _customers = customers
                .OrderBy(customer => customer.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _hasLoaded = true;
            ApplyCustomerFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CustomersPage: Unable to load customers: {ex}");

            if (!_hasLoaded)
                CustomerCollectionView.ItemsSource = new List<Customer>();

            CustomerCountLabel.Text = "Customers unavailable";
            EmptyTitleLabel.Text = "Unable to load customers";
            EmptyDetailLabel.Text = "Pull down to try again.";
            await DisplayAlert("Customers", $"Unable to load customers: {ex.Message}", "OK");
        }
        finally
        {
            _isLoading = false;
            SetInitialLoadingState(false);
            CustomerRefreshView.IsRefreshing = false;
        }
    }

    private void SetInitialLoadingState(bool isLoading)
    {
        LoadingState.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
        CustomerRefreshView.IsVisible = !isLoading;

        if (isLoading)
            CustomerCountLabel.Text = "Loading customers…";
    }

    private void ApplyCustomerFilter()
    {
        var filter = CustomerSearchBar.Text?.Trim();
        IEnumerable<Customer> matches = _customers;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            matches = matches.Where(customer =>
                Contains(customer.CustomerName, filter) ||
                Contains(customer.CompanyName, filter) ||
                Contains(customer.OEM, filter) ||
                Contains(customer.Contact, filter) ||
                Contains(customer.City, filter) ||
                Contains(customer.State, filter) ||
                Contains(customer.Country, filter) ||
                Contains(customer.Email, filter));
        }

        var visibleCustomers = matches.ToList();
        CustomerCollectionView.ItemsSource = visibleCustomers;

        CustomerCountLabel.Text = string.IsNullOrWhiteSpace(filter)
            ? FormatCustomerCount(_customers.Count)
            : $"{visibleCustomers.Count:N0} of {_customers.Count:N0} customers";

        EmptyTitleLabel.Text = string.IsNullOrWhiteSpace(filter)
            ? "No customers found"
            : "No matching customers";
        EmptyDetailLabel.Text = string.IsNullOrWhiteSpace(filter)
            ? "Pull down to try loading the customer list again."
            : "Try a different name, OEM, contact, or location.";
    }

    private static bool Contains(string value, string filter) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string FormatCustomerCount(int count) =>
        count == 1 ? "1 customer" : $"{count:N0} customers";

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

    private void OnSearchSubmitted(object sender, EventArgs e)
    {
        DismissCustomerSearchKeyboard();
        ApplyCustomerFilter();
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
        // Rebuilding hundreds of variable-height cards for every keystroke is
        // noticeably expensive on mobile. Search only on keyboard submission;
        // clearing the field still restores the full list immediately.
        if (_hasLoaded && string.IsNullOrWhiteSpace(e.NewTextValue))
            ApplyCustomerFilter();
    }

    private async void OnCustomersRefreshing(object sender, EventArgs e)
    {
        await LoadCustomersAsync(forceRefresh: true);
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
    public string City { get => _city; set => SetLocationField(ref _city, value); }
    public string State { get => _state; set => SetLocationField(ref _state, value); }
    public string ZipCode { get => _zip; set => SetZip(value); }
    public string Zip { get => _zip; set => SetZip(value); }
    public string Country { get => _country; set => SetLocationField(ref _country, value); }
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

    public string DisplayName => string.IsNullOrWhiteSpace(CustomerName)
        ? CompanyName
        : CustomerName;

    public string Initials
    {
        get
        {
            var words = DisplayName.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0)
                return "?";
            if (words.Length == 1)
                return words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant();

            return string.Concat(words[0][0], words[^1][0]).ToUpperInvariant();
        }
    }

    public string CustomerSummary
    {
        get
        {
            var location = string.Join(", ", new[] { City, State }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(location))
                location = Country;

            var summary = string.Join(" · ", new[] { Contact, location }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(summary) ? "Customer record" : summary;
        }
    }

    public bool HasOem => !string.IsNullOrWhiteSpace(OEM);

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomerSummary)));
    }

    private void SetLocationField(
        ref string field,
        string value,
        [CallerMemberName] string propertyName = null)
    {
        if (SetField(ref field, value, propertyName))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomerSummary)));
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
