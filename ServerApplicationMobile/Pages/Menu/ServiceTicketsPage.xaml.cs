using System.Windows.Input;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class ServiceTicketsPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private bool _loaded;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _isOpeningTicket;

    public ServiceTicketsPage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        StartDatePicker.Date = DateTime.Today.AddDays(-14);
        EndDatePicker.Date = DateTime.Today;
        RefreshCommand = new Command(async () => await LoadAsync());
        BindingContext = this;
    }

    public ICommand RefreshCommand { get; }
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged(nameof(IsRefreshing));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        IsRefreshing = true;
        ActivityIndicator.IsRunning = true;
        ActivityIndicator.IsVisible = true;
        try
        {
            TicketsCollection.ItemsSource = await _databaseService.GetServiceTicketsAsync(
                TicketSearchBar.Text,
                StartDatePicker.Date,
                EndDatePicker.Date,
                OpenOnlyCheckBox.IsChecked);
            _loaded = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Service Tickets", $"Unable to load service tickets: {ex.Message}", "OK");
        }
        finally
        {
            _isLoading = false;
            IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private async void OnTicketTapped(object sender, TappedEventArgs e)
    {
        if (_isOpeningTicket || e.Parameter is not ServiceTicket ticket)
            return;

        _isOpeningTicket = true;
        try
        {
            var navigation = Shell.Current?.Navigation ?? Navigation;
            await navigation.PushAsync(new ServiceTicketDetailPage(ticket, _databaseService));
        }
        finally
        {
            _isOpeningTicket = false;
        }
    }

    private async void OnSearchSubmitted(object sender, EventArgs e) => await LoadAsync();
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadAsync();
    private async void OnFilterChanged(object sender, DateChangedEventArgs e)
    {
        if (_loaded) await LoadAsync();
    }
    private async void OnOpenOnlyChanged(object sender, CheckedChangedEventArgs e)
    {
        if (_loaded) await LoadAsync();
    }
    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loaded && string.IsNullOrWhiteSpace(e.NewTextValue)) await LoadAsync();
    }

}
