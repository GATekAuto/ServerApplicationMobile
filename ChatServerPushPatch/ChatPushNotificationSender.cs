using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ATekChatManageSystem.Push;

/// <summary>
/// Reference provider implementation for the latest chat server (modern .NET).
/// Registration persistence and recipient lookup remain in the chat server's DB layer.
/// </summary>
public sealed class ChatPushNotificationSender
{
    private static readonly HttpClient Client = new();
    private readonly ChatPushOptions _options;
    private readonly SemaphoreSlim _fcmTokenGate = new(1, 1);
    private readonly object _apnsTokenGate = new();
    private string _fcmAccessToken = string.Empty;
    private DateTimeOffset _fcmAccessTokenExpiresUtc;
    private string _apnsProviderToken = string.Empty;
    private DateTimeOffset _apnsProviderTokenCreatedUtc;

    public ChatPushNotificationSender(ChatPushOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ChatPushSendResult> SendAsync(
        MobilePushRegistration registration,
        ChatPushMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(message);

        if (string.Equals(registration.Platform, "fcm", StringComparison.OrdinalIgnoreCase))
            return await SendFcmAsync(registration, message, cancellationToken);

        if (string.Equals(registration.Platform, "apns", StringComparison.OrdinalIgnoreCase))
            return await SendApnsAsync(registration, message, cancellationToken);

        return new ChatPushSendResult(false, false, "Unsupported push platform.");
    }

    private async Task<ChatPushSendResult> SendFcmAsync(
        MobilePushRegistration registration,
        ChatPushMessage message,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetFcmAccessTokenAsync(cancellationToken);
        var payload = new
        {
            message = new
            {
                token = registration.PushToken,
                notification = new { title = message.Title, body = message.Body },
                data = new Dictionary<string, string>
                {
                    ["atek_chat_kind"] = message.ChatKind,
                    ["atek_conversation_id"] = message.ConversationId
                },
                android = new
                {
                    priority = "high",
                    notification = new
                    {
                        channel_id = "atek_chat_messages_v2",
                        tag = $"atek-{message.ChatKind.ToLowerInvariant()}-{message.ConversationId}"
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.FirebaseProjectId)}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent(payload);

        using var response = await Client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
            return new ChatPushSendResult(true, false, string.Empty);

        var invalid = response.StatusCode == HttpStatusCode.NotFound ||
            responseText.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase);
        return new ChatPushSendResult(false, invalid, responseText);
    }

    private async Task<ChatPushSendResult> SendApnsAsync(
        MobilePushRegistration registration,
        ChatPushMessage message,
        CancellationToken cancellationToken)
    {
        var host = string.Equals(
            registration.PushEnvironment,
            "sandbox",
            StringComparison.OrdinalIgnoreCase)
                ? "https://api.sandbox.push.apple.com"
                : "https://api.push.apple.com";

        var aps = new Dictionary<string, object>
        {
            ["alert"] = new { title = message.Title, body = message.Body },
            ["sound"] = "default",
            ["badge"] = 1,
            ["thread-id"] = $"{message.ChatKind.ToLowerInvariant()}:{message.ConversationId}"
        };
        var payload = new Dictionary<string, object>
        {
            ["aps"] = aps,
            ["atek_chat_kind"] = message.ChatKind,
            ["atek_conversation_id"] = message.ConversationId
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{host}/3/device/{Uri.EscapeDataString(registration.PushToken)}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = JsonContent(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "bearer",
            GetApnsProviderToken());
        request.Headers.TryAddWithoutValidation("apns-topic", _options.AppleBundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Headers.TryAddWithoutValidation("apns-priority", "10");

        using var response = await Client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
            return new ChatPushSendResult(true, false, string.Empty);

        return new ChatPushSendResult(
            false,
            response.StatusCode == HttpStatusCode.Gone,
            responseText);
    }

    private async Task<string> GetFcmAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_fcmAccessToken) &&
            _fcmAccessTokenExpiresUtc > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _fcmAccessToken;
        }

        await _fcmTokenGate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_fcmAccessToken) &&
                _fcmAccessTokenExpiresUtc > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _fcmAccessToken;
            }

            var now = DateTimeOffset.UtcNow;
            var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
            {
                alg = "RS256",
                typ = "JWT"
            }));
            var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = _options.FirebaseClientEmail,
                scope = "https://www.googleapis.com/auth/firebase.messaging",
                aud = "https://oauth2.googleapis.com/token",
                iat = now.ToUnixTimeSeconds(),
                exp = now.AddMinutes(55).ToUnixTimeSeconds()
            }));
            var unsigned = $"{header}.{claims}";

            using var rsa = RSA.Create();
            rsa.ImportFromPem(_options.FirebasePrivateKeyPem);
            var signature = rsa.SignData(
                Encoding.ASCII.GetBytes(unsigned),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var assertion = $"{unsigned}.{Base64Url(signature)}";

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            });
            using var response = await Client.PostAsync(
                "https://oauth2.googleapis.com/token",
                content,
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(json);
            _fcmAccessToken = document.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Google returned an empty access token.");
            var lifetime = document.RootElement.TryGetProperty("expires_in", out var expires)
                ? expires.GetInt32()
                : 3600;
            _fcmAccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(lifetime);
            return _fcmAccessToken;
        }
        finally
        {
            _fcmTokenGate.Release();
        }
    }

    private string GetApnsProviderToken()
    {
        lock (_apnsTokenGate)
        {
            if (!string.IsNullOrEmpty(_apnsProviderToken) &&
                _apnsProviderTokenCreatedUtc > DateTimeOffset.UtcNow.AddMinutes(-50))
            {
                return _apnsProviderToken;
            }

            var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
            {
                alg = "ES256",
                kid = _options.AppleKeyId
            }));
            var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = _options.AppleTeamId,
                iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }));
            var unsigned = $"{header}.{claims}";

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(_options.ApplePrivateKeyPem);
            var signature = ecdsa.SignData(
                Encoding.ASCII.GetBytes(unsigned),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            _apnsProviderToken = $"{unsigned}.{Base64Url(signature)}";
            _apnsProviderTokenCreatedUtc = DateTimeOffset.UtcNow;
            return _apnsProviderToken;
        }
    }

    private static StringContent JsonContent(object value) => new(
        JsonSerializer.Serialize(value),
        Encoding.UTF8,
        "application/json");

    private static string Base64Url(byte[] value) => Convert
        .ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

public sealed class ChatPushOptions
{
    public required string FirebaseProjectId { get; init; }
    public required string FirebaseClientEmail { get; init; }
    public required string FirebasePrivateKeyPem { get; init; }
    public required string AppleTeamId { get; init; }
    public required string AppleKeyId { get; init; }
    public required string ApplePrivateKeyPem { get; init; }
    public string AppleBundleId { get; init; } = "com.ATek.ServerApplicationMobile";
}

public sealed class MobilePushRegistration
{
    public required string InstallationId { get; init; }
    public required string Platform { get; init; }
    public required string PushToken { get; init; }
    public required string PushEnvironment { get; init; }
}

public sealed class ChatPushMessage
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string ChatKind { get; init; }
    public required string ConversationId { get; init; }
}

public sealed record ChatPushSendResult(
    bool Succeeded,
    bool RegistrationIsInvalid,
    string ProviderResponse);
