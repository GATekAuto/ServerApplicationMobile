using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class ServiceTicketDetailPage : ContentPage
{
    private readonly ServiceTicket _ticket;
    private readonly DatabaseService _databaseService;
    private bool _loaded;

    public ServiceTicketDetailPage(ServiceTicket ticket, DatabaseService databaseService)
    {
        InitializeComponent();
        _ticket = ticket;
        _databaseService = databaseService;
        BindingContext = ticket;
        Title = ticket.TicketNumber;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        ActivityIndicator.IsRunning = true;
        ActivityIndicator.IsVisible = true;
        try
        {
            BindingContext = await _databaseService.GetServiceTicketAsync(_ticket.TicketNumber);
            _loaded = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Service Ticket", $"Unable to load the full ticket: {ex.Message}", "OK");
        }
        finally
        {
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }
}
