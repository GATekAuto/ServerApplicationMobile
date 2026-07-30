using ServerApplicationMobile.Services;
using System.Collections.Specialized;

namespace ServerApplicationMobile;

public partial class AppTabbedPage
{
    private readonly ChatService _chatService;
    private readonly ChatNotificationService _chatNotificationService;
    private readonly AuthenticationService _authenticationService;
    private readonly ChatPage _chatPage;
    private readonly TabBar _tabBar;
    private readonly Tab _chatTab;
    private readonly SemaphoreSlim _alertGate = new(1, 1);
    private readonly SemaphoreSlim _notificationNavigationGate = new(1, 1);
    private bool _subscribed;

    public AppTabbedPage(
        DatabaseService databaseService,
        CustomerDataService customerDataService,
        ChatService chatService,
        ChatNotificationService chatNotificationService,
        AuthenticationService authenticationService)
    {
        InitializeComponent();

        _chatService = chatService;
        _chatNotificationService = chatNotificationService;
        _authenticationService = authenticationService;

        var customersPage = new CustomersPage(databaseService, customerDataService, authenticationService);
        _chatPage = new ChatPage(chatService);
        var mapPage = new MapPage(databaseService, customerDataService, authenticationService);
        var menuPage = new MenuPage();

        ApplyLoggedInTitle(customersPage, showBrandLogo: true);
        ApplyLoggedInTitle(_chatPage, showBrandLogo: true);
        ApplyLoggedInTitle(mapPage, showBrandLogo: true);
        ApplyLoggedInTitle(menuPage, showBrandLogo: true);

        var customersTab = CreateTab("Customers", "customers.png", "customers", customersPage);
        _chatTab = CreateTab("Chat", "chat.png", "chat", _chatPage);
        _tabBar = new TabBar();
        _tabBar.Items.Add(customersTab);
        _tabBar.Items.Add(_chatTab);
        _tabBar.Items.Add(CreateTab("Map", "map.png", "map", mapPage));
        _tabBar.Items.Add(CreateTab("Menu", "menu.png", "menu", menuPage));
        _tabBar.PropertyChanged += OnTabBarPropertyChanged;

        Items.Add(_tabBar);
        CurrentItem = customersTab;
        Navigated += OnShellNavigated;
        SubscribeToChatNotifications();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SubscribeToChatNotifications();
        ApplyLoggedInTitle(CurrentPage);

        try
        {
            await _chatNotificationService.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Chat notification initialization failed: {ex.Message}");
        }

        await TryOpenPendingNotificationAsync();
    }

    private void OnShellNavigated(object sender, ShellNavigatedEventArgs e)
    {
        ApplyLoggedInTitle(CurrentPage);
    }

    private void ApplyLoggedInTitle(Page page, bool showBrandLogo = false)
    {
        if (page == null || Shell.GetTitleView(page) != null)
            return;

        var user = _authenticationService.CurrentUser;
        var serviceTechName = !string.IsNullOrWhiteSpace(user?.DisplayName)
            ? user.DisplayName
            : user?.UserID ?? "Service Tech";

        View leadingTitle;
        if (showBrandLogo)
        {
            leadingTitle = new Image
            {
                Source = "atek_logo.png",
                WidthRequest = 76,
                HeightRequest = 28,
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            };
        }
        else
        {
            var pageTitle = new Label
            {
                Text = page.Title,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            page.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Page.Title))
                    pageTitle.Text = page.Title;
            };
            leadingTitle = pageTitle;
        }

        var loggedInLabel = new Label
        {
            Text = $"{serviceTechName}",
            FontSize = 10,
            TextColor = Colors.White,
            Opacity = 0.82,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaximumWidthRequest = 190
        };

        var titleView = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            ColumnSpacing = 12,
            Padding = new Thickness(0, 0, 8, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        titleView.Add(leadingTitle);
        titleView.Add(loggedInLabel, column: 1);
        Shell.SetTitleView(page, titleView);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UnsubscribeFromChatNotifications();
    }

    private void SubscribeToChatNotifications()
    {
        if (_subscribed)
            return;

        _chatService.NewChatOpened += OnNewChatOpened;
        _chatService.CustomerMessageReceived += OnCustomerMessageReceived;
        _chatService.ServiceTechMessageReceived += OnServiceTechMessageReceived;
        _chatService.CustomerChatRead += OnCustomerChatRead;
        _chatService.ServiceTechChatRead += OnServiceTechChatRead;
        _chatService.PropertyChanged += OnChatServicePropertyChanged;
        _chatService.ServiceTechs.CollectionChanged += OnServiceTechsChanged;
        ChatNotificationActivation.ActivationRequested += OnNotificationActivationRequested;
        _subscribed = true;
        UpdateChatTabTitle();
    }

    private void UnsubscribeFromChatNotifications()
    {
        if (!_subscribed)
            return;

        _chatService.NewChatOpened -= OnNewChatOpened;
        _chatService.CustomerMessageReceived -= OnCustomerMessageReceived;
        _chatService.ServiceTechMessageReceived -= OnServiceTechMessageReceived;
        _chatService.CustomerChatRead -= OnCustomerChatRead;
        _chatService.ServiceTechChatRead -= OnServiceTechChatRead;
        _chatService.PropertyChanged -= OnChatServicePropertyChanged;
        _chatService.ServiceTechs.CollectionChanged -= OnServiceTechsChanged;
        ChatNotificationActivation.ActivationRequested -= OnNotificationActivationRequested;
        _subscribed = false;
    }

    private void OnChatServicePropertyChanged(
        object sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatService.TotalUnreadCount))
            UpdateChatTabTitle();
    }

    private void OnNewChatOpened(object sender, ChatSessionOpenedEventArgs e)
    {
        UpdateChatTabTitle();
        if (IsPendingNotificationTarget(e.Session))
            _ = TryOpenPendingNotificationAsync();
        else if (!_chatPage.IsShowingChat(e.Session))
            _ = PresentNewChatNotificationAsync(e.Session);
    }

    private void OnCustomerMessageReceived(
        object sender,
        CustomerChatMessageReceivedEventArgs e)
    {
        UpdateChatTabTitle();
        if (IsPendingNotificationTarget(e.Session))
            _ = TryOpenPendingNotificationAsync();
        else if (!_chatPage.IsShowingChat(e.Session))
            _ = PresentCustomerMessageNotificationAsync(e.Session, e.Message);
    }

    private void OnServiceTechMessageReceived(
        object sender,
        ServiceTechMessageReceivedEventArgs e)
    {
        UpdateChatTabTitle();
        if (IsPendingNotificationTarget(e.Session))
            _ = TryOpenPendingNotificationAsync();
        else if (!IsShowingServiceTechChat(e.Session))
            _ = PresentServiceTechNotificationAsync(e.Session, e.Message);
    }

    private void OnCustomerChatRead(object sender, ChatSessionReadEventArgs e)
    {
        _ = _chatNotificationService.DismissAsync(e.Session);
    }

    private void OnServiceTechChatRead(object sender, ServiceTechSessionReadEventArgs e)
    {
        _ = _chatNotificationService.DismissAsync(e.Session);
    }

    private async Task PresentNewChatNotificationAsync(ChatSession session)
    {
        try
        {
            if (await _chatNotificationService.TryShowAsync(session))
                return;

            await _alertGate.WaitAsync();
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var detail = string.IsNullOrWhiteSpace(session.Details)
                        ? session.Title
                        : $"{session.Title}\n{session.Details}";
                    var openChat = await DisplayAlertAsync(
                        "New customer chat",
                        detail,
                        "Open chat",
                        "Dismiss");
                    if (openChat)
                        OpenChat(session);
                });
            }
            finally
            {
                _alertGate.Release();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"New chat notification failed: {ex.Message}");
        }
    }

    private async Task PresentCustomerMessageNotificationAsync(
        ChatSession session,
        ChatMessageItem message)
    {
        try
        {
            if (await _chatNotificationService.TryShowCustomerMessageAsync(session, message))
                return;

            await PresentInAppAlertAsync(
                session.Title,
                message.Message,
                () =>
                {
                    OpenChat(session);
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Customer message notification failed: {ex.Message}");
        }
    }

    private async Task PresentServiceTechNotificationAsync(
        ServiceTechSession session,
        ChatMessageItem message)
    {
        try
        {
            if (await _chatNotificationService.TryShowAsync(session, message))
                return;

            await PresentInAppAlertAsync(
                $"Message from {session.DisplayName}",
                message.Message,
                async () =>
                {
                    _tabBar.CurrentItem = _chatTab;
                    await _chatPage.OpenServiceTechChatAsync(session);
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Service tech notification failed: {ex.Message}");
        }
    }

    private async Task PresentInAppAlertAsync(
        string title,
        string message,
        Func<Task> openConversation)
    {
        await _alertGate.WaitAsync();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var shouldOpen = await DisplayAlertAsync(
                    title,
                    string.IsNullOrWhiteSpace(message) ? "New chat message" : message,
                    "Open chat",
                    "Dismiss");
                if (shouldOpen)
                    await openConversation();
            });
        }
        finally
        {
            _alertGate.Release();
        }
    }

    private void OnTabBarPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabBar.CurrentItem))
            UpdateChatTabTitle();
    }

    private void OpenChat(ChatSession session)
    {
        _tabBar.CurrentItem = _chatTab;
        _chatPage.OpenChat(session);
        UpdateChatTabTitle();
    }

    private void OnNotificationActivationRequested()
    {
        MainThread.BeginInvokeOnMainThread(
            () => _ = TryOpenPendingNotificationAsync());
    }

    private void OnServiceTechsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (ChatNotificationActivation.Peek()?.Kind ==
            ChatNotificationTargetKind.ServiceTech)
        {
            _ = TryOpenPendingNotificationAsync();
        }
    }

    private async Task TryOpenPendingNotificationAsync()
    {
        if (!_authenticationService.IsAuthenticated)
            return;

        await _notificationNavigationGate.WaitAsync();
        try
        {
            var target = ChatNotificationActivation.Peek();
            if (target == null)
                return;

            if (target.Kind == ChatNotificationTargetKind.Customer)
            {
                var customerChat = _chatService.Chats.FirstOrDefault(session =>
                    string.Equals(
                        session.ChatID,
                        target.ConversationId,
                        StringComparison.OrdinalIgnoreCase));
                if (customerChat == null)
                {
                    _chatService.StartConnecting();
                    return;
                }

                await ShowChatTabRootAsync();
                OpenChat(customerChat);
                ChatNotificationActivation.Complete(target);
                return;
            }

            var serviceTechChat = _chatService.ServiceTechs.FirstOrDefault(session =>
                string.Equals(
                    session.Key,
                    target.ConversationId,
                    StringComparison.OrdinalIgnoreCase));
            if (serviceTechChat == null)
            {
                _chatService.StartConnecting();
                return;
            }

            await ShowChatTabRootAsync();
            await _chatPage.OpenServiceTechChatAsync(serviceTechChat);
            ChatNotificationActivation.Complete(target);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Opening chat notification failed: {ex}");
        }
        finally
        {
            _notificationNavigationGate.Release();
        }
    }

    private async Task ShowChatTabRootAsync()
    {
        _tabBar.CurrentItem = _chatTab;
        if (_chatPage.Navigation.NavigationStack.Count > 1)
            await _chatPage.Navigation.PopToRootAsync(animated: false);
    }

    private static bool IsPendingNotificationTarget(ChatSession session)
    {
        var target = ChatNotificationActivation.Peek();
        return target?.Kind == ChatNotificationTargetKind.Customer &&
            string.Equals(
                target.ConversationId,
                session.ChatID,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPendingNotificationTarget(ServiceTechSession session)
    {
        var target = ChatNotificationActivation.Peek();
        return target?.Kind == ChatNotificationTargetKind.ServiceTech &&
            string.Equals(
                target.ConversationId,
                session.Key,
                StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateChatTabTitle()
    {
        var unreadCount = _chatService.TotalUnreadCount;
        _chatTab.Title = unreadCount == 0
            ? "Chat"
            : $"Chat ({unreadCount})";
        _ = _chatNotificationService.SetUnreadCountAsync(unreadCount);
    }

    private bool IsShowingServiceTechChat(ServiceTechSession session) =>
        CurrentPage is ServiceTechConversationPage page &&
        ReferenceEquals(page.ServiceTech, session);

    private static Tab CreateTab(
        string title,
        ImageSource icon,
        string route,
        ContentPage page)
    {
        var tab = new Tab
        {
            Title = title,
            Icon = icon,
            Route = route
        };
        tab.Items.Add(new ShellContent
        {
            Title = title,
            Content = page,
            Route = $"{route}Root"
        });
        return tab;
    }
}
