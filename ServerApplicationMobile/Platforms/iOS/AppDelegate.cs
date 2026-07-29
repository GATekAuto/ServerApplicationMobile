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
}
