using System.Collections.Specialized;
using System.ComponentModel;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class ServiceTechConversationPage : ContentPage
{
    private readonly ChatService _chatService;
    private bool _subscribed;

    public ServiceTechConversationPage(
        ChatService chatService,
        ServiceTechSession serviceTech)
    {
        InitializeComponent();
        _chatService = chatService;
        ServiceTech = serviceTech;
        Title = serviceTech.DisplayName;
        BindingContext = this;
    }

    public ServiceTechSession ServiceTech { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_subscribed)
        {
            ServiceTech.PropertyChanged += OnServiceTechPropertyChanged;
            ServiceTech.Messages.CollectionChanged += OnMessagesChanged;
            _chatService.PropertyChanged += OnChatServicePropertyChanged;
            _subscribed = true;
        }

        _chatService.MarkServiceTechRead(ServiceTech);
        UpdateComposerState();
        ScrollMessagesToEnd();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_subscribed)
            return;

        ServiceTech.PropertyChanged -= OnServiceTechPropertyChanged;
        ServiceTech.Messages.CollectionChanged -= OnMessagesChanged;
        _chatService.PropertyChanged -= OnChatServicePropertyChanged;
        _subscribed = false;
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        await SendCurrentMessageAsync();
    }

    private async void OnMessageCompleted(object sender, EventArgs e)
    {
        await SendCurrentMessageAsync();
    }

    private async Task SendCurrentMessageAsync()
    {
        var text = MessageEntry.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        MessageEntry.IsEnabled = false;
        SendButton.IsEnabled = false;
        try
        {
            if (await _chatService.SendServiceTechMessageAsync(ServiceTech, text))
            {
                MessageEntry.Text = string.Empty;
                ScrollMessagesToEnd();
            }
            else
            {
                await DisplayAlertAsync(
                    "Service tech chat",
                    "The message could not be delivered. The technician may have disconnected.",
                    "OK");
            }
        }
        finally
        {
            UpdateComposerState();
            if (MessageEntry.IsEnabled)
                MessageEntry.Focus();
        }
    }

    private void OnMessagesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _chatService.MarkServiceTechRead(ServiceTech);
        ScrollMessagesToEnd();
    }

    private void OnServiceTechPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServiceTechSession.DisplayName))
            Title = ServiceTech.DisplayName;
        UpdateComposerState();
    }

    private void OnChatServicePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatService.ConnectionStatus))
            UpdateComposerState();
    }

    private void UpdateComposerState()
    {
        var canSend = ServiceTech.CanSend &&
            _chatService.ConnectionStatus == "Connected";
        MessageEntry.IsEnabled = canSend;
        SendButton.IsEnabled = canSend;
    }

    private void ScrollMessagesToEnd()
    {
        var lastMessage = ServiceTech.Messages.LastOrDefault();
        if (lastMessage == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (ServiceTech.Messages.Contains(lastMessage))
                    MessagesCollection.ScrollTo(lastMessage, position: ScrollToPosition.End);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Service tech message scroll failed: {ex.Message}");
            }
        });
    }
}
