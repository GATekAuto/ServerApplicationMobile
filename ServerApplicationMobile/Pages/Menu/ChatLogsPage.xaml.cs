using System.Windows.Input;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class ChatLogsPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly ChatTranscriptService _chatTranscriptService;
    private bool _loaded;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _isOpeningLog;

    public ChatLogsPage(
        DatabaseService databaseService,
        ChatTranscriptService chatTranscriptService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _chatTranscriptService = chatTranscriptService;
        StartDatePicker.Date = DateTime.Today.AddDays(-30);
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
            ChatLogsCollection.ItemsSource = await _databaseService.GetChatLogsAsync(
                ChatSearchBar.Text,
                AllTimeCheckBox.IsChecked ? null : StartDatePicker.Date,
                AllTimeCheckBox.IsChecked ? null : EndDatePicker.Date);
            _loaded = true;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Chat Logs", $"Unable to load chat logs: {ex.Message}", "OK");
        }
        finally
        {
            _isLoading = false;
            IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private async void OnChatLogTapped(object sender, TappedEventArgs e)
    {
        if (_isOpeningLog || e.Parameter is not ChatLog chatLog)
            return;

        _isOpeningLog = true;
        try
        {
            var navigation = Shell.Current?.Navigation ?? Navigation;
            await navigation.PushAsync(new ChatLogDetailPage(chatLog, _chatTranscriptService));
        }
        finally
        {
            _isOpeningLog = false;
        }
    }

    private async void OnSearchSubmitted(object sender, EventArgs e) => await LoadAsync();
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadAsync();

    private async void OnFilterChanged(object sender, DateChangedEventArgs e)
    {
        if (_loaded && !AllTimeCheckBox.IsChecked)
            await LoadAsync();
    }

    private async void OnAllTimeChanged(object sender, CheckedChangedEventArgs e)
    {
        StartDatePicker.IsEnabled = !e.Value;
        EndDatePicker.IsEnabled = !e.Value;
        if (_loaded)
            await LoadAsync();
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loaded && string.IsNullOrWhiteSpace(e.NewTextValue))
            await LoadAsync();
    }
}
