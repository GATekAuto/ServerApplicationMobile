using ServerApplicationMobile.Services;
using System.ComponentModel;

namespace ServerApplicationMobile;

public partial class ServiceTechsPage : ContentPage
{
    private readonly ChatService _chatService;
    private bool _isNavigating;

    public ServiceTechsPage(ChatService chatService)
    {
        InitializeComponent();
        _chatService = chatService;
        BindingContext = this;
    }

    public IEnumerable<ServiceTechSession> ServiceTechs => _chatService.ServiceTechs;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _chatService.PropertyChanged += OnChatServicePropertyChanged;
        UpdateTitle();
    }

    protected override void OnDisappearing()
    {
        _chatService.PropertyChanged -= OnChatServicePropertyChanged;
        base.OnDisappearing();
    }

    private void OnChatServicePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatService.ServiceTechUnreadCount))
            UpdateTitle();
    }

    private void UpdateTitle()
    {
        Title = _chatService.ServiceTechUnreadCount == 0
            ? "Service Techs"
            : $"Service Techs ({_chatService.ServiceTechUnreadCount})";
    }

    private async void OnServiceTechTapped(object sender, TappedEventArgs e)
    {
        if (_isNavigating ||
            e.Parameter is not ServiceTechSession serviceTech)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            if (sender is TapGestureRecognizer { Parent: VisualElement serviceTechCard })
            {
                await serviceTechCard.ScaleToAsync(0.97, 70, Easing.CubicOut);
                await serviceTechCard.ScaleToAsync(1.0, 90, Easing.CubicIn);
            }

            await Navigation.PushAsync(
                new ServiceTechConversationPage(_chatService, serviceTech));
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
