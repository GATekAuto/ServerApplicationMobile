using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.ServiceModel;
using System.Text.Json;
using ATekWeb.ATekWebCommonDBData;
using ConAuto.SharedEnums;
using Microsoft.IdentityModel.Tokens;

namespace ServerApplicationMobile.Services;

/// <summary>
/// Mobile client for the same duplex WCF chat service used by ATekServerApplication.
/// </summary>
public sealed class ChatService : INotifyPropertyChanged
{
    private const string Endpoint = "net.tcp://www.atekglobal.com:7373/ATekChatManageSystem/tcp";
    private const string TokenKey =
        "4GTCU7IlT72W+6A6NP+Fts1LXGoPbLyRWg1W4kO9PD+SLA/3eMzz9IGu6CMBnC2CkOwDToXyOvQOAxd13VYs7A==";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Dictionary<string, ChatSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ServiceTechSession> _serviceTechSessions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly AuthenticationService _authenticationService;

    private DuplexChannelFactory<IATekChatWebService> _factory;
    private IATekChatWebService _channel;
    private CancellationTokenSource _watchdogCancellation;
    private string _connectionStatus = "Disconnected";
    private string _lastError = string.Empty;

    public ChatService(AuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
        ChatPushRegistration.TokenChanged += OnPushTokenChanged;
        AddUniversalServiceTechSession();
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler<ChatSessionOpenedEventArgs> NewChatOpened;
    public event EventHandler<CustomerChatMessageReceivedEventArgs> CustomerMessageReceived;
    public event EventHandler<ServiceTechMessageReceivedEventArgs> ServiceTechMessageReceived;
    public event EventHandler<ChatSessionReadEventArgs> CustomerChatRead;
    public event EventHandler<ServiceTechSessionReadEventArgs> ServiceTechChatRead;

    public ObservableCollection<ChatSession> Chats { get; } = new();
    public ObservableCollection<ServiceTechSession> ServiceTechs { get; } = new();

    public int CustomerUnreadCount => Chats.Sum(session => session.UnreadCount);
    public int ServiceTechUnreadCount => ServiceTechs.Sum(session => session.UnreadCount);
    public int TotalUnreadCount => CustomerUnreadCount + ServiceTechUnreadCount;

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetField(ref _connectionStatus, value);
    }

    public string LastError
    {
        get => _lastError;
        private set => SetField(ref _lastError, value);
    }

    public string CurrentUserName
    {
        get
        {
            var user = _authenticationService.CurrentUser;
            return FirstNotEmpty(user?.DisplayName, user?.UserID, "Service Tech");
        }
    }

    public void StartConnecting()
    {
        _ = ObserveConnectionAsync();
    }

    public async Task<bool> ConnectAsync()
    {
        await _connectionGate.WaitAsync();
        try
        {
            var identity = CreateIdentityCredential();
            if (identity == null)
            {
                return false;
            }

            if (_channel is ICommunicationObject existing &&
                existing.State == CommunicationState.Opened)
            {
                return true;
            }

            SetStatus("Connecting", string.Empty);
            AbortChannel();

            var identityJson = Serialize(identity);
            await Task.Run(() =>
            {
                var callback = new ChatCallback(this);
                var binding = CreateBinding();
                var callbackContext = new InstanceContext(callback);
                var endpoint = new EndpointAddress(Endpoint);
#if IOS
                // ChannelFactory<T> generates its service proxy at runtime, which
                // physical iOS devices prohibit. Use the concrete proxy instead.
                var channel = (IATekChatWebService)new IosChatWcfClient(
                    callbackContext,
                    binding,
                    endpoint);
                DuplexChannelFactory<IATekChatWebService> factory = null;
#else
                var factory = new DuplexChannelFactory<IATekChatWebService>(
                    callbackContext,
                    binding,
                    endpoint);
                var channel = factory.CreateChannel();
#endif
                var communicationObject = (ICommunicationObject)channel;

                communicationObject.Faulted += OnChannelUnavailable;
                communicationObject.Closed += OnChannelUnavailable;
#if !IOS
                communicationObject.Open();
#endif

                // ClientBase<T> creates its inner channel lazily. On iOS the
                // AOT-safe concrete proxy must use that implicit first-call path;
                // explicitly opening the outer DuplexClientBase first opens its
                // factory too early and ChannelBase then tries to finish configuring
                // an already-open DuplexChannelFactory.
                var handshake = channel.FirstCall(GenerateToken());
                if (!string.Equals(handshake, "ATek_S_Ok", StringComparison.Ordinal))
                    throw new SecurityTokenException("The chat server rejected the authentication token.");
                if (!channel.RegisterLogin(GenerateToken(), identityJson))
                    throw new CommunicationException("The chat server rejected the mobile login.");

                _factory = factory;
                _channel = channel;
            });

            SetStatus("Connected", string.Empty);
            StartWatchdog();
            _ = UpdatePushRegistrationAsync();
            return true;
        }
        catch (Exception ex)
        {
            AbortChannel();
            SetStatus("Disconnected", string.Empty);
            return false;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task<bool> AcceptChatAsync(ChatSession session)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(session);
            if (string.IsNullOrWhiteSpace(session.ChatID) || session.IsEnded)
                return false;

            var credential = CreateChatCredential(session);
            var message = new CATekChatMessage
            {
                Message = "[Joined Chat]",
                UTime = DateTime.UtcNow
            };

            var accepted = await InvokeAsync(service => service.ConnectChat(
                GenerateToken(),
                Serialize(credential),
                Serialize(message)));

            if (accepted)
            {
                await RunOnMainThreadAsync(() =>
                {
                    session.IsAccepted = true;
                    session.IsJoined = true;
                    session.UnreadCount = 0;
                    session.Messages.Add(ChatMessageItem.Sent(
                        CurrentUserName,
                        message.Message,
                        DateTime.Now));
                });
            }

            return accepted;
        }
        catch (Exception ex)
        {
            SetStatus("Server Failed", string.Empty);
            System.Diagnostics.Debug.WriteLine($"Joining chat failed: {ex}");
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(ChatSession session, string text)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsJoined || session.IsEnded || string.IsNullOrWhiteSpace(text))
            return false;

        var message = new CATekChatMessage
        {
            Message = text.Trim(),
            UTime = DateTime.UtcNow
        };

        var sent = await InvokeAsync(service => service.SendChatMessageToJob(
            GenerateToken(),
            Serialize(CreateChatCredential(session)),
            Serialize(message)));

        if (sent)
        {
            await RunOnMainThreadAsync(() => session.Messages.Add(
                ChatMessageItem.Sent(CurrentUserName, message.Message, DateTime.Now)));
        }

        return sent;
    }

    public async Task<bool> SendServiceTechMessageAsync(
        ServiceTechSession session,
        string text)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsOnline || string.IsNullOrWhiteSpace(text))
            return false;

        var user = _authenticationService.CurrentUser;
        if (user == null)
            return false;

        var credential = new CATekServiceTechCredential
        {
            OEM = user.OEMName,
            UserID = user.UserID,
            Role = user.Role,
            ServiceTechDisplayName = CurrentUserName,
            IsUniversalServiceMessage = session.IsUniversal,
            OEMList = new List<string>(),
            UserIDList = new List<string>(),
            RoleList = new List<enumOEMUserRole>(),
            ServiceTechDisplayNameList = new List<string>()
        };

        if (!session.IsUniversal)
        {
            credential.OEMList.Add(session.OEMName);
            credential.UserIDList.Add(session.UserID);
            credential.RoleList.Add(session.Role);
            credential.ServiceTechDisplayNameList.Add(session.DisplayName);
        }

        var message = new CATekChatMessage
        {
            Message = text.Trim(),
            UTime = DateTime.UtcNow
        };

        var sent = await InvokeAsync(service =>
            service.SendChatMessageToServiceTechFromServiceTech(
                GenerateToken(),
                Serialize(credential),
                Serialize(message)));

        if (sent)
        {
            await RunOnMainThreadAsync(() => session.Messages.Add(
                ChatMessageItem.Sent(CurrentUserName, message.Message, DateTime.Now)));
        }

        return sent;
    }

    public void MarkRead(ChatSession session)
    {
        if (session != null)
            session.UnreadCount = 0;
    }

    public void MarkServiceTechRead(ServiceTechSession session)
    {
        if (session != null)
            session.UnreadCount = 0;
    }

    public async Task SignOutAsync()
    {
        var identity = CreateIdentityCredential();
        if (identity != null && _channel is ICommunicationObject channel &&
            channel.State == CommunicationState.Opened)
        {
            await _operationGate.WaitAsync();
            try
            {
                await Task.Run(() => _channel.RegisterLogout(
                    GenerateToken(),
                    Serialize(identity)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chat logout failed: {ex.Message}");
            }
            finally
            {
                _operationGate.Release();
            }
        }

        AbortChannel();
        await RunOnMainThreadAsync(() =>
        {
            foreach (var session in _sessions.Values)
                session.PropertyChanged -= OnCustomerSessionPropertyChanged;
            foreach (var session in _serviceTechSessions.Values)
                session.PropertyChanged -= OnServiceTechSessionPropertyChanged;

            Chats.Clear();
            _sessions.Clear();
            ServiceTechs.Clear();
            _serviceTechSessions.Clear();
            AddUniversalServiceTechSession();
            ConnectionStatus = "Signed out";
            LastError = string.Empty;
            RaiseUnreadProperties();
        });
    }

    private async Task ObserveConnectionAsync()
    {
        try
        {
            await ConnectAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Disconnected", string.Empty);
        }
    }

    private async Task<bool> InvokeAsync(Func<IATekChatWebService, bool> operation)
    {
        if (!await ConnectAsync())
            return false;

        await _operationGate.WaitAsync();
        try
        {
            var channel = _channel;
            if (channel is not ICommunicationObject communicationObject ||
                communicationObject.State != CommunicationState.Opened)
            {
                return false;
            }

            return await Task.Run(() => operation(channel));
        }
        catch (Exception ex)
        {
            AbortChannel();
            SetStatus("Disconnected", string.Empty);
            return false;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private static NetTcpBinding CreateBinding()
    {
        return new NetTcpBinding(SecurityMode.None, false)
        {
            MaxBufferPoolSize = 67_108_864,
            MaxBufferSize = 67_108_864,
            MaxReceivedMessageSize = 67_108_864,
            TransferMode = TransferMode.Buffered,
            ReceiveTimeout = TimeSpan.FromHours(20),
            OpenTimeout = TimeSpan.FromSeconds(12),
            SendTimeout = TimeSpan.FromSeconds(20)
        };
    }

    private void StartWatchdog()
    {
        _watchdogCancellation?.Cancel();
        _watchdogCancellation?.Dispose();
        _watchdogCancellation = new CancellationTokenSource();
        _ = WatchdogAsync(_watchdogCancellation.Token);
    }

    private async Task WatchdogAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var identity = CreateIdentityCredential();
                if (identity == null)
                    return;

                await InvokeAsync(service => service.ServiceTechWatchDog(
                    GenerateToken(),
                    Serialize(identity)));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnPushTokenChanged(ChatPushToken registration)
    {
        if (_authenticationService.IsAuthenticated)
            _ = UpdatePushRegistrationAsync();
    }

    private async Task UpdatePushRegistrationAsync()
    {
        var identity = CreateIdentityCredential();
        if (identity == null || string.IsNullOrWhiteSpace(identity.MobilePushToken))
            return;

        await _operationGate.WaitAsync();
        try
        {
            var channel = _channel;
            if (channel is not ICommunicationObject communicationObject ||
                communicationObject.State != CommunicationState.Opened)
            {
                return;
            }

            await Task.Run(() => channel.ServiceTechWatchDog(
                GenerateToken(),
                Serialize(identity)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Push token registration will retry on the next chat connection: {ex.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void OnChannelUnavailable(object sender, EventArgs e)
    {
        SetStatus("Disconnected", "Reconnecting...");
        _ = ReconnectAfterDelayAsync();
    }

    private async Task ReconnectAfterDelayAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        await ConnectAsync();
    }

    private void AbortChannel()
    {
        _watchdogCancellation?.Cancel();
        _watchdogCancellation?.Dispose();
        _watchdogCancellation = null;

        if (_channel is ICommunicationObject channel)
        {
            channel.Faulted -= OnChannelUnavailable;
            channel.Closed -= OnChannelUnavailable;
            try { channel.Abort(); } catch { }
        }

        if (_factory != null)
        {
            try { _factory.Abort(); } catch { }
        }

        _channel = null;
        _factory = null;
    }

    private CATekClientCredential CreateChatCredential(ChatSession session)
    {
        var identity = CreateIdentityCredential()
            ?? throw new InvalidOperationException("The user is not signed in.");

        return new CATekClientCredential
        {
            IsJob = false,
            OEM = identity.OEM,
            Role = identity.Role,
            UserID = identity.UserID,
            ServiceTechDisplayName = identity.ServiceTechDisplayName,
            ChatID = session.ChatID,
            JobNumber = session.JobNumber,
            CustomerName = session.CompanyName,
            ComputerName = session.ComputerName
        };
    }

    private MobileChatCredential CreateIdentityCredential()
    {
        var user = _authenticationService.CurrentUser;
        if (user == null)
            return null;

        var identity = new MobileChatCredential
        {
            IsJob = false,
            OEM = user.OEMName,
            Role = user.Role,
            UserID = user.UserID,
            ServiceTechDisplayName = CurrentUserName
        };

        var push = ChatPushRegistration.Current;
        if (push != null)
        {
            identity.MobilePushPlatform = push.Platform;
            identity.MobilePushToken = push.Token;
            identity.MobilePushInstallationId = push.InstallationId;
            identity.MobilePushEnvironment = push.Environment;
        }

        return identity;
    }

    private void HandleNewChat(string credentialJson, string messageJson)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        var message = Deserialize<CATekChatMessage>(messageJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.ChatID))
            return;

        Dispatch(() =>
        {
            var session = GetOrCreateSession(credential, out var wasCreated);
            AddReceivedMessage(session, credential, message);

            // StartChatConnect is the server's new-chat callback. Only notify when it
            // actually creates a session so reconnects and duplicate callbacks do not
            // repeatedly alert the service technician.
            if (wasCreated)
                NewChatOpened?.Invoke(this, new ChatSessionOpenedEventArgs(session));
        });
    }

    private void HandleMessage(string credentialJson, string messageJson, bool isHistory = false)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        var message = Deserialize<CATekChatMessage>(messageJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.ChatID))
            return;

        Dispatch(() =>
        {
            var session = GetOrCreateSession(credential);
            var sender = isHistory
                ? "Chat History"
                : FirstNotEmpty(credential.CustomerDisplayName,
                    credential.ServiceTechDisplayName,
                    "Customer");
            var receivedMessage = ChatMessageItem.Received(
                sender,
                message?.Message ?? string.Empty,
                ToLocalTime(message?.UTime));
            session.UnreadCount++;
            session.Messages.Add(receivedMessage);

            if (!isHistory)
            {
                CustomerMessageReceived?.Invoke(
                    this,
                    new CustomerChatMessageReceivedEventArgs(session, receivedMessage));
            }
        });
    }

    private void HandleExistingChats(string serviceTechCredentialJson)
    {
        var serviceTech = Deserialize<CATekServiceTechCredential>(serviceTechCredentialJson);
        if (serviceTech == null)
            return;

        foreach (var chatJson in serviceTech.ExistingChatList ?? Enumerable.Empty<string>())
        {
            var existing = Deserialize<CATekExistingChat>(chatJson);
            if (existing == null || string.IsNullOrWhiteSpace(existing.ChatID))
                continue;

            Dispatch(() =>
            {
                var credential = new CATekClientCredential
                {
                    ChatID = existing.ChatID,
                    JobNumber = existing.Job,
                    ComputerName = existing.ComputerName,
                    CustomerName = existing.CustomerName,
                    CustomerDisplayName = existing.DisplayName,
                    OEM = existing.OEM,
                    TeamViewerID = existing.TeamViewerID,
                    IsMaintenanceActive = existing.IsMaintenanceActive
                };
                var session = GetOrCreateSession(credential);
                session.IsAccepted = existing.ServiceTechDisplayNameList?.Count > 0;
                if (!session.IsAccepted)
                    session.UnreadCount = Math.Max(session.UnreadCount, 1);
            });
        }

        Dispatch(() => SynchronizeServiceTechs(serviceTech));
    }

    private void HandleServiceTechLogin(string credentialJson)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.UserID))
            return;

        Dispatch(() => GetOrCreateServiceTechSession(
            credential.OEM,
            credential.UserID,
            credential.Role,
            credential.ServiceTechDisplayName));
    }

    private void HandleServiceTechLogout(string credentialJson)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.UserID))
            return;

        Dispatch(() => SetServiceTechOffline(
            credential.OEM,
            credential.UserID,
            credential.Role));
    }

    private void HandleServiceTechMessage(string credentialJson, string messageJson)
    {
        var credential = Deserialize<CATekServiceTechCredential>(credentialJson);
        var message = Deserialize<CATekChatMessage>(messageJson);
        if (credential == null)
            return;

        Dispatch(() =>
        {
            var session = credential.IsUniversalServiceMessage
                ? GetUniversalServiceTechSession()
                : GetOrCreateServiceTechSession(
                    credential.OEM,
                    credential.UserID,
                    credential.Role,
                    credential.ServiceTechDisplayName);

            var receivedMessage = ChatMessageItem.Received(
                FirstNotEmpty(credential.ServiceTechDisplayName, "Service Tech"),
                message?.Message ?? string.Empty,
                ToLocalTime(message?.UTime));
            session.UnreadCount++;
            session.Messages.Add(receivedMessage);
            ServiceTechMessageReceived?.Invoke(
                this,
                new ServiceTechMessageReceivedEventArgs(session, receivedMessage));
        });
    }

    private void SynchronizeServiceTechs(CATekServiceTechCredential credential)
    {
        var onlineKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = new[]
        {
            credential.OEMList?.Count ?? 0,
            credential.UserIDList?.Count ?? 0,
            credential.RoleList?.Count ?? 0,
            credential.ServiceTechDisplayNameList?.Count ?? 0
        }.Min();

        for (var index = 0; index < count; index++)
        {
            var key = CreateServiceTechKey(
                credential.OEMList[index],
                credential.UserIDList[index],
                credential.RoleList[index]);
            onlineKeys.Add(key);
            GetOrCreateServiceTechSession(
                credential.OEMList[index],
                credential.UserIDList[index],
                credential.RoleList[index],
                credential.ServiceTechDisplayNameList[index]);
        }

        foreach (var session in ServiceTechs.Where(item => !item.IsUniversal).ToList())
        {
            if (!onlineKeys.Contains(session.Key))
            {
                session.IsOnline = false;
                if (session.Messages.Count == 0 && session.UnreadCount == 0)
                    ServiceTechs.Remove(session);
            }
        }
    }

    private ServiceTechSession GetOrCreateServiceTechSession(
        string oem,
        string userId,
        enumOEMUserRole role,
        string displayName)
    {
        var key = CreateServiceTechKey(oem, userId, role);
        if (_serviceTechSessions.TryGetValue(key, out var existing))
        {
            existing.Update(displayName, oem, role);
            existing.IsOnline = true;
            if (!ServiceTechs.Contains(existing))
                ServiceTechs.Add(existing);
            return existing;
        }

        var session = new ServiceTechSession(key, userId, displayName, oem, role);
        session.PropertyChanged += OnServiceTechSessionPropertyChanged;
        _serviceTechSessions[key] = session;
        ServiceTechs.Add(session);
        return session;
    }

    private void SetServiceTechOffline(
        string oem,
        string userId,
        enumOEMUserRole role)
    {
        var key = CreateServiceTechKey(oem, userId, role);
        if (!_serviceTechSessions.TryGetValue(key, out var session))
            return;

        session.IsOnline = false;
        if (session.Messages.Count == 0 && session.UnreadCount == 0)
            ServiceTechs.Remove(session);
    }

    private void AddUniversalServiceTechSession()
    {
        var session = ServiceTechSession.CreateUniversal();
        session.PropertyChanged += OnServiceTechSessionPropertyChanged;
        _serviceTechSessions[session.Key] = session;
        ServiceTechs.Add(session);
    }

    private ServiceTechSession GetUniversalServiceTechSession()
    {
        if (_serviceTechSessions.TryGetValue(ServiceTechSession.UniversalKey, out var session))
            return session;

        AddUniversalServiceTechSession();
        return _serviceTechSessions[ServiceTechSession.UniversalKey];
    }

    private static string CreateServiceTechKey(
        string oem,
        string userId,
        enumOEMUserRole role) => $"{oem?.Trim()}\u001f{userId?.Trim()}\u001f{(int)role}";

    private void HandleAccepted(string credentialJson)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.ChatID))
            return;

        Dispatch(() =>
        {
            var session = GetOrCreateSession(credential);
            session.IsAccepted = true;
            session.Messages.Add(ChatMessageItem.Received(
                FirstNotEmpty(credential.ServiceTechDisplayName, "Service Tech"),
                "[Joined Chat]",
                DateTime.Now));
        });
    }

    private void HandleEnded(string credentialJson, string reason)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.ChatID))
            return;

        Dispatch(() =>
        {
            if (!_sessions.Remove(credential.ChatID, out var session))
                return;

            session.PropertyChanged -= OnCustomerSessionPropertyChanged;
            session.IsEnded = true;
            session.Messages.Add(ChatMessageItem.Received("System", reason, DateTime.Now));
            Chats.Remove(session);
            CustomerChatRead?.Invoke(this, new ChatSessionReadEventArgs(session));
            RaiseUnreadProperties();
        });
    }

    private void HandleServiceTechDisconnected(string credentialJson)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.ChatID))
            return;

        Dispatch(() =>
        {
            if (!_sessions.TryGetValue(credential.ChatID, out var session))
                return;

            var serviceTechName = FirstNotEmpty(
                credential.ServiceTechDisplayName,
                "A service tech");
            session.Messages.Add(ChatMessageItem.Received(
                "System",
                $"{serviceTechName} left the chat.",
                DateTime.Now));
        });
    }

    private void HandleTyping(string credentialJson, string messageJson)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        var message = Deserialize<CATekChatMessage>(messageJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.ChatID))
            return;

        Dispatch(() => GetOrCreateSession(credential).IsCustomerTyping = message?.IsTyping == true);
    }

    private void HandleFileMessage(string credentialJson, string messageJson)
    {
        var credential = Deserialize<CATekClientCredential>(credentialJson);
        var message = Deserialize<CATekChatMessage>(messageJson);
        if (credential == null || string.IsNullOrWhiteSpace(credential.ChatID))
            return;

        Dispatch(() =>
        {
            var session = GetOrCreateSession(credential);
            var fileName = FirstNotEmpty(message?.FileName, "file");
            session.UnreadCount++;
            session.Messages.Add(ChatMessageItem.Received(
                FirstNotEmpty(credential.CustomerDisplayName, "Customer"),
                $"File transfer requested: {fileName}",
                ToLocalTime(message?.UTime)));
        });
    }

    private ChatSession GetOrCreateSession(CATekClientCredential credential)
    {
        return GetOrCreateSession(credential, out _);
    }

    private ChatSession GetOrCreateSession(
        CATekClientCredential credential,
        out bool wasCreated)
    {
        if (string.IsNullOrWhiteSpace(credential.ChatID))
            throw new InvalidDataException("The chat server returned a chat without an ID.");

        if (_sessions.TryGetValue(credential.ChatID, out var existing))
        {
            wasCreated = false;
            existing.UpdateFrom(credential);
            return existing;
        }

        var session = new ChatSession(credential);
        session.PropertyChanged += OnCustomerSessionPropertyChanged;
        _sessions[session.ChatID] = session;
        Chats.Insert(0, session);
        wasCreated = true;
        return session;
    }

    private void OnCustomerSessionPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChatSession.UnreadCount) || sender is not ChatSession session)
            return;

        RaiseUnreadProperties();
        if (session.UnreadCount == 0)
            CustomerChatRead?.Invoke(this, new ChatSessionReadEventArgs(session));
    }

    private void OnServiceTechSessionPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ServiceTechSession.UnreadCount) ||
            sender is not ServiceTechSession session)
        {
            return;
        }

        RaiseUnreadProperties();
        if (session.UnreadCount == 0)
            ServiceTechChatRead?.Invoke(this, new ServiceTechSessionReadEventArgs(session));
    }

    private void RaiseUnreadProperties()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomerUnreadCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ServiceTechUnreadCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalUnreadCount)));
    }

    private static void AddReceivedMessage(
        ChatSession session,
        CATekClientCredential credential,
        CATekChatMessage message)
    {
        session.UnreadCount++;
        session.Messages.Add(ChatMessageItem.Received(
            FirstNotEmpty(credential.CustomerDisplayName, "Customer"),
            message?.Message ?? string.Empty,
            ToLocalTime(message?.UTime)));
    }

    private static DateTime ToLocalTime(DateTime? utcTime)
    {
        if (utcTime == null || utcTime == default)
            return DateTime.Now;

        return utcTime.Value.Kind == DateTimeKind.Local
            ? utcTime.Value
            : utcTime.Value.ToLocalTime();
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"ChatService: Invalid server payload: {ex.Message}");
            return default;
        }
    }

    private static string GenerateToken()
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(TokenKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "ATek") }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateJwtSecurityToken(descriptor));
    }

    private void SetStatus(string status, string error)
    {
        Dispatch(() =>
        {
            ConnectionStatus = status;
            LastError = error;
        });
    }

    private static string FriendlyError(Exception ex)
    {
        var root = ex;
        while (root.InnerException != null)
            root = root.InnerException;
        return root.Message;
    }

    private static void Dispatch(Action action)
    {
        static void RunSafely(Action dispatchedAction)
        {
            try
            {
                dispatchedAction();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chat UI callback failed: {ex}");
            }
        }

        if (MainThread.IsMainThread)
            RunSafely(action);
        else
            MainThread.BeginInvokeOnMainThread(() => RunSafely(action));
    }

    private static Task RunOnMainThreadAsync(Action action)
    {
        return MainThread.IsMainThread
            ? RunImmediately(action)
            : MainThread.InvokeOnMainThreadAsync(action);
    }

    private static Task RunImmediately(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private sealed class ChatCallback : IATekChatWebServiceCallback
    {
        private readonly ChatService _owner;

        public ChatCallback(ChatService owner) => _owner = owner;

        public string TestCallBack() => "ATek_ServiceTech_Chat";
        public bool StartChatConnect(string credential, string message) => Safely(() => _owner.HandleNewChat(credential, message));
        public bool ConnectChatToJob(string credential, string message) => true;
        public bool SetChatMessageFromJob(string credential, string message) => Safely(() => _owner.HandleMessage(credential, message));
        public bool SetChatMessageFromServiceTech(string credential, string message) => Safely(() => _owner.HandleMessage(credential, message));
        public bool SetChatMessageToServiceTechFromServiceTech(string credential, string message) => Safely(() => _owner.HandleServiceTechMessage(credential, message));
        public bool NotifyRegisterLogin(string credential) => Safely(() => _owner.HandleServiceTechLogin(credential));
        public bool NotifyExistingRegisterLogin(string credential) => Safely(() => _owner.HandleExistingChats(credential));
        public bool NotifyRegisterLogout(string credential) => Safely(() => _owner.HandleServiceTechLogout(credential));
        public bool NotifyChatAccepted(string credential) => Safely(() => _owner.HandleAccepted(credential));
        public bool NotifyChatJoined(string credential) => true;
        public bool NotifyEndChat(string credential) => Safely(() => _owner.HandleEnded(credential, "The customer ended the chat."));
        public bool NotifyDisconnectChat(string credential) => Safely(() => _owner.HandleServiceTechDisconnected(credential));
        public bool NotifyNoServiceGuyAvailable(string message) => true;
        public bool NotifyNoChatAvailable(string credential) => Safely(() => _owner.HandleEnded(credential, "This chat is no longer available."));
        public bool NotifyMessageDeliveredFailedTo(string userOrJob, string displayName) => true;
        public bool NotifyChatRequestDeclined(string message) => true;
        public bool NotifyChatHistry(string credential, string message) => Safely(() => _owner.HandleMessage(credential, message, true));
        public bool NotifyFileTransferRequestFromJob(string credential, string message) => Safely(() => _owner.HandleFileMessage(credential, message));
        public bool NotifyFileTransferRequestFromJobAccepted(string credential, string message) => true;
        public bool NotifyFileTransferFromJob(string credential, string message) => Safely(() => _owner.HandleFileMessage(credential, message));
        public bool NotifyIsWritingFromJob(string credential, string message) => Safely(() => _owner.HandleTyping(credential, message));
        public bool NotifyIsWritingFromServiceTech(string credential, string message) => true;

        private static bool Safely(Action callback)
        {
            try
            {
                callback();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chat server callback failed: {ex}");
                return false;
            }
        }
    }
}

public sealed class ChatSessionOpenedEventArgs : EventArgs
{
    public ChatSessionOpenedEventArgs(ChatSession session)
    {
        Session = session;
    }

    public ChatSession Session { get; }
}

public sealed class CustomerChatMessageReceivedEventArgs : EventArgs
{
    public CustomerChatMessageReceivedEventArgs(
        ChatSession session,
        ChatMessageItem message)
    {
        Session = session;
        Message = message;
    }

    public ChatSession Session { get; }
    public ChatMessageItem Message { get; }
}

public sealed class ServiceTechMessageReceivedEventArgs : EventArgs
{
    public ServiceTechMessageReceivedEventArgs(
        ServiceTechSession session,
        ChatMessageItem message)
    {
        Session = session;
        Message = message;
    }

    public ServiceTechSession Session { get; }
    public ChatMessageItem Message { get; }
}

public sealed class ChatSessionReadEventArgs : EventArgs
{
    public ChatSessionReadEventArgs(ChatSession session) => Session = session;

    public ChatSession Session { get; }
}

public sealed class ServiceTechSessionReadEventArgs : EventArgs
{
    public ServiceTechSessionReadEventArgs(ServiceTechSession session) => Session = session;

    public ServiceTechSession Session { get; }
}

public sealed class ChatSession : INotifyPropertyChanged
{
    private string _jobNumber = string.Empty;
    private string _companyName = string.Empty;
    private string _customerDisplayName = string.Empty;
    private string _computerName = string.Empty;
    private string _oemName = string.Empty;
    private string _phoneNumber = string.Empty;
    private bool _isAccepted;
    private bool _isJoined;
    private bool _isEnded;
    private bool _isCustomerTyping;
    private int _unreadCount;

    public ChatSession(CATekClientCredential credential)
    {
        ChatID = credential.ChatID;
        Messages.CollectionChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MessagePreview)));
        UpdateFrom(credential);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string ChatID { get; }
    public string JobNumber { get => _jobNumber; private set => SetField(ref _jobNumber, value); }
    public string CompanyName { get => _companyName; private set => SetField(ref _companyName, value); }
    public string CustomerDisplayName { get => _customerDisplayName; private set => SetField(ref _customerDisplayName, value); }
    public string ComputerName { get => _computerName; private set => SetField(ref _computerName, value); }
    public string OEMName { get => _oemName; private set => SetField(ref _oemName, value); }
    public string PhoneNumber { get => _phoneNumber; private set => SetField(ref _phoneNumber, value); }
    public bool IsAccepted { get => _isAccepted; set { if (SetField(ref _isAccepted, value)) RaiseStatusProperties(); } }
    public bool IsJoined { get => _isJoined; set { if (SetField(ref _isJoined, value)) RaiseStatusProperties(); } }
    public bool IsEnded { get => _isEnded; set { if (SetField(ref _isEnded, value)) RaiseStatusProperties(); } }
    public bool IsCustomerTyping { get => _isCustomerTyping; set => SetField(ref _isCustomerTyping, value); }
    public int UnreadCount { get => _unreadCount; set => SetField(ref _unreadCount, value); }
    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    public string Title => string.IsNullOrWhiteSpace(CompanyName)
        ? FirstNotEmpty(CustomerDisplayName, JobNumber, "Customer chat")
        : CompanyName;
    public string Details => string.Join(" • ", new[] { JobNumber, OEMName, PhoneNumber }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
    public string MessagePreview
    {
        get
        {
            var latest = Messages.LastOrDefault(item => !string.IsNullOrWhiteSpace(item.Message));
            if (latest == null) return "No message received yet.";

            var message = string.Join(" ", latest.Message.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            var sender = FirstNotEmpty(latest.SenderName, "Customer");
            return $"{sender}: {message}";
        }
    }
    public string Status => IsEnded ? "Ended" : IsJoined ? "Joined" : IsAccepted ? "In progress" : "Requested";
    public bool CanJoin => !IsEnded && !IsJoined;
    public bool CanSend => !IsEnded && IsJoined;

    public void UpdateFrom(CATekClientCredential credential)
    {
        JobNumber = Prefer(credential.JobNumber, JobNumber);
        CompanyName = Prefer(credential.CustomerName, CompanyName);
        CustomerDisplayName = Prefer(credential.CustomerDisplayName, CustomerDisplayName);
        ComputerName = Prefer(credential.ComputerName, ComputerName);
        OEMName = Prefer(credential.OEM, OEMName);
        PhoneNumber = Prefer(credential.CustomerPhoneNumber, PhoneNumber);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Details)));
    }

    private void RaiseStatusProperties()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanJoin)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSend)));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private static string Prefer(string incoming, string current) =>
        string.IsNullOrWhiteSpace(incoming) ? current : incoming;

    private static string FirstNotEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class ServiceTechSession : INotifyPropertyChanged
{
    public const string UniversalKey = "__all_service_techs__";

    private string _displayName;
    private string _oemName;
    private enumOEMUserRole _role;
    private bool _isOnline = true;
    private int _unreadCount;

    public ServiceTechSession(
        string key,
        string userId,
        string displayName,
        string oemName,
        enumOEMUserRole role,
        bool isUniversal = false)
    {
        Key = key;
        UserID = userId;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? userId : displayName;
        _oemName = oemName ?? string.Empty;
        _role = role;
        IsUniversal = isUniversal;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string Key { get; }
    public string UserID { get; }
    public bool IsUniversal { get; }
    public string DisplayName => _displayName;
    public string OEMName => _oemName;
    public enumOEMUserRole Role => _role;
    public string Details => IsUniversal
        ? "Broadcast to every connected service tech"
        : string.Join(" • ", new[] { OEMName, FormatRole(Role) }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    public string OnlineStatus => IsOnline ? "Online" : "Offline";
    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (!SetField(ref _isOnline, value))
                return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnlineStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSend)));
        }
    }
    public bool CanSend => IsOnline;
    public int UnreadCount { get => _unreadCount; set => SetField(ref _unreadCount, value); }
    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    public static ServiceTechSession CreateUniversal() => new(
        UniversalKey,
        UniversalKey,
        "All Service Techs",
        string.Empty,
        enumOEMUserRole.ServiceTech,
        isUniversal: true);

    public void Update(string displayName, string oemName, enumOEMUserRole role)
    {
        _displayName = string.IsNullOrWhiteSpace(displayName) ? UserID : displayName;
        _oemName = oemName ?? string.Empty;
        _role = role;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OEMName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Role)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Details)));
    }

    private static string FormatRole(enumOEMUserRole role)
    {
        var text = role.ToString();
        return text == "ServiceTech" ? "Service Tech" : text;
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class ChatMessageItem
{
    public string SenderName { get; init; }
    public string Message { get; init; }
    public DateTime MessageTime { get; init; }
    public bool IsSentByCurrentUser { get; init; }

    public static ChatMessageItem Sent(string sender, string message, DateTime time) => new()
    {
        SenderName = sender,
        Message = message,
        MessageTime = time,
        IsSentByCurrentUser = true
    };

    public static ChatMessageItem Received(string sender, string message, DateTime time) => new()
    {
        SenderName = sender,
        Message = message,
        MessageTime = time,
        IsSentByCurrentUser = false
    };
}
