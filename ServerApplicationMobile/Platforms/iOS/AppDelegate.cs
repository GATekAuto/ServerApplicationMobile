using Foundation;

using ServerApplicationMobile.Services;
using UIKit;

namespace ServerApplicationMobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(
        UIApplication application,
        NSDictionary launchOptions)
    {
        ChatNotificationService.ConfigureIosNotificationHandling();
        return base.FinishedLaunching(application, launchOptions);
    }

    [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
    public void RegisteredForRemoteNotifications(
        UIApplication application,
        NSData deviceToken)
    {
        var token = Convert.ToHexString(deviceToken.ToArray()).ToLowerInvariant();
#if DEBUG
        const string environment = "sandbox";
#else
        const string environment = "production";
#endif
        ChatPushRegistration.Publish("apns", token, environment);
    }

    [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
    public void FailedToRegisterForRemoteNotifications(
        UIApplication application,
        NSError error)
    {
        System.Diagnostics.Debug.WriteLine(
            $"APNs registration failed: {error.LocalizedDescription}");
    }
}
