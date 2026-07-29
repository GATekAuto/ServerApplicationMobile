namespace ServerApplicationMobile.Services;

public enum ChatNotificationTargetKind
{
    Customer,
    ServiceTech
}

public sealed record ChatNotificationTarget(
    ChatNotificationTargetKind Kind,
    string ConversationId);

/// <summary>
/// Holds a notification destination across foreground and cold-start activation.
/// The destination remains pending until the chat service has reconnected and the
/// requested conversation is available.
/// </summary>
public static class ChatNotificationActivation
{
    private static readonly object SyncRoot = new();
    private static ChatNotificationTarget _pendingTarget;

    public static event Action ActivationRequested;

    public static void Publish(ChatNotificationTarget target)
    {
        if (target == null || string.IsNullOrWhiteSpace(target.ConversationId))
            return;

        lock (SyncRoot)
            _pendingTarget = target;

        ActivationRequested?.Invoke();
    }

    public static ChatNotificationTarget Peek()
    {
        lock (SyncRoot)
            return _pendingTarget;
    }

    public static void Complete(ChatNotificationTarget target)
    {
        lock (SyncRoot)
        {
            if (_pendingTarget == target)
                _pendingTarget = null;
        }
    }

    public static bool TryPublish(string kind, string conversationId)
    {
        if (!Enum.TryParse(kind, ignoreCase: true, out ChatNotificationTargetKind targetKind) ||
            string.IsNullOrWhiteSpace(conversationId))
        {
            return false;
        }

        Publish(new ChatNotificationTarget(targetKind, conversationId));
        return true;
    }
}
