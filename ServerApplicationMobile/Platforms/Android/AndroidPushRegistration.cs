#nullable enable

using Android.App;
using Android.Content;
using Firebase;
using Firebase.Messaging;
using ServerApplicationMobile.Services;
using GmsTask = Android.Gms.Tasks.Task;

namespace ServerApplicationMobile;

internal static class AndroidPushRegistration
{
    public static async System.Threading.Tasks.Task InitializeAsync()
    {
        try
        {
            var context = Android.App.Application.Context;
            if (FirebaseApp.InitializeApp(context) == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "FCM is disabled: add Platforms/Android/google-services.json to enable closed-app notifications.");
                return;
            }

            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            FirebaseMessaging.Instance.GetToken()
                .AddOnCompleteListener(new TokenCompleteListener(completion));

            var token = await completion.Task.WaitAsync(TimeSpan.FromSeconds(12));
            if (!string.IsNullOrWhiteSpace(token))
                ChatPushRegistration.Publish("fcm", token, "production");
        }
        catch (TimeoutException)
        {
            System.Diagnostics.Debug.WriteLine(
                "FCM token retrieval timed out and will retry when Firebase rotates the token.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FCM initialization failed: {ex.Message}");
        }
    }

    private sealed class TokenCompleteListener(TaskCompletionSource<string> completion)
        : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(GmsTask task)
        {
            if (task.IsSuccessful)
                completion.TrySetResult(task.Result?.ToString() ?? string.Empty);
            else
                completion.TrySetException(new InvalidOperationException(
                    task.Exception?.Message ?? "Firebase did not return a registration token."));
        }
    }
}

[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public sealed class ATekFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        ChatPushRegistration.Publish("fcm", token, "production");
    }

    // The server sends a notification+data payload. Android displays it itself
    // while the app is backgrounded or terminated and places the routing data on
    // MainActivity's launch intent. While foregrounded, the live chat connection
    // already presents the notification, avoiding a duplicate here.
    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
    }
}
