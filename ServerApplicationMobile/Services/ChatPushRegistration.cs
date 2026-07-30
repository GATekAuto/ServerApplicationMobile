using ATekWeb.ATekWebCommonDBData;

namespace ServerApplicationMobile.Services;

public sealed record ChatPushToken(
    string Platform,
    string Token,
    string InstallationId,
    string Environment);

/// <summary>
/// Holds only the token returned during this process. APNs tokens are deliberately
/// not cached on disk because Apple requires apps to request the current token on
/// every launch. The stable installation id lets the server replace rotated tokens.
/// </summary>
public static class ChatPushRegistration
{
    private const string InstallationIdKey = "atek_push_installation_id";
    private static readonly object SyncRoot = new();
    private static ChatPushToken _current;

    public static event Action<ChatPushToken> TokenChanged;

    public static ChatPushToken Current
    {
        get
        {
            lock (SyncRoot)
                return _current;
        }
    }

    public static void Publish(string platform, string token, string environment)
    {
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(token))
            return;

        var registration = new ChatPushToken(
            platform.Trim().ToLowerInvariant(),
            token.Trim(),
            GetOrCreateInstallationId(),
            string.IsNullOrWhiteSpace(environment)
                ? "production"
                : environment.Trim().ToLowerInvariant());

        lock (SyncRoot)
        {
            if (_current == registration)
                return;
            _current = registration;
        }

        TokenChanged?.Invoke(registration);
    }

    private static string GetOrCreateInstallationId()
    {
        var installationId = Preferences.Default.Get(InstallationIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(installationId))
            return installationId;

        installationId = Guid.NewGuid().ToString("N");
        Preferences.Default.Set(InstallationIdKey, installationId);
        return installationId;
    }
}

/// <summary>
/// Extends the existing login JSON without changing the WCF operation contract.
/// Newtonsoft.Json on the current server ignores these fields until its shared
/// credential type is upgraded with the matching properties.
/// </summary>
internal sealed class MobileChatCredential : CATekClientCredential
{
    public string MobilePushPlatform { get; set; } = string.Empty;
    public string MobilePushToken { get; set; } = string.Empty;
    public string MobilePushInstallationId { get; set; } = string.Empty;
    public string MobilePushEnvironment { get; set; } = string.Empty;
}
