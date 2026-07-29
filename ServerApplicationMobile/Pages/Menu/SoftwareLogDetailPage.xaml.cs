using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class SoftwareLogDetailPage : ContentPage
{
    private readonly SoftwareLog _log;
    private readonly DatabaseService _databaseService;
    private bool _loaded;

    public SoftwareLogDetailPage(
        SoftwareLog log,
        DatabaseService databaseService,
        bool canViewInternalNotes)
    {
        InitializeComponent();
        _log = log;
        _databaseService = databaseService;
        InternalNotesHeading.IsVisible = canViewInternalNotes;
        InternalNotesLabel.IsVisible = canViewInternalNotes;
        BindingContext = log;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        ActivityIndicator.IsRunning = true;
        ActivityIndicator.IsVisible = true;
        try
        {
            BindingContext = await _databaseService.GetSoftwareLogAsync(_log.ID);
            _loaded = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Software Log", $"Unable to load the full log: {ex.Message}", "OK");
        }
        finally
        {
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }
}
