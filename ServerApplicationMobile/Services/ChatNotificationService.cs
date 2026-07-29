#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
#elif IOS
using Foundation;
using UIKit;
using UserNotifications;
#endif

namespace ServerApplicationMobile.Services;

/// <summary>
/// Presents a native notification where the platform supports it. The Shell
/// supplies an in-app fallback and maintains the unread chat indicator.
/// </summary>
public sealed class ChatNotificationService
{
    internal const string NotificationChatKindKey = "atek_chat_kind";
    internal const string NotificationConversationIdKey = "atek_conversation_id";
#if ANDROID
    private const string ChannelId = "atek_chat_messages_v2";
    private const string NotificationTag = "atek_chat";
    private const string PostNotificationsPermission =
        "android.permission.POST_NOTIFICATIONS";
    private bool _initialized;
#elif IOS
    private static readonly ForegroundNotificationDelegate NotificationDelegate = new();
    private static readonly NSString ChatKindUserInfoKey = new(NotificationChatKindKey);
    private static readonly NSString ConversationIdUserInfoKey = new(NotificationConversationIdKey);
#endif

    public Task InitializeAsync()
    {
#if ANDROID
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var context = Android.App.Application.Context;
            if (!_initialized)
            {
                EnsureChannel(context);
                _initialized = true;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity != null &&
                    activity.CheckSelfPermission(PostNotificationsPermission) !=
                    Permission.Granted)
                {
                    activity.RequestPermissions(
                        new[] { PostNotificationsPermission },
                        requestCode: 4107);
                }
            }
        });
#elif IOS
        return InitializeIosAsync();
#else
        return Task.CompletedTask;
#endif
    }

    public Task<bool> TryShowAsync(ChatSession session)
    {
        var body = string.IsNullOrWhiteSpace(session.Details)
            ? session.Title
            : $"{session.Title} - {session.Details}";
        return TryShowAsync(
            CustomerNotificationKey(session),
            "New customer chat",
            body,
            session.UnreadCount,
            new ChatNotificationTarget(
                ChatNotificationTargetKind.Customer,
                session.ChatID));
    }

    public Task<bool> TryShowAsync(
        ServiceTechSession session,
        ChatMessageItem message)
    {
        var body = string.IsNullOrWhiteSpace(message.Message)
            ? $"New message from {session.DisplayName}"
            : message.Message;
        return TryShowAsync(
            ServiceTechNotificationKey(session),
            session.DisplayName,
            body,
            session.UnreadCount,
            new ChatNotificationTarget(
                ChatNotificationTargetKind.ServiceTech,
                session.Key));
    }

    public Task<bool> TryShowCustomerMessageAsync(
        ChatSession session,
        ChatMessageItem message)
    {
        var title = string.IsNullOrWhiteSpace(session.Title)
            ? "Customer chat"
            : session.Title;
        var body = string.IsNullOrWhiteSpace(message.Message)
            ? "New customer message"
            : message.Message;
        return TryShowAsync(
            CustomerNotificationKey(session),
            title,
            body,
            session.UnreadCount,
            new ChatNotificationTarget(
                ChatNotificationTargetKind.Customer,
                session.ChatID));
    }

    public Task DismissAsync(ChatSession session) =>
        DismissAsync(CustomerNotificationKey(session));

    public Task DismissAsync(ServiceTechSession session) =>
        DismissAsync(ServiceTechNotificationKey(session));

    public Task SetUnreadCountAsync(int unreadCount)
    {
#if IOS
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            UIApplication.SharedApplication.ApplicationIconBadgeNumber =
                Math.Max(0, unreadCount);
            if (unreadCount == 0)
            {
                var center = UNUserNotificationCenter.Current;
                center.RemoveAllDeliveredNotifications();
                center.RemoveAllPendingNotificationRequests();
            }
        });
#elif ANDROID
        if (unreadCount != 0)
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var manager = (NotificationManager)Android.App.Application.Context
                .GetSystemService(Context.NotificationService);
            manager?.CancelAll();
        });
#else
        return Task.CompletedTask;
#endif
    }

    private Task<bool> TryShowAsync(
        string notificationKey,
        string title,
        string body,
        int unreadCount,
        ChatNotificationTarget target)
    {
#if ANDROID
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var context = Android.App.Application.Context;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
                context.CheckSelfPermission(PostNotificationsPermission) !=
                Permission.Granted)
            {
                return false;
            }

            EnsureChannel(context);

            var launchIntent = context.PackageManager?
                .GetLaunchIntentForPackage(context.PackageName!);
            if (launchIntent == null)
                return false;

            launchIntent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            launchIntent.PutExtra(NotificationChatKindKey, target.Kind.ToString());
            launchIntent.PutExtra(NotificationConversationIdKey, target.ConversationId);
            var pendingFlags = PendingIntentFlags.UpdateCurrent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                pendingFlags |= PendingIntentFlags.Immutable;

            var pendingIntent = PendingIntent.GetActivity(
                context,
                requestCode: GetNotificationId(notificationKey),
                launchIntent,
                pendingFlags);

            Notification.Builder builder;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                builder = new Notification.Builder(context, ChannelId);
            else
#pragma warning disable CA1422
                builder = new Notification.Builder(context);
#pragma warning restore CA1422

            builder
                .SetContentTitle(title)
                .SetContentText(body)
                .SetStyle(new Notification.BigTextStyle().BigText(body))
                .SetSmallIcon(context.ApplicationInfo?.Icon ?? 0)
                .SetContentIntent(pendingIntent)
                .SetAutoCancel(true)
                .SetCategory(Notification.CategoryMessage)
                .SetNumber(Math.Max(1, unreadCount))
                .SetDefaults(NotificationDefaults.All);

            var manager = (NotificationManager)context.GetSystemService(
                Context.NotificationService);
            if (manager == null)
                return false;

            manager.Notify(
                NotificationTag,
                GetNotificationId(notificationKey),
                builder.Build());
            return true;
        });
#elif IOS
        return ShowIosNotificationAsync(notificationKey, title, body, unreadCount, target);
#else
        return Task.FromResult(false);
#endif
    }

    private Task DismissAsync(string notificationKey)
    {
#if ANDROID
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var manager = (NotificationManager)Android.App.Application.Context
                .GetSystemService(Context.NotificationService);
            manager?.Cancel(NotificationTag, GetNotificationId(notificationKey));
        });
#elif IOS
        var identifier = GetNotificationIdentifier(notificationKey);
        var center = UNUserNotificationCenter.Current;
        center.RemoveDeliveredNotifications(new[] { identifier });
        center.RemovePendingNotificationRequests(new[] { identifier });
        return Task.CompletedTask;
#else
        return Task.CompletedTask;
#endif
    }

    private static string CustomerNotificationKey(ChatSession session) =>
        $"customer:{session.ChatID}";

    private static string ServiceTechNotificationKey(ServiceTechSession session) =>
        $"service-tech:{session.Key}";

    private static int GetNotificationId(string notificationKey)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in notificationKey)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static string GetNotificationIdentifier(string notificationKey) =>
        $"atek-chat-{GetNotificationId(notificationKey)}";

#if ANDROID
    private static void EnsureChannel(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var manager = (NotificationManager)context.GetSystemService(
            Context.NotificationService);
        if (manager == null || manager.GetNotificationChannel(ChannelId) != null)
            return;

        var channel = new NotificationChannel(
            ChannelId,
            "ATek chat messages",
            NotificationImportance.High)
        {
            Description = "Alerts for customer and service technician chat messages."
        };
        channel.EnableVibration(true);
        manager.CreateNotificationChannel(channel);
    }
#elif IOS
    internal static void ConfigureIosNotificationHandling()
    {
        UNUserNotificationCenter.Current.Delegate = NotificationDelegate;
    }

    private static async Task InitializeIosAsync()
    {
        var center = UNUserNotificationCenter.Current;
        center.Delegate = NotificationDelegate;
        await center.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert |
            UNAuthorizationOptions.Badge |
            UNAuthorizationOptions.Sound);
    }

    private static async Task<bool> ShowIosNotificationAsync(
        string notificationKey,
        string title,
        string body,
        int unreadCount,
        ChatNotificationTarget target)
    {
        try
        {
            var center = UNUserNotificationCenter.Current;
            center.Delegate ??= NotificationDelegate;
            var settings = await center.GetNotificationSettingsAsync();
            if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized &&
                settings.AuthorizationStatus != UNAuthorizationStatus.Provisional)
            {
                return false;
            }

            using var content = new UNMutableNotificationContent
            {
                Title = title,
                Body = body,
                Badge = NSNumber.FromInt32(Math.Max(1, unreadCount)),
                Sound = UNNotificationSound.Default
            };
            using var userInfo = new NSMutableDictionary
            {
                [ChatKindUserInfoKey] = new NSString(target.Kind.ToString()),
                [ConversationIdUserInfoKey] = new NSString(target.ConversationId)
            };
            content.UserInfo = userInfo;
            using var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(
                0.1,
                repeats: false);
            using var request = UNNotificationRequest.FromIdentifier(
                GetNotificationIdentifier(notificationKey),
                content,
                trigger);
            await center.AddNotificationRequestAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"iOS chat notification failed: {ex.Message}");
            return false;
        }
    }

    private sealed class ForegroundNotificationDelegate : UNUserNotificationCenterDelegate
    {
        public override void WillPresentNotification(
            UNUserNotificationCenter center,
            UNNotification notification,
            Action<UNNotificationPresentationOptions> completionHandler)
        {
            completionHandler(
                UNNotificationPresentationOptions.Banner |
                UNNotificationPresentationOptions.List |
                UNNotificationPresentationOptions.Sound |
                UNNotificationPresentationOptions.Badge);
        }

        public override void DidReceiveNotificationResponse(
            UNUserNotificationCenter center,
            UNNotificationResponse response,
            Action completionHandler)
        {
            try
            {
                var userInfo = response.Notification.Request.Content.UserInfo;
                ChatNotificationActivation.TryPublish(
                    userInfo?[ChatKindUserInfoKey]?.ToString(),
                    userInfo?[ConversationIdUserInfoKey]?.ToString());
            }
            finally
            {
                completionHandler();
            }
        }
    }
#endif
}
