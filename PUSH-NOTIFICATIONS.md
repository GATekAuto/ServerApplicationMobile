# Closed-app chat notifications

The mobile app now registers for APNs on iOS and Firebase Cloud Messaging (FCM)
on Android. It sends the current push token to the existing chat server by adding
four fields to the JSON already used by `RegisterLogin` and
`ServiceTechWatchDog`. No new WCF operation or endpoint is required.

## Required mobile configuration

### Android

1. Create or select a Firebase project.
2. Add an Android app whose package name is
   `com.ATek.ServerApplicationMobile`.
3. Download `google-services.json` and place it at:

   `ServerApplicationMobile/Platforms/Android/google-services.json`

4. Build and install the app, launch it once, sign in, and allow notifications.

The project intentionally includes the file only when it exists, so contributors
without company Firebase configuration can still compile. Closed-app Android
notifications remain disabled until the real file is present.

### iOS

1. Enable **Push Notifications** for App ID
   `com.ATek.ServerApplicationMobile` in the Apple Developer portal.
2. Regenerate the development/distribution provisioning profiles after enabling
   that capability.
3. Create an APNs `.p8` key for the server and retain its Key ID and Team ID.
4. Build a signed IPA with the new profile, install it, launch it once, sign in,
   and allow notifications.

An unsigned or manually re-signed IPA cannot receive an APNs token. The Release
entitlement requests the production APNs environment; Debug requests sandbox.

## Payload contract

The chat server must include these exact custom values in both Android and iOS
notifications:

- `atek_chat_kind`: `Customer` or `ServiceTech`
- `atek_conversation_id`: customer `ChatID`, or the service-tech conversation key

For a direct service-tech message, construct the conversation key from the
**sender**, because that is the conversation the receiving device opens:

```csharp
string ServiceTechConversationKey(string oem, string userId, enumOEMUserRole role)
    => $"{oem?.Trim()}\u001f{userId?.Trim()}\u001f{(int)role}";
```

For a broadcast service-tech message use `__all_service_techs__`.

Android must use a combined `notification` + `data` FCM payload. That allows
Android to display it while the process is terminated and puts the custom values
on `MainActivity` when the user taps it. Use notification channel
`atek_chat_messages_v2`.

The existing cold-start router retains the target while remembered login runs,
then opens the matching customer or service-tech conversation after the WCF chat
connection has populated it.

## Server changes

See `ChatServerPushPatch/README.md`. The patch deliberately reuses the current
login/watchdog messages, which means this mobile build remains compatible with an
older server: Newtonsoft.Json simply ignores the extra properties until the
server is upgraded.
