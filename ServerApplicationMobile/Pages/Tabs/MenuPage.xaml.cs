using Microsoft.Extensions.DependencyInjection;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class MenuPage : ContentPage
{
    private bool _isSigningOut;

    public MenuPage()
    {
        InitializeComponent();
    }

    private async void OnServiceTicketsClicked(object sender, EventArgs e)
    {
        if (!TryGetServices(out var services))
            return;

        var navigation = Shell.Current?.Navigation ?? Navigation;
        await navigation.PushAsync(new ServiceTicketsPage(
            services.GetRequiredService<DatabaseService>()));
    }

    private async void OnSoftwareLogsClicked(object sender, EventArgs e)
    {
        if (!TryGetServices(out var services))
            return;

        var navigation = Shell.Current?.Navigation ?? Navigation;
        await navigation.PushAsync(new SoftwareLogsPage(
            services.GetRequiredService<DatabaseService>(),
            services.GetRequiredService<AuthenticationService>()));
    }

    private async void OnChatLogsClicked(object sender, EventArgs e)
    {
        if (!TryGetServices(out var services))
            return;

        var navigation = Shell.Current?.Navigation ?? Navigation;
        await navigation.PushAsync(new ChatLogsPage(
            services.GetRequiredService<DatabaseService>(),
            services.GetRequiredService<ChatTranscriptService>()));
    }

    private bool TryGetServices(out IServiceProvider services)
    {
        services = Handler?.MauiContext?.Services;
        if (services?.GetService<AuthenticationService>()?.IsAuthenticated == true)
            return true;

        services = null;
        _ = DisplayAlert("Sign in required", "Sign in before opening server tools.", "OK");
        return false;
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        if (_isSigningOut || Handler?.MauiContext?.Services is not IServiceProvider services)
            return;

        _isSigningOut = true;
        MenuView.IsEnabled = false;
        ActivityIndicator.IsRunning = true;

        try
        {
            await services.GetRequiredService<ChatService>().SignOutAsync();
            services.GetRequiredService<AuthenticationService>().SignOut(forgetSavedSignIn: true);

            if (Window != null)
                Window.Page = services.GetRequiredService<LoginPage>();
        }
        finally
        {
            ActivityIndicator.IsRunning = false;
            MenuView.IsEnabled = true;
            _isSigningOut = false;
        }
    }
}
