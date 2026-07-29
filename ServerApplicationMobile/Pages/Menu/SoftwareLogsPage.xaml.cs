using System.Windows.Input;
using ConAuto.SharedEnums;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class SoftwareLogsPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly AuthenticationService _authenticationService;
    private bool _loaded;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _isOpeningLog;

    public SoftwareLogsPage(
        DatabaseService databaseService,
        AuthenticationService authenticationService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _authenticationService = authenticationService;
        LogTypePicker.SelectedIndex = 0;
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
            var logs = await _databaseService.GetSoftwareLogsAsync(
                LogSearchBar.Text,
                null,
                null,
                SelectedLogType());

            var canViewHidden = string.Equals(
                _authenticationService.CurrentUser?.OEMName,
                "ATek Automation",
                StringComparison.OrdinalIgnoreCase);
            LogsCollection.ItemsSource = canViewHidden
                ? logs
                : logs.Where(log => !log.IsHidden).ToList();
            _loaded = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Software Logs", $"Unable to load software logs: {ex.Message}", "OK");
        }
        finally
        {
            _isLoading = false;
            IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private enumSoftwareLogType? SelectedLogType() => LogTypePicker.SelectedIndex switch
    {
        1 => enumSoftwareLogType.BugFixed,
        2 => enumSoftwareLogType.NewFeature,
        3 => enumSoftwareLogType.ReportBug,
        _ => null
    };

    private async void OnLogTapped(object sender, TappedEventArgs e)
    {
        if (_isOpeningLog || e.Parameter is not SoftwareLog log)
            return;

        _isOpeningLog = true;
        try
        {
            var navigation = Shell.Current?.Navigation ?? Navigation;
            await navigation.PushAsync(new SoftwareLogDetailPage(
                log,
                _databaseService,
                _authenticationService.CanEditCustomers));
        }
        finally
        {
            _isOpeningLog = false;
        }
    }

    private async void OnSearchSubmitted(object sender, EventArgs e) => await LoadAsync();
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadAsync();
    private async void OnTypeChanged(object sender, EventArgs e)
    {
        if (_loaded) await LoadAsync();
    }
    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loaded && string.IsNullOrWhiteSpace(e.NewTextValue)) await LoadAsync();
    }
}
