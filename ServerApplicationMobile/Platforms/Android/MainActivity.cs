using Android.App;
using Android.Content.PM;
using Android.OS;

using Android.Content;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleNotificationIntent(Intent);
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        HandleNotificationIntent(intent);
    }

    private static void HandleNotificationIntent(Intent intent)
    {
        if (intent == null)
            return;

        ChatNotificationActivation.TryPublish(
            intent.GetStringExtra(ChatNotificationService.NotificationChatKindKey),
            intent.GetStringExtra(ChatNotificationService.NotificationConversationIdKey));
    }
}
