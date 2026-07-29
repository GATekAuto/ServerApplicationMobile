using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class CustomerInfoPage : ContentPage
{
    private readonly Customer _selectedCustomer;
    private readonly DatabaseService _databaseService;
    private readonly AuthenticationService _authenticationService;
    private Customer _fullCustomer;
    private bool _isLoaded;
    private bool _isEditing;
    private bool _suppressAddressEvents;

    public CustomerInfoPage(
        Customer customer,
        DatabaseService databaseService,
        AuthenticationService authenticationService)
    {
        InitializeComponent();
        _selectedCustomer = customer ?? throw new ArgumentNullException(nameof(customer));
        _databaseService = databaseService;
        _authenticationService = authenticationService;
        EditButton.IsVisible = authenticationService.CanEditCustomers;
        PopulateForm(customer);
        SetEditing(false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isLoaded)
            return;

        await LoadFullCustomerAsync();
    }

    private async Task LoadFullCustomerAsync()
    {
        SetBusy(true);
        ErrorLabel.IsVisible = false;
        try
        {
            _fullCustomer = await _databaseService.GetCustomerAsync(_selectedCustomer);
            PopulateForm(_fullCustomer);
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Unable to load the complete customer record: {ex.Message}";
            ErrorLabel.IsVisible = true;
            EditButton.IsEnabled = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (!_authenticationService.CanEditCustomers)
        {
            await DisplayAlert("Action not allowed", "Your License Manager role cannot edit customers.", "OK");
            return;
        }

        if (!_isLoaded)
            await LoadFullCustomerAsync();
        if (_fullCustomer == null)
            return;

        SetEditing(true);
        ContactEntry.Focus();
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        if (_fullCustomer != null)
            PopulateForm(_fullCustomer);
        SetEditing(false);
        ErrorLabel.IsVisible = false;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!_isEditing || _fullCustomer == null || !_authenticationService.CanEditCustomers)
            return;

        SetBusy(true);
        ErrorLabel.IsVisible = false;
        try
        {
            var updatedCustomer = _fullCustomer.CreateCopy();
            ApplyForm(updatedCustomer);
            await _databaseService.UpdateCustomerAsync(updatedCustomer);
            _fullCustomer = updatedCustomer;
            _selectedCustomer.CopyFrom(updatedCustomer);
            SetEditing(false);
            await DisplayAlert("Customer updated", "The customer information was saved successfully.", "OK");
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Unable to save customer information: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateForm(Customer customer)
    {
        _suppressAddressEvents = true;
        try
        {
            CustomerNameEntry.Text = customer.CustomerName;
            OemEntry.Text = customer.OEM;
            ContactEntry.Text = customer.Contact;
            PhoneEntry.Text = customer.Phone;
            EmailEntry.Text = customer.Email;
            BillingAddress1Entry.Text = customer.Address1;
            BillingAddress2Entry.Text = customer.Address2;
            BillingCityEntry.Text = customer.City;
            BillingStateEntry.Text = customer.State;
            BillingZipEntry.Text = customer.Zip;
            BillingCountryEntry.Text = customer.Country;
            ShippingAddress1Entry.Text = customer.ShippingAddress1;
            ShippingAddress2Entry.Text = customer.ShippingAddress2;
            ShippingCityEntry.Text = customer.ShippingCity;
            ShippingStateEntry.Text = customer.ShippingState;
            ShippingZipEntry.Text = customer.ShippingZip;
            ShippingCountryEntry.Text = customer.ShippingCountry;
            SameAddressCheckBox.IsChecked = AddressesMatch();
        }
        finally
        {
            _suppressAddressEvents = false;
        }
        UpdateShippingEditability();
    }

    private void ApplyForm(Customer customer)
    {
        customer.Contact = Clean(ContactEntry.Text);
        customer.Phone = Clean(PhoneEntry.Text);
        customer.Email = Clean(EmailEntry.Text);
        customer.Address1 = Clean(BillingAddress1Entry.Text);
        customer.Address2 = Clean(BillingAddress2Entry.Text);
        customer.City = Clean(BillingCityEntry.Text);
        customer.State = Clean(BillingStateEntry.Text);
        customer.Zip = Clean(BillingZipEntry.Text);
        customer.Country = Clean(BillingCountryEntry.Text);
        customer.ShippingAddress1 = Clean(ShippingAddress1Entry.Text);
        customer.ShippingAddress2 = Clean(ShippingAddress2Entry.Text);
        customer.ShippingCity = Clean(ShippingCityEntry.Text);
        customer.ShippingState = Clean(ShippingStateEntry.Text);
        customer.ShippingZip = Clean(ShippingZipEntry.Text);
        customer.ShippingCountry = Clean(ShippingCountryEntry.Text);
    }

    private void OnSameAddressChanged(object sender, CheckedChangedEventArgs e)
    {
        if (_suppressAddressEvents)
            return;
        if (e.Value)
            CopyBillingToShipping();
        UpdateShippingEditability();
    }

    private void OnBillingAddressChanged(object sender, TextChangedEventArgs e)
    {
        if (!_suppressAddressEvents && _isEditing && SameAddressCheckBox.IsChecked)
            CopyBillingToShipping();
    }

    private void CopyBillingToShipping()
    {
        _suppressAddressEvents = true;
        try
        {
            ShippingAddress1Entry.Text = BillingAddress1Entry.Text;
            ShippingAddress2Entry.Text = BillingAddress2Entry.Text;
            ShippingCityEntry.Text = BillingCityEntry.Text;
            ShippingStateEntry.Text = BillingStateEntry.Text;
            ShippingZipEntry.Text = BillingZipEntry.Text;
            ShippingCountryEntry.Text = BillingCountryEntry.Text;
        }
        finally
        {
            _suppressAddressEvents = false;
        }
    }

    private bool AddressesMatch()
    {
        return Same(BillingAddress1Entry.Text, ShippingAddress1Entry.Text) &&
               Same(BillingAddress2Entry.Text, ShippingAddress2Entry.Text) &&
               Same(BillingCityEntry.Text, ShippingCityEntry.Text) &&
               Same(BillingStateEntry.Text, ShippingStateEntry.Text) &&
               Same(BillingZipEntry.Text, ShippingZipEntry.Text) &&
               Same(BillingCountryEntry.Text, ShippingCountryEntry.Text);
    }

    private void SetEditing(bool isEditing)
    {
        _isEditing = isEditing;
        EditButton.IsVisible = !isEditing && _authenticationService.CanEditCustomers;
        SaveButton.IsVisible = isEditing;
        CancelButton.IsVisible = isEditing;
        SameAddressCheckBox.IsEnabled = isEditing;

        foreach (var entry in EditableEntries())
            entry.IsReadOnly = !isEditing;
        UpdateShippingEditability();
    }

    private void UpdateShippingEditability()
    {
        var shippingReadOnly = !_isEditing || SameAddressCheckBox.IsChecked;
        foreach (var entry in ShippingEntries())
            entry.IsReadOnly = shippingReadOnly;
    }

    private IEnumerable<Entry> EditableEntries()
    {
        yield return ContactEntry;
        yield return PhoneEntry;
        yield return EmailEntry;
        yield return BillingAddress1Entry;
        yield return BillingAddress2Entry;
        yield return BillingCityEntry;
        yield return BillingStateEntry;
        yield return BillingZipEntry;
        yield return BillingCountryEntry;
        foreach (var entry in ShippingEntries())
            yield return entry;
    }

    private IEnumerable<Entry> ShippingEntries()
    {
        yield return ShippingAddress1Entry;
        yield return ShippingAddress2Entry;
        yield return ShippingCityEntry;
        yield return ShippingStateEntry;
        yield return ShippingZipEntry;
        yield return ShippingCountryEntry;
    }

    private void SetBusy(bool isBusy)
    {
        ActivityIndicator.IsRunning = isBusy;
        ActivityIndicator.IsVisible = isBusy;
        FormScroll.IsEnabled = !isBusy;
        SaveButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = !isBusy;
        EditButton.IsEnabled = !isBusy && (_isLoaded || _fullCustomer != null);
    }

    private static string Clean(string value) => value?.Trim() ?? string.Empty;
    private static bool Same(string first, string second) =>
        string.Equals(Clean(first), Clean(second), StringComparison.OrdinalIgnoreCase);
}
