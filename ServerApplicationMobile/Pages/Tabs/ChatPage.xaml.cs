using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class ChatPage : ContentPage
{
    private readonly ChatService _chatService;
    private ChatSession _selectedChat;
    private bool _subscribed;
    private bool _isShowingConversation;

    public ChatPage(ChatService chatService)
    {
        InitializeComponent();
        _chatService = chatService;
        BindingContext = this;
    }

    public IEnumerable<ChatSession> Chats => _chatService.Chats;
    public string ConnectionStatus => _chatService.ConnectionStatus;
    public string LastError => _chatService.LastError;
    public string ServiceTechsButtonText => _chatService.ServiceTechUnreadCount == 0
        ? "Service Techs"
        : $"Service Techs ({_chatService.ServiceTechUnreadCount})";
    public bool IsChatListVisible => !_isShowingConversation;
    public bool IsConversationVisible => _isShowingConversation;

    public ChatSession SelectedChat
    {
        get => _selectedChat;
        private set
        {
            if (ReferenceEquals(_selectedChat, value))
                return;

            if (_selectedChat != null)
            {
                _selectedChat.PropertyChanged -= OnSelectedChatPropertyChanged;
                _selectedChat.Messages.CollectionChanged -= OnMessagesChanged;
            }

            _selectedChat = value;

            if (_selectedChat != null)
            {
                _selectedChat.PropertyChanged += OnSelectedChatPropertyChanged;
                _selectedChat.Messages.CollectionChanged += OnMessagesChanged;
                _chatService.MarkRead(_selectedChat);
            }

            MessagesCollection.ItemsSource = _selectedChat?.Messages;
            RaisePropertyChanged();
            UpdateComposerState();
            ScrollMessagesToEnd();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_subscribed)
        {
            _chatService.PropertyChanged += OnChatServicePropertyChanged;
            _chatService.Chats.CollectionChanged += OnChatsChanged;
            _subscribed = true;
        }

        CloseConversationIfRemoved();

        RaiseConnectionProperties();
        RaisePropertyChanged(nameof(ServiceTechsButtonText));
        await _chatService.ConnectAsync();
        RaiseConnectionProperties();

    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_subscribed)
        {
            _chatService.PropertyChanged -= OnChatServicePropertyChanged;
            _chatService.Chats.CollectionChanged -= OnChatsChanged;
            _subscribed = false;
        }
    }

    public void OpenChat(ChatSession session)
    {
        if (session == null || !_chatService.Chats.Contains(session))
            return;

        SelectedChat = session;
        ChatsCollection.SelectedItem = session;
        ShowConversation(true);
    }

    public bool IsShowingChat(ChatSession session) =>
        _isShowingConversation && ReferenceEquals(SelectedChat, session);

    private void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ChatSession selectedChat)
            return;

        OpenChat(selectedChat);
    }

    private void OnChatCardTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is ChatSession selectedChat)
            OpenChat(selectedChat);
    }

    private void OnBackToChatsClicked(object sender, EventArgs e)
    {
        ShowConversation(false);
        ChatsCollection.SelectedItem = null;
        SelectedChat = null;
    }

    private async void OnJoinClicked(object sender, EventArgs e)
    {
        if (SelectedChat == null)
            return;

        JoinButton.IsEnabled = false;
        try
        {
            if (!await _chatService.AcceptChatAsync(SelectedChat))
            {
                var detail = string.IsNullOrWhiteSpace(_chatService.LastError)
                    ? "Check the server connection and try again."
                    : _chatService.LastError;
                await DisplayAlert("Unable to join chat", detail, "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Join UI failed: {ex}");
            await DisplayAlert("Unable to join chat", ex.Message, "OK");
        }
        finally
        {
            UpdateComposerState();
        }
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
        if (SelectedChat == null || string.IsNullOrWhiteSpace(text))
            return;

        SendButton.IsEnabled = false;
        MessageEntry.IsEnabled = false;

        if (await _chatService.SendMessageAsync(SelectedChat, text))
        {
            MessageEntry.Text = string.Empty;
            ScrollMessagesToEnd();
        }
        else
        {
            await DisplayAlert("Chat", "The message could not be delivered.", "OK");
        }

        UpdateComposerState();
        if (MessageEntry.IsEnabled)
            MessageEntry.Focus();
    }

    private async void OnReconnectClicked(object sender, EventArgs e)
    {
        await _chatService.ConnectAsync();
        RaiseConnectionProperties();
    }

    private async void OnServiceTechsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ServiceTechsPage(_chatService));
    }

    public async Task OpenServiceTechChatAsync(ServiceTechSession session)
    {
        if (session == null || !_chatService.ServiceTechs.Contains(session))
            return;

        await Navigation.PushAsync(new ServiceTechConversationPage(_chatService, session));
    }

    private void OnChatServicePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatService.ConnectionStatus) ||
            e.PropertyName == nameof(ChatService.LastError))
        {
            RaiseConnectionProperties();
            UpdateComposerState();
        }

        if (e.PropertyName == nameof(ChatService.ServiceTechUnreadCount))
            RaisePropertyChanged(nameof(ServiceTechsButtonText));
    }

    private void OnSelectedChatPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(SelectedChat));
        UpdateComposerState();
    }

    private void OnChatsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(Chats));
        CloseConversationIfRemoved();
    }

    private void CloseConversationIfRemoved()
    {
        if (SelectedChat == null || _chatService.Chats.Contains(SelectedChat))
            return;

        ShowConversation(false);
        ChatsCollection.SelectedItem = null;
        SelectedChat = null;
    }

    private void ShowConversation(bool show)
    {
        if (_isShowingConversation == show)
            return;

        _isShowingConversation = show;
        RaisePropertyChanged(nameof(IsChatListVisible));
        RaisePropertyChanged(nameof(IsConversationVisible));
    }

    private void OnMessagesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedChat != null)
            _chatService.MarkRead(SelectedChat);
        ScrollMessagesToEnd();
    }

    private void UpdateComposerState()
    {
        var connected = _chatService.ConnectionStatus == "Connected";
        JoinButton.IsEnabled = connected && SelectedChat?.CanJoin == true;
        JoinButton.IsVisible = SelectedChat?.CanJoin == true;
        MessageEntry.IsEnabled = connected && SelectedChat?.CanSend == true;
        SendButton.IsEnabled = connected && SelectedChat?.CanSend == true;
    }

    private void ScrollMessagesToEnd()
    {
        var selectedChat = SelectedChat;
        var lastMessage = selectedChat?.Messages.LastOrDefault();
        if (lastMessage == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (ReferenceEquals(SelectedChat, selectedChat) &&
                    selectedChat.Messages.Contains(lastMessage))
                {
                    MessagesCollection.ScrollTo(lastMessage, position: ScrollToPosition.End);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chat message scroll failed: {ex.Message}");
            }
        });
    }

    private void RaiseConnectionProperties()
    {
        RaisePropertyChanged(nameof(ConnectionStatus));
        RaisePropertyChanged(nameof(LastError));
    }

    private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
    {
        OnPropertyChanged(propertyName);
    }
}
