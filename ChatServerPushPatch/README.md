# Chat server push-notification patch

This is the patch contract for the current company chat service. Apply it to the
latest server tree when it is available. Do not add a second chat endpoint: the
mobile app already sends registration data through the existing
`RegisterLogin` and `ServiceTechWatchDog` calls.

## 1. Extend `CATekClientCredential`

Add these members to `ATekChatCommon.cs` inside `CATekClientCredential`:

```csharp
[DataMember]
public string MobilePushPlatform { get; set; } = string.Empty; // "apns" or "fcm"

[DataMember]
public string MobilePushToken { get; set; } = string.Empty;

[DataMember]
public string MobilePushInstallationId { get; set; } = string.Empty;

[DataMember]
public string MobilePushEnvironment { get; set; } = string.Empty; // sandbox/production
```

Also copy all four properties in `CATekClientCredential.Clone()`.

## 2. Store registrations

Use the installation ID as the primary key—not the token and not the user ID.
A technician can be signed in on more than one phone, and APNs/FCM can rotate a
device token.

```sql
CREATE TABLE dbo.ATekMobilePushRegistration
(
    InstallationId varchar(64) NOT NULL PRIMARY KEY,
    OEM nvarchar(100) NOT NULL,
    UserID nvarchar(200) NOT NULL,
    UserRole int NOT NULL,
    Platform varchar(10) NOT NULL,
    PushToken nvarchar(512) NOT NULL,
    PushEnvironment varchar(16) NOT NULL,
    UpdatedUtc datetime2 NOT NULL
        CONSTRAINT DF_ATekMobilePushRegistration_UpdatedUtc DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_ATekMobilePushRegistration_User
ON dbo.ATekMobilePushRegistration(OEM, UserID, UserRole);
```

Upsert implementation (parameterize it using the server's existing DB helper):

```sql
MERGE dbo.ATekMobilePushRegistration AS target
USING (VALUES (@InstallationId, @OEM, @UserID, @UserRole, @Platform,
               @PushToken, @PushEnvironment))
      AS source(InstallationId, OEM, UserID, UserRole, Platform,
                PushToken, PushEnvironment)
ON target.InstallationId = source.InstallationId
WHEN MATCHED THEN UPDATE SET
    OEM = source.OEM,
    UserID = source.UserID,
    UserRole = source.UserRole,
    Platform = source.Platform,
    PushToken = source.PushToken,
    PushEnvironment = source.PushEnvironment,
    UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (InstallationId, OEM, UserID, UserRole, Platform, PushToken,
     PushEnvironment, UpdatedUtc)
VALUES
    (source.InstallationId, source.OEM, source.UserID, source.UserRole,
     source.Platform, source.PushToken, source.PushEnvironment, SYSUTCDATETIME());
```

## 3. Register from the existing operations

Immediately after deserializing a valid service-tech credential in
`DelegateRegisterLogin`, call:

```csharp
RegisterMobilePushDevice(client);
```

Replace the empty `DelegateServiceTechWatchDog` body with:

```csharp
public void DelegateServiceTechWatchDog(string clientCredential, object callback)
{
    var client = JsonConvert.DeserializeObject<CATekClientCredential>(clientCredential);
    if (client != null && !client.IsJob)
        RegisterMobilePushDevice(client);
}
```

`RegisterMobilePushDevice` should return without doing anything if any of
`MobilePushPlatform`, `MobilePushToken`, or `MobilePushInstallationId` is empty.
Otherwise execute the upsert above. This lets old desktop clients continue to
use exactly the same server.

Also add `MobilePushInstallationId` to `CATekLogin` and copy it from the client
when the login is created or reconnected. This allows the sender to suppress
push only for the exact device whose WCF callback is open; it must not suppress a
second phone belonging to the same technician.

In `DelegateRegisterLogout`, after deserializing the credential, delete only the
row matching `MobilePushInstallationId`. Do not remove every token belonging to
the user because another phone may still be signed in.

## 4. Send notifications at these points

Queue push work and return from the WCF operation; never wait for APNs/FCM while
holding the server's chat lock. Exclude a device while that device's WCF callback
is still in `CommunicationState.Opened`: the live callback already produces the
local notification. Send push when the callback is absent, faulted, or closed.
This prevents duplicate local + remote alerts while still covering suspended or
terminated processes.

### New customer chat

At the point in `DelegateStartChat` where the new `ChatID` has been assigned and
the server notifies service technicians:

```csharp
_ = push.SendToAllServiceTechsAsync(new ChatPushMessage
{
    Title = "New customer chat",
    Body = FirstNonEmpty(client.CustomerDisplayName, client.CustomerName,
                         client.JobNumber, "A customer needs help"),
    ChatKind = "Customer",
    ConversationId = strChatID
});
```

### Customer sends a message

In `DelegateSendChatMessageToServiceTech`, after the chat has been found, send to
the technicians joined to that chat. If there are no joined technicians yet,
send to all eligible technicians:

```csharp
_ = push.SendCustomerMessageAsync(
    chat.ChatID,
    chat.LoginList,
    FirstNonEmpty(client.CustomerDisplayName, client.CustomerName, "Customer"),
    ReadMessageText(strMessage));
```

`ReadMessageText` must deserialize `CATekChatMessage` and use its `Message`
property; do not put the JSON envelope in the notification body.

### Service tech sends a message

In `DelegateSendChatMessageToServiceTechFromServiceTech`, send to every recipient
selected by `OEMList`, `UserIDList`, and `RoleList`, excluding the sender. For a
universal message, send to every registered service tech except the sender:

```csharp
var conversationId = client.IsUniversalServiceMessage
    ? "__all_service_techs__"
    : ServiceTechConversationKey(client.OEM, client.UserID, client.Role);

_ = push.SendServiceTechMessageAsync(
    recipients,
    client.OEM,
    client.UserID,
    client.Role,
    FirstNonEmpty(client.ServiceTechDisplayName, client.UserID, "Service tech"),
    ReadMessageText(strMessage),
    conversationId);
```

The key uses the sender's identity because the receiving app groups the incoming
message under the sender.

## 5. Provider payloads

Use `ChatPushNotificationSender.cs` as the .NET 8+ provider implementation. It
sends these required payloads:

Android FCM HTTP v1:

```json
{
  "message": {
    "token": "...",
    "notification": { "title": "...", "body": "..." },
    "data": {
      "atek_chat_kind": "Customer",
      "atek_conversation_id": "chat-id"
    },
    "android": {
      "priority": "high",
      "notification": { "channel_id": "atek_chat_messages_v2" }
    }
  }
}
```

iOS APNs:

```json
{
  "aps": {
    "alert": { "title": "...", "body": "..." },
    "sound": "default",
    "badge": 1,
    "thread-id": "customer:chat-id"
  },
  "atek_chat_kind": "Customer",
  "atek_conversation_id": "chat-id"
}
```

## 6. Secrets and invalid tokens

The sender needs these server-side settings. Store them in protected server
configuration or a secret manager, never in Git:

- Firebase project ID, service-account client email, and private key
- Apple Team ID, APNs Key ID, `.p8` private key, and bundle ID
  `com.ATek.ServerApplicationMobile`

Delete a registration when FCM reports an unregistered token or APNs returns
HTTP 410. A periodic cleanup can also remove registrations not refreshed for 90
days.

The provider reference targets modern .NET because the checked-in old server is
.NET Framework 4.7.2 and its `HttpClient` cannot reliably provide the HTTP/2 APNs
connection. If the latest chat server is still 4.7.2, keep the integration and
SQL changes above but host the provider class in a small .NET 8 Windows service
on the same server machine, invoked through an in-process-safe queue or localhost.
